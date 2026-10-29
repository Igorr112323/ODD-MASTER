#!/usr/bin/env python3
"""Страж «эталон — закон» (F0 программы LINUX-ПОЛИГОН).

Правило: если в ветке/PR изменены эталонные или демонстрационные артефакты
(`tests/*/Approved/**`, `docs/demo/**`, `tests/golden/**`), то в том же изменении
обязан быть файл `docs/visual/REVIEW-NN.md`, в котором:

1. есть перечень изменённых артефактов — точный путь, имя файла, каталог или
   шаблон (`docs/demo/signs/*.svg` тоже считается);
2. указана хотя бы одна причина или ссылка на требование — токен `CR-NN`,
   `GOST-NNN` или слово «причин».

Молчаливая регенерация эталонов — нарушение, равное подделке теста
(§9.3 LINUX_FIRST_PROGRAM). Поэтому проверка живёт в CI, а не в голове агента.

Использование:
    python3 scripts/check_golden_review.py                     # origin/main...HEAD
    python3 scripts/check_golden_review.py --base HEAD~1 --head HEAD
    python3 scripts/check_golden_review.py --base f9fa901 --strict-names

Выход: 0 — всё честно; 1 — есть изменения артефактов без записи в REVIEW;
2 — ошибка запуска (нет git, нет базы).
"""

import argparse
import fnmatch
import re
import subprocess
import sys

ARTIFACT_PATTERNS = (
    "tests/golden/*",
    "tests/*/Approved/*",
    "docs/demo/*",
)

REVIEW_RE = re.compile(r"^docs/visual/REVIEW-[0-9]+\.md$")
REASON_RE = re.compile(r"\b(CR-[0-9]+|GOST-[0-9]+|причин)", re.IGNORECASE)


def git(*args):
    result = subprocess.run(["git", *args], capture_output=True, text=True)
    if result.returncode != 0:
        raise RuntimeError("git " + " ".join(args) + " → " + result.stderr.strip())
    return result.stdout


def changed_files(base, head):
    output = git("diff", "--name-status", f"{base}...{head}")
    files = []
    for line in output.splitlines():
        if not line.strip():
            continue
        parts = line.split("\t")
        status = parts[0]
        if status.startswith("R") or status.startswith("C"):
            files.append((parts[1], parts[2]))
        else:
            files.append((status[0], parts[1]))
    return files


def is_artifact(path):
    return any(fnmatch.fnmatchcase(path, pattern) for pattern in ARTIFACT_PATTERNS)


def mentions(text, path):
    """Упоминает ли запись этот артефакт: путь, имя, каталог или шаблон."""
    name = path.rsplit("/", 1)[-1]
    directory = path.rsplit("/", 1)[0] + "/*"
    tokens = re.split(r"[\s,;()\[\]`'\"]+", text)
    for token in tokens:
        token = token.strip("|:.").strip()
        if not token:
            continue
        if token == path or token == name or token == directory:
            return True
        if "*" in token and fnmatch.fnmatchcase(path, token):
            return True
    return False


def main(argv):
    parser = argparse.ArgumentParser(description="Проверка записи в docs/visual/REVIEW об изменениях эталонов.")
    parser.add_argument("--base", default="origin/main", help="база сравнения (по умолчанию origin/main)")
    parser.add_argument("--head", default="HEAD", help="вершина сравнения (по умолчанию HEAD)")
    parser.add_argument("--strict-names", action="store_true",
                        help="требовать упоминание каждого изменённого файла (по умолчанию — да, файл за файлом)")
    args = parser.parse_args(argv)

    try:
        files = changed_files(args.base, args.head)
    except RuntimeError as error:
        print("❌ страж эталонов: " + str(error))
        print("   Подсказка: в CI нужен checkout с полной историей (fetch-depth: 0).")
        return 2

    artifacts = [path for _, path in files if is_artifact(path)]
    if not artifacts:
        print(f"✅ страж эталонов: артефакты не менялись ({args.base}...{args.head})")
        return 0

    reviews = [path for _, path in files if REVIEW_RE.match(path)]
    if not reviews:
        print(f"❌ страж эталонов: изменено артефактов — {len(artifacts)}, "
              f"но ни один docs/visual/REVIEW-NN.md не обновлён.")
        for path in artifacts[:20]:
            print("   · " + path)
        if len(artifacts) > 20:
            print(f"   … ещё {len(artifacts) - 20}")
        print("   Требование: перечислить изменённые файлы, причину и CR в REVIEW-NN.md (F0).")
        return 1

    text = ""
    for path in reviews:
        try:
            text += "\n" + open(path, encoding="utf-8").read()
        except OSError as error:
            print("❌ страж эталонов: не читается " + path + ": " + str(error))
            return 2

    if not REASON_RE.search(text):
        print("❌ страж эталонов: в " + ", ".join(reviews) + " нет ссылки на требование (CR-NN/GOST-NNN) "
              "и нет слова «причина».")
        return 1

    missing = [path for path in artifacts if not mentions(text, path)]
    if missing:
        print(f"❌ страж эталонов: в {', '.join(reviews)} не перечислены изменённые артефакты "
              f"({len(missing)} из {len(artifacts)}):")
        for path in missing[:30]:
            print("   · " + path)
        if len(missing) > 30:
            print(f"   … ещё {len(missing) - 30}")
        return 1

    print(f"✅ страж эталонов: артефактов {len(artifacts)}, запись есть ({', '.join(reviews)}).")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
