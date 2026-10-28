#!/usr/bin/env bash
# Пишет отчёт о прогоне CI комментарием в PR: в песочнице логи GitHub Actions
# (results-receiver.actions.githubusercontent.com) недоступны, поэтому это «оракул».
set -uo pipefail
PR="${1:-}"
STATUS="${2:-unknown}"
if [ -z "$PR" ]; then
  PR="$(gh pr list --head "${GITHUB_REF_NAME:-}" --json number --jq '.[0].number' 2>/dev/null || true)"
fi
[ -z "$PR" ] && { echo "PR не найден — комментарий не публикуется"; exit 0; }
{
  echo "### CI: ${STATUS} · $(date -u +%Y-%m-%dT%H:%M:%SZ) · ${GITHUB_SHA:0:7}"
  echo
  if [ -f dotnet-version.txt ]; then echo "SDK: \`$(cat dotnet-version.txt)\`"; fi
  if [ -f coverage.log ]; then echo '```'; cat coverage.log; echo '```'; fi
  if [ -f test.log ]; then echo "<details><summary>Тесты (последние 40 строк)</summary>"; echo; echo '```'; tail -40 test.log; echo '```'; echo; echo "</details>"; fi
  if [ "$STATUS" != "success" ]; then
    echo "#### Логи ошибок"
    for f in build.log test.log netstandard.log; do
      if [ -f "$f" ]; then echo "**$f**"; echo '```'; tail -80 "$f"; echo '```'; fi
    done
  fi
} > /tmp/ci-report.md
gh pr comment "$PR" --body-file /tmp/ci-report.md || echo "Не удалось опубликовать комментарий"
