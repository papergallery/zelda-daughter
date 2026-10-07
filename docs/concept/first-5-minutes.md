# Первые 5 минут — сценарий и кадры (K-01)

2026-10-08, черновик агента — ждёт «годится» автора. Источник — `project-design.md` §1 (управление, камера), §2 «Пролог», §3 этап 1 языка, §6 «Онбординг», §9 звук. Раскладка мест — апрельский `region_startmeadow.json` как черновик (`docs/april-review.md` §2): спавн (−40, 0), дорога на восток, хижина с полем (−10, 15), город ≈ (14…27, 3…13). Механика уже в ядре: жесты (C-02), ходьба и бег (C-03), утро → день (C-04), тарабарщина и иконки (C-11), реплики (C-12).

Всё — без интерфейса (§1): ни полос, ни цифр, ни маркеров. Камера статичная изометрическая, привязана к герою; телефон вертикально.

## Сценарий по секундам

| Время | Что делает игрок | Что видит | Что слышит |
|---|---|---|---|
| 0:00–0:08 | ничего | Затемнение → глаза открываются: герой лежит у обочины грунтовой дороги, раннее утро, туман над травой. Встаёт. | Тишина → птицы, ветер в траве |
| 0:08–0:20 | первый свайп | Неблокирующая подсказка «веди пальцем» (полупрозрачная рука у нижнего края) — исчезает после первого шага (§6). Дорога уходит на восток — направление «само». | Шаги по земле |
| 0:20–0:40 | идёт по дороге ~15 с | Обочины: кусты качаются, когда герой проходит (§1 Rigidbody); палка у дороги. Реплика при подборе: «Палка... сгодится». | Шаги, птицы, далёкий стук топора |
| 0:40–1:40 | подходит к полю, тап по крестьянину | Хижина, поле, крестьянин работает. Облачко: руны-тарабарщина + иконки (город, рука-указатель). Герой отвечает иконкой «?» — крестьянин сначала не понимает (§3 «провал»), со второй иконки показывает рукой на восток. | Стук мотыги, голос крестьянина (интонация без слов) |
| 1:40–2:40 | идёт к городу | Дорога поднимается на холм — за ним крыши, дым из труб. Ягоды у дороги: «Ягоды. Съедобные?» Первая подсказка тапа — у куста. | Ветер сильнее на холме, вдали — гул голосов |
| 2:40–3:40 | у края города, тап по стражнику | Ворота или просто край: изгородь, первый дом, стражник. Тарабарщина + жест «проходи». | Лязг, голоса, скрип телеги |
| 3:40–5:00 | входит в город | Площадь с фонтаном, лавка, вывеска таверны, 2–3 жителя по делам — им до героя нет дела (§2 «равнодушный мир»). Конец пролога — герой в городе. | Гул голосов, стук инструментов, вдали лютня из таверны (диегетическая музыка §9) |

Ночной вариант (проверка стиля, §2): тот же кадр пробуждения ночью — герой у костра (как в апрельском спавне: костёр у бревна).

## Ключевые кадры (для K-02 и K-03)

| № | Кадр | Зачем |
|---|---|---|
| F1 | Пробуждение у дороги, утро, туман | главный кадр направления мира: земля, трава, дорога, свет |
| F1n | То же место ночью, костёр у бревна, герой сидит | держится ли стиль ночью; свет от огня |
| F2 | Крестьянин в поле у хижины, облачко с рунами и иконками | персонаж-NPC рядом с героем; читаемость облачка |
| F3 | Край города: изгородь, первый дом, стражник | постройки в стиле мира |
| F4 | Площадь с фонтаном, жители | насыщенность сцены, масштаб |

## Общее для всех промптов

Чтобы кадры разных направлений можно было сравнить рядом (K-02 «один ракурс и масштаб»):

