#!/usr/bin/env python3
"""D-10: scenes/region.json — стартовый регион (спавн → поле → лес → река с мостом → город), по docs/concept/first-5-minutes.md и §2.

Запуск из корня репозитория:  python3 tools/gen-region.py
Конфиг — результат этого скрипта (как data/models.json от gen-models.py): правим здесь и перегенерируем, не руками в JSON.
Координаты: x — на восток (вдоль главной дороги), z — на север; метры; скорость шага 2,5 м/с (data/movement.json).
Модели ставятся по центру границ (data/model-bounds.json), дома — по двери (дверь модуля Town — в таблице DOORS).
"""
import json
import math
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS = json.load(open(os.path.join(ROOT, "data/models.json")))["models"]
BOUNDS = json.load(open(os.path.join(ROOT, "data/model-bounds.json")))["bounds"]

objects = []
ids = set()


def r1(v):
    return round(v + 0.0, 3)


def rot(lx, lz, yaw):
    a = math.radians(yaw)
    return lx * math.cos(a) + lz * math.sin(a), -lx * math.sin(a) + lz * math.cos(a)


def bounds_center(model):
    p = MODELS[model].get("path")
    b = BOUNDS.get(p) if p else None
    return (b["center"][0], b["center"][2]) if b else (0.0, 0.0)


def add(id_, model, x, z, yaw=0, tags=None, item=None, collide=None, scale=None, centered=True, station=None):
    """Модель так, чтобы центр её границ оказался в (x, z)."""
    assert id_ not in ids, id_
    assert model in MODELS, model
    ids.add(id_)
    cx, cz = bounds_center(model) if centered else (0.0, 0.0)
    if scale:
        cx, cz = cx * scale, cz * scale
    ox, oz = rot(cx, cz, yaw)
    o = {"id": id_, "model": model, "position": {"x": r1(x - ox), "y": 0, "z": r1(z - oz)}}
    if yaw:
        o["rotation"] = {"x": 0, "y": yaw, "z": 0}
    if scale:
        o["scale"] = {"x": scale, "y": scale, "z": scale}
    if collide is not None:
        o["collide"] = collide
    if tags:
        o["tags"] = tags
    if item:
        o["item"] = item
    if station:
        o["station"] = station
    objects.append(o)
    return o


def marker(id_, x, z, tags=None, **extra):
    assert id_ not in ids, id_
    ids.add(id_)
    o = {"id": id_, "marker": True, "position": {"x": r1(x), "y": 0, "z": r1(z)}}
    if tags:
        o["tags"] = tags
    o.update(extra)
    objects.append(o)


def npc(key, x, z, color):
    id_ = "npc_" + key
    assert id_ not in ids
    ids.add(id_)
    objects.append({"id": id_, "shape": "capsule", "position": {"x": r1(x), "y": 1, "z": r1(z)}, "color": color, "tags": ["npc", "poi"]})


def pickup(id_, model, item, x, z, yaw=0, scale=None, poi=False):
    add(id_, model, x, z, yaw, ["pickup"] + (["poi"] if poi else []), item=item, collide=False, scale=scale)


# дверь модуля (локально, от центра) и сторона, куда она смотрит при yaw 0 (−z)
DOORS = {"house_small": (-1.5, -2.85), "house_hut": (-1.5, -2.85), "house_long": (0.0, -2.85), "house_big": (0.0, -4.35)}
YAW = {"S": 0, "W": 90, "N": 180, "E": 270}  # куда смотрит дверь


def house(id_, model, door_x, door_z, face, tags=None):
    yaw = YAW[face]
    dx, dz = rot(*DOORS[model], yaw)
    cx, cz = door_x - dx, door_z - dz
    add(id_, model, cx, cz, yaw, tags or ["building"], centered=False)
    return cx, cz


paths, water, zones, scatter = [], [], [], []


def P(x, z):
    return {"x": x, "z": z}


def line(id_, pts, width, color, terrain=None):
    d = {"id": id_, "points": [P(*p) for p in pts], "width": width, "color": color}
    if terrain:
        d["terrain"] = terrain
    return d


