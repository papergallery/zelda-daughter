# Скрипты для ПК автора (через мост TensorLay)

Кладутся в `C:\dev\zelda-tools\` (`tlbridge.py put tools/pc/<файл> C:/dev/zelda-tools/<файл>`), запускаются `tlbridge.py exec '& C:\dev\zelda-tools\<имя>.ps1'`. Клиент моста Unity — `C:\dev\zelda-tools\umcp.py` (копия `/var/www/html/thechest/tools/umcp.py`), `UMCP_PROJECT=ZeldaDaughter` — вызов идёт только в редактор Zelda, иначе ошибка (никогда — в The Chest).

| Скрипт | Что делает |
|---|---|
| `zd-sync.ps1` + `empty-scene.cs` | редактор на пустую сцену (без окна «Reload?»), клон `C:\dev\zelda` → `origin/master` (`reset --hard`), владелец файлов — пользователь (мост работает от администратора) |
| `zd-refresh.ps1` + `refresh.cs` | `AssetDatabase.Refresh()` — перекомпиляция после изменения файлов |
| `zd-state.ps1` + `state.cs` | `compiling=… failed=…` — ждать, пока `compiling=False`, прежде чем читать консоль |
| `zd-check.ps1` | `project/info` (какой редактор ответил) + ошибки и предупреждения консоли |
| `zd-console.ps1` | ошибки и предупреждения консоли |
| `zd-apply.ps1` + `apply.cs` | `ProjectSetup.Apply()` + отчёт `[ZD:Setup]` + `git status` |
| `zd-build.ps1` + `build.cs` | `SceneBuilder.BuildAll()` дважды + консоль (`[ZD:Scene] … hash=…`) |
| `zd-import-fire.ps1` + `fx-build.cs` | D-16: выборочный импорт Ian's Fire Pack из кэша Asset Store в `ThirdParty/IansFirePack`; затем `fx-build.cs` (`FxBuilder.Build`) делает приглушённые префабы `campfire`/`torch_flame`/`grass_fire` |
| `zd-list.ps1`, `zd-instances.ps1` | инструменты моста; подключённые редакторы |
| `zd-reload.ps1` | нажать «Reload» в окне Unity «scene modified externally» (Win32 `BM_CLICK`) — вызывается из `zd-sync` |
| `zd-run.ps1 <файл.cs>` | `execute_code` с таймаутом 15 мин (сборки): `build-win-release.cs`, `build-win-debug.cs` |
| `zd-types.ps1` | есть ли типы (`PerfProbe`, …) в `ZeldaDaughter.dll` сборок release/debug |
| `build-release-all.cs`, `build-win-perf.cs`, `switch-win.cs` | D-19: Windows release + Android APK одной командой; сборка для замера (релиз + `ZD_DEBUG`); вернуть платформу редактора на Windows |
| `zd-perf.ps1`, `zd-run-release.ps1` | замер плеера 60 с в окне 1080×2340 (`[ZD:Perf] summary`); релиз 60 с — ошибки журнала. Запускать отсоединённо (`Start-Process powershell …`), `exec` живёт 60 с |
| `unstick.cs`, `probe-time.cs`, `probe-data.cs`, `probe-android.cs` | снять зависшее задание тестов; состояние времени/данных/платформы в Play |
| `zd-player.ps1 [-exe <путь>]` | Windows-плеер 15 с без окна (по умолчанию отладочный), строки `[ZD:*]` из журнала |

**Модальные окна глушат мост.** Редактор молчит («ping not answered», `TimeoutError`) — почти всегда окно в Unity. Посмотреть — скриншот **только окна Unity** (UI Automation `BoundingRectangle`, не весь экран: там терминалы автора), удалить после. «Scene modified externally» — `zd-reload.ps1`. Запуск отладочного плеера вызывает окно брандмауэра Windows (порт профайлера) — оно системное, мостом не закрывается: не запускать debug-плеер без нужды, в записке автору — «нажмите Отменить».

Порядок проверки изменения Unity-кода: файлы на ПК (`put` или архив `tar` → `tar -xf`) → `zd-refresh` → ждать `zd-state` `compiling=False` → `zd-check`. PlayMode-тесты — `run_tests` `{"mode":"PlayMode",…}` → `get_test_job` (см. `docs/test-runs/`); `run_tests` включает Enter Play Mode Options — `ProjectSetup.Apply()` возвращает.
