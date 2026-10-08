# Формат сцены `scenes/<имя>.json` (T-04)

Один сборщик — `ZeldaDaughter/Assets/Scripts/Editor/SceneBuilder.cs`: читает конфиг и пересобирает `Assets/Scenes/<имя>.unity` целиком (меню **Zelda → Scenes → Build all from config**, через мост — `ZeldaDaughter.Editor.SceneBuilder.BuildAll()`). Сцены руками не правим: меняем конфиг или сборщик. Модель и проверка — в ядре (`core/ZeldaDaughter.Core/Scenes/SceneConfig.cs`), тесты на сервере проверяют каждый `scenes/*.json` до Unity.

Объекты — в формате апрельского `region_startmeadow.json` (id, prefab, position, rotation, scale, tags), плюс примитивы для заглушек.

```json
{
  "name": "g1-capsule",                       // = имя файла; имя сцены
  "ground":  { "sizeX": 40, "sizeZ": 40, "color": "#7d8a5c", "terrain": "grass" },   // terrain — id из data/movement.json
  "light":   { "rotation": {"x":50,"y":-30,"z":0}, "color": "#fff1dc", "intensity": 1.1, "shadows": "hard" },  // none|hard|soft
  "ambient": { "color": "#9aa0a8" },
  "camera":  { "orthographic": true, "pitch": 35, "yaw": 45, "distance": 20, "size": 8, "followSmoothTime": 0.15 },
  "build":   { "include": true, "order": 0 },   // в сборку плеера и в каком порядке (0 — стартовая сцена); include=false — тестовая сцена, в релиз не входит
  "save":    { "slot": "slot" },                // имя файла слота сохранения; пусто — сцена не сохраняется (тестовая)
  "hero":    { "spawn": {"x":0,"y":0,"z":0}, "shape": "capsule", "color": "#c9a46a" },  // или "prefab": "Assets/…"
  "objects": [
    { "id": "rock_1", "shape": "cube", "position": {"x":5,"y":0.4,"z":6}, "rotation": {"x":0,"y":30,"z":0},
      "scale": {"x":1.5,"y":0.8,"z":1.2}, "color": "#8c8a84", "tags": ["obstacle"] }
  ]
}
```

Правила (проверяет `SceneConfig.Validate`): id — `[a-z0-9_]` с буквы, без повторов; у героя и объекта ровно одно из `shape` (cube, sphere, capsule, cylinder, plane, quad) / `prefab` (путь в `Assets/`, должен существовать); позиции — в пределах земли; цвета — `#rrggbb`; масштаб > 0; камера: наклон в (0; 90), дистанция > 0.

`EditorBuildSettings.scenes` пересобирается целиком из `build` всех конфигов (`SceneConfig.BuildList`; одинаковый `order` у двух включённых сцен — ошибка). Сборщик не работает в Play Mode и не трогает сцену с несохранёнными правками — бросает `[ZD:Scene] … unsaved changes`.

Материалы примитивов — по одному на цвет, `Assets/Generated/Materials/c_<rrggbb>.mat` (URP/Lit), создаются один раз и переиспользуются.

Проверка идемпотентности: сборщик пишет `[ZD:Scene] built <имя> objects=N hash=…` — хеш имён, трансформов и типов компонентов; два прогона подряд дают один хеш (`.unity` побайтно не совпадёт: Unity выдаёт новые fileID).

## Модели, дороги, вода, зоны, россыпь (D-10)

Каталог моделей — `data/models.json` (генерирует `tools/gen-models.py`): id → `path` (FBX в `Assets/Art/Models`, CC0, лицензии рядом) или составная модель `parts` (дом из модулей Town: `offset`, `y`, `yaw`, `collider: "none"` для проёма); `collider`: `none | box | capsule | parts` (+ `radius`, `height`, `shrink`); `tags`. Размеры — `data/model-bounds.json` (замер в Unity). Масштаб запечён при импорте: единицы — метры, клетка 3 м. Ядро: `ModelCatalog`, `Area`, `TerrainMap`, `Scatterer` (`core/ZeldaDaughter.Core/Scenes/`).