# ---------------------------------------------------------------- дороги и вода
ROAD = [(-172, 0), (-150, 1.5), (-131, 0.5), (-110, -1), (-90, 1.5), (-70, 0), (-50, -2.5), (-30, -1), (-12, 0), (12, 0), (28, 0.5), (36, 0),
        (58, 0), (63, -5), (81, -5), (86, 0), (110, 0), (150, 0)]
paths.append(line("road_main", ROAD, 3, "#b09a6e"))
paths.append(line("trail_glade", [(-88, 1), (-88, -10), (-82, -22), (-76, -34), (-72, -43)], 1.8, "#a38e66"))
paths.append(line("trail_stump", [(-72, -43), (-84, -48), (-96, -52), (-104, -56)], 1.4, "#9c8760"))
paths.append(line("trail_lair", [(-72, -43), (-64, -52), (-56, -60), (-50, -68)], 1.4, "#9c8760"))
paths.append(line("square_pave", [(62, 0), (82, 0)], 26, "#a39c8a"))
for name, a, b, w in [("door_tavern", (60, 18.3), (60, 9), 2.4), ("door_smithy", (94, 18.7), (94, 1.5), 2.4), ("door_herb", (78.5, 18.7), (78.5, 9), 2.4),
                      ("door_gatehouse", (38.5, 7.2), (38.5, 1.5), 2.2),
                      ("door_house_a", (102.5, 6), (102.5, 1.5), 2.2), ("door_house_b", (108.0, -5), (105.5, -1.5), 2.2),
                      ("door_house_c", (122.5, 6), (122.5, 1.5), 2.2), ("door_hut", (-151.5, 21.2), (-151.5, 14), 2.0)]:
    paths.append(line(name, [a, b], w, "#a8946c"))
paths.append(line("door_shop", [(113, 18.7), (115.8, 16.5), (115.8, 9.5), (113, 1.5)], 2.4, "#a8946c"))  # в обход прилавка
paths.append(line("track_hut", [(-151.5, 14), (-150, 1.5)], 2.0, "#a8946c"))
paths.append(line("lane_field", [(-131, 8), (-131, 2)], 2.0, "#a8946c"))
RIVER = [(-2, -100), (-1, -60), (-2, -20), (0, 0), (0, 20), (2, 60), (1, 100)]
water.append(line("river", RIVER, 7, "#5f8fa3"))

# ---------------------------------------------------------------- зоны
zones.append({"id": "bridge_deck", "shape": "rect", "center": P(0, 0), "size": P(11.5, 3.8), "terrain": "road", "tags": ["bridge"]})
zones.append({"id": "glade", "shape": "circle", "center": P(-72, -47), "radius": 11, "tags": ["glade"]})
zones.append({"id": "lair_den", "shape": "circle", "center": P(-48, -70), "radius": 5, "tags": ["lair"]})
zones.append({"id": "field_plot", "shape": "rect", "center": P(-131, 16.5), "size": P(15, 13), "tags": ["field"]})
zones.append({"id": "square_zone", "shape": "circle", "center": P(72, 0), "radius": 14, "tags": ["square"]})
zones.append({"id": "camp_spawn", "shape": "circle", "center": P(-166.5, -4.6), "radius": 3, "tags": ["camp"]})
for zid, cx, cz, rad in [("mud_river_west", -12, -9, 3.5), ("mud_trail", -84, -14.5, 3.0), ("mud_river_east", 12, 9, 3.5), ("mud_field", -118, 9, 3.0)]:
    zones.append({"id": zid, "shape": "circle", "center": P(cx, cz), "radius": rad, "tags": ["mud"]})

# ---------------------------------------------------------------- спавн: бревно, костёр, первая палка
add("log_spawn", "log_large", -171.5, -3.8, 8, ["decor", "poi", "log"])
add("campfire_spawn", "campfire_logs", -166.5, -4.6, 0, ["campfire", "rest_point", "poi"], collide=False)
pickup("pickup_firewood_1", "log", "firewood", -164.2, -6.4, 70, 0.7)
pickup("pickup_firewood_2", "log", "firewood", -169.0, -7.2, 20, 0.7)
pickup("pickup_stick_1", "log", "stick", -149, 3.6, 25, 0.35, poi=True)
pickup("pickup_stick_2", "log", "stick", -161, 3.2, 100, 0.35)
pickup("pickup_stone_1", "stone_small_a", "stone", -156, -3.4)

