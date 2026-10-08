# Купленные и сторонние ассеты, которые используются

Купленное лежит в `ZeldaDaughter/Assets/ThirdParty/` и **в git не попадает** (`.gitignore`, ADR-0004). В git — только ключи и пути внутри `ThirdParty` (например, `Assets/Art/Registries/sounds.json`). Файлы кладёт на ПК выборочно скрипт чтения `.unitypackage` из кэша Asset Store (`%APPDATA%\Unity\Asset Store-5.x`), без скриптов пакета.

| Пакет | Издатель | Что взято (D-17) | Где лежит на ПК |
|---|---|---|---|
| Footsteps - Pack (Footsteps Pack Expanded) | CPLOMEDIA | 44 одиночных шага: трава, земля, гравий (дорога), грязь, вода, бетон (камень площади), по 6–8 клипов | `ThirdParty/FootstepsPack/Footsteps Pack Expanded/SingleSteps/…` |
| Medieval Fantasy SFX Bundle | Magic Sound Effects | 57 клипов: удары и взмахи клинка, плоть, UI-бумага и предметы, монеты, дерево (шаги по мосту и полу — «loot_wood_pickup»), костёр и факел (петли), ночные существа (`amb_creatures`), ветер, дождь | `ThirdParty/MedievalFantasySFX/…` |
| Village Ambience (Dynamic Village Ambience) | Cafofo Studio | 17 клипов: птицы, ручей, толпа, кузница (2 набора), повозка, хрюк кабана, куры/корова/двери. Скрипты пакета (`AmbienceMixer.cs`, CS0592 в Unity 6) **не берём** | `ThirdParty/CafofoVillage/VillageAmbience/…` (копия из `C:\dev\thechest\…\Plugins\CafofoStudio`) |
| Ian's Fire Pack (Universal) | Ian Scilipoti | D-16: всё для URP-префабов — `_URP Specific` (костёр S/M/L, угли, факел, малый огонь, материалы, шейдер-графы), текстуры, меши, звуки, два скрипта; без `_Standard Pipeline Specific` и демо. Игровые префабы `campfire`, `torch_flame`, `grass_fire` — копии с приглушённым цветом без света/звука (`FxBuilder`) | `ThirdParty/IansFirePack/…` (скрипт `tools/pc/zd-import-fire.ps1`, GUID сохранены); лицензия Asset Store EULA |

Чего в библиотеке нет (D-17): шагов по дереву (взяты стуки дерева), звуков волка, лютни/барда. Апрельские треки барда (`archive/2026-04/UnityProject/Assets/Audio/Music/Bard/`, ID3: «RandomMind», «Forgotten Ballads vol. I», 2018) — лицензия в архиве не записана, поэтому **не используются**: `docs/demo/backlog.md`.
