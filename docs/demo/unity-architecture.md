# Архитектура Unity-слоя демо (D-11…D-18)

Спроектировано агентом architect 2026-10-08 по коду ядра (`core/ZeldaDaughter.Core`, 304 теста). Источник истины для пакетов W0, C, D-11…D-18. Отклонение — только с записью в `docs/demo/decisions.md`.

## 0. Что сломано сейчас (чинит W0)

- `GameSession.Talk` создаёт `new Conversation(...)` без `onEffect` → метки карты, блокнот, поручения, `teach_coins` в Unity не срабатывают. Нужно `g.Talk(npcId)` (`GameState.cs`).
- `HeroController.Move` передаёт `SpeedModel` пустые модификаторы — раны, перегруз, голод, грязь не замедляют. Нужно `GameState.SpeedModifiers()`.
- `Condition.Tick(dt, RestKind.None)` — должно быть `g.CurrentRest()` (отдых у костра).
- `g.TickWorld`, `g.Combat.Position`/`Tick` не вызываются — нет погоды, травы, ночных волков, прогорания костров.
- Реплики: `topics[0]` + `Say` → `Remarks.SayFirst` (D-01).
- Аллокации каждый кадр в ядре (`WorldClock.AdvanceDays`, `HeroCondition.Tick`, `NpcRoster.Sync`, `TickWorld`, `Camp.Tick`, `Enemy.Tick`, `GestureRecognizer.Feed`) — пакет C8.
- Глифы рун этапа 1 (`data/language.json`) — во встроенном шрифте их нет → нужен резервный Noto Sans Runic (OFL).
- NavMesh-пакета нет (`com.unity.ai.navigation`); TextMeshPro есть (в `com.unity.ugui` 2.0.0).

## 1. Модули

Владелец состояния один — `GameSession`. Каждая функция — презентер (MonoBehaviour) в своём файле, создаётся своей partial-частью `SceneBuilder`, ссылки — через `Configure(...)`, подписка — на `SessionEvents`, действия — вызовы ядра.

