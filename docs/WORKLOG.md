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
