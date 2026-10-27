# ODD-MASTER

Каркас модуля «ОДД-Мастер» для nanoCAD: параметрические знаки по ГОСТ Р 52290-2024, дислокация по пикетам, ведомость ТС ОДД, контроль и Актуализатор номенклатуры 2004 → 2024.

**Статус:** каркас ночного прогона №1 (2026-10-27). Это не готовый продукт: нормативные данные имеют статус `CuratorExtracted` или `Placeholder`, адаптер nanoCAD не компилировался, карта замен Актуализатора пустая. Всё, где использованы заглушки, помечено «ЧЕРНОВИК».

**Позиционирование.** Прямой аналог — IndorTrafficPlan (с апреля 2025 в нём есть ПОДД, авторасстановка ТСОДД, ведомости по Приказу Минтранса №49 и знаки по 52290-2024). В экосистеме nanoCAD инструмента для постоянных схем ОДД по ГОСТ Р 52290-2024 по открытым источникам на 27.10.2026 не найдено.

## Быстрый старт (5 минут, Linux)

```bash
bash scripts/bootstrap-dotnet.sh     # только если dotnet не установлен
source scripts/dotnet-env.sh
dotnet restore OddMaster.sln
dotnet build OddMaster.sln -warnaserror
dotnet test OddMaster.sln
dotnet run --project src/OddMaster.Demo -- all
```

Или одной командой: `bash scripts/verify.sh`.

Если у вас стоит обычный .NET 8 SDK — bootstrap не нужен: `global.json` разрешает более новый SDK, а целевой фреймворк переключается переменной `-p:OddTargetFramework=net8.0` (ADR-0009).

## Структура

```
config/   нормативные данные и JSON Schema (источник истины)
src/OddMaster.Core/        ядро: нормы, геометрия, генератор, ось, ведомость, контроль, Актуализатор
src/OddMaster.Render.Svg/  SVG-рендер знаков и схемы
src/OddMaster.Demo/        сквозной сценарий → docs/demo/
src/OddMaster.NanoCAD/     скелет адаптера MultiCAD .NET (отдельное решение, не собирается в CI)
tests/                     тесты и ДЕМО-фикстуры
docs/                      архитектура, ADR, реестры GOST-/SDK-маркеров, отчёт
```

## Документы

- [Архитектура](docs/ARCHITECTURE.md) · [ADR](docs/adr/) · [Источники нормативки](docs/NORMATIVE_SOURCES.md)
- [Очередь проверки ГОСТ](docs/VERIFICATION_QUEUE.md) · [Предположения SDK](docs/SDK_ASSUMPTIONS.md)
- [Объяснение для автора](docs/EXPLAIN_FOR_AUTHOR.md) · [Как добавить знак](CONTRIBUTING.md)
- [Журнал применения ИИ](docs/AI_USAGE_LOG.md) · [Отчёт ночного прогона](docs/NIGHT_REPORT.md)
- [Демо-артефакты](docs/demo/) — SVG, CSV, MD, JSON видны в PR без запуска кода