| Модуль | Файл (`Assets/Scripts/Runtime/…`) | Ответственность | Пакет |
|---|---|---|---|
| GameSession | `Game/GameSession.cs` | Держит `GameState`; загрузка/сохранение; порядок тика (§2.3); пересылка событий ядра в `SessionEvents`; таблица обработчиков тапа; помощники `Say(topic)`, `BagChanged(why)`, `JumpTime(...)`. Логики функций нет. | W0 |
| SessionEvents | `Game/SessionEvents.cs` (+ `SessionEvents.<Area>.cs`) | Шина событий, обычный C#-класс, `partial` | W0 объявляет всё |
| RollStreams | `Game/RollStreams.cs` | Именованные потоки случайных чисел от сида сессии | W0 |
| WorldIndex | `Game/WorldIndex.cs` | Реестр сцены: объекты (`SceneTags`), зоны, якоря, клетки травы, станки, кровати, точки появления врагов; динамические `Tappable`; `RegisterInto(GameState)` | W0 |
| Tappable | `Input/Tappable.cs` | `Id`, `TapKind`, `ScreenRadiusPx`, `Enabled` | W0 |
| WorldPicker | `Input/WorldPicker.cs` | Экран → `TouchHit` по приоритетам (§3) | W0 |
| InputRouter | `Input/InputRouter.cs` | Жест → действие: тап → `session.Tap(id)`, лонг-пресс → радиальное меню, отметки подсказок | W0 |
| HeroController | `Hero/HeroController.cs` | + `SetPicker`, `SetSpeedSource`, `Locked`, `FingerPosition`, `CancelMove()` | W0 |
| BillboardSprite | `Rendering/BillboardSprite.cs` | Общий спрайт героини/NPC/зверей: 4 направления (бок — зеркало), кадр по пройденному пути, позы кодом (наклон, присед, тряска, «лежит»), оттенок через `MaterialPropertyBlock`, пятно-тень | W0 — API+заглушка, D-11 — наполнение |
| Реестры | `Rendering/CharacterSpriteSet.cs`, `CharacterRegistry.cs`, `IconRegistry.cs`, `Audio/SoundRegistry.cs`, `World/FxRegistry.cs`, `UI/UiLook.cs`, `Rendering/SpriteLook.cs` | ScriptableObject, по одному на вид ассетов (§2.5) | W0 |
| HeroView | `Hero/HeroView.cs` | Направление, шаг (хромота — сбитый ритм), удар, подбор, еда, разделка, оттенки ран, нокаут; событие `Step` для звука | D-11 |
| ScreenFader | `UI/ScreenFader.cs` | Нокаут (тьма с проблесками) и сон (затемнение → действие → возврат) | W0 API, D-11 вид |
| PickupPresenter | `World/PickupPresenter.cs` | Тап по `SceneTags.Item`, скрытие поднятого (`g.Picked`) | W0 (перенос) |
| NpcPresenter / NpcView | `NPC/NpcPresenter.cs`, `NPC/NpcView.cs` | `g.Npcs.Sync()`, ходьба по маршрутам, расстановка у якоря, сон (скрыт), поза в таверне, жест «туда» | D-12 |
| TalkPresenter / TalkBubbleView | `NPC/TalkPresenter.cs`, `UI/TalkBubbleView.cs` | `g.Talk`; облачко NPC (руны/смесь/текст + иконки), ответы героини иконками, «?», иконка `trade` у открытой лавки | W0 перенос, D-12 переписывает |
| CombatPresenter / EnemyView / CarcassView | `Combat/*.cs` | Враги из ядра (спавны + ночные волки), удар, читаемый замах, урон без цифр, туша, разделка | D-13 |
| RadialMenu | `UI/RadialMenu.cs` | Сумка / карта / блокнот; карта скрыта, пока `!g.Map.HasMap` | D-14 |
| InventoryWindow | `UI/InventoryWindow.cs` | Ячейки `g.Data.Inventory.Slots`, иконки, количество | D-14 |
| ItemDrag | `UI/ItemDrag.cs` | Одно перетаскивание: предмет на предмет (крафт), за край окна — в мир | D-14 |
| CampPresenter | `World/CampPresenter.cs` | `g.Camp.Objects`, `g.Camp.Campfires`: модель, огонь, свет по `Campfire.Light`, прогорание | D-14 |
| WindowStack | `UI/WindowStack.cs` | Одно окно, закрытие тапом мимо, стоп ходьбы при открытии | W0 |
| StationWindow / TradeWindow / MapWindow (+ Editor `MapBaker.cs`) / NotebookWindow / RestPresenter | `UI/*.cs`, `World/RestPresenter.cs` | Станки, торговля, карта, блокнот, сон | D-15 |
| NatureFx / HeroTorchLight | `World/NatureFx.cs`, `Hero/HeroTorchLight.cs` | Дождь, мокрая земля, грязь, горящая/сгоревшая трава, ветер; свет факела, пока `g.Bag.Count("torch") > 0` | D-16 |
| AudioDirector / BardSource | `Audio/*.cs` | Среда, шаги по поверхностям, город, бой, огонь, бард | D-17 |
| HintView / RemarkBubble | `UI/HintView.cs`, `UI/RemarkBubble.cs` | Рука-подсказка; облачко реплик героини | D-18 (W0 — заглушки) |
| SessionUI | `Game/SessionUI.cs` | Корневой Canvas и слои + фасад для тестов (`CurrentHint`, `HeroBubbleText`, `NpcBubbleText`, `ReplyButtons`) | W0 |

Объединено: SleepFader → ScreenFader; CraftDrag+PlaceDrag → ItemDrag; костёр и положенные вещи → CampPresenter; окна — композиция `WindowFrame` + содержимое `IWindow`, без наследования.

## 2. Контракты

### 2.1 GameSession — публичный API (API тестов T-05/T-10 сохраняется)
```csharp
public GameState State { get; }  public SessionEvents Events { get; }  public SessionUI UI { get; }
public WorldIndex Index { get; }  public RollStreams Rolls { get; }  public HeroController Hero { get; }
public string SlotPath { get; }  public void Save(string reason);
public void Tap(string objectId);                                  // диспетчер по TapKind
public void OnTap(TapKind kind, Action<Tappable> handler);         // презентеры регистрируются в Start
public void Reply(string icon);                                    // → TalkPresenter (тестовый API)
public bool Say(string topic);                                     // Remarks.Say + HeroSaid + [ZD:Remark]
public void BagChanged(string why);
public void JumpTime(Func<IReadOnlyList<ClockEvent>> jump, double hours); // сон: события часов, TimeJumped, Sync со snap
```
Ссылку на `g` презентеры берут только из `Events.StateReady`. На события ядра (`Map.MarkOpened`, `Notebook.EntryAdded`, `Quests.Done`, `Carcasses.Disappeared`) подписан только `GameSession`, он пересылает.

