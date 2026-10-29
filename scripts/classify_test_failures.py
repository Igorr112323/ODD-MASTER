#!/usr/bin/env python3
"""Классификатор падений `dotnet test` для workflow регенерации (F0).

Зачем: регенерация эталонов допустима только тогда, когда падают **только**
проверки самих эталонов (`Golden.CheckText`). Если упал обычный тест — это
настоящая ошибка, и перезапись эталона превратила бы её в «зелёный» CI,
то есть в подделку теста (§9.3 LINUX_FIRST_PROGRAM).

Как различаем: `Golden.CheckText` бросает сообщение с путём внутри `Approved/`
и фразой «Эталон обновляется только вместе с docs/visual/REVIEW». Всё остальное
считается обычным падением.

Использование:
    python3 scripts/classify_test_failures.py strict.log
Выход: 0 — падений нет или все они эталонные (регенерация разрешена);
       1 — есть обычные падения (регенерация запрещена);
       2 — лог не найден.
"""

import re
import sys

FAILED_RE = re.compile(r"^\s+(?:Failed|Провален)\s+(?P<name>[^\[\n]+?)\s*(?:\[[^\]]*\])?\s*$")
SUMMARY_RE = re.compile(r"^\s*(?:Failed|Провалено)\s*:\s*(?P<count>\d+)\s*$")
GOLDEN_MARKERS = ("Approved/", "Approved\\", "Эталон обновляется", "golden")


def blocks(lines):
    """Группирует строки лога по падениям: от строки "Failed ..." до следующего падения."""
    current = None
    for line in lines:
        match = FAILED_RE.match(line)
        if match:
            if current:
                yield current
            current = {"name": match.group("name").strip(), "text": []}
            continue
        if current is not None:
            current["text"].append(line)
    if current:
        yield current


def main(argv):
    if not argv:
        print("использование: classify_test_failures.py <лог>")
        return 2
    path = argv[0]
    try:
        with open(path, encoding="utf-8", errors="replace") as handle:
            lines = handle.readlines()
    except OSError as error:
        print("❌ нет лога: " + str(error))
        return 2

    failures = list(blocks(lines))
    if not failures:
        print("✅ падений в логе нет — регенерация разрешена")
        return 0

    golden, ordinary = [], []
    for failure in failures:
        body = "\n".join(failure["text"])
        (golden if any(marker in body for marker in GOLDEN_MARKERS) else ordinary).append(failure["name"])

    print(f"Падений: {len(failures)}; эталонных: {len(golden)}; обычных: {len(ordinary)}")
    for name in golden:
        print("   эталон: " + name)
    for name in ordinary:
        print("   обычный: " + name)

    if ordinary:
        print("❌ есть падения, не связанные с эталонами: перезапись эталонов запрещена (F0).")
        return 1
    print("✅ все падения — эталонные: регенерацию можно выполнять.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
