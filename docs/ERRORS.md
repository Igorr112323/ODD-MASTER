# Журнал ошибок и исправлений

`ID | симптом | корневая причина (5 почему) | исправление | как предотвращено`

| ID | Симптом | Причина | Исправление | Предотвращение |
|---|---|---|---|---|
| E-001 | `dotnet: command not found` | Нет SDK → нет SDK → нет установщика → сеть ограничена → нужен резервный канал → npm-зеркало | Найден npm-пакет с полным SDK 3.1.102, распакован вне репозитория | `scripts/bootstrap-dotnet.sh` повторяет путь; `INVENTORY.md` фиксирует |
| E-002 | `curl` к `dot.net`, `api.nuget.org`, release-ассетам GitHub → `000` | Хосты не в allowlist | Все зависимости убраны; схемы и тесты свои; SDK из доступного npm | `NuGet.Config` с `<clear/>`: restore не требует сети |
| E-003 | `No usable version of libssl was found` при `dotnet build` | .NET Core 3.1 → crypto-шим → ищет `libssl.so.1.1` → Debian 12 даёт OpenSSL 3 → ABI несовместим | Собран OpenSSL 1.1.1w (`no-asm`, shared) из `codeload.github.com`, `LD_LIBRARY_PATH` | `scripts/dotnet-env.sh` + CI-шаг bootstrap |
| E-004 | `dotnet test`: `MSB4057`/пустой `DotNetHostPath`, Exec завершался кодом 127 | `$(DotNetHostPath)` не определён в SDK 3.1, команда начиналась с кавычки | Цель `VSTest` вызывает `dotnet "$(TargetPath)"` через PATH | smoke-тест + проверка `dotnet test -v:n` в `verify.sh` |
| E-005 | .NET Core 3.1 не видит ICU 72 | Жёстко прошитый диапазон версий `libicuuc.so.N` | `CLR_ICU_VERSION_OVERRIDE` вычисляется из `ldconfig -p` | Тесты форматирования под `ru-RU`/`en-US` |
| E-006 | Попытка `gh search code` по бинарникам SDK дала 0 | Официальные репозитории .NET не публикуют бинарники SDK в git | Переключились на npm-зеркало | Опись в `INVENTORY.md` |

Дополняется по ходу M1–M10.

| E-007 | CS1929: `IReadOnlyList<string>` не содержит `Contains` с компаратором | `Enumerable.Contains` требует `IEnumerable<T>`, но перегрузка со сравнением не видна из-за неоднозначности с `MemoryExtensions.Contains` (Span) | Переписал на `SizeClasses.Any(x => string.Equals(...))`, добавил `using System.Linq` | Сборка с `TreatWarningsAsErrors` ловит сразу |
| E-008 | CS8606/CS8602 на `foreach (Match m in Regex.Matches(...))` | `MatchCollection` реализует только не-generic `IEnumerable`, nullable-анализ считает переменную возможно null | `.Cast<Match>().Select(m => m.Value)` | Тест маркеров + warnaserror |
| E-009 | Тест M1-M01 упал: незарегистрированный GOST-маркер в реестре | В `QUESTIONS.md` случайно появился несуществующий маркер | Заменён на GOST-015 (профиль 32945) | `MarkerRegistryTests` сверяет все маркеры из кода/конфигов/доков с очередью |

| E-010 | `Station.Format` давал «ПК1+300,00» для 1299,996 м | Я принял ПК за километр; в дорожной практике ПК = 100 м | Делитель 10000 см вместо 100000, разбор тоже на 100 м; добавлены тесты M4-A01/A02 | Тест на перенос разряда |
| E-011 | M4-A14 падал на 41-й случайной точке | Возле вершины ломаной проекция перескакивает на соседний сегмент, поэтому пикет/смещение не равны исходным | Тест теперь проверяет обратимость координаты `PointAt(Project(p)) ≈ p`, что и требуется в ТЗ | Рандомизированный тест с seed 20261027 |
| E-012 | Golden CSV не совпадал на первой строке при повторном запуске | `File.ReadAllText` убирает BOM UTF-8, а `CsvWriter` его добавляет | `Golden.CheckText` читает байты и декодирует сам, нормализует CRLF→LF при сравнении; CRLF отдельно проверяет M5-S04 | Повторный `dotnet test` без ODD_UPDATE_GOLDEN |

| E-013 | В отчёте аудитора вместо нормы печаталось `OddMaster.Core.Norms.NormRef` | У `NormRef` не был переопределён `ToString()` | Добавил `ToString() => Describe()`, обновил эталоны, добавил проверку в M6-C04 | Golden + тест на отсутствие имени типа |
| E-014 | M6-C09 падал: отчёт различался в ru-RU и en-US | Сообщение `ParameterNorms` интерполировало `decimal` в текущей культуре («2,35» против «2.35») | Все числа в сообщениях форматируются InvariantCulture через `Inv()` | Тест культурной инвариантности |
| E-015 | R003 не находило «висячее» начало зоны | Повтором считался любой знак 3.24 с другим пикетом, в том числе стоящий раньше | Ищем только более поздние пикеты; добавлена фикстура E12 | Тест M6-C02 на все R001–R007 |

