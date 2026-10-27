# ADR-0009: Целевой фреймворк и переключение на .NET 8

- **Статус:** см. «Последствия».
- **Дата:** 2026-10-27.

## Контекст

Промпт требует .NET 8, но в песочнице доступен только .NET Core SDK 3.1.102 (нет dot.net/NuGet).

## Решение

`Directory.Build.props` содержит одну переменную `OddTargetFramework`, по умолчанию `netcoreapp3.1`. `global.json` с `rollForward: latestMajor` разрешает .NET 8+: `dotnet build -p:OddTargetFramework=net8.0`. `CheckEolTargetFramework=false` отключает предупреждение о EOL. Резервный SDK скачивается `scripts/bootstrap-dotnet.sh` и не попадает в репозиторий.

## Альтернативы

Не собирать вообще; собрать на 2.1; выложить DLL SDK в git.

## Последствия

Плюсы: зелёная сборка здесь и сейчас. Минусы: .NET Core 3.1 — EOL; на неделе 1 переключить на .NET 8 и убрать bootstrap. Риск окончания поддержки .NET 8 в 11.2026. Статус: предварительное (Q-001).
