#!/usr/bin/env python3
"""D-28: пилотная сцена «уголок f1» (ADR-0010) — scenes/pilot-f1.json. Регион D-22b не трогаем: отдельная сцена, build.include=false.

    python3 tools/gen-pilot-f1.py        # правим скрипт, не JSON

Расстановка — по кадру docs/concept/world-b/f1.jpg (827×1792): точка основания каждого объекта на кадре переводится в мир под
игровой камерой (наклон 35°, поворот 45°, ортографическая): экранные метры sx (вправо), sy (вверх) от стоп героини, 112 px/м
(героиня 185 px ≈ 1,65 м, README world-b); земля: мир = sx·R + sy/sin35°·F, R = (1, −1)/√2, F = (1, 1)/√2 по (x, z).
Камера f1 круче нашей — совпадёт экранная раскладка, а не перспектива (README world-b, «Ограничения»).
Модели и коллайдеры — прежние (data/models.json, model-bounds.json); вид — рисованные карточки (секция painted, SceneBuilder.Painted.cs).
"""
import json, math, pathlib

ROOT = pathlib.Path(__file__).resolve().parents[1]
PPM, FEET = 112.0, (395.0, 1045.0)
SIN = math.sin(math.radians(35))
R, F = (1 / math.sqrt(2), -1 / math.sqrt(2)), (1 / math.sqrt(2), 1 / math.sqrt(2))


def at(px, py):
    sx, sy = (px - FEET[0]) / PPM, (FEET[1] - py) / PPM
    d = sy / SIN
    return round(sx * R[0] + d * F[0], 3), round(sx * R[1] + d * F[1], 3)


def obj(oid, model, px, py, yaw=0.0, scale=1.0, tags=('decor',), collide=None):
    x, z = at(px, py)
    o = {'id': oid, 'model': model, 'position': {'x': x, 'y': 0, 'z': z}, 'rotation': {'x': 0, 'y': yaw, 'z': 0},
         'scale': {'x': scale, 'y': scale, 'z': scale}, 'tags': list(tags)}
    if collide is not None:
        o['collide'] = collide
    return o


# Деревья: основание ствола на f1 (px), модель (коллайдер — капсула ствола), рисунок — по модели (painted.models).
TREES = [('tree_1', 'tree_oak', 150, 385), ('tree_2', 'tree_small', 662, 235), ('tree_3', 'tree_default', 805, 470),
         ('tree_4', 'tree_small', 88, 745), ('tree_5', 'tree_tall', 800, 840)]
# Камни: крупные — с коллайдером (box по замеру модели), масштаб — чтобы ширина на экране ≈ как на f1.
ROCKS = [('rock_1', 'rock_large_b', 630, 690, 0.42), ('rock_2', 'rock_large_c', 680, 790, 0.36), ('rock_3', 'rock_small_a', 585, 820, 0.7),
         ('rock_4', 'rock_tall_a', 25, 1010, 0.42), ('rock_5', 'rock_large_c', 30, 1230, 0.34), ('rock_6', 'rock_large_a', 740, 1330, 0.5),
         ('rock_7', 'rock_large_a', 690, 1530, 0.5), ('rock_8', 'rock_large_b', 765, 1640, 0.38), ('rock_9', 'rock_large_c', 650, 1710, 0.36),
         ('rock_10', 'rock_small_a', 100, 1655, 0.6), ('rock_11', 'rock_small_a', 180, 885, 0.5), ('rock_12', 'rock_small_flat_a', 240, 330, 0.6),
         ('rock_13', 'rock_large_c', 25, 830, 0.3), ('rock_14', 'rock_small_a', 10, 1300, 0.6), ('rock_15', 'rock_small_flat_a', 530, 330, 0.5),
         ('rock_16', 'rock_small_a', 470, 1150, 0.4)]
# Дорога: ось по f1 (px снизу вверх), ширина — как на f1 (≈ 190 px поперёк на экране ≈ 2,5 м по земле).
ROAD = [(-60, 1860), (120, 1450), (290, 1170), (385, 950), (425, 760), (480, 560), (610, 380), (800, 190), (980, 0)]


