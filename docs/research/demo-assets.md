# Демо стартового региона: что из купленного реально есть на ПК и годится

Агент, 2026-10-08. Только чтение: на ПК ничего не скачивалось, не импортировалось и не менялось. Unity не запускался.

**Что проверено:**
- кэш Asset Store `C:\Users\paper\AppData\Roaming\Unity\Asset Store-5.x\` (далее `%AS%`): полный список файлов, размеры, даты; состав нужных `.unitypackage` прочитан потоком gzip+tar, пути взяты из записей `pathname`;
- импортированное: `C:\dev\zelda\ZeldaDaughter\Assets\` и апрельский `C:\dev\zelda\UnityProject\Assets\`, проект The Chest `C:\dev\thechest\unity\TheChest\Assets\` (имена, размеры папок, состав нужного);
- поиск `*.unitypackage` по `C:\Users` и `C:\dev`;
- база Asset Inventory `C:\Users\paper\OneDrive\Документы\AssetInventory\AssetInventory.db`;
- список 654 покупок `/var/www/html/thechest/docs/asset-library.md`, реестр `/var/www/html/thechest/docs/asset-licenses.md`, разбор `docs/research/asset-library-zd.md`;
- CC0-модели апреля в git: `archive/2026-04/UnityProject/Assets/Models/`;
- несколько страниц Asset Store и издателей (ссылки — в конце).

Проект ZD: Unity **6000.3.24f1**, URP **17.3.0** (`ZeldaDaughter/ProjectSettings/ProjectVersion.txt`, `ZeldaDaughter/Packages/manifest.json`). Расхождение «2022.3 / URP 14» из `asset-library-zd.md` устарело: тот проект теперь архив.

---

## Главное

1. **На диске почти ничего подходящего по стилю нет.** В кэше 31 пакет. Почти все скачаны 2026-09-20…09-30 под The Chest: реалистичный HDRP, ПК. Это NatureManufacture, GameAssetFactory, Big Castle Kit, Rock and Boulders 2, MicroSplat, Bakery и подобные. Ни одного stylized/low-poly окружения в кэше нет.
2. **Под демо уже сейчас годны:**
   - звук: Footsteps, Medieval Fantasy SFX Bundle, Cafofo Village Ambience;
   - огонь: Ian's Fire Pack, есть URP-префабы костра и факела;
   - UI: Medieval Kingdom UI (иконки, значки карты) и, как заглушка, Layerlab GUI Pro - Fantasy RPG.
3. **Всё stylized есть только на аккаунте и не скачано.** Это Flat Kit, Pure Poly Blacksmith, Toony Tiny RTS, Triforge, Sics, Synty, KayKit и другие. Скачивание через `AssetStoreDownloadManager` займёт минуты, но состав и стиль до скачивания проверить можно только по витрине.
4. **Самая полная готовая база мира — не покупки, а CC0 апреля в git.** Это Kenney Nature Kit 2.1 (329 FBX) и Fantasy Town Kit 2.0 (167 FBX) и Quaternius Fantasy Props (CC0). Там есть почти всё из списка демо: мост, река, дорога, поле, частокол, ворота, костёр, лавка, фонтан, кровать, наковальня, предметы.
5. **Индекс Asset Inventory пуст:** таблицы `Asset` и `AssetFile` по 0 строк. Библиотеку так и не проиндексировали, поиск по составу 654 пакетов недоступен.
6. **Риск утечки в публичный git.** `.gitignore` закрывает только `/UnityProject/Assets/ThirdParty/`. Путь `ZeldaDaughter/Assets/ThirdParty/` **не игнорируется**: `git check-ignore` ничего не нашёл. Первый же импорт покупного пакета по ADR-0004 попадёт в публичный репозиторий. Правило в `.gitignore` надо добавить до любого импорта. Агент этого не правил: задача только читать.

---

## 0. Что физически лежит на ПК

### Кэш Asset Store (`%AS%`, 31 файл)

| Пакет | Путь от `%AS%` | Размер | Для демо ZD |
|---|---|---|---|
| Ian's Fire Pack | `Ian Scilipoti\Particle SystemsFire\Ians Fire Pack.unitypackage` | 56,3 МБ | **да**: костёр, факел, горн (см. §1) |
| Footsteps - Pack | `cplomedia\AudioSound FXFoley\Footsteps - Pack.unitypackage` | 626,5 МБ | **да**: шаги, брать выборочно |
| Medieval Fantasy SFX Bundle | `Magic Sound Effects\AudioSound FX\Medieval Fantasy SFX Bundle.unitypackage` | 1 543,1 МБ | **да**: удары, кровь, UI, ветер, дождь; брать выборочно |
| GUI Pro - Fantasy RPG (Layerlab) | `LAYERLAB\Textures MaterialsGUI Skins\GUI Pro - Fantasy RPG.unitypackage` | 158,9 МБ | заглушки иконок, шрифты OFL; стиль глянцевый, не наш |
| Medieval Farm Tools (Black House) | `Black House\3D ModelsPropsTools\Medieval Farm Tools.unitypackage` | 104,8 МБ | только болванки или рендер в иконку: реалистичный PBR, Standard |
| Asset Inventory | `Impossible Robert\Editor ExtensionsUtilities\Asset Inventory.unitypackage` | 5,6 МБ | инструмент для индекса библиотеки |
| Mesh Optimizer | `IndieChest\ScriptingModeling\Mesh Optimizer.unitypackage` | 84,3 МБ | инструмент: LOD и снижение полигонажа |
| Procedural fire (Hovl) | `Hovl Studio\Particle SystemsFire\Procedural fire.unitypackage` | 52,8 МБ | нет: магические огненные сферы, не костёр |
| Rock and Boulders 2 | `Manufactura K4\3D ModelsPropsExterior\Rock and Boulders 2.unitypackage` | 320,9 МБ | нет: фотореализм, свои шейдеры |
| The Big Castle Kit | `Triplebrick\3D ModelsEnvironmentsHistoric\The Big Castle Kit.unitypackage` | 1 435,5 МБ | нет, разве что болванки: реалистичный каменный замок (есть `woodhut_a…c`, `booth`, `bed_a1…a4`, `fireplace`, `gate_a`) |
| Medieval Houses Modular Vol 1 | `GameAssetFactory\3D ModelsEnvironments\Medieval Houses Modular Vol 1.unitypackage` | 2 735,0 МБ | нет: реализм, готовый дом 54 584 трис (`thechest/docs/s3-05-cottage-kit.md`) |
| NatureManufacture: Forest Environment / Mountain Trees / Advanced Foliage Pack 2.0 | `NatureManufacture\…` | 3 501 / 749 / 1 540 МБ | нет: реализм, вес |
| Free SpeedTrees | `SpeedTree\…\Free SpeedTrees Package.unitypackage` | 73,5 МБ | нет: реализм, своя лицензия |
| Terrain Sample, HDRI Pack, HDRP Time Of Day, InfiniCLOUD, Bakery, MicroSplat, MicroVerse, EasyRoads3D Pro, Real Ivy 2 | `Unity Technologies\…`, `Procedural Worlds Inc\…`, `ARTnGAME\…`, `Mr F\…`, `Jason Booth\…`, `AndaSoft\…`, `3Dynamite\…` | 104–1 636 МБ | нет: террейн, HDRP, ПК |
| Crafting Mecanim Animation Pack, Texture Combiner, F Texture Tools, Vegetation Spawner, Procedural Terrain Painter, Ultimate Screenshot Tool | — | 0,7–12,5 МБ | не нужны. Ultimate Screenshot Tool ронял компиляцию (CS0433, The Chest) |

### Импортировано в The Chest, в кэше нет (`C:\dev\thechest\unity\TheChest\Assets\`)

| Папка | На диске | Для демо ZD |
|---|---|---|
| `MedievalKingdomUI` (Poneti) | 1 644 файла, 936 МБ | **да**, см. §1 «UI» |
| `Plugins\CafofoStudio` (Village Ambience) | 580 файлов, 381 МБ | **да**: кузница, лесопилка, люди, птицы, огонь, двери, фонтан |
| `NV3D\Wild Harvest` | 4 403 файла, 690 МБ | кандидат для поля и ягодника: культуры по стадиям роста, URP-шейдеры издателя (`asset-licenses.md` The Chest). Стиль не проверен |
| `Townsfolk_MoCap_02` | — | болванки реквизита: `Forge_Mesh`, `Anvil_Mesh`, `Bellows_Rig`, `Hammer_Rig`, `Tongs_Rig`, `Blade_Rig`, `Pickaxe_Rig`, `Axe_Rig`, `Shovel_Rig`, `Stool_Mesh`. У всех общий `Prop_Mat`, материалы URP. Вид не проверен, похоже на серые болванки |
| `ANIMALS FULL PACK` (Protofactor) | 777 файлов, 869 МБ | не нужен: враги и звери у нас 2D |
| `castle`, `Medieval_Environment`, `Rocks and Boulders 2`, `NatureManufacture Assets`, `_TheTalesFactory`, `MoCapCentral`, `3DForge` | 0,5–20 ГБ | нет: реализм и HDRP; 3DForge без моделей |

**ZD сейчас:** `C:\dev\zelda\ZeldaDaughter\Assets\ThirdParty\` нет, покупного в проекте ноль. В апрельском `C:\dev\zelda\UnityProject\Assets\ThirdParty\` пусто.

---

## 1. Нужда демо → лучший кандидат

Обозначения:
- **ПК** — лежит на диске;
- **акк.** — куплено, не скачано (путь появится в `%AS%\<издатель>\…` после `AssetStoreDownloadManager.Download(<id>)`);
- **CC0** — апрельские модели в git, `archive/2026-04/UnityProject/Assets/Models/…`, их можно класть в репозиторий.

«Стиль» — насколько легко довести до «перо + акварель, приглушённая тёплая»: **лёгко** (плоский цвет или атлас-палитра, перекрашивается и затеняется шейдером), **средне** (hand-painted или toon со своей насыщенной палитрой), **плохо** (PBR-реализм).

| Нужда | Кандидат | Где | Размер | URP | Стиль | Примечания |
|---|---|---|---|---|---|---|
| Деревья, кусты, трава, камни | **Kenney Nature Kit 2.1** | CC0, `Models/Kenney/NatureKit` | 329 FBX, 18 МБ на оба Kenney | FBX + атлас; материал свой (URP Lit/Flat Kit) | лёгко: flat low-poly | `tree_*` (default, cone, blocks, по `_dark`/`_fall`), `plant_bush*`, `grass*`, `flower_*`, `mushroom_*`, `rock_*`/`stone_*`, `stump_*`, `log*`, `lily_*` |
| | TRIFORGE Top Down - Fantasy Forest | акк. | 803 МБ (стр. 30.09) | Built-in+URP на 6000.0/6000.3 | средне: hand-painted, насыщенный | сделан под камеру сверху. URP 1.3 и HDRP 1.2 сняты с продажи, на аккаунте может оказаться старая ветка — проверить при скачивании |
| | SICS Toon Fantasy Nature; ARTKOVSKI The Illustrated Nature (+ Sample 33,6 МБ) | акк. | 992 / 148,7 МБ | Sics: Built-in+URP до 6000.1; Artkovski: на витрине только Built-in | средне | Illustrated Nature ближе всех к «иллюстрации», но свои шейдеры придётся переписать на URP |
| Дорога, тропа | **Kenney Nature Kit**: `ground_path*`, `path_stone*`, `path_wood*`; **Fantasy Town Kit**: `road*` | CC0 | — | — | лёгко | тайлы под сетку; для плавной дороги лучше свой меш по сплайну (`com.unity.splines`) + текстура |
| | CHROMISU Handpainted Grass & Ground (Top-Down RPG); GAME BUFFS 2500+ Stylized Textures; TEXTURE ME! Hand Painted Rocks-Road | акк. | не проверено | текстуры, от пайплайна не зависят | средне | рисованная земля и обочины под рисованный пол |
| Поле, огород | **Kenney Nature Kit**: `crops_dirt*Row*`, `crops_wheatStage*`, `crops_cornStage*`, `crop_carrot/turnip/pumpkin/melon` | CC0 | — | — | лёгко | стадии роста есть |
| | NV3D Wild Harvest | ПК (в The Chest) | 690 МБ | URP-шейдеры издателя | не проверен; по описанию реалистичнее | ягодники и злаки по 12–15 стадий; для мобилки тяжеловат |
| | EMACEART NatureForge LITE: Meadow & Farm; M STUDIO HUB M Farm RPG; LOWSCOPE RPG Farming Kit | акк. | не проверено | не проверено | вероятно, лёгко/средне | ничего не проверено |
| Хижина крестьянина | **Kenney Fantasy Town Kit** (модульные стены, `roof*`, `chimney*`, `planks*`, `pillar-wood`) + **Kenney Nature Kit** `tent_*` | CC0 | — | — | лёгко | собирается кодом из модулей, как избы в The Chest |
| | POLYGON BLACKSMITH Toony Tiny RTS Set | акк. | 31,6 МБ (стр. 30.09) | только URP; **собран на 6000.5.0** | лёгко: toon | на 6000.3 может не встать, шейдеры URP 17.5. Масштаб «tiny». Состав на витрине не расписан |
| Городские дома (2–3 жилых, лавка, дом травницы) | **Kenney Fantasy Town Kit** (стены, окна, балконы, крыши, лестницы, `banner-*`, `sign`) | CC0 | — | — | лёгко | хватает на квартал; дома различаются цветом крыш и вывесками |
| | Toony Tiny RTS Set; SYNTY Simple Town Lite | акк. | 31,6 МБ / — | URP | лёгко | **Simple Town у Synty — современный город** (кафе, кинотеатр): не брать |
| Таверна снаружи | Fantasy Town Kit (двухэтажный модульный дом, `balcony-*`, `banner-*`, `sign`) | CC0 | — | — | лёгко | отдельной модели таверны в библиотеке не нашлось |
| Кровать и мебель внутри | **Quaternius Fantasy Props (Free)**: `Bed_Twin1/2`, `Table_Large`, `Stool`, `Chair_1`, `Bench`, `Shelf_*`, `Cabinet`, `Mug`, `Chalice`, `Candle*`, `Chandelier` | CC0, `Models/FantasyProps` | 70 МБ | FBX, трим-текстуры PBR | средне: stylized, но с ORM/Normal | лучшая готовая мебель в доступе. Kenney Town Kit тоже даёт `bed`, `bed_floor` |
| | KAY LOUSBERG KayKit Restaurant Bits; STYLARTS Stylized House Interior; JUSTCREATE Low Poly Cartoon House Interiors; VERTEX STUDIO Big Furniture Pack | акк. | не проверено | Stylized House Interior, **возможно, только HDRP** (по сторонней витрине того же набора, не подтверждено) | средне | при камере сверху интерьер, скорее всего, не нужен: кровать в таверне — объект для тапа |
| Кузница: горн/плавильня, наковальня | **PURE POLY Ultimate Low Poly Mining, Cave & Blacksmith Pack** | акк. | не проверено | **URP по умолчанию**, отдельные пакеты под 6000.2 и 2021.3 | **лёгко: все меши на одной текстуре 256×256**, в среднем 564 трис | 1 156 моделей: наковальни, молоты, кирки, тачки, факелы, фонари, руда, самоцветы, мечи, кинжалы, щиты, бочки, ящики, 52 частицы (дым, огонь). Лучший кандидат по кузнице, руде и инструментам сразу |
| | Quaternius Fantasy Props: `Anvil`, `Anvil_Log`, `Workbench`, `Whetstone`, `Cauldron` | CC0 | — | — | средне | горна или плавильни нет |
| | Townsfolk MoCap 02: `Forge_Mesh`, `Anvil_Mesh`, `Bellows_Rig`, `Tongs_Rig` | ПК (в The Chest) | — | URP | похоже на болванки | годится как геометрия горна под свой материал; проверить вид |
| Прилавок, лавка | **Kenney Fantasy Town Kit**: `stall`, `stall-green`, `stall-red`, `stall-bench`, `stall-stool`, `cart`, `cart-high` | CC0 | — | — | лёгко | плюс Quaternius `Stall_Empty`, `Stall_Cart_Empty`, `FarmCrate_*`, `Barrel_Apples` |
| Мост | **Kenney Nature Kit**: `bridge_wood`, `bridge_woodRound`, `bridge_woodNarrow`, `bridge_stone*` и секции `bridge_center_*`, `bridge_side_*` | CC0 | — | — | лёгко | в покупках отдельного моста не нашлось |
| Вода, река | **DUSTYROOM Flat Kit** (шейдер Water, только URP) | акк. | не проверено | URP — основная цель, Built-in как legacy | лёгко: плоская стилизованная | Kenney `ground_river*` даёт русло тайлами. Альтернативы на аккаунте: NVJOB Water Shaders V2, PURE EVIL Water Effect for Lowpoly, EBRU DOGAN LowPoly Water, LOWLYPOLY Stylize Water Texture |
| Частокол, ворота | **Kenney Fantasy Town Kit**: `fence`, `fence-broken`, `fence-curved`, `fence-gate`, `hedge*`; **Nature Kit**: `fence_*`, `fence_gate`, `fence_planks*` | CC0 | — | — | лёгко | **настоящего частокола из заострённых брёвен нет нигде**: собрать кодом из `log`/`poles`, либо башня ворот из Town Kit (`pillar-wood`, `poles-horizontal`) |
| Костёр + огонь VFX | **Ian's Fire Pack**, папка `_URP Specific`: `Camp Fire Large URP`, `Camp Fire Medium URP`, `Embers URP`, `Torch URP`, `Floor Fire URP`, `Oil Fire URP`, `Burning Wood Decal URP`, шейдер `Shimmer-URP.shadergraph` | **ПК**, `%AS%\Ian Scilipoti\…` | 56,3 МБ | **URP есть** (Built-in лежит рядом в `_Standard Pipeline Specific`, его не импортировать) | средне: частицы с фото-текстурой пламени | огонь костра, факела и горна сразу. Цвет приглушить, проверить overdraw на телефоне |
| | Kenney Nature Kit: `campfire_logs`, `campfire_stones`, `campfire_bricks`, `campfire_planks` | CC0 | — | — | лёгко | геометрия кострища под огонь Ian's |
| | VEFECTS Free Fire VFX - URP, Candle VFX - URP; JEAN MORENO Cartoon FX Remaster (+Free) | акк. | не проверено | URP по названию | лёгко: мультяшный | Cartoon FX — для ударов, пыли и искр кузницы |
| Предметы-пропсы | **Kenney/Quaternius CC0**: `Sword_Bronze`, `Axe_Bronze`, `Pickaxe_Bronze`, `Torch_Metal`, `Shield_Wooden`, `Table_Knife`, `Bag`, `Pouch_Large`, `Rope_*`, `Carrot`, `log`, `log_stack` | CC0 | — | — | средне/лёгко | — |
| | **PURE POLY** (молоты, руда, мечи, кинжалы, факелы) | акк. | — | URP | лёгко | закрывает руду, молот, нож, меч, факел |
| | Medieval Farm Tools: `Hammer_01…04`, `FireWood_01…06`, `FireWoodPile_01`, `Log_*`, `Axe_*`, `Sickle_*` | **ПК** | 104,8 МБ | Standard/Built-in, PBR | плохо | только болванки или рендер в иконку |
| | TRIFORGE Medieval Weapons; DANIEL MISTAGE STYLIZED Fantasy Armory; SICS Low Poly Weapons; UNITY Food Props; LUMO-ART FREE Casual Food Pack; MUMIFIER Food Pack | акк. | — | не проверено | средне | запас |
| | — | — | — | — | — | **В 3D нет нигде:** ягоды (кустик), кремень, ткань. При billboard-предметах в мире их проще генерировать 2D |
| Шейдеры, пост под акварель, скетч, тушь | **DUSTYROOM Flat Kit**: Stylized Surface (+Outline), Water, Gradient Skybox, Fog/Outline как Renderer Features | акк. | не проверено | URP — основная цель, исходники в комплекте | — | главный клей стиля: cel-shading, пообъектный контур «пером», вода, туман |
| | DANIEL ILETT Toon Shaders Pro for URP; DMITRY CHALOVSKIY Simple Toon; JEAN MORENO Toony Colors Free | акк. | — | URP (Ilett) | — | запас к Flat Kit, два toon-набора не держать |
| | CHRIS NOLET Quick Outline; INAB Ultimate Outlines & Highlights | акк. | — | не проверено | — | подсветка цели тапа |
| | FRONKON GAMES Artistic Bundle | акк. | — | URP | — | **акварели и скетча в составе подтвердить не удалось**: найденный элемент набора, Artistic Photo, — плёнка и глитчи |
| | AMPLIFY Amplify Color | акк. | — | вероятно, Built-in, старый | — | LUT-грейдинг в URP есть штатно, пакет не нужен |
| | MedievalKingdomUI `Artworks\Textures\G_Textures_01…12` | ПК | — | — | — | по названию — фоновые текстуры набора, возможная «бумага» для оверлея; не смотрел |
| | SEASIDE All In 1 Sprite Shader | акк. | — | не проверено | — | обводка тушью, вспышки и тинт на billboard-спрайтах |
| Звуки: шаги | **CPLOMEDIA Footsteps - Pack** | **ПК** | 626,5 МБ, 3 033 файла | — | — | одиночные шаги: Earthground 446, Mud 354, Concrete 395 (камень площади), Gravel 211, Grass 154, Water 270 (брод), Metal 217, Snow 242; есть петли. **Шагов по дереву (мост, пол таверны) нет** |
| Звуки: город, кузница, огонь | **Cafofo Village Ambience** | **ПК** (`Plugins\CafofoStudio`, The Chest) | 381 МБ | — | — | `Blacksmith` 40, `Lubermill` 85 (топор, пила), `Human Activity` 59, `Birds` 40 + 37 редких, `Fire` 4 петли, двери, повозка, куры, корова, `Water Fountain Loop`. В Unity 6 нужна локальная правка `AmbienceMixer.cs` (CS0592) или брать только клипы, без скриптов |
| Звуки: удары, раны, природа, UI | **Medieval Fantasy SFX Bundle** | **ПК** | 1 543 МБ, 3 028 файлов | — | — | клинки 310, щиты 110, плоть и кровь ≈ 500, вжухи 40, ножны 66; UI: Interface 77, Equip 68, Use 40; Environment 91, Soundscapes 12, дождь 40, ветер 45, Creatures 64. Половина — магия, не нужна |
| Звуки: звери, лес, ночь | DAYDREAM SOUND Fantasy Ambience; CAFOFO Fantasy Sounds Bundle; MAGIC PIG Battle Sound Library; AD SOUNDS Real Doors; EPIC SOUNDS Fantasy Game SFX | акк. | — | — | — | кабан и волк — не проверено ни в одном пакете |
| UI-иконки (разговор, инвентарь) | **Poneti Medieval Kingdom UI** | **ПК** (The Chest) | 936 МБ на диске | uGUI; шейдеры Blur/Fluid под Built-in | средне: рисованные, бронза и пергамент | `UI_icons\Colored` 97 + `Classic` 98: `53_chat`, `85_village`, `56_map`, `31_herbs`, `80_meat`, `54_wood`, `51_mining`, `18_craft`, `08_door`, `04_sword`, `76_provisions`, `79_compass`, `44_time`, `24_man`, `25_woman`. Карандашные `Map_elements` 35: `House`, `Village`, `palisade`, `Fort`, `Tower`, `tree_01…04`, `hill_*`. Хорошо для иконок разговора и карты |
| | Layerlab GUI Pro - Fantasy RPG | **ПК** (кэш) | 158,9 МБ | uGUI | плохо: глянцевый казуал | `Icon_ItemIcons` 1 026, `Icon_EquipIcons` 509, `Icon_PictoIcons` 2 172. Есть камень, дерево, мясо, молоты, мечи, деревянный щит — **только заглушки** |
| Шрифты | Layerlab GUI Pro: Alata, Josefin Sans, Play (TMP SDF, лицензия OFL в пакете) | ПК | — | TMP | — | кириллица по памяти агента только у **Play**. Alata и Josefin — латиница. Рукописного нет |
| | Medieval Kingdom UI `Fonts\CENTURY.TTF` | ПК | — | — | — | **не использовать**: Monotype, права не проверены, кириллицы нет |

---

## 2. Чего в библиотеке нет

| Пробел | Чем закрыть |
|---|---|
| Окружение в технике «перо + акварель» (вообще) | шейдер (Flat Kit + свой Renderer Feature: бумага, затемнение краёв, квантизация) или генерация 2D |
| Частокол из заострённых брёвен, ворота в частоколе | кодом из `log`/`poles` Kenney или примитивов (цилиндр + конус) |
| Таверна как отдельное здание, дом травницы | сборка из модулей Kenney Town Kit + вывеска (генерация) |
| Горн или плавильня в stylized-виде | Pure Poly (в описании наковальни есть, горна не названо — проверить после скачивания) или `Forge_Mesh` Townsfolk под свой материал, или примитивы + огонь Ian's |
| Ягоды (куст с ягодами), кремень, ткань как предметы в мире | 2D-генерация (billboard-предмет) или примитив + цвет; Kenney `plant_bush` + сфера-ягоды кодом |
| Шаги по дереву (мост, пол таверны) | freesound.org CC0 или апрельское `Audio/SFX/Footsteps` (источник и лицензия не проверены) |
| Звуки кабана и волка | Battle Sound Library и Medieval SFX `Creatures` проверить на слух; иначе freesound CC0 |
| Фольклорная музыка (таверна, лютня) | апрельские `Audio/Music/Bard/` (`The_Old_Tower_Inn`, `Market_Day_Loop`, `The_Bards_Tale`, `Kings_Feast`). По памяти агента это CC0-треки RandomMind с OpenGameArt — **проверить лицензию** |
| Акварельные иконки предметов | генерация тем же пайплайном, что и персонажи; на время — Medieval Kingdom UI и Layerlab как заглушки |
| Рукописный шрифт с кириллицей | Google Fonts, OFL: Caveat, Marck Script, Neucha |
| Бумажная текстура и пост «акварель» | свой Renderer Feature. Вода и туман — Flat Kit |
| Индекс библиотеки | Asset Inventory уже на ПК, но база пуста: проиндексировать перед массовой проверкой 654 позиций |

---

## 3. Какое направление мира лучше обеспечено библиотекой

Направления — по `docs/concept/first-5-minutes.md`: **А** — рисованный 2D-мир как Don't Starve, **Б** — стилизованное 3D + акварельный пост, **В** — бумажная диорама.

- **А.** Библиотека даёт только обвязку: звук, огонь, UI. Ни одного рисованного billboard-набора деревьев и домов в нужной манере нет. Из 2D-покупок есть только Super Brutal Painted HQ 2D Forest Medieval Background: 9 облаков, 6 гор, 3 неба, платформер-фоны, к виду сверху не подходят. Всё остальное — сотни сгенерированных спрайтов.
- **Б.** Обеспечено лучше всего:
  - геометрия всего списка демо уже лежит в git (Kenney Nature и Town Kit, Quaternius Props, CC0, flat low-poly легко перекрашивается);
  - кузница, руда и инструменты — Pure Poly (URP, одна текстура 256×256, лёгкая геометрия);
  - стиль, вода, контур и туман — Flat Kit;
  - огонь — Ian's Fire Pack (URP, на ПК);
  - звук и UI — уже на ПК.

  Для демо остаётся докачать два пакета (Flat Kit, Pure Poly) и написать один пост-эффект.
- **В.** Та же геометрия, что в Б, но каждой модели нужна рисованная текстура «бумага + акварель». Готовых таких текстур в библиотеке нет: Game Buffs и Chromisu — игровой hand-painted. Значит, генерация текстур на каждую модель и UV-развёртки под них: у Kenney атлас-палитра, не развёртка под рисунок.

**Рекомендация агента: Б.** Это единственное направление, где каждую строку §1 закрывает уже имеющееся (CC0 в git + ПК) плюс два докачанных пакета. Риск Б — «3D-игра со вклеенными рисованными человечками». Его проверяет кадр-проба: Kenney + Flat Kit + контур + бумажный оверлей рядом с billboard-героем, день и ночь с костром Ian's. Решение о направлении остаётся за автором на воротах GК (K-02). Кадры внешних сервисов покажут, хочет ли он А, а библиотека его не обеспечит.

---

## 4. Риски

**Тяжёлые пакеты (мобильный бюджет):**
- Medieval Fantasy SFX 1,5 ГБ, Footsteps 0,6 ГБ, Cafofo 381 МБ: брать выборочно через `PackagePick`, как в The Chest (там из 2,2 ГБ взяли 25 МБ).
- Medieval Kingdom UI 4 ГБ, в основном PSD/PSB: в проект брать только PNG.
- Toon Fantasy Nature 992 МБ, Top Down Fantasy Forest 803 МБ, Wild Harvest 690 МБ: брать выборочно или не брать.
- Реализм с диска (NatureManufacture, GameAssetFactory, Castle Kit, Rock and Boulders) — не для мобилки: 4K-текстуры, десятки тысяч трис на объект.

**Пайплайн и версия:**
- Built-in: Medieval Farm Tools (Standard), Ian's Fire Pack — только папку `_URP Specific`, Hovl, Amplify Color, шейдеры Blur/Fluid Medieval Kingdom UI, Illustrated Nature (по витрине только Built-in).
- Пакеты-контейнеры: настоящие SRP-материалы лежат во вложенном `.unitypackage` (GameAssetFactory, Real Ivy, EasyRoads, NatureManufacture).
- **Toony Tiny RTS Set собран на 6000.5.0**, у нас 6000.3.24 и URP 17.3: Shader Graph новой версии может не открыться.
- Triforge: ветки URP 1.3 и HDRP 1.2 сняты с продажи, какая лежит на аккаунте, неизвестно.

**Компиляция:**
- Cafofo `AmbienceMixer.cs`: CS0592 в Unity 6.
- Ultimate Screenshot Tool: CS0433.
- Импортировать сначала в пустой проект или брать только ассеты без скриптов.

**Лицензии:**
- Asset Store EULA: встраивать можно, атрибуция не нужна, исходники распространять нельзя. Отсюда **риск №1 — `ZeldaDaughter/Assets/ThirdParty/` не в `.gitignore`** при публичном репозитории (ADR-0006).
- Отдельно проверять:
  - Free SpeedTrees — своя лицензия;
  - сэмплы Unity — Unity Companion License;
  - шрифт Century — Monotype;
  - гербы реальных государств в Medieval Kingdom UI — не использовать;
  - апрельскую музыку и звуки — источник не записан;
  - FMOD.
- Kenney и Quaternius — CC0, файлы `License.txt` в папках; апрельский `Characters/Modular RPG Characters` без лицензии — не брать (`docs/april-review.md`).
- **Мобильная цена эффектов:** полноэкранный контур Flat Kit (глубина + нормали), прозрачный overdraw огня Ian's и Cartoon FX, живописные постэффекты Fronkon. Только после замера на телефоне.

---

## Что скачать первым (если автор выберет Б)

1. DUSTYROOM Flat Kit.
2. PURE POLY Ultimate Low Poly Mining, Cave & Blacksmith Pack. Пакет издателя под 6000.2 URP встанет в 6000.3.
3. По желанию для пробы: SEASIDE All In 1 Sprite Shader, VEFECTS Free Fire VFX - URP.

Id пакетов для `AssetStoreDownloadManager` — классические короткие; в этом отчёте их нет (Pure Poly на витрине — 189279, Flat Kit — 143368, проверить при скачивании). До импорта — правило в `.gitignore` и строки в `docs/asset-licenses.md` (ADR-0004).

## Источники в сети (2026-10-08)
- Toony Tiny RTS Set: https://assetstore.unity.com/packages/3d/characters/toony-tiny-rts-set-135258 — URP only, 6000.5.0, 31,6 МБ.
- Pure Poly Mining/Cave/Blacksmith: https://assetstore.unity.com/linkmaker/embed/package/189279/widget-wide , https://www.artstation.com/marketplace/p/dGWx/ultimate-low-poly-mining-cave-blacksmith-pack-ores-gems-props-tools-rails-mine-carts — 1 156 моделей, одна текстура 256×256, URP по умолчанию.
- Flat Kit: https://assetstore.unity.com/packages/vfx/shaders/flat-kit-toon-shading-and-water-143368 — URP основной, Built-in legacy, исходники в комплекте.
- Synty Simple Town: https://assetstore.unity.com/packages/3d/environments/urban/simple-town-cartoon-assets-43500 — современный город.
- Hex Medieval-Fantasy Locations: https://dgbaumgart.itch.io/hex-medieval-fantasy-locations — 2D-тайлы гексов (для карты, не для мира).
- Painted HQ 2D Forest Medieval Background: https://assetstore.unity.com/packages/2d/environments/painted-hq-2d-forest-medieval-background-97738 — 9 облаков, 6 гор, 3 неба.
- Stylized House Interior (HDRP-only по сторонней витрине): https://leartesstudios.gumroad.com/l/StylizedHouseInteriorUnity — соответствие покупке не подтверждено.
- Triforge Top Down (снятые ветки): https://assetstore.unity.com/packages/slug/182365 , https://assetstore.unity.com/packages/slug/182145