# ---------------------------------------------------------------- поле и хижина (крестьянин ≈ 37 м от спавна)
npc("peasant", -131, 5.4, "#8f7a4a")
marker("anchor_field", -131, 5.4, ["anchor"])
hx, hz = house("hut", "house_hut", -151.5, 21.2, "S", ["building", "poi"])
marker("anchor_hut", -151.5, 19.6, ["anchor"])
add("hut_barrel", "prop_barrel", -146.2, 20.2, 0, ["decor"])
add("hut_crate", "prop_crate_wooden", -146.0, 21.4, 30, ["decor"])
pickup("pickup_short_stick_1", "log", "short_stick", -145.5, 25.5, 60, 0.25)
pickup("pickup_firewood_3", "log", "firewood", -155.2, 20.6, 10, 0.7)
pickup("pickup_cloth_1", "prop_bag", "cloth", -156.4, 24.2, 40, 0.7)
for row in range(4):
    zrow = 11.6 + row * 2.4
    for col in range(4):
        xc = -137.5 + col * 3.0 + 1.5
        add(f"field_dirt_{row}_{col}", "crops_dirt_row", xc, zrow, 0, ["decor", "field"], collide=False)
        for k in (-0.75, 0.75):
            add(f"field_wheat_{row}_{col}_{'a' if k < 0 else 'b'}", "crops_wheat_stage_b" if (row + col) % 2 == 0 else "crops_wheat_stage_a", xc + k, zrow, 0, ["decor", "field"], collide=False)
for i, xf in enumerate([-139.5, -136.5, -133.5, -130.5, -127.5, -124.5]):
    add(f"field_fence_n_{i}", "fence_simple", xf, 23.6, 0, ["decor", "fence"])
for i, zf in enumerate([21.0, 18.0, 15.0, 12.0]):
    add(f"field_fence_e_{i}", "fence_simple", -123.2, zf, 90, ["decor", "fence"])
add("scarecrow", "prop_dummy", -131, 22.0, 0, ["decor", "poi"])

# ---------------------------------------------------------------- вдоль дороги на восток
add("cart_roadside", "town_cart", -112, 5.8, 25, ["decor", "poi"])
add("cart_barrel", "prop_barrel", -109.8, 3.8, 0, ["decor"])
add("sign_trail", "sign", -90.5, -3.6, 0, ["decor", "poi"])
add("wood_pile", "log_stack", -68, 4.2, 15, ["decor", "poi"])
pickup("pickup_stick_3", "log", "stick", -95, -3.0, 70, 0.35)
pickup("pickup_herbs_1", "plant_flat_tall", "herbs", -78, 5.5)
pickup("pickup_herbs_2", "plant_flat_tall", "herbs", -57, 6.0)
# развалины-кольцо камней у дороги
for i, (dx_, dz_, m) in enumerate([(-2.6, 0, "stone_tall_a"), (2.6, 0, "stone_tall_c"), (0, 2.6, "stone_tall_b"), (0, -2.6, "stone_tall_d"), (1.9, 1.9, "stone_large_a"), (-1.9, -1.9, "stone_large_c")]):
    add(f"ruin_stone_{i}", m, -43 + dx_, -8.5 + dz_, i * 40, ["decor", "poi"] if i == 0 else ["decor"])
pickup("pickup_stone_2", "stone_small_b", "stone", -43.8, -7.5)
pickup("pickup_flint_1", "stone_small_flat_a", "flint", -42.2, -9.3)
add("bush_berries_road", "plant_bush_detailed", -20, 4.8, 0, ["pickup", "poi"], item="berries", collide=False)
pickup("pickup_stone_3", "stone_small_c", "stone", -8.5, -4.6)
pickup("pickup_flint_2", "stone_small_flat_b", "flint", 7.5, -4.8)

