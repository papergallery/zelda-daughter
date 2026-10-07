#!/usr/bin/env python3
"""R0-05: rules the agents of Zelda's Daughter (and The Chest before them) kept breaking, as a PreToolUse hook (.claude/settings.json).

- Secrets: the repository is public (ADR-0006) and a Unity password already leaked through a command allowlist (R0-01).
  A command must not carry a password, and must not print a token file; a commit must not stage a known secret.
- Heavy work only under the lock shared with The Chest: `flock -o /tmp/thechest-heavy.lock` (docs/agent-handbook.md §1).
- The Unity editors and the shared unity-mcp server on the author's PC are not ours to kill (CLAUDE.md; R1-02 — the server
  on 6510 belongs to The Chest editor).
- A visible task (K, R2, R1-07) ticked without evidence gets a reminder: proof is a frame or a phone recording plus a [ZD:*] line.

Reads the hook JSON on stdin; prints a decision in JSON, or nothing when the call is fine.
"""
import json
import os
import re
import subprocess
import sys

# A command, not a word in a grep or a message: at the start of a line or after ; & | ( then do, maybe behind env/timeout/nice.
HEAVY = re.compile(r"(^|[;&|(]|\bthen\b|\bdo\b)\s*((env\s+)?\w+=\S+\s+|timeout\s+\S+\s+|nice\s+(-n\s*-?\d+\s+)?)*"
                   r"dotnet\s+(build|test)\b", re.I | re.M)
KILL_UNITY = re.compile(r"(^|[;&|(']|\bthen\b|\bdo\b)\s*(sudo\s+)?(taskkill|Stop-Process|pkill|killall|kill)\b[^\n;&|]*\b(Unity|mcp-for-unity)\b",
                        re.I | re.M)
EDITOR_EXIT = re.compile(r"EditorApplication\.Exit|StopLocalHttpServer|StopManagedLocalHttpServer", re.I)
PASSWORD_ARG = re.compile(r"(^|\s)--?(password|passwd|pass)(\s+|=)(?!\$|\"\$|<|\*{3})\S+", re.I)
PRINT_SECRET = re.compile(r"\b(cat|less|more|head|tail|type|Get-Content|echo)\b[^\n;&|]*"
                          r"(\.tensorlay_bridge_token|gh/hosts\.yml|\.cloudflare_api_token|polza\.env|id_ed25519(?!\.pub)|zelda_deploy(?!\.pub))", re.I)
SECRET_TEXT = re.compile(r"ghp_[A-Za-z0-9]{20,}|gho_[A-Za-z0-9]{20,}|-----BEGIN [A-Z ]*PRIVATE KEY-----|"
                         r"-password\s+\\?\"(?![A-Z_]+\\?\")[^\"$<]+\\?\"")  # ALLCAPS — заглушка вида "PASSWORD"
TICK = re.compile(r"^\+- \[x\] (K-\d{2}|R2-\d{2}|R1-07) ", re.M)
EVIDENCE = re.compile(r"кадр|frame|shot|видео|video|запис|\.jpg|\.png|\.mp4|\[ZD:|logcat|Player\.log|годится", re.I)


def known_secrets():
    """Real secret values on this host — a commit must never contain them (values are read, never printed)."""
    values = []
    for path in ("~/.tensorlay_bridge_token",):
        try:
            v = open(os.path.expanduser(path)).read().strip()
            if len(v) >= 16:
                values.append(v)
        except OSError:
            pass
    try:
        for line in open(os.path.expanduser("~/.config/gh/hosts.yml")):
            m = re.search(r"oauth_token:\s*(\S{16,})", line)
            if m:
                values.append(m.group(1))
    except OSError:
        pass
    return values


def deny(reason):
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "deny",
                                             "permissionDecisionReason": reason}}, ensure_ascii=False))


def remind(text):
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse", "additionalContext": text}}, ensure_ascii=False))


def main():
    try:
        call = json.load(sys.stdin)
    except ValueError:
        return 0
    tool = call.get("tool_name", "")
    data = call.get("tool_input") or {}
    text = data.get("command", "") if tool == "Bash" else json.dumps(data, ensure_ascii=False)

    if KILL_UNITY.search(text) or (tool != "Bash" or "umcp" in text or "tlbridge" in text) and EDITOR_EXIT.search(text):
        deny("R0-05: редакторы Unity и общий сервер моста на ПК не закрывать и не останавливать — сервер 6510 принадлежит The Chest "
             "(docs/agent-handbook.md, R1-02). Нужен перезапуск — попросить автора.")
        return 0

    if PASSWORD_ARG.search(text) or (tool == "Bash" and PRINT_SECRET.search(text)):
        deny("R0-05: пароль в команде или вывод файла с токеном. Репозиторий публичный, пароль Unity уже утекал через allowlist "
             "(R0-01, ADR-0006). Секрет — из файла или переменной окружения, без печати.")
        return 0
    if tool != "Bash" and SECRET_TEXT.search(text):
        deny("R0-05: в тексте похоже на секрет (токен GitHub, приватный ключ, -password). Секреты — только вне репозитория.")
        return 0
    secrets = known_secrets()
    if any(s in text for s in secrets):
        deny("R0-05: в вызове настоящий токен с этого сервера. Читать его из файла внутри команды, не вписывать значение.")
        return 0

    if tool == "Bash" and re.search(r"flock[^|;&\n]*check\.sh", text):
        deny("R0-05: tools/check.sh берёт общую блокировку сам — запускать без flock снаружи, иначе он ждёт сам себя.")
        return 0
    if tool == "Bash" and HEAVY.search(text) and "flock" not in text:
        deny("R0-05: dotnet build/test — только под общей с The Chest блокировкой: flock -o /tmp/thechest-heavy.lock <команда> "
             "(docs/agent-handbook.md §1).")
        return 0

    if tool == "Bash" and re.search(r"\bgit\s+commit\b", text):
        try:
            staged = subprocess.run(["git", "diff", "--cached", "-U0"], capture_output=True, text=True, timeout=20).stdout
        except (OSError, subprocess.SubprocessError):
            return 0
        added = "\n".join(l for l in staged.splitlines() if l.startswith("+") and not l.startswith("+++"))
        if SECRET_TEXT.search(added) or any(s in added for s in secrets):
            deny("R0-05: в индексе секрет (токен, ключ или пароль). Убрать из файла и из индекса до коммита — репозиторий публичный.")
            return 0
        plan = "\n".join(l for l in staged.splitlines() if l.startswith("+- [x]"))
        if TICK.search(plan) and not EVIDENCE.search(text + plan):
            remind("R0-05: галочка видимой задачи (K, R2, R1-07) без доказательства в строке или сообщении. Видимое «сделано» — "
                   "кадр или запись с телефона и строка [ZD:*] из Player.log/logcat; вкус — «годится» автора (CLAUDE.md).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
