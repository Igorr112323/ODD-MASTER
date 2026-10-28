# ADR-0017: Только официальный .NET 8 SDK

- **Статус:** принято (сессия 02, 28.10.2026).
- **Дата:** 2026-10-28.

## Контекст

CR-02: netcoreapp3.1 (EOL 13.12.2022), LangVersion 8.0, CheckEolTargetFramework=false и bootstrap SDK из npm + самосборный OpenSSL — риск цепочки поставок.

## Решение

TFM `net8.0` (переменная `OddTargetFramework`), `LangVersion latest`, `global.json` = 8.0.100 с `rollForward: latestFeature`. Удалены `scripts/bootstrap-dotnet.sh`, `scripts/dotnet-env.sh`, `CLR_ICU_VERSION_OVERRIDE`, `CheckEolTargetFramework`. CI использует `actions/setup-dotnet@v4` (8.0.x).

## Альтернативы

Оставить резервный канал (запрещено §11.2); собрать .NET 8 из исходников.

## Последствия

Плюсы: официальная цепочка поставок, актуальный рантайм, нет костылей. Минусы: в песочнице .NET 8 недоступен → режим «CI как оракул». Заменяет ADR-0009/ADR-0014 в части тулчейна. Статус: принято.
