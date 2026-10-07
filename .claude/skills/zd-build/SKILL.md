---
name: zd-build
description: Собрать Zelda's Daughter под Android на ПК автора, поставить на телефон, снять запись экрана и строки [ZD:*] из logcat — доказательство для видимых задач и ворот G1/G2. Использовать для T-07 и любых проверок «на телефоне».
---

# Сборка на телефон (T-07)

**Статус 2026-10-08:** заготовка. Не готово: модуль Android Build Support в Unity 6000.3.24f1 на ПК (стоит только Windows) и телефон с отладкой по USB — ждёт автора (`docs/for-author.md` п. 3).

## Как будет
1. `BuildScript.Android(debug|release)` в `ZeldaDaughter/Assets/Scripts/Editor/` через мост (`execute_code`) или `-executeMethod`; сборщик **не меняет** настройки проекта и материалы (урок апрельского `AndroidBuilder`); отладочная сборка — `extraScriptingDefines = ZD_DEBUG` только для неё.
2. Перед сборкой — `DataSync` (data/ → Resources) и `SceneBuilder.BuildAll()`.
3. `adb install -r <apk>` (adb из `…\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools`), запуск `adb shell monkey -p com.papergallery.zeldasdaughter 1`.
4. `adb logcat -s Unity` → строки `[ZD:*]` на сервер; запись — `adb shell screenrecord --time-limit 60 /sdcard/zd.mp4` → `adb pull` → на сервер (`tlbridge.py get`).
5. Кадры из записи (ffmpeg) — рядом с концептом для листа вкуса; итог — `docs/test-runs/`.

## Нельзя
Менять в сборщике URP, материалы, `ProjectSettings` (только `ProjectSetup.Apply` их меняет); тянуть на сервер Android SDK/эмулятор (ADR-0003).
