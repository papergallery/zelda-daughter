#!/usr/bin/env python3
"""R0-05: plan consistency — every task-list line has its section; a ticked visible task has docs/done-criteria/<ID>.md."""
import glob
import os
import re
import sys

VISIBLE = re.compile(r"^(K|R2|G)\b|^R1-07$|^D-(0[89]|1\d|20)$")
bad = []
ids = 0
for path in sorted(glob.glob("plan/stage-*.md")):
    text = open(path, encoding="utf-8").read()
    for m in re.finditer(r"^- \[( |x)\] ([A-Z]+\d*-\d{2})\b", text, re.M):
        ids += 1
        done, tid = m.group(1) == "x", m.group(2)
        if not re.search(r"^### " + re.escape(tid) + r" ", text, re.M) and not done:
            bad.append(f"{path}: {tid} — нет раздела ### {tid}")
        if done and VISIBLE.search(tid) and not os.path.exists(f"docs/done-criteria/{tid}.md"):
            bad.append(f"{path}: {tid} отмечена, но нет docs/done-criteria/{tid}.md")
for b in bad:
    print("plan:", b)
print(f"plan: {ids} tasks" + ("" if not bad else f", {len(bad)} problem(s)"))
sys.exit(1 if bad else 0)
