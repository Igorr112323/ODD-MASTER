# Журнал ночного прогона №1

Время в ISO 8601, +03:00 (Краснодар). Запись ведётся по ходу работы.

## 2026-10-27T05:40+03:00 — M0, старт

Цель: сохранить промпт, убедиться в праве на запись и понять среду.

- Прочитан `01_NIGHT_RUN_PROMPT.md` (1161 строка). Сохранён дословно в `docs/prompts/NIGHT_RUN_PROMPT.md`, коммит `2ff0e61` — первый коммит по правилу 6.1.
- Репозиторий практически пуст: один `README.md`. Опись — `docs/INVENTORY.md`.
- `dotnet --info` → «command not found». `curl` к `dot.net`, `api.nuget.org`, `dotnetcli.azureedge.net`, `ci.dot.net`, `raw.githubusercontent.com`, release-ассетам GitHub → код `000`.
- Доступные хосты: `github.com`, `codeload.github.com`, `api.github.com`, `registry.npmjs.org`, `pypi.org`, `files.pythonhosted.org`.
- Дизайн: варианты (1) писать код без сборки; (2) искать SDK в доступных реестрах; (3) собрать компилятор из исходников. Выбор (2): npm-пакет `dotnet-sdk-3.1-linux-x64@3.1.1007` содержит полный SDK 3.1.102 (108 МБ tgz). Риск: старый runtime несовместим с Debian 12 — снят сборкой OpenSSL 1.1.1w из GitHub-исходников и `CLR_ICU_VERSION_OVERRIDE=72`.
- Проверка: scratch-проект собирается с 0 предупреждениями, `ru-RU` даёт запятую, `en-US` — точку.
- `git push origin arena/649405ec-odd-master` успешен.

## 2026-10-27T05:55+03:00 — M0, каркас решения

Цель: «одна команда» для сборки и тестов.

- Варианты тестов без NuGet: (1) вообще не писать тесты; (2) свой микро-раннер + цель `VSTest`. Выбор (2) — ADR-0014: `dotnet test OddMaster.sln` реально запускает тесты и возвращает ненулевой код при падении.
- Созданы `OddMaster.sln` (5 проектов), `global.json`, `Directory.Build.props`, `Directory.Build.targets`, `NuGet.Config` с `<clear/>`, `.editorconfig`, `.gitattributes`, `.gitignore`, `scripts/bootstrap-dotnet.sh`, `scripts/dotnet-env.sh`, `scripts/verify.sh`, `.github/workflows/ci.yml`.
- Проверка: `dotnet build OddMaster.sln -warnaserror` → 0 ошибок / 0 предупреждений; `dotnet test OddMaster.sln -v:n` → 3 smoke-теста пройдены.
- Осталось: наполнить ядро, конфиги, документы.

## 2026-10-27T06:30+03:00 — M1, нормативные данные и конфиг

Цель: вся нормативка в данных, ядро не знает чисел ГОСТ; пробелы видны.

- Дизайн: System.Text.Json +разбор вручную JsonElement (в 3.1 нет атрибутов JsonNumberHandling), собственный валидатор с русскими сообщениями и JSON Pointer. Вариант с DTO-атрибутами отклонён: хуже диагностика.
- Данные раздела 8.4 перенесены дословно в `config/profiles/`, `config/norms/`; добавлены `sources.json`, POC-каталог на 6 знаков, `controls.json`, `drawing.json`, пустая боевая карта. 9 JSON Schema (draft-07).
- Ядро: `SignNumber`, `SignNumberSet`, `SignNumberRange`, `NormRef`, `Verification`, `Finding`, `VerificationSummary`, загрузчики, `SizeResolver`, `ConfigValidator` (несостыковки 1–4 автоматически), реестр GOST-NNN.
- 16 ADR, `NORMATIVE_SOURCES.md`, `VERIFICATION_QUEUE.md` (21 маркер), `SDK_ASSUMPTIONS.md` (8 маркеров).
- Проверка: `dotnet build -warnaserror` — 0/0; `dotnet test -v:n` — 27 тестов, 0 упало. Тесты culture ru-RU/en-US, кратность decimal, разрешение POC-знаков в Д.2/Д.3/Д.4/Д.5/Д.8.
- Ошибки по ходу: CS1929 (`IReadOnlyList.Contains` с компаратором) → `Any`; CS8606 на `MatchCollection` → `.Cast<Match>()`; «висячий» незарегистрированный GOST-маркер в QUESTIONS → заменён на GOST-015. Записано в ERRORS.md.
- Осталось: M2–M10.

