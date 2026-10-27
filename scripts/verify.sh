#!/usr/bin/env bash
# Одна команда для проверки каркаса: сборка, тесты, демо, проверка детерминизма.
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/dotnet-env.sh

echo "== 1/4 restore =="
dotnet restore OddMaster.sln
echo "== 2/4 build -warnaserror =="
dotnet build OddMaster.sln --no-restore -warnaserror
echo "== 3/4 test =="
dotnet test OddMaster.sln --no-build
echo "== 4/4 demo all + детерминизм =="
dotnet run --project src/OddMaster.Demo --no-build -- all
if ! git diff --exit-code -- docs/demo; then
  echo "ОШИБКА: docs/demo изменился после повторной генерации — нарушен детерминизм."
  exit 1
fi
echo "ВСЁ ЗЕЛЁНОЕ: 0 ошибок, 0 предупреждений, артефакты воспроизводимы."
