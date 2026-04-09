# Наблюдения за процессом тестирования на эмуляторе

## Дата: 2026-04-10

### Общие ограничения эмулятора

1. **SwiftShader крашит URP** — любая сцена с URP pipeline вызывает SIGSEGV. Решение: Built-in RP + Unlit/Color шейдеры.
2. **SwiftShader крашит сложные сцены** — >15 root objects может крашить. Решение: лёгкие сцены (EmuStage*) под каждый этап.
3. **GLES3 работает, Vulkan крашит** — Auto выбирает Vulkan, нужно принудительно GLES3.
4. **2048MB RAM для эмулятора** — 1024MB недостаточно для стабильной работы.
5. **Эмулятор нужно перезапускать** после каждого краша Unity (Vulkan errors убивают эмулятор).

### Ввод (Input)

1. **`adb shell input swipe/tap` НЕ работает с Unity** — Unity не видит эти события (touchCount=0, mouseButton=False). Причина: `adb input` инжектирует через InputFlinger, который Unity игнорирует.
2. **`adb shell monkey` РАБОТАЕТ** — генерирует настоящие touch events через Android framework. Unity видит их как touch (touchCount=1) и mouse (mouseButton=True).
3. **monkey не позволяет указать координаты** — рандомные позиции. Для точного ввода нужен другой инструмент.
4. **monkey делает быстрые tap-like events** — не drag/swipe. GestureDispatcher не видит drift > threshold.
5. **Workaround: emulator fallback в GestureDispatcher** — статионарное касание (drift < 5px) интерпретируется как свайп от центра экрана к точке касания. Работает, но неточно.

### Решение для точного ввода: RemoteInputReceiver

**`Assets/Scripts/Debug/RemoteInputReceiver.cs`** — MonoBehaviour который читает команды из файла и вызывает GestureDispatcher напрямую через reflection.

**Путь к файлу команд:**
```
/storage/emulated/0/Android/data/com.papergallery.zeldasdaughter/files/unity_input.txt
```

**Использование:**
```bash
# Свайп (X1 Y1 X2 Y2 DURATION_MS) — координаты в пикселях экрана
adb shell "echo 'swipe 540 1500 540 700 500' > /storage/emulated/0/Android/data/com.papergallery.zeldasdaughter/files/unity_input.txt"

# Тап по точке
adb shell "echo 'tap 540 1000' > /storage/emulated/0/Android/data/com.papergallery.zeldasdaughter/files/unity_input.txt"

# Лонг-пресс
adb shell "echo 'hold 540 1170 700' > /storage/emulated/0/Android/data/com.papergallery.zeldasdaughter/files/unity_input.txt"
```

**Почему это работает:** Вместо инжекции через Android InputManager (который Unity не видит), RemoteInputReceiver вызывает `GestureDispatcher.OnPointerDown/Held/Up` напрямую. Обходит всю цепочку ОС.

**Что НЕ работает:**
- `adb shell input swipe/tap` — Unity Old Input System не видит (подтверждённый баг Unity: issuetracker.unity3d.com)
- `adb shell sendevent` — Unity не видит (даже с root)
- `adb shell monkey` — работает но без контроля координат

**Альтернативные подходы (не реализованы, на будущее):**
- `activeInputHandler: 2` (Both) в ProjectSettings — может помочь с `adb input`, но потребует тестирования
- Appium + UiAutomator2 — надёжная инжекция через Instrumentation API, но тяжёлый (~500MB + Java)
- AltTester Unity SDK — open-source, TCP-инжекция touch events, но нужен instrumented build

### Скриншоты и визуальная проверка

1. **`adb exec-out screencap -p`** работает стабильно
2. **Python PIL** для анализа цвета (розовый экран, сравнение скриншотов) — работает, но deprecated warning на `getdata()`
3. **Сравнение скриншотов** (% изменённых пикселей) — хороший способ проверить что персонаж/камера двигается

### Debug-логи [ZD:*]

1. **ZD_DEBUG define** работает (не Development Build — тот крашит ARM на x86)
2. **ZDLog.Log** с `[Conditional("ZD_DEBUG")]` — вызовы вырезаются в release
3. **DebugEventLogger** подписывается на static events — покрывает Input, Move, Combat, Inventory, Progression
4. **DebugPositionLogger** — позиция каждые 5 сек
5. **DebugPerformanceLogger** — FPS + память каждые 10 сек
6. **CrashDiagnostics** — GraphicsAPI, device info, компоненты сцены

### Процесс тестирования (workflow)

1. Собрать EmuStage* сцену через Editor-скрипт
2. Собрать APK через AndroidBuilder (GLES3, no URP, ZD_DEBUG)
3. Запустить эмулятор (2048MB, swiftshader_indirect)
4. Установить APK
5. Запустить игру через monkey launcher
6. Ждать 10 сек загрузки
7. Проверить logcat на краши
8. Сделать скриншот
9. Отправить ввод через monkey
10. Проверить ZD-логи + скриншот
11. **ЗАКРЫТЬ ИГРУ** (am force-stop) после тестов
12. Обновить TESTING_GUIDE.md

### Сцены для тестирования

| Сцена | Этапы | Содержимое |
|---|---|---|
| EmuStage1 | 1-2 | Ground + Player + Camera + деревья + кусты + камни + Input |
| EmuStage2 | 3-4 | + Pickupable + ResourceNode + TapInteractionManager + PlayerInventory |
| EmuStage3 | 5-7 | + Inventory UI + Crafting (TODO) |

### Координаты экрана

Эмулятор zelda_test (Pixel 4) имеет физическое разрешение **480x800** (не 1080x2340!).
RemoteInputReceiver масштабирует: `scaleX = 480/1080 = 0.444`, `scaleY = 800/2340 = 0.342`.
Команды принимают "логические" координаты 1080x2340, скрипт пересчитывает.