# ---------------------------------------------------------------- лес и поляна
marker("anchor_glade", -72, -45, ["anchor", "poi_side"])
for i, (bx, bz) in enumerate([(-76, -43), (-69, -50), (-77, -52), (-67, -44)]):
    add(f"bush_berries_{i + 1}", "plant_bush_detailed", bx, bz, i * 70, ["pickup", "poi_side"], item="berries", collide=False)
pickup("pickup_healing_herbs_1", "flower_purple_b", "healing_herbs", -70, -41)
pickup("pickup_healing_herbs_2", "flower_purple_c", "healing_herbs", -75, -48)
pickup("pickup_herbs_3", "plant_flat_tall", "herbs", -79, -46)
pickup("pickup_stick_4", "log", "stick", -66, -47, 40, 0.35)
pickup("pickup_stick_5", "log", "stick", -80, -41, 120, 0.35)
add("glade_stump", "stump_round", -73, -51, 0, ["decor"])
pickup("pickup_flint_3", "stone_small_flat_c", "flint", -64, -50)
# логово кабана
marker("spawn_boar", -48, -70, ["enemy_spawn", "enemy_boar", "poi_side"], enemy="boar")
for i, (dx_, dz_, m) in enumerate([(-3.5, 1.5, "rock_large_c"), (3.5, 1.0, "rock_large_a"), (0.5, 3.8, "rock_large_e"), (-2.0, -3.2, "stone_large_b")]):
    add(f"lair_rock_{i}", m, -48 + dx_, -70 + dz_, i * 55, ["decor"])
add("lair_log", "log_large", -49, -67.5, 340, ["decor"])
pickup("pickup_special_herb_1", "flower_red_c", "special_herb", -53, -72)
pickup("pickup_ore_1", "stone_tall_g", "ore", -44, -73)
# коряга с медальоном
add("stump_old_locket", "stump_old_tall", -104, -56, 15, ["decor", "poi_side"])
pickup("pickup_locket", "prop_pouch_large", "locket", -102.4, -55.0, 0, 0.6)
# зоны волков (центры зон; радиус — data/night.json zoneRadius)
marker("zone_wolves_forest", -125, -78, ["predator_zone"])
marker("zone_wolves_river", 10, -58, ["predator_zone"])
pickup("pickup_ore_2", "stone_tall_h", "ore", 9, -44)

# ---------------------------------------------------------------- мост, берег
for i, bx in enumerate([-3.1, 0, 3.1]):
    add(f"bridge_{i + 1}", "bridge_wood", bx, 0, 0, ["bridge"] + (["poi"] if i == 1 else []), centered=True)
marker("anchor_bridge", 0, 0, ["anchor"])
add("east_tent", "tent_small_open", 15, -7.5, 200, ["decor", "poi"])
add("east_camp_log", "log", 12, -9.5, 60, ["decor"], scale=0.9)
add("east_campfire", "campfire_stones", 13.8, -5.2, 0, ["campfire", "decor"], collide=False)

# ---------------------------------------------------------------- город
# изгородь по западной границе с проёмом ворот
for k in range(-15, 16):
    if k == 0:
        continue
    add(f"fence_w_{k + 15}", "town_fence", 35, 3 * k, 0, ["decor", "fence"])
add("gate_post_s", "town_pillar_stone", 35, -1.9, 0, ["gate"])
add("gate_post_n", "town_pillar_stone", 35, 1.9, 0, ["gate"])
add("gate_banner_s", "town_banner_green", 34.4, -1.9, 90, ["decor", "gate"], collide=False)
add("gate_banner_n", "town_banner_green", 34.4, 1.9, 90, ["decor", "gate"], collide=False)
marker("town_gate", 35, 0, ["gate", "poi"])
npc("guard", 37.6, -2.7, "#56606a")
marker("anchor_gate", 37.6, -2.7, ["anchor"])
house("gatehouse", "house_hut", 38.5, 7.2, "S", ["building", "poi"])
marker("anchor_gatehouse", 40, 10, ["anchor"])
add("gate_lantern", "town_lantern", 41.6, 3.6, 0, ["decor"])
# живая изгородь по северу, югу и востоку
for k in range(0, 39):
    xh = 36.5 + 3 * k
    add(f"hedge_n_{k}", "town_hedge", xh, 45, 90, ["decor", "hedge"])
    add(f"hedge_s_{k}", "town_hedge", xh, -45, 90, ["decor", "hedge"])