## 2026-10-27T07:10+03:00 — M2, генератор носителей

Цель: знаки строятся из параметрического описания, а не рисуются руками.

- Дизайн: `Path` из `LineSegment`/`ArcSegment`; дуги настоящие (SVG `A`), скругление вершин через биссектрису и тангенс половинного угла; для поля знака повторно строим тот же примитив с габаритом, уменьшенным на 2×ширину каймы — так кайма получается ровно по данным без смещения контуров общего вида.
- Вариант с полилинией вместо дуг отклонён: промпт требует дуги и это ухудшило бы точность габарита окружности.
- Заглушки внутренней каймы, высоты надписей и радиуса пиктограммы вынесены в `config/conventions/drawing.json` (Placeholder), в коде нет нормативных чисел.
- `SignGenerator` возвращает `GenerationResult`: геометрия + находки + `VerificationSummary`. Исключения — только для ошибок программиста.
- Проверка: 40 тестов зелёные. Проверены все POC-знаки во всех типоразмерах, габариты 0,01 мм, кайма 10/20, 2.5/I, скорость 45, индивидуальное проектирование, зеркальность треугольника, детерминизм, архитектурный тест.
- Ошибки: CS8629 на `Nullable<double>.Value` → локальная переменная; тест зеркальности сначала сравнивал точки в порядке обхода (зеркало меняет направление) → сравнение как множество.

## 2026-10-27T07:45+03:00 — M3, SVG-рендер и галерея

Цель: знаки видны в PR без запуска кода; вывод детерминирован.

- Дизайн: `SvgPathWriter` превращает `Path` в `M/L/A/Z`; группа `scale(1,-1)` переворачивает ось Y для геометрии, надписи рисуются отдельно (иначе шрифт зеркалится). Semantic color → RGB только в рендере.
- Галерея: все POC-знаки во всех типоразмерах таблиц (24 SVG) + `docs/demo/signs/README.md` с таблицей статусов. Golden-тесты на 3 файла.
- Красная команда №1: 10 находок, 6 исправлено, 4 принято осознанно (`docs/RED_TEAM.md`). Ключевое: golden-файл больше не создаётся молча; схемы теперь проверяют `required` у конфигов.
- Проверка: `dotnet test` — 49 тестов (39 + 10), 0 упало; `dotnet build -warnaserror` — 0/0. Два прогона рендера дают одинаковые байты, SVG разбирается как XML, культуры ru-RU/en-US не влияют.

## 2026-10-27T08:40+03:00 — M4/M5, ось, дислокация и ведомость

Цель: ось можно двигать, а журнал координат и ведомость пересчитываются в ядре.

- `Station`: ПК = 100 м, округление до см переносит разряд (1299,996 → ПК13+00,00), формат настраивается (пробел/разделитель/знаки). Разбор с плохим форматом → FormatException.
- `Alignment`: полилиния, `PointAt`/`Project`, правая нормаль `(dy,-dx)` = «+» (ADR-0015), `Outside` за концами. `Shift`/`Rotate`/`WithInitialStation`/`WithVertices` — реактор в ядре.
- Дизайн-решение: вместо отдельного класса `Placement` как сущности САПР сделал строку журнала `Placement` в ядре; адаптер позже будет создавать из неё примитивы.
- `CoordinateLog`: CSV (BOM, «;», запятая в дроби, CRLF) + Markdown + детерминированный diff до/после.
- `TsoddStatement`: группировка по номер+типоразмер+параметр (ADR-0011), натуральная сортировка, перечень по опорам, табличная модель для будущей таблицы чертежа. Форма колонок — черновик, GOST-011.
- Проверка: 73 теста зелёные (63 Core + 10 Render). 200 случайных точек с фиксированным seed подтвердили обратимость проекции.
- Ошибки: сначала считал ПК километром (1299,996 → ПК1+300), исправил на пикет 100 м; тест случайной проекции требовал равенства пикета возле вершин ломаной — заменён на обратимость координаты; `File.ReadAllText` съедал BOM у golden CSV — чтение через `ReadAllBytes` + нормализация переводов строк.

## 2026-10-27T09:30+03:00 — M6/M7, аудитор и актуализатор

Цель: на «чужой» схеме видны нормативные проблемы; движок замен работает без выдуманной боевой карты.