| E-016 | `verify.sh` initially падал молча в песочнице | `dotnet-env.sh` не находил dotnet, поэтому `DOTNET_ROOT` указывал на корень репозитория | В песочнице задаю `ODD_DOTNET_DIR`/`ODD_OPENSSL_DIR`; на обычном Linux dotnet уже на PATH, bootstrap соберёт резервный SDK сам | `bash scripts/verify.sh` с этими переменными → зелёно |
| E-017 | Баннер scheme.svg рисовался за пределами viewBox | Y = `-maxY - 12` был выше верхней границы | Y = `-maxY + 15` (внутрь листа) | Тест M8-S02 проверяет наличие баннера и структуры |
| E-018 | Тест M8-S06 не видел BOM у CSV-файла | `File.ReadAllText` убирает BOM при декодировании | Проверяем первые три байта файла напрямую | Повторный прогон тестов |

| E-019 | `dotnet build -p:OddTargetFramework=net8.0` → MSB3644 | SDK 3.1 не знает TFM net8.0 и считает его .NET Framework | Переключение целевого фреймворка требует установленного .NET 8 SDK; в песочнице недоступно. README явно говорит «на машине с .NET 8» | Обычная сборка `netcoreapp3.1` остаётся зелёной; ограничение записано в отчёт |
| E-020 | Адаптер `NanoCadDrawingPort` сначала не реализовывал `DrawFilledPath`, а порт не масштабировал залитые контуры | Порт был добавлен раньше, чем реализация | `GeometryPortWriter.TransformPath` масштабирует весь контур, тест M9-A04 проверяет MinX после масштаба | Тест M9-A04 |

## Сессия 02 (28.10.2026) — долги качества

| ID | Симптом | Причина | Исправление | Правило |
|---|---|---|---|---|
| E-021 | Локальная ветка в начале сессии указывала на `2af39c8`, все файлы были untracked | Платформа сбросила локальную ветку на `main`, PR #1 не смёржен | `git reset --hard origin/arena/649405ec-odd-master` (удалённая ветка цела) | В начале сессии проверять `git log -1` и `git branch -avv`, а не только `git status` |
| E-022 | `gh repo view --json visibility` → «Unknown JSON field» | CLI не поддерживает поле `visibility` | `gh api repos/OWNER/REPO --jq '{private,visibility}'` | Проверять видимость через REST API |
| E-023 | `gh run view --log` / `gh run download` → EOF | Хосты `results-receiver.actions.githubusercontent.com` и `blob.core.windows.net` недоступны в песочнице | `scripts/ci-report.sh` публикует статус, покрытие и хвост логов комментарием в PR | «CI как оракул» (§11.3) требует своего канала диагностики |
| E-024 | Локально нет .NET 8 и NuGet | Сеть ограничена GitHub/npm/PyPI | Локальный оракул: официальный SDK 3.1 из npm **только в /tmp** для проверки типов C# (не коммитится, не в CI) + CI как основной оракул | Разделять «средство проверки типов» и «цепочку поставок продукта» |
| E-025 | `System.Text.Json` недоступен для netstandard2.0 | API отсутствует в BCL netstandard2.0 | Пакет System.Text.Json 8.0.5 в страховочном проекте; `HashCode.Combine` и `Replace(...,StringComparison)` заменены на netstandard2.0-совместимые | Проверять переносимость ядра отдельной сборкой в CI |
| E-026 | CI: `error xUnit2020: Do not use Assert.True(false, message)` | Анализатор xunit.analyzers запрещает такой способ провалить тест | `Assert.Fail(message)` в `TestAssert`; shim локального оракула дополнен | Локальная проверка типов + CI-комментарий |
| E-027 | Windows-job `dotnet test` падал при зелёном Linux | Тесты проверяли пути через `/obj/`, `/bin/`, `/.git/`; на Windows разделитель `\` | `IsIgnored()` нормализует разделитель; `Repo.ReadText` нормализует CRLF; добавлен Windows-диагностический комментарий в PR | job `windows-build` в CI |
| E-028 | Golden-файлы нельзя было обновить локально (нет xUnit) | Тесты генерируют эталоны, а в песочнице нет .NET 8/NuGet | `/tmp/oracle/check/GoldenGen` (вне репозитория) пересобирает 12 эталонов теми же API ядра; эталоны обновляются только с `REVIEW-01.md` | Сверка в CI |