### 2.2 SessionEvents
| Событие | Поднимает | Слушают |
|---|---|---|
| `StateReady(GameState)` | Session после загрузки | все |
| `Clock(ClockEvent)` | тик | NatureFx, Audio |
| `TimeJumped(double hours)` | `JumpTime` | NpcPresenter (snap), CampPresenter, NatureFx |
| `World(WorldEvent)` | `TickWorld` | NatureFx, CampPresenter (`CampfireBurntOut`), CombatPresenter (`PredatorSpawned/Despawned`), HeroView (`HeroScorched`), Audio |
| `NpcMoved(NpcChange, bool snap)` | Sync | NpcPresenter |
| `Condition(ConditionEvent)` | тик, бой | HeroView, ScreenFader, Audio |
| `Skill(SkillChange)` | тик, бой | Session → `Say(Topics.Skill(...))` |
| `HeroSaid(topic, line)` | `Say` | RemarkBubble, Audio |
| `HeroActed(HeroAct{Kind: Strike/Pickup/Eat/Treat/Butcher/Place/Craft, toward, item})` | презентеры | HeroView, Audio |
| `BagChanged` | `BagChanged()` | InventoryWindow, TradeWindow, HeroTorchLight, Session (подсказка `has_item`) |
| `PickedUp(objectId, itemId)` | PickupPresenter | HeroView, Audio |
| `Enemy(EnemyNotice{EnemyId, EnemyEvent})` | CombatPresenter | EnemyView, HeroView, ScreenFader (`HeroKnockedOut`), Audio |
| `HeroStruck(enemyId, StrikeResult)` | CombatPresenter | HeroView, EnemyView, Audio |
| `Looted(carcassId, LootResult)`, `CarcassGone(id)` | CombatPresenter | CarcassView, Audio |
| `Placed(PlacedObject)`, `UsedOnWorld(objectId, UseResult)` | ItemDrag / CampPresenter | CampPresenter, NatureFx, Audio |
| `TalkStarted/TalkEnded(npcId)` | TalkPresenter | NpcView, Audio |
| `MarkOpened(id)`, `NoteAdded(entry)`, `QuestDone(id)` | Session (из ядра) | MapWindow, NotebookWindow, RemarkBubble |
| `TradeRequested(npcId, itemId?)` | TalkBubbleView (иконка `trade`), ItemDrag | TradeWindow |
| `Traded(TradeResult)` | TradeWindow | Audio, Session |
| `WindowOpened/Closed(id)` | WindowStack | InputRouter, HintView, Audio |

Связи между пакетами — только событиями.

### 2.3 Тик (только `GameSession.Update`)
1. `dt = min(Time.deltaTime, 0.1)`.
2. Позиция героини → `g.HeroPosition`, `HeroHeight`, `HeroFacingDegrees`.
3. `g.Step(dt, walkedMeters, restOverride, report)` (C1) — часы, состояние с `CurrentRest`, голод, `Combat.Position`+`Tick`, навык ношения.
4. Мир — фиксированным шагом 0,25 с: `g.TickWorld(0.25f, Rolls.World.Next())`.
5. Враги — фиксированным шагом 0,05 с: `g.Enemies.Tick(...)` (C2); EnemyView сглаживает.
6. `g.Npcs.Sync()` раз в 0,5 с.
7. Реплики раз в `RemarkCheckSeconds` через `SayFirst`; подсказка обновляется при смене.
8. Автосохранение.
Окна мир не останавливают; время стоит только при паузе приложения.

### 2.4 Ссылки
Только `Configure(...)` из `SceneBuilder`, `[SerializeField] private`; без `Find*`; `Resources.Load` — только данные. Динамические объекты — `WorldIndex.RegisterDynamic(Tappable)`.

### 2.5 Реестры ассетов
Источник истины — JSON (правится на сервере), `.asset` собирает `Editor/Art/RegistryBuilder.cs` (меню Zelda → Art → Build registries; вызывается из `BuildAll`).