for k in range(-14, 15):
    if k == 0:
        continue
    add(f"hedge_e_{k + 14}", "town_hedge", 150.5, 3 * k, 0, ["decor", "hedge"])

# площадь
add("fountain", "town_fountain_round", 72, 0, 0, ["fountain", "poi"])
marker("anchor_fountain", 72, -4.6, ["anchor"])
marker("town_square", 72, 5.2, ["square", "poi"])
npc("old_man", 72, -4.6, "#6e6a5a")
for i, (lx, lz) in enumerate([(52, 6), (52, -6), (92, 6), (92, -8)]):
    add(f"square_lantern_{i}", "town_lantern", lx, lz, 0, ["decor"])
add("square_bench_1", "prop_bench", 66, 8.5, 0, ["decor"])
add("square_bench_2", "prop_bench", 78, 8.5, 0, ["decor"])
add("square_tree_1", "town_tree", 53, 10.5, 0, ["decor"])
add("square_tree_2", "town_tree", 91, -11.0, 0, ["decor"])
add("square_tree_3", "town_tree", 66, -11.5, 0, ["decor"])
add("square_tree_4", "town_tree", 78, -11.5, 0, ["decor"])
add("gate_street_cart", "town_cart", 47, 5.5, 160, ["decor", "poi"])
add("gate_street_barrel_1", "prop_barrel", 45.2, 7.5, 0, ["decor"])
add("gate_street_barrel_2", "prop_barrel_apples", 49.0, 8.0, 0, ["decor"])
add("gate_street_crates", "prop_crate_wooden", 45.5, -5.0, 20, ["decor"])
add("gate_street_crates_2", "prop_farm_crate_apple", 47.5, -5.4, 0, ["decor"])

# таверна (большой дом, дверь на юг к площади)
tx, tz = house("tavern", "house_big", 60, 18.3, "S", ["building", "poi"])
marker("anchor_tavern_hall", 60, 21.4, ["anchor"])
marker("anchor_tavern_bar", 63.2, 24.4, ["anchor"])
marker("anchor_tavern_room", 57.3, 23.6, ["anchor"])
npc("barkeep", 63.2, 24.4, "#8a5a4a")
add("tavern_bar_table", "prop_table_large", 63.2, 25.9, 0, ["decor"])
add("tavern_table", "prop_table_plate", 59.5, 23.4, 0, ["decor"])
add("tavern_stool_1", "prop_stool", 58.0, 23.4, 0, ["decor"])
add("tavern_stool_2", "prop_stool", 61.0, 23.4, 0, ["decor"])
add("tavern_barrel_1", "prop_barrel", 56.4, 26.2, 0, ["decor"])
add("tavern_barrel_2", "prop_barrel_holder", 64.2, 21.2, 0, ["decor"])
add("tavern_bed_inside", "prop_bed_twin2", 56.8, 25.0, 90, ["decor"])
# кровать для постояльцев — на крыльце у входа (крыша над залом закрывает вид; тап по кровати — сон)
add("bed_tavern", "prop_bed_twin1", 66.8, 15.4, 0, ["bed", "poi"], collide=False)
add("bed_tavern_lantern", "town_lantern", 68.6, 14.2, 0, ["decor"])
add("tavern_sign", "sign", 62.4, 15.0, 0, ["decor"])

# кузница: большой дом на севере (дверь на юг, к камере), двор перед ним с плавильней и наковальней
house("smithy", "house_long", 94, 18.7, "S", ["building", "poi"])
marker("anchor_forge_home", 94, 22, ["anchor"])
npc("smith", 94, 13.4, "#6a5048")
marker("anchor_forge", 94, 13.4, ["anchor"])
add("station_smelter", "town_chimney_base", 90.6, 15.6, 90, ["station", "smelter", "poi"], collide=True, station="smelter")
add("smelter_fire", "campfire_bricks", 90.6, 14.2, 0, ["decor"], collide=False)
add("station_anvil", "prop_anvil", 97.6, 15.4, 0, ["station", "anvil", "poi"], station="anvil")
add("forge_workbench", "prop_workbench", 100.0, 14.0, 0, ["decor"])
add("forge_barrel", "prop_barrel", 88.4, 14.0, 0, ["decor"])
add("forge_weapons", "prop_weapon_stand", 88.2, 17.0, 0, ["decor"])
add("forge_chimney", "town_chimney_base", 97.0, 21.0, 0, ["decor"], collide=False)