```json
"objects": [ { "id": "house_a", "model": "house_small", "position": {...}, "rotation": {...}, "collide": true } ],
"paths":  [ { "id": "road_main", "points": [ {"x":0,"z":-26}, {"x":0,"z":-8} ], "width": 3, "color": "#b09a6e" } ],   // лента на земле, terrain road
"water":  [ { "id": "river", "points": [...], "width": 7, "color": "#5f8fa3" } ],                                      // лента воды, terrain water (×0,4)
"zones":  [ { "id": "bridge_deck", "shape": "rect", "center": {"x":6,"z":8.6}, "size": {"x":3.6,"z":9}, "rotation": 0,
              "terrain": "road", "tags": ["bridge"] },                                                              // terrain — необязательно; rect | circle (radius) | strip (points+width)
            { "id": "wolves", "shape": "circle", "center": {...}, "radius": 6, "tags": ["predator_spawn"] } ],
"scatter": [ { "id": "meadow_grass", "area": { "shape": "rect", "center": {...}, "size": {...} },   // или { "ref": "river" } — геометрия пути/воды/зоны
               "models": [ { "id": "grass", "weight": 4 }, { "id": "flower_yellow_a", "weight": 1 } ],
               "density": 18, "seed": 1, "scale": { "min": 0.8, "max": 1.2 }, "minSpacing": 0, "collide": false, "randomYaw": true,
               "avoid": { "paths": 0.8, "water": 1, "objects": 0.6, "zones": ["bridge_deck"], "zoneMargin": 0 } } ]
```

- **Террейн под героем:** `TerrainMap` — земля сцены, затем `paths` (road), `water` (water), затем зоны с `terrain`; побеждает последний слой, поэтому зона-мост после реки отменяет воду. Скорость — `SpeedModel` (`data/movement.json`). В Unity — `TerrainZones` (JSON карты в сцене), `HeroController.CurrentTerrain`; лог `[ZD:Move] terrain a -> b`.
- **Россыпь:** `density` — штук на 100 м²; позиции только из `seed`; отказ, если точка ближе `avoid.*` метров к краю дороги/воды/объекта (по замеренному следу модели) или внутри зоны; `minSpacing` — между элементами одной россыпи; край земли — отступ 1 м. Trees/rocks — `collide: true`, трава — без коллайдеров. Лог `[ZD:Scene] scatter <сцена> placed=N digest=…`.
- Зоны в сцене — объекты `Zones/<id>` с `ZoneArea` (id, теги, геометрия). Пути и вода — ленты без коллайдера; предметы `model` — под `Objects/<id>` (тап по составной модели находит объект по предку).
- Обратная совместимость: у `prologue-grey` и `g1-capsule` новых секций нет — собираются как раньше (тест `Old_scenes_keep_working…`).
- Пробная сцена `scenes/models-test.json` (`build.include: false`): дом, хижина, мост, деревья, камни, забор, поле, реквизит, три россыпи.

## Стартовый регион `scenes/region.json` (D-10)

Генерируется `python3 tools/gen-region.py` (правим скрипт, не JSON). Первая сцена сборки (`build.order: 0`), слот `slot`; серый пролог `prologue-grey` — `include: false` (тестовая сцена, грузится в PlayMode-тестах по пути `Assets/Scenes/prologue-grey.unity`).

- **Маркер** — объект без модели: `{ "id": "anchor_gate", "marker": true, "position": {...}, "tags": [...] }`. Пустой `GameObject` с `SceneTags`; для якорей расписаний, точек спавна, центров зон. Одновременно `marker` и `model`/`shape`/`prefab` — ошибка.
- **Дома** — составные модели; дверь задана в `tools/gen-region.py` (`DOORS`), дом ставится по двери и стороне, куда она смотрит.
- **Теги:** `poi` — точка интереса вдоль главной дороги `road_main` (тест: соседние ≤ 15 с × скорость шага, от старта и до конца дороги тоже); `poi_side` — в стороне от дороги (поляна, логово, коряга); `anchor` — якорь расписания (`data/npcs.json`); `npc` — NPC (капсулы-заглушки до D-12; id `npc_<ключ>` как в `data/dialogues.json`); `pickup` + `item`; `enemy_spawn` + `enemy_<id>` — точка спавна врага (кабан — `spawn_boar`); `predator_zone` — центры зон ночных волков `zone_wolves_forest`/`zone_wolves_river` (радиус — `zoneRadius` в `data/night.json`); `grass_cell` — клетка сухой травы (D-06, `grass_cell_NN`, модель `grass_large`, шаг 1,5 м); зоны с тегом `mud` — грязь после дождя (без `terrain`: замедление включает ядро по дождю); `bed` — кровать (тап — сон); `station` + `smelter`/`anvil` — станки кузницы (`data/recipes.json` `station`); `campfire` + `rest_point` — костёр на спавне; `gate`, `fountain`, `shop`.
