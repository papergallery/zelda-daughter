#!/usr/bin/env python3
"""T-03: пишет недостающие .meta для пакета ядра core/ZeldaDaughter.Core (копия thechest/tools/unity-meta.py).

Unity сама создаёт .meta у файлов локального пакета, но со случайным GUID и на той машине, где открыт редактор: на ПК автора
они остаются неотслеженными, а два клона получают разные GUID. Здесь GUID выводится из имени пакета и пути файла — один и тот же
на любой машине. Уже существующие .meta не трогает.

    python3 tools/unity-meta.py            # все пакеты
    python3 tools/unity-meta.py --check    # только перечислить недостающие, код выхода 1, если есть
    python3 tools/unity-meta.py ZeldaDaughter/Assets/Scripts/Editor/Mcp   # папка в Assets (и она сама)
"""
import hashlib
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACKAGES = ["core/ZeldaDaughter.Core"]
SKIP_DIRS = {"bin", "obj", "artifacts"}

FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
FILE = """fileFormatVersion: 2
guid: {guid}
"""


def guid(package: str, rel: str) -> str:
    return hashlib.md5(f"zelda:{package}:{rel}".encode("utf-8")).hexdigest()


def hidden(name: str) -> bool:
    # Unity не импортирует скрытые файлы и папки и всё, что кончается на «~».
    return name.startswith(".") or name.endswith("~")


def missing(pkg_dir: str):
    with open(os.path.join(ROOT, pkg_dir, "package.json"), encoding="utf-8") as f:
        package = json.load(f)["name"]
    yield from walk(os.path.join(ROOT, pkg_dir), package, os.path.join(ROOT, pkg_dir))


def missing_in_assets(folder: str):
    """Папка внутри ZeldaDaughter/Assets: GUID — от пути относительно корня репозитория, сама папка тоже получает .meta."""
    base = os.path.join(ROOT, folder)
    rel = os.path.relpath(base, ROOT).replace(os.sep, "/")
    if not os.path.exists(base + ".meta"):
        yield base, FOLDER.format(guid=guid("assets", rel))
    yield from walk(base, "assets", ROOT)


def walk(base: str, package: str, rel_root: str):
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = sorted(d for d in dirnames if d not in SKIP_DIRS and not hidden(d))
        for name in sorted(dirnames) + sorted(filenames):
            if hidden(name) or name.endswith(".meta"):
                continue
            path = os.path.join(dirpath, name)
            if os.path.exists(path + ".meta"):
                continue
            rel = os.path.relpath(path, rel_root).replace(os.sep, "/")
            yield path, (FOLDER if os.path.isdir(path) else FILE).format(guid=guid(package, rel))


def main() -> int:
    check = "--check" in sys.argv[1:]
    folders = [a for a in sys.argv[1:] if not a.startswith("--")]
    jobs = [missing_in_assets(f) for f in folders] if folders else [missing(p) for p in PACKAGES]
    count = 0
    for job in jobs:
        for path, text in job:
            count += 1
            print(os.path.relpath(path, ROOT) + ".meta")
            if not check:
                with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
                    f.write(text)
    return 1 if check and count else 0


if __name__ == "__main__":
    sys.exit(main())