# лавка торговца: прилавок на улице, дом позади (дверь на юг)
add("shop_stall", "town_stall_red", 113, 12.6, 180, ["building", "poi", "shop"])
npc("merchant", 113, 9.6, "#7a5a8a")
marker("anchor_shop", 113, 9.6, ["anchor"])
house("shop_house", "house_small", 113, 18.7, "S", ["building"])
marker("anchor_shop_rear", 114.5, 21.5, ["anchor"])
add("shop_crate_1", "prop_crate_wooden", 109.8, 12.0, 10, ["decor"])
add("shop_crate_2", "prop_crate_metal", 119.0, 12.2, 0, ["decor"])
add("shop_barrel", "prop_barrel", 119.2, 10.2, 0, ["decor"])

# дом травницы
house("herb_house", "house_small", 78.5, 18.7, "S", ["building", "poi"])
marker("anchor_herb_house", 80, 21.5, ["anchor"])
npc("herbalist", 78.5, 16.2, "#5f7d4f")
for i, (px, pz, m) in enumerate([(75.4, 15.6, "pot_large"), (82.4, 15.6, "pot_small"), (75.6, 19.6, "plant_bush_small"), (84.4, 20.8, "plant_bush_small")]):
    add(f"herb_deco_{i}", m, px, pz, 0, ["decor"])
add("herb_flowers_1", "flower_purple_a", 76.2, 16.8, 0, ["decor"], collide=False)
add("herb_flowers_2", "flower_yellow_b", 81.2, 16.6, 0, ["decor"], collide=False)

# дома жителей вдоль восточной улицы
house("house_a", "house_small", 102.5, 6.0, "S", ["building", "poi"])
marker("anchor_house_a", 102.5, 8.2, ["anchor"])
npc("townswoman", 102.5, 3.9, "#7a6a8a")
house("house_b", "house_hut", 108.0, -5.0, "W", ["building", "poi"])
marker("anchor_house_b", 109.5, -6.2, ["anchor"])
house("house_c", "house_small", 122.5, 6.0, "S", ["building", "poi"])
marker("anchor_house_c", 122.5, 8.2, ["anchor"])
npc("weaver", 122.5, 3.9, "#9a7a6a")
add("house_c_loom", "prop_shelf_simple", 126.6, 4.0, 0, ["decor"])
add("east_lantern_1", "town_lantern", 106, 3.6, 0, ["decor"])
add("east_lantern_2", "town_lantern", 118, -3.6, 0, ["decor"])
add("house_a_barrel", "prop_barrel", 106.6, 6.6, 0, ["decor"])
add("house_b_crate", "prop_crate_wooden", 107.4, -9.6, 15, ["decor"])
add("east_end_cart", "town_cart", 144, -4.2, 200, ["decor", "poi"])
add("east_end_barrels", "prop_barrel", 146.4, -3.4, 0, ["decor"])
add("east_end_well", "pot_large", 140, 4.0, 0, ["decor", "poi"])
for k, (wx, wz) in enumerate([(132, 9), (136, -9), (130, -22), (86, 28), (104, 28), (44, 24), (44, -24), (98, -24)]):
    add(f"town_tree_{k}", "town_tree", wx, wz, 0, ["decor"])

# сухая трава: клетки огня D-06 (id grass_cell_NN, тег grass_cell; шаг 1,5 м, не ближе 2 м к костру)
def grass_cells(prefix, x0, x1, z0, z1, avoid, start):
    n = start
    zc = z0
    row = 0
    while zc <= z1:
        xc = x0 + (0.75 if row % 2 else 0)
        while xc <= x1:
            jx = math.sin(n * 12.9898) * 0.15
            jz = math.cos(n * 78.233) * 0.15
            x, z = xc + jx, zc + jz
            ok = all(math.hypot(x - ax, z - az) > ar for ax, az, ar in avoid)
            if ok:
                add(f"{prefix}_{n:03d}", "grass_large", x, z, (n * 47) % 360, ["decor", "grass_cell"], collide=False)
                n += 1
            xc += 1.5
        zc += 1.3
        row += 1
    return n


