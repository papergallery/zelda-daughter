# Общее для всех агентов Zelda (план 0.2, ADR-0007/0008)

- Читать: `CLAUDE.md`, `project-design.md` (источник истины), `plan/README.md`, такс-лист своего этапа, `docs/agent-handbook.md`.
- Правила игры — в ядре `core/ZeldaDaughter.Core` (netstandard2.1, C# 9, без `UnityEngine`, `#nullable enable` первой строкой), тесты xUnit до кода, `tools/check.sh` → `CHECK OK`. Числа — в `data/*.json` с полем `_source`, не в коде.
- Unity-проект — `ZeldaDaughter/`; Unity есть только на ПК автора, через мост (`tools/pc/README.md`). Код Unity, не скомпилированный на ПК, — «не проверен», так и писать.
- Апрель (`archive/2026-04/UnityProject`) — только справочник (`docs/april-review.md`): правила, числа, тексты берём, код не переносим.
- Видимое — критерии в `docs/done-criteria/<ID>.md` до работы; «сделано» — с доказательством (тест, кадр, строка `[ZD:*]`).
- Тяжёлое (`dotnet build|test`) — под общей с The Chest блокировкой; `tools/check.sh` берёт её сам.
