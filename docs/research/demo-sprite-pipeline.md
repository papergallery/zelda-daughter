# Спрайты для демо на ПК автора: что есть и как делать

2026-10-08, агент. Это разведка: ничего не установлено и ничего не сгенерировано. ComfyUI запускался на ≈ 5 мин только ради `/system_stats` и `/object_info`, потом остановлен; на это время видеокарта была занята через `/tmp/thechest-gpu.busy`. Проверочную картинку не делал: на ПК шла Dota 2 (загрузка видеокарты 49–78 %). Что API отвечает, подтвердил `/object_info` (729 нод). Сам Klein-воркфлоу на этом ПК уже работал в comiclang: выводы `output/klein/` от 2026-09-04 и `output/genom/` от 2026-10-05.

**Задача.** Героиня (молодая женщина из нашего мира: джинсы, куртка, рюкзак) и NPC: крестьянин, стражник, торговец, кузнец, бармен, травница, 2–3 жителя. Враги — кабан и волк. Каждому нужен billboard-спрайт минимум в 3 ракурсах: к камере, от камеры, сбоку (второй бок — зеркалом). Желательно 2–4 кадра шага. Фон прозрачный, камера изометрическая (35°/45°, `docs/april-review.md` §3). Отдельно — иконки разговора и инвентаря в том же стиле.

**Источники:** `tlbridge.py services|exec|gpu`, листинг файлов ПК, `/object_info`, метаданные PNG в `art/concepts/`, код `/var/www/html/Other/gpu-tunnel/character_pipeline/`, наработки comiclang (`/var/www/html/comiclang/tooling/comfyui-workflows/06-flux2-klein-edit.json`, `tooling/scripts/klein_run.py`, `rmbg-*.sh`, `rmbg-despill.py`, `comic_post.py`, `tasks/RESEARCH-2026-10-05-comic-art.md`), `tools/resources.sh`.

## 1. Что установлено и как запустить

| Что | Где | Состояние 2026-10-08 |
|---|---|---|
| ComfyUI 0.20.1 (Python 3.12.10, torch 2.11 + cu128) | `C:\Users\paper\ai-hub\comfyui` | установлен, остановлен; аргументы запуска `--listen --port 8188` |
| SD WebUI Forge | `C:\Users\paper\ai-hub\sd-forge` | установлен, остановлен; в `webui-user.bat` пустые `COMMANDLINE_ARGS` (нет `--api`); для нашей задачи бесполезен, см. §2 |
| fluxgym (обучение LoRA для Flux.1) | `C:\Users\paper\fluxgym` | лежат модели Flux.1-dev (см. §2) |
| Прочее в ai-hub | `alltalk`, `f5-tts`, `whisper`; плюс Ollama | к арту отношения не имеют |

- **Запуск:** `python3 /var/www/html/Other/tensorlay/tools/bridge/tlbridge.py svc start comfyui` (или `sd-forge`), остановка — `svc stop comfyui`, состояние — `tlbridge.py services`. ComfyUI поднялся меньше чем за ≈ 3 мин (точно не замерял). После `svc stop` TensorLay показывает `State: Error`, хотя процесс завершён и порт закрыт (проверено `Get-CimInstance`, `curl` → `000`). Похоже на код выхода принудительной остановки, на работу не влияет.
- **Туннель:** на сервере в `ss -tln` слушают `127.0.0.1:7860` и `127.0.0.1:8188` (а также 6520, 7851, 7863), и это при остановленных сервисах: TensorLay пробрасывает порты установленных сервисов заранее. Работающий сервис отвечает по `curl 127.0.0.1:8188/system_stats`.
- **Ресурсы ПК** (`tools/resources.sh`, 07:15 по времени ПК): C: свободно 63 ГБ, память — 13 ГБ свободно из 64 (6 процессов Unity, Blender, Dota 2). Видеокарта RTX 4080: 16 ГБ, во время игры занято ≈ 9 ГБ. Диска D: в листинге нет.

