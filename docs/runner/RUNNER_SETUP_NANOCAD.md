# Пакет передачи студенту: раннер nanoCAD (G1/G2)

**Одна страница. Время: 2–4 часа.** Не делать до приватизации репозитория (CR-01).

## Чего нет у агента

- SDK nanoCAD 26 и лицензия; `docs/feedback/RUNNER_READY.md`; self-hosted раннер.
- Поэтому адаптер `src/OddMaster.NanoCAD/` не компилировался, все 8 предположений — `SDK-NNN`.

## Шаги (порядок обязателен)

1. **Приватизировать репозиторий**: Settings → General → Danger Zone → Change repository visibility → Private. Проверить `gh api repos/Igorr112323/ODD-MASTER --jq .private` → `true`.
2. **Смёрджить PR #1 в `main`** (Create a merge commit) — иначе история продукта начнётся с пустого `main`.
3. **Получить SDK nanoCAD 26** (вузовская лицензия), распаковать в `C:\nanoCAD\sdk26\include-x64`.
4. **Поднять раннер**: Settings → Actions → Runners → New self-hosted runner (Windows), метки `self-hosted`, `nanocad`. Установить .NET 8 SDK и MSBuild/VS Build Tools.
5. **Переменные репозитория**: `vars.NANOCAD_RUNNER=on`, `vars.NANOCAD_SDK_PATH=C:\nanoCAD\sdk26\include-x64`.
6. **Активировать workflow**: скопировать `docs/runner/nanocad-build.yml.template` → `.github/workflows/nanocad-build.yml`. Пока `NANOCAD_RUNNER != on`, job пропускается и не висит в очереди.
7. **Собрать**: `dotnet build OddMaster.NanoCAD.sln -c Release -p:NanoCadSdkPath=...`. Первым делом закрыть SDK-001 (имена DLL) и SDK-002/003 (`Multicad.Runtime`, `CommandMethod`) по логу компиляции.
8. **SdkProbe**: `tools/SdkProbe` на `MetadataLoadContext` (создаётся в P4) читает типы/сигнатуры SDK, результат `sdk-api.txt` — артефакт, **в git не коммитить** (лицензия SDK).
9. **Смоук**: загрузить DLL в nanoCAD (`NETLOAD`), выполнить `ODD_HELLO`, заполнить `docs/feedback/SMOKE-2026-11-..md` по шаблону (P4). Только после этого ставить L3.

## Чек-лист готовности G1

- [ ] репозиторий PRIVATE;
- [ ] PR #1 смёржен в `main`;
- [ ] `workflow nanocad-build` стартует на раннере `[self-hosted, nanocad]`;
- [ ] лог сборки зелёный, `sdk-api.txt` получен;
- [ ] `docs/feedback/RUNNER_READY.md` создан студентом.

## Оценка риска

Без этого шага продукт остаётся набором алгоритмов и SVG. Все остальные работы (P5–P8, D1–D6, заявка) заблокированы G1/G2. Это **главный риск для приза**.
