#!/usr/bin/env python3
"""R0-05: refuse a commit whose staged additions carry a secret (pattern or a real token value on this host)."""
import os
import subprocess
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "hooks"))
from guard import SECRET_TEXT, known_secrets  # noqa: E402

staged = subprocess.run(["git", "diff", "--cached", "-U0", "--no-color", "--", "."],
                        capture_output=True, text=True).stdout
added = "\n".join(l for l in staged.splitlines() if l.startswith("+") and not l.startswith("+++"))
if SECRET_TEXT.search(added) or any(s in added for s in known_secrets()):
    print("pre-commit (R0-05): в индексе секрет — токен, ключ или пароль. Репозиторий публичный (ADR-0006); "
          "уберите его из файла и из индекса.", file=sys.stderr)
    sys.exit(1)

# ADR-0004: purchased Asset Store content never goes to the public repo, even with `git add -f`.
names = subprocess.run(["git", "diff", "--cached", "--name-only", "--diff-filter=ACMR"],
                       capture_output=True, text=True).stdout.splitlines()
bought = [n for n in names if "/Assets/ThirdParty/" in n or n.endswith("/Assets/ThirdParty.meta")]
if bought:
    print("pre-commit (ADR-0004): в индексе купленные ассеты — " + ", ".join(bought[:5]) +
          ". Им место только на ПК (Assets/ThirdParty в .gitignore); уберите из индекса: git rm --cached -r <путь>.",
          file=sys.stderr)
    sys.exit(1)
