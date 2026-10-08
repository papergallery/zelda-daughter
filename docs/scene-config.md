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

Дальше (R2-02, V-01): зоны декора с весами, точки спавна, тропы, вода — из апрельского формата, по мере надобности.
