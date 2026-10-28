#!/usr/bin/env bash
# Канал обратной связи «CI как оракул»: логи GitHub Actions в песочнице недоступны.
# На успехе — компактный commit status с покрытием (читается через API, не спамит PR).
# На ошибке — комментарий в PR с хвостами логов.
set -uo pipefail
PR="${1:-}"
STATUS="${2:-unknown}"
RATE=""
if [ -f coverage-summary.md ]; then
  RATE="$(grep -oE '\*\*[0-9]+\.[0-9]+ %\*\*' coverage-summary.md | head -1 | tr -d '*')"
fi
if [ "$STATUS" = "success" ]; then
  DESC="Core coverage ${RATE:-n/a}; CI success"
  gh api "repos/${GITHUB_REPOSITORY}/statuses/${GITHUB_SHA}" \
    -f state=success -f context="coverage/core" -f description="${DESC:0:140}" \
    -f target_url="${GITHUB_SERVER_URL}/${GITHUB_REPOSITORY}/actions/runs/${GITHUB_RUN_ID}" >/dev/null 2>&1 \
    || echo "commit status не записан (нет права statuses:write)"
  exit 0
fi
if [ -z "$PR" ]; then
  PR="$(gh pr list --head "${GITHUB_REF_NAME:-}" --json number --jq '.[0].number' 2>/dev/null || true)"
fi
[ -z "$PR" ] && { echo "PR не найден — комментарий не публикуется"; exit 0; }
{
  echo "### CI: ${STATUS} · $(date -u +%Y-%m-%dT%H:%M:%SZ) · ${GITHUB_SHA:0:7}"
  echo
  if [ -f dotnet-version.txt ]; then echo "SDK: \`$(cat dotnet-version.txt)\`"; fi
  if [ -n "$RATE" ]; then echo "Покрытие Core: **$RATE**"; fi
  if [ -f test.log ]; then
    if grep -qE '^\s*Failed ' test.log; then
      echo "#### Упавшие тесты"; echo '```'; grep -E '^\s*Failed |^\s*Error Message:|^\s{3}[^ ]' test.log | head -120; echo '```'
    fi
    echo "<details><summary>Тесты (последние 60 строк)</summary>"; echo; echo '```'; tail -60 test.log; echo '```'; echo; echo "</details>"
  fi
  echo "#### Логи ошибок"
  for f in build.log test.log tools.log netstandard.log; do
    if [ -f "$f" ]; then echo "**$f**"; echo '```'; tail -60 "$f"; echo '```'; fi
  done
} > /tmp/ci-report.md
gh pr comment "$PR" --body-file /tmp/ci-report.md || echo "Не удалось опубликовать комментарий"
