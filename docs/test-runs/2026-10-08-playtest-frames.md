# 2026-10-08 · Windows-сборка серого пролога и кадры из редактора

- Редактор `ZeldaDaughter@24ee0fcd79850490` (`projectRoot` = `C:/dev/zelda/ZeldaDaughter`). На ПК ядро с C-16/C-17, `data/` и Editor-скрипты переносились архивом. Компиляция: 0 ошибок, 0 предупреждений.
- `BuildScript.WindowsPlaytest()` (`tools/pc/build-win-playtest.cs`) → `[ZD:Build] StandaloneWindows64 release result=Succeeded size=93,0MB time=19s path=C:\dev\zelda-builds\playtest\ZeldaDaughter.exe`. В сборке только `prologue-grey`, мышь — палец (`TouchSimulation` в релизе есть, не под `ZD_DEBUG`).
- Запуск 15 с без окна (`zd-player.ps1 -exe …\playtest\ZeldaDaughter.exe`): `[ZD:Data] loaded items=32 recipes=7`, `[ZD:Save] loaded day=1 t=0,360`, исключений нет. Ходьба мышью руками не проверялась.
- `FrameCapture.Capture` (`tools/pc/frames-prologue.cs`): `[ZD:Frame] prologue-grey daylight=1,00` и `daylight=0,00` → `frames/prologue-grey-day.png`, `frames/prologue-grey-night.png` (1080×2340). Сцена после съёмки открыта с диска заново, `git status` клона чистый (только перенесённые файлы).
- Что видно (вид — заглушка, не оценивается до Р2): днём в кадре капсула героя на дороге, бревно, палка, ствол дерева. Ночью почти чёрный кадр, дорога едва различима: ночного света пока нет, его делает R2-05.