### Модели (`C:\Users\paper\ai-hub\comfyui\models`)

| Тип | Файл | Размер | Годится для нас |
|---|---|---|---|
| diffusion_models | `flux-2-klein-4b-fp8.safetensors` (distilled, 4 шага) | 3,79 ГБ | **да — основная модель.** Лицензия Apache-2.0, коммерческое использование можно (comiclang RESEARCH). Умеет multi-reference edit |
| diffusion_models | `flux-2-klein-base-4b-fp8.safetensors` (base, ≈ 20 шагов, CFG) | 3,81 ГБ | для точной доводки; по опыту comiclang «копирует референс буквально» |
| text_encoders / vae | `qwen_3_4b.safetensors` 7,49 ГБ; `flux2-vae.safetensors` 0,31 ГБ | | нужны Klein |
| checkpoints (SDXL) | `ponyDiffusionV6XL_v6StartWithThisOne`, `CyberRealisticPony_V17.0_FP16` | по 6,46 ГБ | нет: аниме и фотореализм, у Pony своя лицензия |
| loras (SDXL) | `AzaleaXL2`, `BryonyXL2`, `CamelliaXL2`, `EllenXL-000017`, `FionaXL`, `GardeniaXL` (похоже, персонажи из других проектов), `sdxl_cute_social_comic`, `Wiz-VintageComicBookCover_v01`, `wizards_vintage_comics-Undergroundf16` | 0,06–0,16 ГБ | нет: ни одна не «перо + акварель» |
| pulid | `pulid_v1.1.safetensors` (SDXL) + insightface `buffalo_l` + EVA-CLIP (кэш HF) | 0,92 ГБ | нет: держит лицо, на стилизованных лицах ненадёжен. `pulid_flux` в манифесте comiclang есть, на диске его нет |
| photomaker | `photomaker-v1.bin`, `photomaker-v2.bin` (SDXL) | 0,87 / 1,68 ГБ | нет, по той же причине |
| sam2 | `sam2.1_hiera_base_plus` (+ fp16) | 0,30 ГБ | маски частей тела, если пойдём в cutout |
| liveportrait | 6 файлов | | нет (анимация лиц) |
| удаление фона | `custom_nodes/ComfyUI-BRIA_AI-RMBG/RMBG-1.4/model.pth` | 0,16 ГБ | работает, **но лицензия BRIA RMBG-1.4 некоммерческая**: для черновиков можно, для релизных спрайтов нужна замена (§4) |
| See-through | кэш HF: `layerdifforg/seethroughv0.0.2_layerdiff3d` (SDXL, 7,6 ГБ) + `seethroughv0.0.1_marigold` | ≈ 12 ГБ | раскладывает одну иллюстрацию на слои (волосы, лицо, одежда, конечности) с порядком глубины — заготовка под cutout. Обучен на аниме, на акварели не проверен |

**Пусто:** `controlnet`, `upscale_models`, `clip_vision`, `style_models`, IP-Adapter. В Forge — только `Realistic_Vision_V5.1` (SD1.5, фото) и `sd_turbo`; папки `Lora`, `ControlNet`, `ESRGAN` пустые.

**fluxgym** (`C:\Users\paper\fluxgym\models`): `unet/flux1-dev.sft` 22,17 ГБ (bf16), `clip/t5xxl_fp16` 9,12 ГБ, `clip/clip_l`, `vae/ae.sft`. Это полный Flux.1-dev. ComfyUI его не видит: `extra_model_paths.yaml` нет, есть только `.example`.

### Custom nodes ComfyUI
`ComfyUI-BRIA_AI-RMBG`, `ComfyUI-LivePortraitKJ`, `ComfyUI-PuLID-Flux-Enhanced` (без модели, с Flux.2 не работает), `ComfyUI-See-through`, `ComfyUI-segment-anything-2`, `PuLID_ComfyUI`, `websocket_image_save.py`. Нет ComfyUI-Manager, нет `comfyui_controlnet_aux` (DWPose/openpose, depth, lineart), нет IPAdapter_plus и InstantID.