- M6: `SchemeAuditor` читает R001–R007 из `config/rules/controls.json` (включённость, важность, допуск, коридор — всё из данных). CSV-фикстура на 12 записей ловит дубли, табличку без знака, «висячее» начало зоны, выход из коридора, непредусмотренный типоразмер, скорость 45, пробелы каталога. `AuditReport` сортирует находки, ставит баннер «Часть правил не проверена — ЧЕРНОВИК» и метку «[не проверено по ГОСТ]».
- M7: `ActualizationEngine` = распознаватель по шаблонам JSON + карта замен + каталог 2024. Шесть категорий, конфликт → NeedsDecision, баннер «Карта замен не сверена». Боевая карта `config/actualizer/map-2004-2024.json` осталась пустой; ДЕМО-карта в `tests/fixtures/actualizer/` явно помечена синтетической.
- Дизайн: вместо «придумать замены 2004→2024» оставил движок без данных — это безопаснее и честнее (ADR-0010).
- Красная команда №2: 8 находок, 5 исправлено. Ключевые: `NormRef.ToString()` печатал имя типа в отчёте; интерполяция `decimal` в сообщениях была культурно-зависимой.
- Проверка: 93 теста зелёные (83 Core + 10 Render). Golden: ведомость, отчёт аудитора, отчёт актуализатора, 3 SVG. PDF не делался: нет pandoc/wkhtmltopdf, по условию достаточно MD + CSV.

## 2026-10-27T10:15+03:00 — M8, сквозное демо

Цель: одна команда `bash scripts/verify.sh` даёт все артефакты и проверяет детерминизм.

- `SvgSchemeRenderer`: ось полилинией, пикетные отметки, опоры, мини-знаки (8 мм на бумаге × 1:1000), выносные линии, подписи, баннер «ЧЕРНОВИК/ДЕМО». Все соглашения в `config/conventions/drawing.json`.
- `DemoRunner` больше не имеет заглушек: `signs`, `scheme`, `check`, `actualize`, `all`. Схема читается из `tests/fixtures/demo/scheme.json`, а не зашита в код.
- Артефакты: `scheme.svg`, `statement.{csv,md}`, `coordinates.{csv,md}`, `reactor_before/after/diff`, `audit_report.*`, `actualization_report.*`, `config_validation.md`, `README.md` + 20 SVG галереи.
- `scripts/verify.sh` (restore → build -warnaserror → test --no-build → demo all → `git diff --exit-code -- docs/demo`) выполнен успешно: 0 ошибок, 0 предупреждений, 102 теста, детерминизм подтверждён. CI делает то же самое.
- Проверка отдельно: `dotnet test --no-build -v:n` действительно запускает оба тестовых проекта (83 + 19), а не «тихо проходит».

## 2026-10-27T10:50+03:00 — M9, изоляция адаптера

Цель: ни одного выдуманного API nanoCAD в ядре и в зелёной сборке.

- Добавлен порт `OddMaster.Core.Ports.IDrawingPort` и `GeometryPortWriter`: ядро отдаёт отрезки, дуги, заливки и текст, ничего не зная о САПР.
- `src/OddMaster.NanoCAD/` — отдельный проект в `OddMaster.NanoCAD.sln`, не входит в основной solution и CI. Восемь предположений SDK-001…SDK-008 зарегистрированы; у каждого вызова стоит `// [ПРОВЕРИТЬ В SDK: SDK-NNN]`.
- Адаптер честно бросает `NotImplementedException` в методах порта: лучше явная заглушка, чем выдуманный вызов API. Комментарии и `BUILD_NANOCAD.md` объясняют, что компиляция не выполнялась.
- Тесты M9-A01…A04: адаптер не в основном solution, нет DLL/EXE/PDF, маркеры в коде, порт отдаёт примитивы с учётом масштаба.
- Проверка: 106 тестов зелёные (87 Core + 19 Render).
- Попытка собрать с `-p:OddTargetFramework=net8.0` на SDK 3.1 закономерно упала (MSB3644): переключение на .NET 8 требует .NET 8 SDK; в песочнице его нет. Записано как ограничение, а не как успех.

## 2026-10-27T11:40+03:00 — M10, финализация

Цель: документы, честный отчёт, Draft PR, зелёный последний коммит.

- Написаны `ARCHITECTURE.md` (Mermaid-диаграммы слоёв и потока данных), `DEPENDENCIES.md`, `AI_USAGE_LOG.md`, `EXPLAIN_FOR_AUTHOR.md` (модули простыми словами, 5 алгоритмов, 20 вопросов жюри, шаблон ответа про ИИ), финальный `NIGHT_REPORT.md` по шаблону раздела 12.
- Обновлены README, CONTRIBUTING, INVENTORY (финальная опись), QUESTIONS (Q-011…Q-015), ERRORS (E-001…E-020), RED_TEAM (проход №3), NIGHT_PLAN (M0–M10 закрыты).
- Исправлены неточности документации: шаблон знака `signTemplate`, wording про .NET 8, отсутствие CJK-символов и незаполненных заглушек проверено скриптом.
- Чистый клон: `git clean -xdf` → `bash scripts/verify.sh` → 0/0, 106 тестов, `git diff --exit-code -- docs/demo` пуст.
- Создан Draft PR из ветки `arena/649405ec-odd-master`.