AVOID_CAMP = [(-166.5, -4.6, 2.2), (-171.5, -3.8, 2.4), (-164.2, -6.4, 1.2), (-169, -7.2, 1.2), (-156, -3.4, 1), (-161, 3.2, 1)]
nn = grass_cells("grass_cell", -178, -156, -12, -5.5, AVOID_CAMP, 0)
nn = grass_cells("grass_cell", -178, -158, 4, 11, AVOID_CAMP, nn)

# ---------------------------------------------------------------- россыпи
MEADOW = [{"id": "grass", "weight": 4}, {"id": "grass_large", "weight": 2}, {"id": "flower_yellow_a", "weight": 1}, {"id": "flower_purple_a", "weight": 1},
          {"id": "flower_red_a", "weight": 1}, {"id": "plant_flat_short", "weight": 1}]
TREES = [{"id": "tree_default", "weight": 2}, {"id": "tree_oak", "weight": 1}, {"id": "tree_pine_tall_a", "weight": 2}, {"id": "tree_pine_tall_b", "weight": 2},
         {"id": "tree_pine_tall_c", "weight": 1}, {"id": "tree_fat", "weight": 1}, {"id": "tree_pine_round_a", "weight": 1}, {"id": "tree_detailed", "weight": 1},
         {"id": "tree_small", "weight": 1}]
SOFT = {"paths": 0.9, "water": 1.2, "objects": 0.6, "zones": ["bridge_deck", "field_plot", "square_zone"]}
FOREST_ZONES = {"paths": 2.2, "water": 3, "objects": 2.2, "zones": ["glade", "lair_den", "camp_spawn"], "zoneMargin": 1.2}


def rect(cx, cz, sx, sz):
    return {"shape": "rect", "center": P(cx, cz), "size": P(sx, sz)}


def sc(id_, area, models, density, seed, avoid, smin=0.8, smax=1.2, spacing=0, collide=False):
    d = {"id": id_, "area": area, "models": models, "density": density, "seed": seed, "scale": {"min": smin, "max": smax}, "avoid": avoid}
    if spacing:
        d["minSpacing"] = spacing
    if collide:
        d["collide"] = True
    scatter.append(d)


sc("meadow_west", rect(-92, 14, 176, 60), MEADOW, 4.5, 11, SOFT)
sc("meadow_east_bank", rect(18, 0, 30, 70), MEADOW, 4, 12, SOFT)
sc("meadow_town", rect(92, 0, 115, 90), MEADOW, 1.0, 13, {"paths": 1.2, "water": 1.5, "objects": 1.0, "zones": ["square_zone"]})
sc("forest_trees", rect(-88, -56, 152, 80), TREES, 4.2, 21, FOREST_ZONES, 0.85, 1.25, 2.6, True)
sc("forest_undergrowth", rect(-88, -56, 152, 80),
   [{"id": "plant_bush", "weight": 2}, {"id": "plant_bush_large", "weight": 1}, {"id": "plant_bush_small", "weight": 2}, {"id": "mushroom_red", "weight": 0.3},
    {"id": "mushroom_tan", "weight": 0.3}, {"id": "grass", "weight": 3}, {"id": "plant_flat_tall", "weight": 1}],
   2.8, 22, {"paths": 1.0, "water": 1.5, "objects": 0.8, "zones": ["glade", "lair_den"]})
sc("forest_rocks", rect(-88, -56, 152, 80), [{"id": "rock_large_a", "weight": 1}, {"id": "rock_large_b", "weight": 1}, {"id": "rock_large_d", "weight": 1}, {"id": "stone_tall_c", "weight": 1}],
   0.25, 23, FOREST_ZONES, 0.8, 1.3, 4.0, True)
