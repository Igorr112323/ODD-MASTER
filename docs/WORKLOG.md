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