Нужные встроенные ноды на месте: `ReferenceLatent`, `EmptyFlux2LatentImage`, `Flux2Scheduler`, `ImageScaleToTotalPixels`, `LoraLoader`, `ControlNetLoader`/`ControlNetApplyAdvanced` (без моделей), `Canny`, `TextEncodeQwenImageEdit(Plus)`.

## 2. Апрельское: модели, пайплайн, пробы

**Апрельских моделей на ПК больше нет.** Все пробы в `art/concepts/` сделаны в Forge на `flux1-dev-bnb-nf4-v2` (hash `fef37763b8`; это видно из PNG-параметров), а ds_lora — это `j_dsstyle_flux` (триггер `Juaner_dsstyle`, hash `d07437ce09fe`, Civitai). ai-hub собран заново в мае 2026, ни nf4-модели, ни этой LoRA на диске нет.

**`character_pipeline`** (`/var/www/html/Other/gpu-tunnel/character_pipeline/`). Схема: txt2img через A1111 API Forge (`sd_client.py`) → `rembg` → нарезка «частей тела» по фиксированным долям рамки (`sprite_cutter.py`) → состояния через img2img с denoise 0,35 (`state_generator.py`).
- **Почему не согласовал ракурсы:** каждый ракурс — отдельный txt2img с суффиксом «back view» и случайным seed. Модель ничем не связана с предыдущим кадром, поэтому 4 ракурса дают 4 разных героя. Позы нет (нет ControlNet), кадров шага нет, нарезка по пропорциям не совпадает с реальным телом.
- **Что пригодится:** `config.py` — описания NPC (как черновик, переписать под дизайн: травница там «эльфийка», кузнец «дварф»), `STATE_PROMPTS` для «ранен» и прочих состояний, идея img2img с низким denoise. `sd_client.py` под Forge не нужен — переходим на ComfyUI API. `background_remover.py` (rembg, лицензия MIT) — запасной вариант удаления фона; на сервере `rembg` не установлен.

**Пробы стиля** (просмотрены 27 штук листом). Все сняты на уровне глаз, ни одна не сверху под 35–45°, у всех герой — парень.
- **Ближе всего к «Don't Starve, перо»:** `ds_lora_v2_02`/`_03` (Flux.1-dev nf4 + `j_dsstyle_flux` 1.0). Неровный контур пером, грязно-оливковая акварельная заливка, гротескные пропорции, тень-пятно под ногами. Минусы: палитра скорее серо-холодная, чем тёплая; в углу `_02` — подпись-закорючка.
- **Ближе всего к «перо + акварель, приглушённая тёплая»:** `ds_storybook_hybrid_01` (тот же Flux nf4 без LoRA, промпт «Arthur Rackham meets Edward Gorey, fine pen ink linework and muted watercolor washes, browns ochre olive grey»). Тёплая охра и олива, тонкое перо, лёгкая акварель, но пропорции реалистичные — от Don't Starve там мало.
- `protagonist_hd_01` — тоже «перо + акварель», но ближе к чистому мультяшному виду. `ds_style*` — чиби, не наш стиль. `ds_final_v*` и `ds_exact_01` — сепия-«Горей», слишком мрачно и монохромно.
- **Вывод:** целевая манера — между `ds_lora_v2_02` (линия и силуэт) и `ds_storybook_hybrid_01` (цвет и тепло). Повторить их один в один на нынешнем ПК нельзя — моделей нет (что делать, см. §5, п. 5).

## 3. Видеокарта: правила