| Реестр | Источник → ассет | Ключи | Без ассета |
|---|---|---|---|
| `CharacterRegistry` → `CharacterSpriteSet` (`Front[]`, `Back[]`, `Side[]`, `Down?`, `PixelsPerMeter`, `StrideMeters`) | `Assets/Art/Registries/characters.json` | `heroine`, id из `npcs.json`, `enemies.json` | `PlaceholderSprites`: цветной силуэт с «носом» |
| `IconRegistry` ×2 | `item-icons.json` → `ItemIcons.asset`; `talk-icons.json` → `TalkIcons.asset` | id предметов; иконки диалогов + `trade`, `radial_bag`, `radial_map`, `radial_notebook`, `hint_hand` | Бумажная плашка с буквой; `[ZD:Art] missing icon <id>` |
| `SoundRegistry` (`SoundDef{clips[], volume, pitchJitter, spatial}`) | `sounds.json` → `Sounds.asset` | `step_<surface>`, `amb_*`, `hit_*`, `fire_*`, `bard_tavern` | Тишина + предупреждение |
| `FxRegistry` | `fx.json` → `Fx.asset` | `campfire`, `torch_flame`, `grass_fire`, `burnt_patch`, `rain`, `placed_<item>` | Примитив |
| `UiLook`, `SpriteLook` | Editor-скрипт | Шрифт, бумага, тушь, 9-slice; параметры билборда | — |

Картинки: спрайты D-09 — `Assets/Art/Sprites/<id>/` (git); сгенерированные иконки — `Assets/Art/Icons/<id>.png` (git); Medieval Kingdom UI — `Assets/ThirdParty/…` (не git; без папки — заглушка). Проверка на сервере — `tools/check-registries.py` в `check.sh` (каждый id есть в JSON или `"placeholder": true`).

### 2.6 UI
- Один Canvas `SessionUI` (Overlay, 1080×2340) + вложенные `World` (облачка, рука, призрак перетаскивания; `raycastTarget = false`), `Windows`, `Overlay` (затемнение).
- Строится кодом: `UI/UiKit.cs` — `Text`, `Icon`, `Slot`, `Bubble`, `Button` на `UiLook`. LayoutGroup — только в статичных окнах.
- Шрифт: TextMeshPro, статический атлас — рукописный кириллический OFL (Caveat или Neucha — выбирает W0) + резервный **Noto Sans Runic** (OFL) для глифов `language.json`. `Editor/Art/FontBuilder.cs`; TTF + `OFL.txt` — `Assets/Art/Fonts/`. TMP Essentials W0 импортирует кодом один раз (осторожно: модальное окно глушит мост).

## 3. Ввод
`HeroController.Update` → `OnTouch` (касание над UI отброшено) → `GestureRecognizer` (ядро, без изменений) → `Gesture` → **`InputRouter`** (GameSession больше не подписан на `Gesture`).

`HeroController.HitAt` → `WorldPicker.Pick(screen)`, если picker задан (иначе старый код — T-05 не меняются). Порядок:
1. Героиня (`GestureSettings.IsOnHero`) → `TouchHit.Hero` (только лонг-пресс).
2. Динамические цели в радиусе `ScreenRadiusPx × dpi/ReferenceDpi`, минимум по (приоритет, расстояние): Enemy 0 → Carcass 1 → Npc 2 → Pickup 3 → Placed/Campfire 4 → Station/Bed 5.
3. Луч `Physics.Raycast` → `GetComponentInParent<Tappable>()` (у каждого объекта со `SceneTags` — `Tappable`, kind Scenery по умолчанию).
4. Иначе `Ground`.

Тап → `GameSession.Tap(id)` → обработчик по `TapKind`: Enemy → `CombatPresenter.Strike`; Carcass → `g.Carcasses.Tap`; Npc → `TalkPresenter.Begin` (`g.Talk`); Pickup → `g.Bag.Add`; Placed → `g.Camp.PickUp`; Station → `StationWindow`; Bed → `RestPresenter`. Враг далеко (`OutOfRange`) — героиня поворачивается, `[ZD:Combat] out_of_range`, не идёт.

