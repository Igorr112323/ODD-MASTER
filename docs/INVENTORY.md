# Опись репозитория на момент старта (M0)

Дата: 2026-10-27. Ветка: `arena/649405ec-odd-master` (создана платформой, от `main`, коммит `2af39c8`).

## Что было в репозитории

| Путь | Содержимое | Действие |
|---|---|---|
| `README.md` | 13 байт, единственная строка `# ODD-MASTER` | Перезаписан в M0, исходная строка сохранена как заголовок первого раздела |
| `.git/` | один коммит `2af39c8 Initial commit` | Не тронут |
| `docs/brief/` | отсутствует | Поправки куратора (раздел 3 промпта) применены как есть |
| CI, исходники, конфиги | отсутствуют | Созданы с нуля |

Существующие файлы не удалялись и не переименовывались. Конфликтов с поправками куратора нет: исходного брифа в репозитории не было.

## Что было в среде агента

| Факт | Проверка | Следствие |
|---|---|---|
| `dotnet` отсутствует | `dotnet --info` → `command not found` | SDK получен резервным каналом (см. ниже) |
| `api.nuget.org`, `dot.net`, `ci.dot.net`, `raw.githubusercontent.com`, release-ассеты GitHub недоступны | `curl` → код `000` | xUnit/coverlet/JsonSchema.Net недоступны; принято решение ADR-0014 |
| Доступны `github.com`, `codeload.github.com`, `api.github.com`, `registry.npmjs.org`, `pypi.org`, `files.pythonhosted.org` | `curl` → `200` | См. ниже |
| В `registry.npmjs.org` найден пакет `dotnet-sdk-3.1-linux-x64@3.1.1007` с полным SDK 3.1.102 внутри | `tar tzf` → `package/sdk/3.1.102/MSBuild.dll` | Резервный SDK для локальной сборки, **не попадает в репозиторий** |
| Debian 12: OpenSSL 3 и ICU 72; .NET Core 3.1 требует libssl 1.1 и ICU ≤ 67 | «No usable version of libssl was found» | OpenSSL 1.1.1w собран из исходников `codeload.github.com/openssl/openssl`, `CLR_ICU_VERSION_OVERRIDE=72` |
| ICU работает: `ru-RU` → запятая, `en-US` → точка | scratch-проект, `CultureInfo` | Тесты форматирования при двух культурах возможны |
| Права на push есть | `git push origin arena/649405ec-odd-master` → успех | Коммиты и Draft PR возможны |

## Отклонение от задания

Промпт просил .NET 8. В песочнице доступен только .NET Core SDK 3.1.102. Решение: целевой фреймворк вынесен в одну переменную `OddTargetFramework` (`Directory.Build.props`), по умолчанию `netcoreapp3.1`; `global.json` с `rollForward: latestMajor` позволяет собрать те же исходники на .NET 8 без правки кода. Подробности — ADR-0009, вопрос Q-001.

---

## Финальная опись (M10, 2026-10-27)

Исходно в репозитории был один файл `README.md` на 13 байт и один коммит `2af39c8`. Ничего не удалялось и не переименовывалось, кроме перезаписи этого README (первая строка `# ODD-MASTER` сохранена как заголовок).

| Категория | Количество | Где |
|---|---|---|
| Коммитов | 11 | `git log --oneline` |
| Проектов в основном solution | 5 | `OddMaster.sln` |
| Проектов в nanoCAD solution | 2 | `OddMaster.NanoCAD.sln` |
| C# файлов / строк | 77 / ~7 960 | `src/`, `tests/` (без obj/bin) |
| Конфигурационных JSON | 10 + 9 схем | `config/` |
| Фикстур | 4 | `tests/fixtures/{checks,actualizer,demo}` |
| Тестов | 106 | `tests/*/…Tests.cs`, `OddFact` |
| Golden-файлов | 12 | `tests/*/Approved/` |
| ADR | 16 | `docs/adr/` |
| Документов `.md` | 37 | `README.md`, `CONTRIBUTING.md`, `docs/` |
| Демо-артефактов | 14 + 20 SVG | `docs/demo/` |
| GOST-NNN / SDK-NNN | 21 / 8 | `VERIFICATION_QUEUE.md`, `SDK_ASSUMPTIONS.md` |

### Что осталось вне репозитория (осознанно)

- Резервный .NET SDK 3.1.102, OpenSSL 1.1.1w, ICU — скачиваются `bootstrap-dotnet.sh` в `$HOME/.cache/oddmaster`, в git не попадают.
- DLL MultiCAD .NET SDK — по условию задания не добавляются; ссылки в csproj через `$(NanoCadSdkPath)`.
- PDF ГОСТ, сканы, персональные данные, секреты, LICENSE, большие бинарники — отсутствуют (тест M9-A02).
- Сгенерированные `bin/`, `obj/`, `TestResults/` — в `.gitignore`.

### Как проверить опись

```bash
bash scripts/verify.sh
git status --short          # пусто после коммита
find . -name '*.dll' -not -path './.git/*' | grep -v '/obj/' | grep -v '/bin/'   # пусто
```