- **Кадр:** вертикальный экран телефона 9:19.5 (например 1080×2340); камера сверху сбоку — наклон ≈ 35°, поворот 45° (изометрия, как в игре: `scenes/g1-capsule.json`); без перспективных искажений (ортографическая).
- **Герой:** в центре, чуть ниже середины; ростом ≈ 1/9 высоты кадра; 2D-рисунок поверх мира (billboard), смотрит вдоль дороги.
- **Палитра:** приглушённая тёплая; «книжная иллюстрация» — перо + акварель (ADR-0001).
- **Без:** интерфейса, текста, логотипов (в апрельском `style2_watercolor.png` на кроссовках — знак Nike), водяных знаков.
- **Герой** — до ответа автора на `docs/for-author.md` п. 1 подставлять **[HERO]**:
  - вариант «дочь»: *a young woman in her early twenties from our modern world — plain t-shirt, rolled-up jeans, worn sneakers without logos, small olive backpack, messy dark hair*;
  - вариант «парень»: то же, *a young man in his early twenties*.

**Негатив (для сервисов, где он есть):** `text, letters, UI, health bar, logo, brand, watermark, signature, photorealistic, 3d render gloss, extra limbs, perspective distortion, landscape orientation`.

## Направления мира (K-02) — по одному промпту на кадр

**А · Рисованный мир, как Don't Starve.** Всё плоское и нарисованное: деревья, дома, трава — billboard-спрайты на рисованной земле; контур пером, заливка акварелью; чуть гротескные пропорции.
**Б · Стилизованное 3D + акварельный пост-эффект.** Простые низкополигональные формы (как Kenney / Flat Kit / Top Down Fantasy Forest из библиотеки — `docs/research/asset-library-zd.md`), мягкий акварельный шейдер и контур; герой — рисованный спрайт поверх.
**В · Бумажная диорама.** 3D-формы, обтянутые рисованными текстурами, как вырезанные из бумаги и раскрашенные акварелью; мягкие тени, ощущение настольного макета.

Шаблон: `[кадр] + [направление] + общее`. Готовые промпты (английский — для внешних сервисов):

### F1 — пробуждение у дороги, утро
- **А:** `Vertical mobile game screenshot 9:19.5, isometric top-down view 35 degrees, ink and watercolor storybook illustration in the style of Don't Starve, everything hand-drawn and flat: a dirt road crossing a misty meadow at dawn, tall grass, a few crooked trees, wild flowers, muted warm palette, soft morning light. [HERO] just woke up and stands at the roadside, small in the frame (1/9 of the frame height), slightly below center, looking along the road. No UI, no text.`
- **Б:** `Vertical mobile game screenshot 9:19.5, isometric orthographic camera 35 degrees, stylized low-poly 3D world with a soft watercolor post-process and thin ink outlines: a dirt road through a misty meadow at dawn, rounded low-poly trees, grass tufts, muted warm palette. A hand-drawn 2D character sprite, [HERO], stands at the roadside, 1/9 of the frame height, slightly below center. No UI, no text.`
- **В:** `Vertical mobile game screenshot 9:19.5, isometric view 35 degrees, paper diorama world: hills, trees and a dirt road made of cut paper painted with watercolor, visible paper edges and soft contact shadows, misty dawn, muted warm palette, tabletop miniature feeling. A flat hand-drawn paper character, [HERO], stands at the roadside, 1/9 of the frame height, slightly below center. No UI, no text.`

### F1n — то же место ночью, костёр
Заменить в F1: `at dawn … soft morning light` → `at night, deep blue moonlight, a small campfire next to a fallen log lights the scene with warm orange light, [HERO] sits by the fire`.

### F2 — крестьянин в поле
Заменить сцену F1 на: `a small wooden hut with a vegetable field, a peasant in simple medieval clothes working with a hoe; above the peasant a speech bubble with runic gibberish symbols and two small pictograms (a house, a pointing hand); [HERO] stands a few steps away facing the peasant`.

### F3 — край города
Сцена: `the edge of a small medieval town: a wooden fence, the first timber house with a smoking chimney, a guard with a spear at the opening in the fence; [HERO] walks up the road towards the guard`.

### F4 — площадь
Сцена: `a small town square with a stone fountain, a market stall with green awning, a tavern sign, two or three townsfolk going about their business and ignoring [HERO], who stands at the edge of the square`.

## Что агент проверит в присланных кадрах (K-02)
Один ракурс и масштаб героя во всех кадрах направления (сравнение по сетке); вертикальный формат; нет текста и логотипов; кадр F1 и F1n одного места. Кадры класть в `docs/concept/world-a/`, `world-b/`, `world-c/` с именами `f1.png`, `f1n.png`, `f2.png`…