- Busy-файл `/tmp/thechest-gpu.busy` общий с The Chest; генерация арта Zelda тоже его занимает (`docs/agent-handbook.md` §2). Занять: `echo "<агент> <ID> $(date -Is)" > /tmp/thechest-gpu.busy`; освободить: `: > /tmp/thechest-gpu.busy`. Чужой busy — ждать.
- Сервис, поднятый под генерацию, после работы останавливать (`svc stop`): ComfyUI держит модели в VRAM и RAM, а свободной памяти на ПК ≈ 13 ГБ.
- Ресурсы — `tools/resources.sh`. Пороги: C: < 20 ГБ или память < 4 ГБ — не запускать Unity; для генерации разумно то же самое.
- Сверх handbook: проверять `tlbridge.py gpu` и процессы — автор может играть (сегодня Dota 2). Klein 4B fp8 по объёму влезает и рядом с игрой (≈ 7 ГБ свободно), но мешает игре; генерировать лучше, когда игры нет.
- Правило навыка `zd-art-pipeline`: генерация на ПК — только по слову автора. Слово дано в этой задаче; поправить навык в K-03.

## 4. Рекомендуемый пайплайн (на том, что есть, плюс одна доустановка)

Основа — **FLUX.2 Klein 4B** в ComfyUI через API. Граф и запускалка готовы в comiclang: `06-flux2-klein-edit.json` + `klein_run.py` (до 2 референсов через `ReferenceLatent`, загрузка через `/upload/image`). Копируем к себе в `tools/art/` и меняем выходную папку. Главная идея: **все ракурсы одного персонажа — одной генерацией на одном листе**. Внутри одного изображения модель держит одежду, цвета и рост гораздо лучше, чем между отдельными генерациями — именно на этом сломался апрель.

**Шаг 0. Стилевой эталон (один на игру).** Klein text-to-image: нейтральный лист без сцены — пара предметов, кусок земли, куст. Сцену брать нельзя: по опыту comiclang (`prompts-klein.md`, «правило сцепки») её содержимое протекает в генерации. Вместе с эталоном закрепляем дословный блок `[STYLE]`: *ink pen outlines with uneven line weight, loose muted watercolor washes, warm ochre / olive / umber / dusty red palette, slightly grotesque storybook proportions in the spirit of Don't Starve, paper texture, no text, no logos* и вывод палитры — 12–16 цветов (`palette.json`). Решает автор: лист вкуса, ворота GК.

**Шаг 1. Лист персонажа (turnaround).** Klein: ref2 — стилевой эталон, холст 1536×1024 (3 ракурса) или 2048×1024 (4). Промпт: *character turnaround sheet of the same [описание], three full-body views in a row: front view facing the viewer, right side profile, back view; all three standing on one ground line, identical height and scale, seen slightly from above (high three-quarter camera), plain flat light-grey background, no shadows, no text* + `[STYLE]`. На каждого — 4–8 seed, отбор по критерию K-03 «узнаваемы рядом», плюс frame-critic.
- Внешность — текстовый «якорь», дословно один и тот же во всех промптах персонажа (comiclang: «паспорт» персонажа).
- Фон — ровный светло-серый, не белый и не розовый: на белом теряются светлые акварельные края, розовый comiclang потом вычищал despill.
- Угол камеры: модели рисуют turnaround почти на уровне глаз. Для billboard хватит лёгкого взгляда сверху (видна макушка, стопы чуть сверху) — Don't Starve тоже рисует персонажей почти фронтально. Честные 35–45° этим путём не получить (см. шаг 3б).
- Без асимметричных примет (надписи, шрам с одного бока, оружие только в правой руке): второй бок делается зеркалом (`flipX`).

**Шаг 2. Починка ракурса.** Если один вид на листе плохой — Klein edit: ref1 — лучший лист, промпт *the same character as in the reference image, back view, same clothes and colors* + `[STYLE]`, затем вставить вид обратно. Если «плывёт» весь лист — base-модель, ≥ 8 шагов (рецепт comiclang против рук и анатомии).

