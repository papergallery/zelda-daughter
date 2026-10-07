#!/usr/bin/env python3
"""R0-05: no secrets in tracked files. Known values are read from their files on this host and never printed."""
import os
import re
import subprocess
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "hooks"))
from guard import SECRET_TEXT, known_secrets  # noqa: E402

files = subprocess.run(["git", "ls-files", "-z"], capture_output=True, text=True).stdout.split("\0")
secrets = known_secrets()
hits = []
for f in files:
    if not f or not os.path.isfile(f) or os.path.getsize(f) > 2_000_000:
        continue
    try:
        text = open(f, encoding="utf-8").read()
    except (UnicodeDecodeError, OSError):
        continue
    if SECRET_TEXT.search(text) or any(s in text for s in secrets):
        hits.append(f)
for f in hits:
    print("secret-like content:", f)
sys.exit(1 if hits else 0)
