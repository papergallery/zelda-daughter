#!/usr/bin/env python3
"""D-10: data/models.json — каталог моделей мира (id → FBX или составная модель), из списка FBX в Assets/Art/Models.

Запуск из корня репозитория:  python3 tools/gen-models.py
Правила коллайдеров и тегов — в таблицах ниже; дома собираются из модулей Kenney Town (клетка 3 м). Границы моделей (замер в Unity) лежат
отдельно, в data/model-bounds.json (Zelda → Models → Measure bounds) — этот файл их не трогает.
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS = "ZeldaDaughter/Assets/Art/Models"
KITS = [("KenneyNature", "", "nature"), ("KenneyTown", "town_", "town"), ("QuaterniusProps", "prop_", "prop")]


def snake(name: str) -> str:
    name = name.replace("-", "_")
    name = re.sub(r"(?<=[a-z0-9])([A-Z])", r"_\1", name)
    return name.lower()


def coll(kind="box", **kw):
    d = {"kind": kind}
    d.update(kw)
    return d


def nature_rule(n: str):
    """(collider, tags) по имени файла Kenney Nature."""
    if n.startswith("tree_"):
        return coll("capsule", radius=0.5 if "fat" in n else 0.35), ["tree"]
    if n.startswith("plant_bush"):
        return coll("box", shrink=0.7), ["bush"]
    if n.startswith(("grass", "plant_flat")):
        return coll("none"), ["grass"]
    if n.startswith("flower_"):
        return coll("none"), ["flower"]
    if n.startswith("mushroom_"):
        return coll("none"), ["mushroom"]
    if n.startswith("lily_"):
        return coll("none"), ["water_plant"]
    if n.startswith(("rock_", "stone_")):
        small = "small" in n
        return (coll("none") if small else coll("box", shrink=0.75)), ["rock"] + (["small"] if small else [])
    if n.startswith(("stump_", "log")):
        return coll("box", shrink=0.85), ["wood"]
    if n.startswith("crop"):
        return (coll("none") if True else None), ["crop"]
    if n.startswith("fence"):
        return coll("box"), ["fence"]
    if n.startswith("bridge"):
        return coll("none"), ["bridge"]
    if n.startswith("campfire"):
        return coll("none"), ["campfire"]
    if n.startswith(("ground_", "path_")):
        return coll("none"), ["ground_tile"]
    if n.startswith("bed"):
        return coll("none"), ["bed"]
    if n.startswith("tent"):
        return coll("box", shrink=0.9), ["tent"]
    if n.startswith(("pot_",)):
        return coll("box", shrink=0.8), ["prop"]
    if n == "sign":
        return coll("box", shrink=0.6), ["sign"]
    return coll("none"), []


TOWN_BOX = ("fence", "hedge", "pillar", "stall", "cart", "fountain", "windmill", "wall")
TOWN_NONE_PREFIX = ("roof", "road", "stairs", "banner", "chimney", "wheel", "watermill", "blade", "planks", "poles", "overhang", "balcony", "lantern")


def town_rule(n: str):
    if n.startswith("tree"):
        return coll("capsule", radius=0.4), ["tree"]
    if n.startswith("rock"):
        return coll("box", shrink=0.75), ["rock"]
    if "door" in n:  # an opening is not an obstacle on its own
        return coll("none"), ["wall", "door"]
    if n.startswith(TOWN_NONE_PREFIX):
        tag = n.split("-")[0]
        return coll("none"), [tag]
    if n.startswith(TOWN_BOX):
        return coll("box"), [n.split("-")[0]]
    return coll("none"), []


PROP_BOX = ("Anvil", "Barrel", "Bed_", "Bench", "Bookcase", "BookStand", "Cabinet", "Cauldron", "Chair", "Chest", "Crate", "Dummy", "FarmCrate",
            "Nightstand", "Shelf", "Stall", "Stool", "Table_Large", "WeaponStand", "Workbench")


def prop_rule(n: str):
    tags = ["prop"]
    if n.startswith("Bed_"): tags.append("bed")
    if n.startswith("Anvil"): tags.append("anvil")
    if n.startswith(("Stall", "Table", "Bench", "Chair", "Stool")): tags.append("furniture")
    if n.startswith(PROP_BOX):
        return coll("box", shrink=0.9), tags
    return coll("none"), tags


RULES = {"nature": nature_rule, "town": town_rule, "prop": prop_rule}


# --- составные модели: дома из модулей Town (клетка 3 м; стена стоит на западном ребре клетки при yaw 0, остальные стороны — поворот на 90°) ---
TILE = 3.0
WALL_H = 3.0
SIDE_YAW = {"w": 0, "n": 90, "e": 180, "s": 270}  # n = +z


def house(nx, nz, wall, door, window, roof_kind, door_side="s", door_at=None, windows=True, roof_y=WALL_H, roof_yaw=0):
    parts = []
    xs = [(i - (nx - 1) / 2) * TILE for i in range(nx)]
    zs = [(j - (nz - 1) / 2) * TILE for j in range(nz)]
    edges = []  # (side, tile index along the side, x, z)
    for j in range(nz):
        edges.append(("w", j, xs[0], zs[j]))
        edges.append(("e", j, xs[-1], zs[j]))
    for i in range(nx):
        edges.append(("s", i, xs[i], zs[0]))
        edges.append(("n", i, xs[i], zs[-1]))
    n_along = {"w": nz, "e": nz, "s": nx, "n": nx}
    if door_at is None:
        door_at = lambda side: (n_along[side] - 1) // 2
    k = 0
    for side, idx, x, z in edges:
        model = wall
        coll_override = None
        if side == door_side and idx == door_at(side):
            model = door
            coll_override = "none"
        elif windows and window and idx % 2 == 1 or (windows and window and n_along[side] == 1 and side != door_side):
            model = window
        parts.append({"model": model, "offset": {"x": x, "z": z}, "yaw": SIDE_YAW[side], **({"collider": coll_override} if coll_override else {})})
    for i in range(nx):
        for j in range(nz):
            parts.append({"model": roof_kind, "offset": {"x": xs[i], "z": zs[j]}, "y": roof_y, "yaw": roof_yaw, "collider": "none"})
    return {"parts": parts, "collider": coll("parts"), "tags": ["building"]}


def composites():
    c = {}
    c["house_hut"] = house(2, 2, "town_wall_wood", "town_wall_wood_door", "town_wall_wood_window_small", "town_roof_gable")
    c["house_small"] = house(2, 2, "town_wall", "town_wall_door", "town_wall_window_small", "town_roof_gable")
    c["house_long"] = house(3, 2, "town_wall", "town_wall_door", "town_wall_window_shutters", "town_roof_gable")
    c["house_big"] = house(3, 3, "town_wall", "town_wall_door", "town_wall_window_glass", "town_roof_gable")
    return c


def main():
    models = {}
    for kit, prefix, kind in KITS:
        d = os.path.join(ROOT, MODELS, kit)
        for f in sorted(os.listdir(d)):
            if not f.endswith(".fbx"):
                continue
            base = f[:-4]
            mid = prefix + snake(base)
            if mid in models:
                sys.exit(f"duplicate model id {mid}")
            collider, tags = RULES[kind](base)
            models[mid] = {"path": f"Assets/Art/Models/{kit}/{f}", "collider": collider, "tags": tags}
    for mid, c in composites().items():
        if mid in models:
            sys.exit(f"duplicate model id {mid}")
        models[mid] = c
    out = {
        "_source": "tools/gen-models.py (D-10): id из имени файла (nature — без префикса, town_, prop_), коллайдеры и теги — правила в скрипте; размеры — data/model-bounds.json (замер в Unity). Модули: Kenney Nature Kit 2.1, Fantasy Town Kit 2.0, Quaternius Fantasy Props (все CC0, лицензии рядом с FBX). Масштаб запечён при импорте (метры, клетка 3 м) — ModelImport.KitScale.",
        "models": models,
    }
    with open(os.path.join(ROOT, "data", "models.json"), "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print(len(models), "models")


main()
