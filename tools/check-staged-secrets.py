#!/usr/bin/env python3
"""R0-05: refuse a commit whose staged additions carry a secret (pattern or a real token value on this host)."""
import os
import subprocess
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "hooks"))
from guard import SECRET_TEXT, known_secrets  # noqa: E402

staged = subprocess.run(["git", "diff", "--cached", "-U0", "--no-color", "--", ".", ":(exclude)tools/hooks/cases.jsonl"],
                        capture_output=True, text=True).stdout
added = "\n".join(l for l in staged.splitlines() if l.startswith("+") and not l.startswith("+++"))
if SECRET_TEXT.search(added) or any(s in added for s in known_secrets()):
    print("pre-commit (R0-05): в индексе секрет — токен, ключ или пароль. Репозиторий публичный (ADR-0006); "
          "уберите его из файла и из индекса.", file=sys.stderr)
    sys.exit(1)
