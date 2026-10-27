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
