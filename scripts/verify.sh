#!/usr/bin/env bash
# Одна команда для проверки каркаса: сборка, тесты с покрытием, демо, детерминизм.
# Требуется официальный .NET 8 SDK (global.json). Никаких резервных каналов.
set -euo pipefail
cd "$(dirname "$0")/.."

echo "== 0/6 dotnet =="
dotnet --version
echo "== 1/6 restore =="
dotnet restore OddMaster.sln
echo "== 2/6 build -warnaserror =="
dotnet build OddMaster.sln --no-restore -warnaserror
echo "== 3/6 test + покрытие =="
dotnet test OddMaster.sln --no-build --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory ./coverage
echo "== 4/6 demo all =="
dotnet run --project src/OddMaster.Demo --no-build -- all
echo "== 5/6 детерминизм =="
if ! git diff --exit-code -- docs/demo; then
  echo "ОШИБКА: docs/demo изменился после повторной генерации — нарушен детерминизм."
  exit 1
fi
echo "== 6/6 покрытие Core =="
python3 scripts/coverage_summary.py coverage
echo "ВСЁ ЗЕЛЁНОЕ: 0 ошибок, 0 предупреждений, артефакты воспроизводимы."