Лонг-пресс на героине → `RadialMenu.Open`; сектор подсвечивается по `HeroController.FingerPosition`; отпустила на секторе — окно; в центре — меню остаётся как кнопки.

Окна: касание над окном — UGUI; `WindowStack` — подложка (тап мимо закрывает, до героини не доходит); пока окно открыто — `HeroController.Locked = true`, `CancelMove()`.

Перетаскивание (`ItemDrag`, порог `input.json moveThreshold`):
- на другую ячейку → `g.Crafting.Combine(a, b, g.Bag)`: `Done` → `craft_ok`, `NoRecipe` → `craft_fail`, `NeedsStation` → `craft_station`, `NoRoom` → `craft_no_room`;
- за край окна → окно прячется (`WindowStack.Suspend`), иконка на пальце; отпустила: героиня → `g.UseOnHero(item)` (C4); NPC → `g.Quests.Give` или `TradeRequested`; положенная вещь/костёр/горящая трава → `g.UseOnWorld(id, item)`; земля → `g.Camp.Place(item, точка, validSurface)` (луч в `Ground`, `CheckSphere(0.3, Blocking)` пуст, не `water`); «не вышло» — окно возвращается;
- над объектом с `g.Crafting.HasWorldUse(kind, item)` — героиня один раз говорит `hint_world_use` (§7);
- долгий тап по ячейке — описание предмета (когда появятся описания).

Сигнатуры `Feed`, `OnTouch`, `HitAt`, `UseDpi`, `IsOverUI`, `IsMoving`, строки логов `[ZD:Move]`, `[ZD:Gesture]`, `[ZD:Pickup]`, `[ZD:Talk] … point …`, `[ZD:Remark]`, `[ZD:Save]`, кнопки `Reply_<icon>` — сохраняются.

## 4. Пакет C — ядро для вида (сервер, тесты до кода)
1. **C1** `GameState.Step(float dt, float walkedMeters, RestKind restOverride, StepReport into)` + перегрузки `WorldClock.Advance(double, List<ClockEvent>)`, `HeroCondition.Tick(float, RestKind, List<ConditionEvent>)`; `StepReport` — переиспользуемые списки.
2. **C2** `GameState.Enemies : EnemyRoster` — `Spawn(id, defId, Vec2) : Enemy?` (null, если в `Killed`), `Remove`, `Get`, `Active`, `Tick(dt, roll, List<EnemyNotice>)`, `Strike(enemyId, roll) : StrikeResult` (через `WeaponInHand`); при `Died` — `EnemyKilled` и `Predators.Released`; `Predators.Locate` подключён; `TickWorld` сам спавнит/убирает ночных; `Blocked : Func<Vec2,bool>` от вида.
3. **C3** `GameState.WeaponInHand` — сильнейшее оружие в рюкзаке (по `Damage`, при равенстве по id), иначе `fists`.
4. **C4** `GameState.UseOnHero(item) : HeroUseResult {Ate|Treated|NotUsable, Topic}` (лекарство списывается).
5. **C5** `Trade.Evaluate(traderId, offer) : TradeResult` — без изменений состояния.
6. **C6** `Enemy.StateSeconds`, `Enemy.WindupProgress`.
7. **C7** Сцена (согласовать с владельцем `region.json`): `ObjectConfig.Enemy`, `ObjectConfig.Station`; `shape: "empty"`; секция `walkways`; `RouteGraph.From(SceneConfig)` → `Route(fromAnchor, toAnchor)` (Дейкстра по `paths` + `walkways`); `GrassCells.Grid(Area, spacing, prefix)`; тест `RegionLinksTests` (якоря, метки, жесты, зоны `night.json`, грязь — круги, достижимость якорей, маршруты не пересекают дома по `ModelCatalog.Bounds`).
8. **C8** Без выделений в обычном кадре: `Advance`, `Tick`, `Sync`, `TickWorld`, `Camp.Tick`, `Enemy.Tick`, `GestureRecognizer.Feed` — общий пустой массив, если ничего не произошло; `GameState.SpeedMultiplier` без `yield`.
9. **C9** Данные: `npcs.json walkSpeed`; `elements.json grass.cellSpacing`; благодарность при сдаче поручения.