sc("glade_flowers", {"ref": "glade"}, [{"id": "flower_yellow_a", "weight": 2}, {"id": "flower_purple_a", "weight": 2}, {"id": "flower_red_b", "weight": 1}, {"id": "grass_large", "weight": 3}],
   12, 24, {"paths": 0.8, "objects": 0.7})
sc("trees_north_west", rect(-92, 72, 176, 48), TREES, 1.1, 25, {"paths": 2.2, "water": 3, "objects": 2.0}, 0.85, 1.25, 4.0, True)
sc("trees_north_east", rect(92, 72, 115, 48), TREES, 1.4, 26, {"paths": 2.2, "water": 3, "objects": 2.0}, 0.85, 1.25, 4.0, True)
sc("trees_south_east", rect(92, -72, 115, 48), TREES, 1.4, 27, {"paths": 2.2, "water": 3, "objects": 2.0}, 0.85, 1.25, 4.0, True)
sc("trees_east_bank_south", rect(19, -66, 30, 60), TREES, 2.0, 28, {"paths": 2.2, "water": 3, "objects": 2.0, "zones": ["mud_river_east"]}, 0.85, 1.25, 3.4, True)
sc("trees_east_bank_north", rect(19, 66, 30, 60), TREES, 2.0, 29, {"paths": 2.2, "water": 3, "objects": 2.0}, 0.85, 1.25, 3.4, True)
sc("river_stones", {"ref": "river"}, [{"id": "stone_small_a", "weight": 1}, {"id": "stone_small_c", "weight": 1}, {"id": "rock_small_b", "weight": 1}],
   1.5, 31, {"paths": 1.5, "zones": ["bridge_deck"], "zoneMargin": 2.0}, 0.8, 1.4)
scatter.append({"id": "edge_trees", "area": {"shape": "strip", "points": [P(-176, -96), P(176, -96), P(176, 96), P(-176, 96), P(-176, -96)], "width": 8},
                "models": TREES, "density": 4.5, "seed": 41, "scale": {"min": 0.9, "max": 1.3}, "minSpacing": 2.6, "collide": True,
                "avoid": {"paths": 2.0, "water": 3, "objects": 1.5}})

config = {
    "_source": "D-10: стартовый регион (docs/concept/first-5-minutes.md, project-design.md §2). Генерируется tools/gen-region.py — править там. Дорога road_main: спавн (−168; 0) → крестьянин ≈ 37 м (15 с при 2,5 м/с) → лес и поляна в стороне (trail_glade) → мост через реку (x = 0) → ворота города (x = 35) → площадь (72; 0) → восточный край (150; 0). Теги: poi — точка интереса на пути (≤ 15 с ходьбы между соседними, тест SceneConfigTests.Region_*), poi_side — вне главной дороги; anchor — якоря расписаний data/npcs.json; npc — NPC (капсулы до D-12); enemy_spawn + enemy_<id> — точка спавна врага; predator_zone — центры зон ночных волков (data/night.json); grass_cell — клетка сухой травы (D-06); зоны с тегом mud — грязь после дождя (D-06); bed — кровать (тап — сон); station + smelter|anvil — станки кузницы (data/recipes.json station).",
    "name": "region",
    "build": {"include": True, "order": 0},
    "save": {"slot": "slot"},
    "ground": {"sizeX": 360, "sizeZ": 200, "color": "#7d8a5c", "terrain": "grass"},
    "light": {"rotation": {"x": 40, "y": -60, "z": 0}, "color": "#ffe9c8", "intensity": 1.0, "shadows": "hard"},
    "ambient": {"color": "#a8aeb4"},
    "camera": {"orthographic": True, "pitch": 35, "yaw": 45, "distance": 20, "size": 8, "followSmoothTime": 0.15},
    "hero": {"spawn": {"x": -168, "y": 0, "z": 0}, "shape": "capsule", "color": "#c9a46a"},
    "paths": paths,
    "water": water,
    "zones": zones,
    "objects": objects,
    "scatter": scatter,
}

with open(os.path.join(ROOT, "scenes/region.json"), "w") as f:
    json.dump(config, f, indent=1, ensure_ascii=False)
    f.write("\n")
print(f"region.json: objects={len(objects)} paths={len(paths)} zones={len(zones)} scatter={len(scatter)}")
