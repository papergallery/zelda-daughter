# Купленные и сторонние ассеты, которые используются

Купленное лежит в `ZeldaDaughter/Assets/ThirdParty/` и **в git не попадает** (`.gitignore`, ADR-0004). В git — только ключи и пути внутри `ThirdParty` (например, `Assets/Art/Registries/sounds.json`). Файлы кладёт на ПК выборочно скрипт чтения `.unitypackage` из кэша Asset Store (`%APPDATA%\Unity\Asset Store-5.x`), без скриптов пакета.

| Пакет | Издатель | Что взято (D-17) | Где лежит на ПК |
|---|---|---|---|
| Footsteps - Pack (Footsteps Pack Expanded) | CPLOMEDIA | 44 одиночных шага: трава, земля, гравий (дорога), грязь, вода, бетон (камень площади), по 6–8 клипов | `ThirdParty/FootstepsPack/Footsteps Pack Expanded/SingleSteps/…` |
| Medieval Fantasy SFX Bundle | Magic Sound Effects | 57 клипов: удары и взмахи клинка, плоть, UI-бумага и предметы, монеты, дерево (шаги по мосту и полу — «loot_wood_pickup»), костёр и факел (петли), ночные существа (`amb_creatures`), ветер, дождь | `ThirdParty/MedievalFantasySFX/…` |
| Village Ambience (Dynamic Village Ambience) | Cafofo Studio | 17 клипов: птицы, ручей, толпа, кузница (2 набора), повозка, хрюк кабана, куры/корова/двери. Скрипты пакета (`AmbienceMixer.cs`, CS0592 в Unity 6) **не берём** | `ThirdParty/CafofoVillage/VillageAmbience/…` (копия из `C:\dev\thechest\…\Plugins\CafofoStudio`) |
| Ian's Fire Pack (Universal) | Ian Scilipoti | D-16: всё для URP-префабов — `_URP Specific` (костёр S/M/L, угли, факел, малый огонь, материалы, шейдер-графы), текстуры, меши, звуки, два скрипта; без `_Standard Pipeline Specific` и демо. Игровые префабы `campfire`, `torch_flame`, `grass_fire` — копии с приглушённым цветом без света/звука (`FxBuilder`) | `ThirdParty/IansFirePack/…` (скрипт `tools/pc/zd-import-fire.ps1`, GUID сохранены); лицензия Asset Store EULA |

Чего в библиотеке нет (D-17): шагов по дереву, звуков волка, лютни/барда, сверчков — **сгенерированы (D-17b), см. ниже**. Апрельские треки барда (`archive/2026-04/UnityProject/Assets/Audio/Music/Bard/`, ID3: «RandomMind», «Forgotten Ballads vol. I», 2018) — лицензия в архиве не записана, поэтому **не используются**: `docs/demo/backlog.md`.

## Сгенерированное нами (в git)

Лежит в `ZeldaDaughter/Assets/Art/Audio/Generated/` (OGG Vorbis, 5,6 МБ), **в git** — это не купленное. Запросы и что из ответа взято — `tools/audio/d17b-prompts.json`, обработка (обрезка, громкость, нарезка шагов, склейка петли) — `tools/audio/d17b-generated.py`. Получено 2026-10-08 через облачный шлюз **Polza** (`POST /media`; провайдер у Suno в каталоге Polza — `mie`, сторонний).

| Что | Файлы | Модель (через Polza) | Цена |
|---|---|---|---|
| Бард: весёлая «Oakbeam Revel» 2:48, задумчивые «Embers at the Inn» a/b 1:48 и 1:50 (инструментал, лютня/цистра, флейта, бубен) | `bard/*.ogg` | `suno/generate` V6 (2 запроса × 15 ₽, по 2 варианта) | 30 ₽ |
| Волк: вой ×2, рычание ×3, укус ×2, визг от боли ×1 | `wolf/*.ogg` | `suno/sounds` V6 (7 запросов × 2 ₽; 3 неудачных — визг, крик человека, лай) | 14 ₽ |
| Шаги по дереву ×6 (нарезка одной дорожки) | `steps/*.ogg` | `suno/sounds` | 2 ₽ |
| Ночь: сверчки (петля 22 с из двух вариантов), сова ×2 | `night/*.ogg` | `suno/sounds` (2 запроса) | 4 ₽ |
| Проба, не взята: `google/lyria-3-pro-preview` (вокальная фраза на 1:19) | — | Lyria 3 Pro | 8 ₽ |

Ещё ~14 ₽ — «прослушивание» результатов аудиомоделью (`google/gemini-3.8-flash`, вход — аудио; 0,15–0,7 ₽ за файл). Всего 71,9 ₽ (баланс 123,50 → 51,62 ₽).

**Условия (оговорка — не ясно до конца):**
- **Suno** (suno.com/terms, прочитано 2026-10-08): права на результат (Output) Suno передаёт **платным** подписчикам (Pro/Premier) — «assigns to you all of its right, title and interest in and to any Output owned by Suno», без гарантии, что авторское право вообще возникнет; бесплатным — только личное некоммерческое использование. Коммерческое использование — для результатов, скачанных «through an approved channel». Об API условия не говорят; официального публичного API у Suno нет, Polza ходит через стороннего провайдера (`mie`). **Значит: является ли это «approved channel» и платной подпиской — не ясно.** Для демо годится; перед публикацией в сторах — либо подтвердить условия у Polza (их оферта по правам на результат не найдена), либо перегенерировать музыку в платном аккаунте Suno / заменить на CC0 / купленное.
- **Google Lyria** (не используется): общие условия Gemini API — Google не претендует на права на сгенерированное, ответственность за использование на пользователе; в каждом треке водяной знак SynthID; модель в статусе Preview.
- **Polza** — оферта с разделом о правах на результат не найдена (polza.ai/terms — 404).