## 5. SceneBuilder
- W0: `Editor/SceneBuilder.Session.cs` — `BuildContext` (config, DataSet, catalog, hero, cam, iso, session, uiRoot, index, реестры, объекты по id); объект `Game` (GameSession, SessionUI, InputRouter, WorldPicker, WorldIndex, WindowStack, ScreenFader, PickupPresenter, TalkPresenter, EventSystem); partial-методы по порядку: `AddHeroView` (D-11, `SceneBuilder.Hero.cs`), `AddNpcs` (D-12), `AddCombat` (D-13), `AddItemsUi` (D-14), `AddScreens` (D-15, + MapBaker), `AddNature` (D-16), `AddAudio` (D-17), `AddHints` (D-18). `SceneBuilder.cs` правит только W0.
- `Tappable` из конфига: `item` → Pickup; `station` → Station; тег `bed` → Bed; тег `npc` → Npc; тег `anchor` → только `WorldIndex.Anchors`.
- Слои `Ground`, `Blocking`, `Actors` — `ProjectSetup.cs`.
- NPC (D-12): по записи `npcs.json` — `Npcs/npc_<id>` с `BillboardSprite`, `NpcView`, `Tappable(Npc)`, без коллайдера; маршруты `RouteGraph` сериализуются в `NpcPresenter`.
- Враги (D-13): объекты с `enemy` → маркеры `Spawns/<id>`; враги создаются в игре, кроме `g.Killed`; туши — из `g.Carcasses.Active`.
- Природа (D-16): зоны с тегом `dry_grass` → `GrassCells.Grid`; `WorldIndex.RegisterInto(g)`: `Nature.Grass.AddCell`, `Mud.AddZone` (зоны `mud`), `Predators.AddZone` (id из `night.json`); до `Load`.
- Героиня (D-11): `hero.sprite` в конфиге → рендер капсулы выключен, дочерние `BillboardSprite` + `HeroView`.
- Тестовая сцена W0: `scenes/test-demo.json` (`build.include: false`, без слота) — крестьянин, якоря, кабан, кровать, наковальня, плавильня, трава, грязь, зона волков, предметы.

## 6. Порядок и параллельность
| Волна | Пакет | Файлы | Зависит |
|---|---|---|---|
| 0 | **C** ядро для вида | `core/…`, `data/…` | — |
| 0 | **W0** каркас | всё «W0» + `SceneBuilder.Session.cs`, `scenes/test-demo.json` | — |
| A | D-11 героиня | `Hero/HeroView.cs`, `BillboardSprite` (наполнение), `ScreenFader` (вид), `SceneBuilder.Hero.cs` | W0 |
| A | D-12 NPC и разговор | `NPC/*`, `UI/TalkBubbleView.cs`, `SceneBuilder.Npcs.cs` | W0, C7 |
| A | D-14 меню, инвентарь, перетаскивание, костёр | `UI/RadialMenu.cs`, `InventoryWindow.cs`, `ItemDrag.cs`, `World/CampPresenter.cs`, `SceneBuilder.Items.cs` | W0, C4 |
| A | D-18 подсказки и реплики | `UI/HintView.cs`, `UI/RemarkBubble.cs`, `SceneBuilder.Hints.cs` | W0 |
| B | D-13 бой | `Combat/*`, `SceneBuilder.Combat.cs` | W0, C2, C3, C6 |
| B | D-15 экраны | `UI/StationWindow.cs`, `TradeWindow.cs`, `MapWindow.cs`, `NotebookWindow.cs`, `World/RestPresenter.cs`, `Editor/MapBaker.cs`, `SceneBuilder.Screens.cs` | W0, C5 |
| B | D-16 стихии и ночь | `World/NatureFx.cs`, `Hero/HeroTorchLight.cs`, `SceneBuilder.Nature.cs` | W0, C7–C9 |
| B | D-17 звук | `Audio/*`, `SceneBuilder.Audio.cs` | W0 |

Общие: `Registries/*.json` — по строке на запись, сортировка; `decisions.md` — только дописывать; тесты — файл на пакет (`Tests/PlayMode/D11HeroViewTests.cs` …).

**Очередь на ПК** (`/tmp/zelda-editor-turn`, `docs/agent-handbook.md`): пушить в master — только в свою очередь; очередь освобождается только при чистой компиляции (иначе исправить или откатить свой коммит). Порядок: W0 → D-11 → D-14 → D-12 → D-18 → D-13 → D-15 → D-16 → D-17 → D-19. В каждую очередь: компиляция → `BuildAll` → все PlayMode → кадры. До D-09 — заглушки; кадры со спрайтами — повторной очередью. Финальные кадры — `region`, тесты — `test-demo`.

