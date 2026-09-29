# Реестр апрельского кода (R0-04)

Снят 2026-09-30 аудитом без запуска Unity. **Статус всего ниже — «написано, не проверено»** (ADR-0005): в апреле проверялось на эмуляторе на капсулах, результатов EditMode-тестов нет. Unity 2022.3.30f1, URP 14 — до перехода на Unity 6 (R1-01).

## Объём
242 файла `.cs`, 30 949 строк; из них Editor — 51 файл, 15 370 строк. asmdef: `ZeldaDaughter` (runtime), `ZeldaDaughter.Editor`, `ZeldaDaughter.Tests.EditMode`. ScriptableObject-данных — 99 (`Data/`, `Content/`).

| Папка | Файлов | Строк | Что внутри | Судьба |
|---|---|---|---|---|
| Input | 3 | 587 | `GestureDispatcher`, `CharacterMovement`, `CharacterAutoMove` | Р2 — основа управления |
| World | 46 | 3 504 | `DayNightCycle`, `WeatherSystem`, `Element*`, `WaterZone`, ресурсы | Р2 (свет), дальше по системам |
| Combat | 35 | 2 364 | `CombatController`, `EnemyFSM`, `Boar/WolfBehavior`, `Wound*`, `KnockoutSystem`, `HungerSystem` | после G2; формулы → ядро логики (R1-04) |
| UI | 27 | 2 512 | радиальное меню, инвентарь drag&drop, пузыри, подсказки | после G2, стиль — по GК |
| NPC | 24 | 2 127 | `DialogueManager`, `LanguageSystem`, `TextScrambler`, `NPCScheduler`, `TradeManager` | R2-03 (крестьянин), дальше город |
| Progression | 12 | 881 | `PlayerStats`, `ActionTracker`, `WeaponProficiency*` | после G2; формулы → ядро |
| Inventory | 10 | 687 | `PlayerInventory`, `WeightSystem`, `CraftingSystem` | после G2; формулы → ядро |
| Audio | 7 | 677 | шаги, зоны | R2-06 |
| Rendering | 5 | 402 | `BillboardRenderer`, `BillboardDirectionResolver`, `CharacterSpriteController`, `CharacterVisualConfig` | R2-01 — ни к чему не подключено |
| Quest | 5 | 375 | `QuestManager`, `MapManager`, `NotebookManager` | после G2 |
| Save | 3 | 275 | `SaveManager` (JSON, `ISaveable` ×12) | после G2 |
| Debug | 11 | 1 049 | `RemoteInputReceiver`, `CrashDiagnostics`, `AwakeTracer`, `MeshBisector` — **попадают в релиз** | R1-06: за `#if ZD_DEBUG` |
| Editor | 51 | 15 370 | 17 сборщиков сцен и «фиксеров», 71 пункт меню в двух корнях | R1-06: один сборщик из конфига |

## Долги в коде
- 32 `FindObjectOfType`/`FindObjectsOfType` и 16 `FindWithTag("Player")` в runtime (`SaveManager`, `InventoryDragHandler`, `QuestManager`, `MapManager`, `WaterZone`…); синглтоны `SaveManager`, `WeatherSystem`, `SpeechBubbleManager`; 69 `static event`.
- Литералы вместо SO: `CharacterMovement` (26), `EnemyFSM` (23), `CombatController` (18), `DayNightCycle` (15), `RadialMenuController` (15).
- `ZD_DEBUG` включён в `ProjectSettings` для Android постоянно; `AndroidBuilder.cs:75` — `ZD_EMULATOR` не добавляется при `ZD_DEBUG`.
- `AndroidBuilder` отключает URP и меняет материалы; `IOSBuilder` возвращает — настройки зависят от последнего запуска.
- Дубли префабов врагов (`Boar`/`Enemy_Boar`/`Emu_Boar`, то же с волком); 26 сцен, играбельная одна — `DemoScene`.
- SRP Batcher выключен; `activeInputHandler: 0` (пакет Input System не используется); Cinemachine не используется.

## Тесты
~210 EditMode-тестов в 10 файлах (Combat 67, Inventory 33, Progression 33, WeaponProficiency 17, Language 16, Quest 13, Dialogue 10, Trade 8, Weather 8, Element 5). PlayMode нет. Прогонов не зафиксировано — первый прогон R1-05.

## Известные баги апреля (`archive/2026-04/TESTING_OBSERVATIONS.md`, память сессии 2026-04-10)
Персонаж проваливается (Y≈-6000) после 10+ свайпов; подсказка свайпа не видна, TapHint не исчезает; лонг-пресс не попадает в персонажа; `butcherToolId="knife"` vs `item_knife` — полная разделка не работает; `ItemCache` не знает крафтовых предметов; `tap_enemy` выбирает мёртвых; `KnockoutSystem` оживляет за ~30 мс; подбираемое возрождается после загрузки.

## Арт
3D: KayKit, Kenney, FantasyProps, Quaternius (животные). 2D-спрайтов нет (`art/sprites/protagonist/*` пусто); пробы стиля героя — `art/concepts/` (не в git, 32 PNG).
