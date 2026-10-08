#!/usr/bin/env python3
"""W0: реестры ассетов вида (docs/demo/unity-architecture.md §2.5).

Каждый id из data/*.json есть в Assets/Art/Registries/*.json, запись — либо "placeholder": true, либо с файлами (они существуют,
если лежат в git: Assets/Art/…); ключи отсортированы. Печатает строки "registries: …" и выходит с 1 при ошибке.
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REG = ROOT / "ZeldaDaughter" / "Assets" / "Art" / "Registries"
UNITY = ROOT / "ZeldaDaughter"
errors = []


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def table(name, key):
    data = load(REG / name)
    t = data.get(key)
    if not isinstance(t, dict):
        errors.append(f"{name}: no object '{key}'")
        return {}
    if list(t) != sorted(t):
        errors.append(f"{name}: keys of '{key}' are not sorted")
    return t


def expect(name, table_, ids, files=()):
    for i in ids:
        if i not in table_:
            errors.append(f"{name}: no entry '{i}'")
    for k, rec in table_.items():
        if not isinstance(rec, dict):
            errors.append(f"{name}: '{k}' is not an object")
            continue
        has_files = any(rec.get(f) for f in files)
        if not rec.get("placeholder") and not has_files:
            errors.append(f"{name}: '{k}' has neither placeholder:true nor {'/'.join(files)}")
        for f in files:
            v = rec.get(f)
            for p in ([v] if isinstance(v, str) else v or []):
                if p.startswith("Assets/ThirdParty/"):
                    continue  # bought assets are not in git
                if not (UNITY / p).exists():
                    errors.append(f"{name}: '{k}' file missing: {p}")


def main():
    items = [i["id"] for i in load(ROOT / "data" / "items.json")["items"]]
    placeable = [i["id"] for i in load(ROOT / "data" / "items.json")["items"] if i.get("placeable")]
    npcs = list(load(ROOT / "data" / "npcs.json")["npcs"])
    enemies = list(load(ROOT / "data" / "enemies.json")["enemies"])
    dialog_icons = load(ROOT / "data" / "dialogues.json")["icons"]

    expect("characters.json", table("characters.json", "characters"), ["heroine"] + npcs + enemies, ("front", "back", "side", "down"))
    expect("item-icons.json", table("item-icons.json", "icons"), items, ("path",))
    expect("talk-icons.json", table("talk-icons.json", "icons"),
           dialog_icons + ["trade", "radial_bag", "radial_map", "radial_notebook", "hint_hand"], ("path",))
    sounds = table("sounds.json", "sounds")
    expect("sounds.json", sounds, ["bard_tavern"] + [f"step_{t}" for t in load(ROOT / "data" / "movement.json")["terrain"]], ("clips",))
    expect("fx.json", table("fx.json", "fx"),
           ["campfire", "torch_flame", "grass_fire", "burnt_patch", "rain"] + [f"placed_{p}" for p in placeable], ("prefab",))

    if errors:
        for e in errors:
            print("registries: " + e)
        return 1
    print("registries: all ids covered (characters, item-icons, talk-icons, sounds, fx)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
