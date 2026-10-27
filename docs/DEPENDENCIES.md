# Зависимости

## NuGet-пакеты

**Внешних NuGet-пакетов нет.** `NuGet.Config` очищает источники (`<clear/>`), `Directory.Build.props` включает `TreatWarningsAsErrors`. Причина: `api.nuget.org` в среде агента недоступен, а вендорить nupkg в репозиторий запрещено условиями задания. Решение зафиксировано в ADR-0014.

| Компонент | Откуда | Лицензия | Где используется |
|---|---|---|---|
| BCL (`System.Text.Json`, `System.Xml.Linq`, `System.Security.Cryptography`) | .NET SDK, в комплекте | MIT (лицензия .NET) | разбор конфигов, XML-тесты, хэши демо |
| `OddMaster.Core` | этот репозиторий | — | везде |
| `OddMaster.Render.Svg` | этот репозиторий | — | SVG |
| `OddMaster.Demo` | этот репозиторий | — | демо CLI |

Если понадобятся xUnit / coverlet / JsonSchema.Net — это MIT/Apache-2.0/BSD, но добавлять их нужно осознанно: сначала строка здесь, потом ADR, потом `PackageReference`. Покрытие кода сейчас **не измеряется**.

## Инструменты (не в репозитории)

| Инструмент | Версия | Зачем | Где берётся |
|---|---|---|---|
| .NET SDK | 8 (рекомендуется) / 3.1.102 (фактически в песочнице) | сборка, тесты, демо | обычный установщик .NET 8; резервный канал `scripts/bootstrap-dotnet.sh` тянет npm-пакет `dotnet-sdk-3.1-linux-x64@3.1.1007` |
| OpenSSL | 1.1.1w | нужен .NET Core 3.1 на Debian 12+ | собирается из исходников `codeload.github.com/openssl/openssl` в `scripts/bootstrap-dotnet.sh`, кладётся в `$HOME/.cache/oddmaster` |
| ICU | 72 в системе | культуры `ru-RU`/`en-US` | системный; `CLR_ICU_VERSION_OVERRIDE` |
| bash, curl, tar, make, gcc | системные | bootstrap, verify.sh | образ CI `ubuntu-22.04` |
| `jq` | системный | проверка JSON в работе агента | не обязателен для сборки |
| nanoCAD 26 + MultiCAD .NET SDK | — | сборка адаптера | только у студента после регистрации; DLL **не** в git |

`LICENSE` в репозиторий не добавлялся по условию задания.

## Совместимость

- `global.json`: SDK 3.1.102, `rollForward: latestMajor` — более новый SDK подхватывается.
- `Directory.Build.props`: одна переменная `OddTargetFramework` (сейчас `netcoreapp3.1`). На машине с .NET 8 SDK: `dotnet build OddMaster.sln -p:OddTargetFramework=net8.0`. В песочнице это **не проверено** (SDK 3.1 не знает net8.0), см. `docs/ERRORS.md` E-019.
- `netcoreapp3.1` — EOL: это временное решение, на неделе 1 переключить на .NET 8 (ADR-0009).
