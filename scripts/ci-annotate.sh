#!/usr/bin/env bash
# Краткая диагностика падений CI в комментарии PR.
#
# Зачем: полные логи Actions лежат в хранилище, которое из песочницы недоступно,
# а комментарий PR доступен и агенту, и студенту. Поэтому при падении job выкладывает
# только выжимку: строки с ошибками и хвост логов.
#
# Использование: bash scripts/ci-annotate.sh <номер-PR> <файл-лога> [ещё файлы...]
set -uo pipefail

PR="${1:-}"
shift || true
OUT="$(mktemp)"
{
  echo "### ❌ CI: выжимка логов"
  echo
  echo "Коммит: \`${GITHUB_SHA:-локально}\`, job: \`${GITHUB_JOB:-?}\`, шаг: \`${GITHUB_STEP_SUMMARY:-?}\`"
  echo
  for file in "$@"; do
    [ -f "$file" ] || continue
    echo "<details><summary><code>$file</code></summary>"
    echo
    echo '```'
    grep -nE "error [A-Z]+[0-9]+|error:|Failed|failing|не прошла|❌|Traceback|Unhandled exception|Assert" "$file" | tail -40 || true
    echo '--- хвост ---'
    tail -40 "$file"
    echo '```'
    echo
    echo "</details>"
    echo
  done
} > "$OUT"

cat "$OUT"

if [ -n "${GH_TOKEN:-}" ]; then
  if [ -z "$PR" ]; then
    PR="$(gh pr list --head "${GITHUB_REF_NAME:-}" --json number --jq '.[0].number' 2>/dev/null || true)"
  fi
  if [ -n "$PR" ]; then
    gh pr comment "$PR" --body-file "$OUT" || echo "Не удалось оставить комментарий в PR"
  fi
fi
