# ODD-MASTER

Каркас модуля «ОДД-Мастер» для nanoCAD: параметрические знаки по ГОСТ Р 52290-2024, дислокация по пикетам, ведомость ТС ОДД, контроль и Актуализатор номенклатуры 2004 → 2024.

**Статус:** сессия №2 программы «Продукт» (28.10.2026). P1 закрыл долги качества, P2 дал каталог MVP-30 + 11 табличек и конвейер оцифровки, P3 — модель документа и живую ведомость. В nanoCAD продукт **пока не работает**: гейты G1–G5 закрыты, адаптер не компилировался. См. `docs/PRODUCT_STATUS.md` и `docs/reports/SESSION-02.md`. Это не готовый продукт: нормативные данные имеют статус `CuratorExtracted` или `Placeholder`, адаптер nanoCAD не компилировался, карта замен Актуализатора пустая. Всё, где использованы заглушки, помечено «ЧЕРНОВИК».

**Позиционирование.** Прямой аналог — IndorTrafficPlan (с апреля 2025 в нём есть ПОДД, авторасстановка ТСОДД, ведомости по Приказу Минтранса №49 и знаки по 52290-2024). В экосистеме nanoCAD инструмента для постоянных схем ОДД по ГОСТ Р 52290-2024 по открытым источникам на 27.10.2026 не найдено.

## Быстрый старт (5 минут, Linux)

```bash
# нужен официальный .NET 8 SDK (global.json: 8.0.100, rollForward latestFeature)
dotnet restore OddMaster.sln
dotnet build OddMaster.sln -warnaserror
dotnet test OddMaster.sln
dotnet run --project src/OddMaster.Demo -- all
```

Или одной командой: `bash scripts/verify.sh`.

`global.json` фиксирует 8.0.100 с `rollForward: latestFeature`: подходят все SDK 8.0.x. Резервные каналы (npm, самосборный OpenSSL) удалены по CR-02. Тесты — xUnit с покрытием (ADR-0018); CI гоняет Linux на каждый пуш и Windows на PR.

## Структура

```
config/   нормативные данные и JSON Schema (источник истины)
src/OddMaster.Core/        ядро: нормы, геометрия, генератор, ось, ведомость, контроль, Актуализатор, документ, пиктограммы
tools/OddMaster.Tools/     odd-tools: импорт/накладка пиктограмм, номенклатура, selftest
config/pictograms/         заглушки пиктограмм в клетках сетки прил. Б
src/OddMaster.Render.Svg/  SVG-рендер знаков и схемы
src/OddMaster.Demo/        сквозной сценарий → docs/demo/
src/OddMaster.NanoCAD/     скелет адаптера MultiCAD .NET (отдельное решение, не собирается в CI)
tests/                     тесты и ДЕМО-фикстуры
docs/                      архитектура, ADR, реестры GOST-/SDK-маркеров, отчёт
```

## Документы

- [Архитектура](docs/ARCHITECTURE.md) · [ADR](docs/adr/) · [Источники нормативки](docs/NORMATIVE_SOURCES.md)
- [Очередь проверки ГОСТ](docs/VERIFICATION_QUEUE.md) · [Предположения SDK](docs/SDK_ASSUMPTIONS.md)
- [Объяснение для автора](docs/EXPLAIN_FOR_AUTHOR.md) · [Как добавить знак](CONTRIBUTING.md) · [Оцифровка пиктограмм](docs/howto/PICTOGRAMS.md)
- [Статус продукта](docs/PRODUCT_STATUS.md) · [Визуальные ревью](docs/visual/REVIEW-01.md) · [Настройка раннера nanoCAD](docs/runner/RUNNER_SETUP_NANOCAD.md)
- [Журнал применения ИИ](docs/AI_USAGE_LOG.md) · [Отчёт ночного прогона](docs/NIGHT_REPORT.md)
- [Демо-артефакты](docs/demo/) — SVG, CSV, MD, JSON видны в PR без запуска кода
