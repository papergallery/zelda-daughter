#!/usr/bin/env bash
# R0-05: «проверить» одной командой на сервере. Итог — строка CHECK OK / CHECK FAIL.
#   tests   — dotnet test ядра логики (если оно есть), под общей с The Chest блокировкой (docs/agent-handbook.md §1);
#   secrets — известные секреты сервера и шаблоны ключей/паролей в отслеживаемых файлах (репозиторий публичный, ADR-0006);
#   plan    — у каждой строки такс-листа есть раздел задачи, у видимой задачи с галочкой — файл критериев;
#   guard   — сторож правил решает свои случаи из tools/hooks/cases.jsonl.
# Unity-код здесь не собирается — только на ПК автора (ADR-0003).
set -u
cd "$(dirname "$0")/.."
fail=()
lock=/tmp/thechest-heavy.lock

sln=$(ls core/*.sln 2>/dev/null | head -1)
if [[ -n "$sln" ]]; then
  [[ -x "$HOME/.dotnet/dotnet" ]] && export PATH="$HOME/.dotnet:$PATH"
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 MSBUILDDISABLENODEREUSE=1
  out=$(flock -o "$lock" dotnet test "$sln" -m:1 2>&1)
  grep -E '^\s*Failed ' <<<"$out" | sed 's/^ */test failed: /'
  summary=$(grep -E 'Passed!|Failed!' <<<"$out" | tail -1)
  echo "tests: ${summary:-no summary}"
  [[ "$summary" == *"Passed!"* && "$summary" != *"Failed!"* ]] || fail+=(tests)
else
  echo "tests: ядра логики ещё нет"
fi

if python3 tools/check-secrets.py; then echo "secrets: none in tracked files"; else fail+=(secrets); fi
if python3 tools/check-plan.py; then :; else fail+=(plan); fi

bad=0
while IFS= read -r case; do
  # Fake secrets are made here, never stored: a fixture in git would itself be a secret-like line.
  case=${case//\{\{FAKE16\}\}/zq$(date +%N)x7k}
  want=$(python3 -c 'import json,sys;print(json.loads(sys.argv[1])["want"])' "$case")
  got=$(printf '%s' "$case" | python3 tools/hooks/guard.py | grep -q '"deny"' && echo deny || echo ok)
  [[ "$want" == "$got" ]] || { echo "guard: expected $want, got $got: $case"; bad=1; }
done < tools/hooks/cases.jsonl
if ((bad)); then fail+=(guard); else echo "guard: $(wc -l < tools/hooks/cases.jsonl) cases"; fi

if ((${#fail[@]})); then echo "CHECK FAIL: ${fail[*]}"; exit 1; fi
echo "CHECK OK"
