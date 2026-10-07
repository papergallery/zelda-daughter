# R1-01 — проект на Unity 6: итог первого импорта (2026-10-07)

**Как.** Клон `C:\dev\zelda` (мост TensorLay, `tlbridge.py exec`), затем `Unity.exe -batchmode -nographics -quit -projectPath C:\dev\zelda\UnityProject -logFile …\Logs\r1-01-import.log`, редактор 6000.3.24f1 (как у The Chest). С 21:38:34 до 21:40:40 по серверу — ≈ 2 мин на импорт с нуля. Выход — `Exiting batchmode successfully`, код 0. В окне редактора проект ещё не открывали.

**Ошибки компиляции: 0.** Сборки `ZeldaDaughter`, `ZeldaDaughter.Editor`, `ZeldaDaughter.Tests.EditMode` собраны (компиляция скриптов 13,8 с по логу).

**Предупреждения: 224 вхождения в логе** (часть повторяется по сборкам): CS0618 — 208, CS0414 — 10, CS0067 — 6. Из CS0618: `FindObjectOfType` — 130, `FindObjectsOfType` — 38 (в игровом цикле запрещены правилами — уйдут вместе с «фиксерами» в R1-06), `NPCData` (свой `[Obsolete]`) — 10, `PlayerSettings.*ForTargetGroup`/`SetScriptingBackend`/`SetArchitecture` — 28 (апрельский `AndroidBuilder`, R1-06/R1-07). Больше всего — `Editor/SceneBuilder.cs` 36, `EmulatorSceneBuilder.cs` 26, `EmuStageBuilder.cs` 18, `DemoSceneWirer.cs` 18, `Debug/RemoteInputReceiver.cs` 12; всего 34 файла.

**Что поменял импорт (коммит R1-01, 117 файлов).** `ProjectVersion.txt` 2022.3.30f1 → 6000.3.24f1. Пакеты: URP 14.0.11 → 17.3.0, Input System 1.7.0 → 1.20.0, Test Framework 1.3.9 → 1.6.0, Cinemachine 2.9.7 → 2.10.7; TextMeshPro 3.0.6 убран из манифеста (в Unity 6 он внутри uGUI); добавлены `multiplayer.center` и модули accessibility / adaptiveperformance / vectorgraphics. 106 материалов `Assets/Materials` (поля URP 17), `GraphicsSettings`, `ProjectSettings`, `ShaderGraphSettings`, `UniversalRenderPipelineGlobalSettings`; новые `DefaultVolumeProfile.asset`, `MultiplayerManager.asset`.

**Не решено (R1-06/R1-07):** Input System стоит, а `activeInputHandler` — старый ввод; Cinemachine нужен ли; лишний `multiplayer.center` — убрать при чистке пакетов.

**Первое открытие в окне (2026-10-07, вечер)** дописало миграцию: `URP_PipelineAsset` `k_AssetVersion` 11 → 13 (поля URP 17: probe volumes, GPU Resident Drawer — выключен, `m_EnableRenderGraph` убран), `GraphicsSettings` `m_LightsUseColorTemperature` 0 → 1, правки `ProjectSettings`/`QualitySettings`, новые `PackageManagerSettings.asset`, `URPProjectSettings.asset`. Ошибок компиляции в окне нет.