## 7. PlayMode-тесты по пакетам
- **W0:** `StateReady` один раз; `Tap` по виду цели; picker: враг важнее NPC при перекрытии, попадание в героиню; после ответа `town` крестьянину `g.Map.IsOpen("town")`; с переломом 1.0 свайп проходит меньше; костёр с малым `BurnLeft` → `CampfireBurntOut`; `ProfilerRecorder "GC Allocated In Frame"` = 0 за 120 кадров простоя (после C8); T-05/T-10 зелёные без правок.
- **D-11:** к камере → Front, от камеры → Back, вправо/влево → Side (зеркало); стоит — кадр не меняется; `Limping` — неровный ритм; удар — поза удара; нокаут: «лежит», затемнение > 0,8 с проблеском, после `Revived` — 0.
- **D-12:** NPC у якорей текущего часа; 16:55→17:05 торговец идёт в таверну (отклонение от маршрута ≤ 0,3 м, не сквозь дом); после сна — сразу на месте; понимание 0 → руны, 1 → исходная строка; `Reply_<icon>`; «?» при непонимании; жест поворачивает к цели; ночью нет `trade`.
- **D-13:** тап по кабану → `[ZD:Combat] hit|miss`; при `Windup` амплитуда > 0; отбежала → `Dodged`; туша без ножа — минимум, с ножом — полный набор и исчезает; убитый кабан не возвращается после перезагрузки; ночной волк не ближе `MinHeroDistance`.
- **D-14:** лонг-пресс → меню → сумка → окно с `Slots` ячейками; палка + ткань → `torch_unlit`; ягоды на героиню → голод ниже; дрова на землю → лежат; на стену → `place_invalid`, предмет в рюкзаке; кремень на дрова → костёр со светом; `torch_unlit` на костёр → `torch`.
- **D-15:** наковальня: металл + палка + удар → меч; кровать: часы +`SleepHours`, раны ≤ порога; бартер с травницей → `Done`; в 22:00 → `Closed`; без карты сектора нет, с картой метки видны; блокнот после крестьянина.
- **D-16:** `StartRain` → дождь, `_ZD_Wetness` > 0; факел на сухую клетку → горящих больше, потом гарь; в грязи медленнее; ночью свет костра > 0, факел — свет на героине.
- **D-17:** шаги дорога/трава — разные клипы (`[ZD:Audio] step road|grass`); лес/город — разная среда; бард вечером, днём молчит; число AudioSource постоянно.
- **D-18:** рука свайпа исчезает после свайпа; подсказка тапа — рядом с целью; облачко реплики гаснет через `RemarkBubbleSeconds`; вне окон инвентаря/торговли нет цифр в TMP-текстах.

## 8. Риски
- Цифры количества в инвентаре/торговле — показываем (правило «без цифр» — о состоянии героини); решение агента.
- Перетаскивание сразу, без лонг-пресса; долгий тап — описание. Решение агента.
- Ошибка компиляции ломает всех → правило очереди (§6).
- D-10/D-09 ещё не готовы → тесты на `test-demo`, кадры ждут.
- GC в ядре → C8 + фиксированный шаг мира и врагов; тест W0.
- Перестроения UI → вложенный Canvas, текст только при изменении, TMP со статическим атласом.
- Спрайты: альфа-срез с записью глубины (иначе контур D-08 по глубине не увидит) — согласовать `SpriteLook` с D-08; тень — пятно.
- Огонь: пул огня травы ≤ 12 эффектов, гарь — `Graphics.RenderMeshInstanced`.
- Детерминизм: `UnityEngine.Random` не используется; `RollStreams` (World, Combat, Enemies, Talk, Remarks, отдельно Fx); генератор SplitMix64 — сделать публичным в `Core.Common`.
- Враги сквозь стены, если C2 `Blocked` не успеет — в известные проблемы D-19.
- Облачка и подсказки — `raycastTarget = false`.
- TMP Essentials и слои — кодом; модальное окно импорта TMP может заглушить мост — W0 делает это первым и отдельно.
