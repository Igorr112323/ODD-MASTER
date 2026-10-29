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
| E-029 | После расширения каталога 3.4 перестал генерироваться: «не задан параметр mass» | Демо-значения параметров были рассчитаны только на speed/distance | `DemoRunner.DefaultParameters` и тесты используют карту произвольных демо-значений (mass=8, timeMinutes=30) — это не нормативка | `InvariantCheck` локально + `odd-tools selftest` |
| E-030 | `M9-V01` падал на 2.4/I: слой выходил за габарит | Вершины равностороннего треугольника дают габарит, смещённый на r/4 относительно центра | `GeometryPrimitives.Triangle` сдвигает вершины, чтобы габарит был центрирован; номинальный BoundingBox совпадает | Локальный `InvariantCheck` на 111 геометриях |
| E-031 | xUnit-анализатор CS8606 на `foreach (Match m in Regex.Matches(...))` | `MatchCollection` — не-generic `IEnumerable` | `.Cast<Match>()` во всех импортёрах | Локальная проверка типов + CI |
| E-032 | Windows: `IOException` при чтении SVG галереи; Linux: «Root element is missing» | xUnit выполнял классы параллельно: `DemoArtifactsTests.M8-S03` перегенерировал `docs/demo`, пока `SvgTests.M3-S01` читал те же файлы | Оба класса (и `VisualInvariantTests`) в коллекции `Demo artifacts` с `DisableParallelization = true` | job `windows-build` + Linux-тесты |
| E-033 | M10-P06 падал на 3.4: «Значение 40 отсутствует в наборе (mass)» | Тест подставлял всем параметрам одно значение 40 | Для каждого знака берётся демо-значение его параметра (mass=8); 6.2.1 без правила — не ошибка | `CatalogMvpTests` |
| E-034 | `M9-A04` падал с `Assert.InRange() Failure` | Тест сравнивал габарит скруглённого контура с номинальным габаритом острых вершин | Сравнение с `outline.Bounds()` после TransformPath | Локальный `InvariantCheck` |
| E-035 | Сводка покрытия показывала 70 % и «0/92» по файлам, которые точно вызывались тестами | Скрипт суммировал два Cobertura-файла по имени класса; разные пути давали дубли и нули | `coverage_summary.py` объединяет по «путь внутри OddMaster.Core → номер строки → max(hits)» | commit status `coverage/core` в CI |
| E-036 | Покрытие оставалось 70 % после явного добавления 16 тестов | `coverlet.collector` без runsettings не инструментировал сборку Core в одном из тестовых проектов | `coverage.runsettings` с `<Include>[OddMaster.Core]*</Include>`, `--settings` в CI и verify.sh → **91.3 %** | commit status `coverage/core`, артефакт `coverage-and-logs` |
| E-037 | Комментарии CI в PR перестали публиковаться | Лимит/частота комментариев при «CI как оракул» | Успех пишется в commit status `coverage/core` (читается через API), комментарий в PR — только при падении | `scripts/ci-report.sh` |

## Сессия 03 (CURATOR-03 и F0 «LINUX-ПОЛИГОН»)

| ID | Симптом | Причина | Исправление | Предотвращение |
|---|---|---|---|---|
| E-073 | job «Страховка netstandard2.0» зелёный, а сборка Core на net8.0 падает | Страховочный проект компилирует подмножество файлов и не видит проблем зависимостей (`SignLayer` не найден) | Смотреть Linux-job, а не только страховку; ошибку ловит `dotnet build OddMaster.sln -warnaserror` | В отчётах явно указывать, какой job что доказывает |
| E-074 | `elements.json`: «отсутствует обязательное поле verification» | Элементы состава наследуют `libraryVerification` библиотеки | `ConfigLoader.Elements` подставляет `libraryVerification`, если у элемента нет своего статуса | Не дублировать `verification` у элемента без причины |
| E-075 | Состав не собирался: не хватало `outerWidth` | `BuildLayers` требует внешнюю и внутреннюю ширину каймы явно | Параметры передаются из `SignGenerator` | Проверка на локальном оракуле до пуша |
| E-076 | `POST /actions/runs/<id>/approve` → 403 «Resource not accessible by integration» | У интеграции агента нет права подтверждать прогоны | Ничего: push-прогоны выполняются сами | В отчёте — просьба владельцу нажать «Approve and run» |
| E-077 | `gh workflow run regenerate.yml` → 404, файл в ветке есть | `workflow_dispatch` доступен, только если workflow есть в ветке по умолчанию (`main`) | Запуск меткой `[regen]` в сообщении коммита + `push`-триггер | Описано в ADR-0030 |
| E-078 | Регенерация не обновляла несовпавшие эталоны | `Golden.CheckText` учитывал `ODD_UPDATE_GOLDEN` только при создании файла | Флаг теперь перезаписывает эталон и при расхождении (видно в диффе) | Страж эталонов (F0) + запись в REVIEW |
| E-079 | Выжимка упавших тестов не появлялась в PR | В workflow регенерации не было права `pull-requests: write` | Добавлены `pull-requests: write` и `issues: write` | Проверка прав в шапке workflow |
| E-080 | Бот-коммит «регенерация» содержал только `test.log` | `git add -A` коммитил лог тестов, а `*.log` не был в `.gitignore` | `*.log` в `.gitignore`, `test.log` удалён из индекса, перед коммитом логи удаляются | Правило: логи — артефакт прогона, не содержимое репозитория |
| E-081 | Диагональные полосы состава выходили за знак и рисовались клиньями | Ширина полосы откладывалась по Y, а не по нормали к направлению | `Band` строит углы через единичные векторы `u` (вдоль) и `p` (поперёк) | M17-C05 и визуальная проверка контактного листа |
| E-082 | `Flatten` (разбиение контура) ошибался на дугах 90° и 180° | Центр дуги восстанавливался по хорде, знак выбирался эвристикой | Берём `Center`/`StartAngleDeg`/`SweepDeg` у самого `ArcSegment` | Тест M17-C03 на круге |
| E-083 | У обрезанной полосы появился нулевой сегмент | Сазерленд—Ходжмен возвращает первую вершину ещё раз в конце, а `Path.Close()` замыкает сам | Дубль удаляется с допуском 1e-9 | Проверка в M17-C01 |
| E-084 | После пересоздания среды локальная ветка оказалась на `Initial commit` | Песочница переклонировала репозиторий, файлы дерева сохранились как неотслеживаемые | Бэкап дерева в `/tmp`, затем `git reset --hard origin/arena/649405ec-odd-master` | Первым делом сверять `git log --oneline -1` и `git status -sb` |
| E-085 | CI: «Inconsistent accessibility: parameter type ... is less accessible than method» | `internal`-метод принимал приватный вложенный тип | Вложенный класс объекта эмулятора стал `internal` | Собирать `-warnaserror` в CI, а не только локально |
| E-086 | CI: xUnit2013 «Do not use Assert.Equal() to check for collection size» | Проверка `Assert.Equal(1, list.Count)` вместо `Assert.Single` | `Assert.Single`/`Assert.Empty` | Анализаторы xUnit включены в сборку тестов |
| E-087 | M18-R04 и C-EVT-002 падали: исключение вместо проверки | Сценарий ждал `CadMutationRejectedException` от `Create`, а запрет срабатывает раньше — на `BeginTransaction` | `try` охватывает открытие транзакции целиком | Правило «повторный вход» проверяется с первой операции |

