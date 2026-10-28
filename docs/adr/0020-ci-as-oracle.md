# ADR-0020: CI как оракул и матрица сборок

- **Статус:** принято (сессия 02, 28.10.2026).
- **Дата:** 2026-10-28.

## Контекст

В песочнице агента недоступны `api.nuget.org`, `dot.net`, `builds.dotnet.microsoft.com`, а логи GitHub Actions (`results-receiver.actions.githubusercontent.com`) и артефакты (`blob.core.windows.net`) тоже недоступны.

## Решение

CI: `ubuntu-latest` на каждый пуш (build/test/coverage/demo/детерминизм), `windows-latest` только на PR и ручной запуск, отдельные job'ы `netstandard` и `previews`. `concurrency` с `cancel-in-progress`. Скрипт `scripts/ci-report.sh` пишет статус, покрытие и хвост логов комментарием в PR — это канал обратной связи для агента.

## Альтернативы

Одна Ubuntu-джоба без диагностики; читать логи напрямую.

## Последствия

Плюсы: быстрый цикл «пачка правок → push → комментарий → исправление», экономятся Windows-минуты. Минусы: каждый прогон стоит минут, комментарии в PR шумят. Статус: принято.