- Финальная проверка после `git clean -xdf`: `bash scripts/verify.sh` → 0/0, 106 тестов, `git status --short` пуст. Draft PR: https://github.com/Igorr112323/ODD-MASTER/pull/1

---

## 2026-10-28 · Сессия 02, P1 (часть 1): тулчейн и тесты

- Старт по протоколу §13: промт программы сохранён дословно в `docs/prompts/PRODUCT_PROGRAM_PROMPT.md`; созданы `docs/PRODUCT_STATUS.md` и `docs/sessions/SESSION-02-PLAN.md`.
- CR-01: `gh api repos/... --jq .visibility` → **public**. Это первая строка отчёта. Self-hosted workflow не создаётся; вместо него шаблон в `docs/runner/` (готовится).
- Гейты G1–G5 закрыты: нет `docs/feedback/`, `samples/legacy/`, `data/diff-2004-2024.csv`, `docs/contest/`.
- CR-02: `net8.0`, `LangVersion latest`, `global.json` 8.0.100/latestFeature; удалены bootstrap-скрипты, `dotnet-env.sh`, `Directory.Build.targets`, ICU/EOL-костыли; CI на `actions/setup-dotnet@v4`.
- CR-03: 106 тестов перенесены 1:1 на xUnit (`[Fact(DisplayName=...)]`), `TestAssert` — тонкая обёртка над `Xunit.Assert`; пакеты Microsoft.NET.Test.Sdk/xunit/xunit.runner.visualstudio/coverlet.collector; `NuGet.Config` с nuget.org; покрытие Cobertura.
- CR-15: страховочный проект `src/OddMaster.Core.NetStandard` (netstandard2.0 + System.Text.Json 8.0.5); из ядра убраны `HashCode.Combine` и `Replace(...,StringComparison)`.
- ADR-0017…0020; обновлены README, DEPENDENCIES, QUESTIONS (ответы куратора Q-001…Q-015), ERRORS (E-021…E-025).
- Локальная проверка типов: `/tmp/oracle` (SDK 3.1 **вне репозитория**) компилирует Core+Render+Demo и все 106 тестов с xUnit-shim — 0 ошибок. Это не заменяет зелёный CI.

## 2026-10-28 · Сессия 02, P1 (часть 2): визуальные дефекты, аудитор, Актуализатор

- CI-оракул заработал: первая ошибка xUnit2020 (`Assert.True(false, msg)`) исправлена на `Assert.Fail(msg)`; второй прогон дал Linux **success** и покрытие Core **80.9 %** (2339/2893 строк) при 106 тестах. Windows-job упал — добавлена диагностика и убрана зависимость тестов от `/` в путях (`IsIgnored`, нормализация `Repo.ReadText`).
- CR-04: цвета только из каталога (`field/outerBorder/innerBorder/symbol/text`), в `Generation/*` нет цветовых литералов; красная внутренняя кайма рисуется только у 2.4; `OuterBorderUnverified` + GOST-022.
- CR-05/06: 3.25 — отдельная заглушка `diagonalBand` (3 тонкие линии) + GOST-023; у 2.4 пиктограммы нет.
- CR-07: `config/typography/fonts.json` + `FontMetrics` + `TextEngine`; кегль/ширина/базовая линия вычисляются, SVG больше не использует `text-anchor`/`dominant-baseline`.
- CR-08: водяной знак в две строки, раскладка `Watermark()` доступна тестам; габарит внутри знака.
- CR-09: `Path.LineTo/ArcTo` пропускают вырожденные сегменты.
- CR-10: `docs/visual/REVIEW-01.md`, job `previews` (rsvg-convert + montage), 8 новых инвариантов M9-V01…V08.
- CR-11: фикстура Актуализатора — 12 элементов, ≥2 в каждой из 6 категорий; в `NIGHT_REPORT.md` исправлена завышенная оценка прогона №1.
- CR-12: аудитор без дублей (R005/R006), пометка «не проверено по ГОСТ» только у Unverified/Placeholder, ID в сообщении согласованы с Location; тесты M6-C11/C12.
- ADR-0021 (изображение для схемы) и ADR-0022 (цвета и текст).
- Локальный оракул: все C#-файлы компилируются (Core/Render/Demo/117 тестов с xUnit-shim), демо-артефакты и 12 golden-файлов перегенерированы через `/tmp/oracle` (вне репозитория).