**Шаг 3. Кадры шага.** Два пути, решать на одном герое (K-03):
- **3а (рекомендую для демо): кадры через edit.** Для каждого ракурса Klein edit от вида с листа: *same character, mid-stride, left foot forward* / *right foot forward*. Цикл: контакт Л → стойка → контакт П → стойка (2 новых кадра на ракурс). Небольшое «дрожание» линии между кадрами для пера даже уместно: в Don't Starve оно есть. В Unity поверх — покачивание по вертикали.
- **3б (если позы и угол поплывут): позы с 3D-болванки.** KayKit Adventurers (CC0, `archive/2026-04/UnityProject/Assets/Models/KayKit`, анимации там же): Editor-скриптом в Unity рендерим силуэт с камеры игры (35°/45°) в трёх ракурсах × кадры ходьбы, и этот рендер идёт вторым референсом в Klein edit (*redraw the character from image 1 in the exact pose and camera angle of image 2*). Даёт точный угол камеры и одинаковый масштаб у всех. Без ControlNet это догадка: умеет ли Klein 4B держать позу по референсу — не проверено. Надёжный вариант — `comfyui_controlnet_aux` + ControlNet (§5).
- **3в (на потом): cutout,** как у самого Don't Starve. See-through или SAM2 режет спрайт на части, Unity анимирует части. See-through обучен на аниме — на акварели не проверено.

**Шаг 4. Удаление фона.** Черновики — BRIA RMBG-1.4 (есть; граф — `comiclang/tooling/scripts/rmbg-test.sh`), затем `rmbg-despill.py` (вычитает цвет фона с полупрозрачных краёв, цвет фона берём серый). Для релиза — BiRefNet (MIT) вместо RMBG-1.4 (§5, п. 1).

**Шаг 5. Нормализация (сервер, Python/PIL, код в `tools/art/`).**
- Нарезка листа: по пустым вертикальным полосам альфы, а не по фиксированной сетке.
- Масштаб: рост в метрах — в `data/characters.json` (героиня ≈ 1,65, стражник ≈ 1,85, кузнец ≈ 1,8 и шире, травница ≈ 1,6; кабан и волк — по холке и длине). Единая плотность px на метр для всех. Ориентир: герой на экране ≈ 1/9 от 2340 ≈ 260 px (`first-5-minutes.md`), исходник спрайта ≈ 2× (≈ 512 px роста героя) — запас на зум и атлас. Окончательное число — после кадра на телефоне.
- Холст: у всех кадров персонажа одного размера. **Pivot — середина между стопами по нижней непрозрачной строке**, одинаковый для всех кадров (Unity: Custom Pivot, `(x/width, y/height)` пишем в `.json` рядом).
- Палитра: мягкое приведение к `palette.json` — подмешивание с долей 0,4–0,6, тёмные линии не трогаем. Готовая основа — `comic_post.py --palette … --mix … --ink …` из comiclang. Жёсткая постеризация убьёт акварель.
- Импорт в Unity — заново по `docs/april-review.md` §3: Bilinear, ASTC, Sprite Atlas, кадры ходьбы, pivot из json.

**Шаг 6. Иконки.** Klein text-to-image с тем же эталоном: лист 3×3 иконок одной генерацией (*nine separate item icons on a grid, each centered in its cell, …* + `[STYLE]`) → нарезка по сетке → удаление фона → 256 и 128 px. Разговорные пиктограммы (дом, рука-указатель, «?», монета…) — отдельным листом.

**Звери.** Кабан и волк: сбоку модели рисуют уверенно, а прямо спереди и сзади четвероногих — плохо (мало таких картинок в обучающих данных). Ракурсы «к камере» и «от камеры» для них лучше делать в 3/4. Если лист не выйдет — путь 3б с болванкой из Quaternius Animals Pack (CC0, волк и свинья, `docs/april-review.md` §3).

### Надёжность (оценка без замеров; замер — проба K-03)

