---
name: zd-unity-bridge
description: Проверить Unity-код Zelda's Daughter в редакторе на ПК автора через мост TensorLay — перенос файлов, перекомпиляция, консоль, сборка сцен из конфига, PlayMode-тесты. Использовать после любой правки в ZeldaDaughter/ или core/, до коммита Unity-кода в master.
---

# Мост к редактору Zelda

Unity есть только на ПК автора (ADR-0003). С сервера: мост TensorLay `127.0.0.1:6520` (`/var/www/html/Other/tensorlay/tools/bridge/tlbridge.py`) запускает PowerShell на ПК; там `C:\dev\zelda-tools\umcp.py` говорит с сервером unity-mcp `127.0.0.1:6510`, общим с The Chest (R1-02). Скрипты — `tools/pc/` (README там).

## Перед началом
1. `tools/resources.sh` — на ПК C: ≥ 20 ГБ и память ≥ 4 ГБ, иначе не запускать Unity.
2. `tlbridge.py ping` — мост жив. Нет — записать в `docs/for-author.md` и делать серверные задачи.
3. Скрипты на месте: `tlbridge.py put tools/pc/<f> C:/dev/zelda-tools/<f>` (и `umcp.py` из `/var/www/html/thechest/tools/umcp.py`).

## Цикл проверки
1. Файлы на ПК: архив `tar -cf … <пути>` → `put` → `tar -xf` в `C:\dev\zelda` (или `zd-sync.ps1` после пуша).
2. `zd-refresh.ps1` → ждать `zd-state.ps1` = `compiling=False failed=False`.
3. `zd-check.ps1`: `projectRoot` = `C:/dev/zelda/ZeldaDaughter` (иначе ответил чужой редактор — стоп) и 0 ошибок/предупреждений.
4. Сцены: `zd-build.ps1` → `[ZD:Scene] built … hash=…` дважды одинаковый.
5. PlayMode — **только свои и затронутые классы**: `zd-tests.ps1 -filter "ZeldaDaughter.Tests.<Класс>,…"` → `get_test_job` до `succeeded`; итог и **время прогона** — `docs/test-runs/`. Полный прогон — не в каждой очереди (правило ниже, «Сколько тестировать»).
6. Файлы, которые Unity создал или поменял (сцены, материалы, ProjectSettings, `.meta`), — забрать архивом обратно; концы строк — LF (`.gitattributes`); `git status` на ПК после `zd-sync` должен быть чистым.
7. `run_tests` включает Enter Play Mode Options — `zd-apply.ps1` возвращает.

## Окно «Scene modified externally — Reload?»
Появляется, когда файл открытой сцены меняется на диске (zd-sync, `tar -xf`, `git checkout`), и глушит мост (а автору — надоедает). Перед любой перезаписью файлов сцен на ПК: `umcp.py code C:\dev\zelda-tools\empty-scene.cs` (редактор переходит на пустую безымянную сцену; грязную не трогает). `zd-sync.ps1` делает это сам. После своей работы не оставляй открытой сгенерированную сцену из `Assets/Scenes`.

## Нельзя
- Закрывать или перезапускать редакторы и сервер моста (сервер принадлежит редактору The Chest).
- Запускать Unity через мост без задачи планировщика `RunLevel Limited` — иначе окно и файлы от администратора (`docs/agent-handbook.md`).
- `-batchmode` по проекту, открытому в редакторе.

## Сколько тестировать (2026-10-08, после вопроса автора)
Редактор один на всех агентов, полный PlayMode идёт десятки минут — гонять его каждым агентом на каждую правку значит держать очередь часами.
- **В своей очереди:** `zd-check` 0/0 + PlayMode **своих** классов и классов, которые трогает правка (вызывающие твой код; по `grep` имени класса/события в `Tests/PlayMode`). Ядро — `dotnet test --filter` по своим классам; полный `dotnet test` дешёвый (сервер) — его можно всегда.
- **Полный PlayMode** — только: (а) правка общего кода (`GameSession`, `SessionEvents*`, `GameData`, `BillboardSprite`/`CharacterSpriteSet`, `SceneBuilder.cs`/`Session`, `HeroController`, `TestSaves`); (б) перед сборкой релиза (D-19); (в) интеграционный прогон координатора (раз в ~2 ч, если были коммиты Unity). Падение чужого теста в полном прогоне — записать и сообщить координатору, не чинить в своей очереди.
- **Кадры** (`*Frames`) — `[Explicit]`, снимаются отдельно по фильтру, в общий прогон не входят.
- Держать очередь ≤ 45 мин; ждёшь долгий прогон — не правь код в это время, готовь следующую партию на сервере.