## 2026-10-28 · Сессия 02, P2: каталог MVP-30 и конвейер оцифровки

- Каталог: **30 знаков MVP + 11 табличек 8.x** (§7 программы). Имена — CuratorExtracted из PDF-копии, статус `StudentVerified` не ставился. Размеры не дублируются: берутся из Д.2–Д.9. Галерея — 111 SVG.
- Поля `mirrorOf` (3 пары: 3.18.2, 5.7.2, 5.19.2) и `composedOf` (5.41/5.42 → 3.27). Асимметричная стрелка-заглушка + `GeometryPrimitives.MirrorPath` позволяют проверять зеркальность геометрией, а не только данными.
- `config/pictograms/placeholders.json` + `config/schema/pictogram.schema.json` + `PictogramRegistry` (ADR-0023): формат в клетках масштабной сетки прил. Б, 31 заглушка, у каждой есть статус.
- `tools/OddMaster.Tools` (`odd-tools`): `pictogram import` (Дуглас–Пекер, привязка цветов, StudentTranscribed), `pictogram overlay`, `nomenclature import`, `selftest` на синтетической стрелке. Инструкция — `docs/howto/PICTOGRAMS.md`.
- `config/catalog/nomenclature-2024.csv` (41 строка) и `data/diff-2004-2024.template.csv` (шаблон Q-008/G4).
- Тесты `CatalogMvpTests` M10-P01…P11 (11 новых), итого 128 тестов. CI добавлен шаг `odd-tools selftest`.
- `docs/visual/REVIEW-02.md`: что видно на 111 картинках и что проверяет студент.

## 2026-10-28 · Сессия 02, P3 (частично) и финализация

- `SchemeDocument` (schemaVersion=1, устойчивые ID оси/стоек/знаков/зон, JSON DTO) — будущая начинка McCustomBase, механизм хранения [ПРОВЕРИТЬ В SDK].
- `LiveStatement`: инкрементальный пересчёт по событиям «добавлен/изменён/удалён», дельта строк; перф-тест 1000 стоек → одно изменение < 50 мс.
- `LegendModel`, `SheetModel`, `ZoneRuleSet` (`config/rules/zones.json`, 8.2.1–8.2.6, статус Unverified), пустой реестр подсказок `hints-52289.json`, `DiffMapImporter` (CSV → MigrationMap, ADR-0024).
- Демо-команда `kernel`: 13 артефактов (`kernel_document.json`, statement/delta, журнал до/после сдвига оси на 50 м, легенда, лист, зоны, Актуализатор на 12 элементах, импорт различий).
- Тесты `DocumentModelTests` M11-D01…D09. Итого **153 теста** (плюс 16 тестов покрытия новых модулей M12-X01…X16 для цели CR-03 ≥80 %).
- Windows-параллелизм: тесты, читающие/пишущие `docs/demo`, объединены в коллекцию `Demo artifacts` с `DisableParallelization` (E-032).
- Пакет передачи студенту: `docs/runner/RUNNER_SETUP_NANOCAD.md` (одна страница), шаблон `nanocad-build.yml` **не активирован** (репозиторий PUBLIC, CR-01), `samples/legacy/README.md` честно говорит, что G4 закрыт.
- `docs/PRODUCT_STATUS.md` обновлён: L1 доказан для MVP-1/2, 🟡 для MVP-3…8, L2–L4 нигде.

## 2026-10-28 · Финализация сессии 02

- CI: Linux/Windows/netstandard/previews — все pass (run 40720411817); `gh pr checks 1` зелёный. 153 теста, покрытие Core **91.3 %** (commit status `coverage/core`).
- Причина прежних 70 % — отсутствие инструментации Core коллектором; закрыто `coverage.runsettings` (E-036). Сводка покрытия исправлена (E-035).
- `docs/reports/SESSION-02.md` — отчёт по §14; `docs/PRODUCT_STATUS.md`, `AI_USAGE_LOG.md`, `EXPLAIN_FOR_AUTHOR.md`, `QUESTIONS.md`, `INVENTORY.md`, `ERRORS.md` обновлены.
- Пакет передачи студенту: `docs/runner/RUNNER_SETUP_NANOCAD.md` + шаблон workflow (не активен, PUBLIC) + `samples/legacy/README.md`.