| Звено | Надёжность | Почему |
|---|---|---|
| Стиль общий для всех персонажей | средняя | держится на эталоне и дословном `[STYLE]`. comiclang: «FLUX.2 не переносит стиль с картинки на картинку» (мейнтейнер Krita, по RESEARCH 2026-10-05) — гарантию даёт только LoRA |
| Узнаваемость в 3 ракурсах (лист) | средняя/высокая | один холст, общий контекст; типовые ошибки — перепутанная сторона рюкзака, лишняя деталь сзади |
| Ракурсы через edit по одному | средняя | по деталям сходство 60–70 % (deskrex, по comiclang RESEARCH) |
| Угол камеры 35–45° | низкая без болванки | модели тянут к уровню глаз |
| Кадры шага (3а) | низкая/средняя | позы ног и рук у Klein 4B на 4 шагах плывут (comiclang: «лишние руки»); лечится base и ≥ 8 шагами или путём 3б |
| Масштаб и pivot | высокая | детерминированный код, не генерация |
| Удаление фона | высокая | на ровном фоне; акварельные края — despill |

Скорость генерации на этом ПК не замерял (из comiclang есть только время в выводе `klein_run.py`, логов не нашёл). Сроков не даю.

## 5. Чего не хватает (сам не ставил)

1. **BiRefNet** (MIT) — например, нода `ComfyUI-RMBG` (1038lab) или `ComfyUI_BiRefNet_ll`; модель ≈ 0,9 ГБ. Нужна для релиза: RMBG-1.4 — некоммерческая лицензия, RMBG-2.0 тоже CC BY-NC. **Приоритет высокий.**
2. **`comfyui_controlnet_aux`** (DWPose, Depth Anything v2, lineart) + **ControlNet под базовую модель.** Для Klein 4B готового ControlNet не нашёл — проверить в K-03. Если нет — SDXL `xinsir/controlnet-union-sdxl-1.0` (promax, ≈ 2,5 ГБ) с SDXL-чекпойнтом не из семейства Pony. Нужно для пути 3б. **Приоритет средний.**
3. **Qwen-Image-Edit-2511** (Apache-2.0) + LoRA поворота камеры (multi-angle). По comiclang RESEARCH, лучшее открытое средство для поворота персонажа. Тяжёлая: ≈ 20B параметров, на 16 ГБ — GGUF Q4 ≈ 12 ГБ с выгрузкой в RAM; нужен свой текстовый энкодер (Qwen2.5-VL 7B, не `qwen_3_4b`). Место есть (C: 63 ГБ). **Средний — если лист и Klein edit не дадут ракурсы.**
4. **ComfyUI-Manager** — удобство, не обязателен.
5. **Стилевая LoRA** — главное средство против гуляющего стиля. Варианты:
   - (а) своя LoRA для Klein 4B base на 20–40 принятых автором картинках. Нужен ai-toolkit: fluxgym учит только Flux.1; рецепт BFL для 9B — в comiclang RESEARCH, для 4B цифр нет;
   - (б) вернуть апрельский путь: `j_dsstyle_flux` с Civitai + Flux.1-dev, который уже лежит в fluxgym (подключить к ComfyUI через `extra_model_paths.yaml`, ничего не качая). Но 22 ГБ bf16 на 16 ГБ видеокарты пойдут с выгрузкой и медленно (лучше скачать fp8 ≈ 11 ГБ). Лицензия весов Flux.1-dev некоммерческая, а коммерческое использование картинок — перепроверить. LoRA подражает конкретной игре Klei;
   - (в) готовая ink/watercolor LoRA под Klein 4B, если найдётся.
   Решает автор после пробы без LoRA.
6. Апскейлер (4x-UltraSharp / RealESRGAN) — не нужен: генерируем ≥ 1024, спрайт ≈ 512.

## 6. Что дальше (предложение)

Проба K-03 на одной героине:
- 8 seed листа (шаг 1);
- 2 кадра шага путём 3а;
- фон и нормализация (шаги 4–5).

Затем frame-critic и лист вкуса для автора с тремя вопросами: манера (ближе к `ds_lora_v2_02` или к `ds_storybook_hybrid_01`), годится ли угол, читается ли шаг. Остальных персонажей — только после «годится». Вопрос автору про BiRefNet и LoRA — после пробы.