def build():
    objects = []
    for oid, model, px, py in TREES:
        objects.append(obj(oid, model, px, py))
    for oid, model, px, py, s in ROCKS:
        objects.append(obj(oid, model, px, py, yaw=0.0, scale=s, tags=('decor', 'rock')))
    # Бревно у дороги (f1: от (530, 1060) до (790, 900)), длинная ось модели log_large — x: на экране вправо-вверх ≈ как на рисунке.
    objects.append(obj('log_spawn', 'log_large', 660, 985, yaw=0.0, scale=0.8, tags=('decor', 'poi', 'log')))
    # Кольцо костра f1n (на краю дороги), костёр зажигает игра (CampPresenter); рисунок — холодное кольцо, огонь — частицы и свет.
    objects.append(obj('campfire_spawn', 'campfire_stones', 330, 1050, scale=0.75, tags=('campfire', 'rest_point', 'poi')))
    # За кадром f1 (для прогулки): пень, указатель, ещё деревья и камни.
    objects.append(obj('stump_1', 'stump_round', 860, 1180, tags=('decor',)))
    objects.append(obj('sign_1', 'sign', 520, 560, yaw=45, tags=('decor',)))
    extra = [('tree_6', 'tree_oak', -200, 200), ('tree_7', 'tree_default', -300, 900), ('tree_8', 'tree_small', 1000, 1300),
             ('tree_9', 'tree_tall', 1100, 600), ('tree_10', 'tree_oak', -150, 1900), ('tree_11', 'tree_default', 900, 2000),
             ('tree_12', 'tree_small', 300, -200), ('tree_13', 'tree_oak', 1200, -100)]
    for oid, model, px, py in extra:
        objects.append(obj(oid, model, px, py))
    # Туман пятнами (как в регионе, D-22): плоские квады, рисует движок.
    for k, (px, py, w, d) in enumerate([(60, 470, 9, 6), (700, 140, 8, 5)]):
        x, z = at(px, py)
        objects.append({'id': f'mist_{k:02d}', 'shape': 'quad', 'position': {'x': x, 'y': 0.45, 'z': z}, 'rotation': {'x': 90, 'y': 45, 'z': 0},
                        'scale': {'x': w, 'y': d, 'z': 1}, 'color': '#f2ead8', 'collide': False, 'tags': ['decor', 'mist']})

    road = [{'x': x, 'z': z} for x, z in (at(px, py) for px, py in ROAD)]
    rect = {'shape': 'rect', 'center': {'x': 0, 'z': 0}, 'size': {'x': 60, 'z': 60}}
    scatter = [
        {'id': 'meadow', 'area': rect,
         'models': [{'id': 'grass', 'weight': 5}, {'id': 'grass_large', 'weight': 3}, {'id': 'grass_leafs', 'weight': 1},
                    {'id': 'flower_yellow_a', 'weight': 2}, {'id': 'flower_purple_a', 'weight': 2}],
         'density': 60, 'seed': 28, 'scale': {'min': 0.8, 'max': 1.25}, 'minSpacing': 0.25, 'collide': False, 'randomYaw': True,
         'avoid': {'paths': 0.15, 'objects': 0.35}},
    ]
    scene = {
        '_source': 'D-28: пилот «уголок f1» (ADR-0010, docs/done-criteria/D-28.md) — генерирует tools/gen-pilot-f1.py; раскладка по кадру f1.',
        'name': 'pilot-f1',
        'build': {'include': False, 'order': 900},
        'save': {},
        'ground': {'sizeX': 64, 'sizeZ': 64, 'color': '#7d8a5c', 'terrain': 'grass'},
        'light': {'rotation': {'x': 40, 'y': -60, 'z': 0}, 'color': '#ffe9c8', 'intensity': 1.0, 'shadows': 'soft'},
        'ambient': {'color': '#a8aeb4'},
        'camera': {'orthographic': True, 'pitch': 35, 'yaw': 45, 'distance': 20, 'size': 6.3, 'followSmoothTime': 0.15},
        'hero': {'spawn': {'x': 0, 'y': 0, 'z': 0}, 'shape': 'capsule', 'color': '#c9a46a'},
        'paths': [{'id': 'road_main', 'points': road, 'width': 2.6, 'color': '#b09a6e'}],
        'objects': objects,
        'scatter': scatter,
        'painted': {
            'atlas': 'Assets/Art/Painted/painted.json',
            'models': {
                'tree_oak': ['tree_big'], 'tree_default': ['tree_mid'], 'tree_small': ['tree_small'], 'tree_tall': ['tree_tall'],
                'rock_large_a': ['rock_big_a'], 'rock_large_b': ['rock_mid_a'], 'rock_large_c': ['rock_mid_b'], 'rock_tall_a': ['rock_big_b'],
                'rock_small_a': ['rock_small', 'rock_mid_b'], 'rock_small_flat_a': ['rock_flat'],
                'log_large': ['log'], 'campfire_stones': ['fire_ring'], 'stump_round': ['stump'], 'sign': ['signpost'],
                'grass': ['grass_tuft', 'grass_clump', 'grass_bush'], 'grass_large': ['grass_tall', 'grass_dry'], 'grass_leafs': ['leaves'],
                'flower_yellow_a': ['flower_yellow'], 'flower_purple_a': ['flower_lilac'],
            },
            'flip': ['rock', 'plant'],
            'fit': ['rock', 'prop'],
            'ground': {'meadow': 'meadow', 'road': 'road'},
        },
    }
    out = ROOT / 'scenes/pilot-f1.json'
    out.write_text(json.dumps(scene, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    print(out, 'objects', len(objects))


if __name__ == '__main__':
    build()
