# Зависимости

## NuGet-пакеты (CR-03, ADR-0018)

`NuGet.Config` содержит `<clear/>` и официальный источник nuget.org. Ядро по-прежнему не имеет внешних зависимостей.

| Пакет | Версия | Лицензия | Где | Зачем |
|---|---|---|---|---|
| Microsoft.NET.Test.Sdk | 17.11.1 | MIT | оба тестовых проекта | `dotnet test`, VSTest |
| xunit | 2.9.2 | Apache-2.0 | оба тестовых проекта | `[Fact]`, `Xunit.Assert` |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 | оба тестовых проекта | обнаружение тестов |
| coverlet.collector | 6.0.2 | MIT | оба тестовых проекта | покрытие, Cobertura |
| System.Text.Json | 8.0.5 | MIT | `src/OddMaster.Core.NetStandard` |страховочная сборка под netstandard2.0 (CR-15) |

Лицензии проверены по метаданным NuGet/GitHub; JsonSchema.Net по желанию (Q-002) — не добавлялся, лицензия не проверялась. BCL (`System.Xml.Linq`, `System.Security.Cryptography`, `System.Text.Json` in-box для net8.0) поставляется с официальным .NET 8 SDK (MIT).

## Инструменты (не в репозитории)

| Инструмент | Версия | Зачем | Где берётся |
|---|---|---|---|
| .NET SDK | 8.0.x | сборка, тесты, демо | `actions/setup-dotnet@v4` в CI; обычный установщик у студента. `global.json` = 8.0.100 / latestFeature |
| librsvg2-bin, ImageMagick | системные | превью SVG→PNG и контактный лист | `apt-get` в job `previews` |
| Python 3 | системный | сводка покрытия, инструменты оцифровки | образ CI |
| nanoCAD 26 + MultiCAD .NET SDK | — | сборка адаптера | только у студента (G1); DLL **не** в git |

`LICENSE` в репозиторий не добавлялся по условию задания. Резервные каналы SDK, самосборный OpenSSL и ICU-костыли **удалены** (CR-02).

## Совместимость

- `OddMaster.sln`: `net8.0` (`Directory.Build.props` → `OddTargetFramework`).
- `src/OddMaster.Core.NetStandard`: `netstandard2.0` — проверка в CI, не входит в основной solution.
- `OddMaster.NanoCAD.sln`: `net8.0-windows`, собирается только на раннере студента после G1.
