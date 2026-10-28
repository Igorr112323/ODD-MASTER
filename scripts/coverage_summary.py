#!/usr/bin/env python3
"""Сводка покрытия OddMaster.Core по Cobertura.

Два тестовых проекта дают два файла Cobertura. Один и тот же файл ядра может
встретиться в обоих с разными путями и разными hits. Берём объединение:
ключ — путь внутри OddMaster.Core, значение — словарь «номер строки → max(hits)».
Так строки не удваиваются и покрытие соответствует объединению тестовых прогонов.
"""
import glob
import os
import sys
import xml.etree.ElementTree as ET


def main() -> int:
    root = sys.argv[1] if len(sys.argv) > 1 else "coverage"
    files = glob.glob(os.path.join(root, "**", "coverage.cobertura.xml"), recursive=True)
    merged: dict[str, dict[int, int]] = {}
    for path in files:
        tree = ET.parse(path)
        for cls in tree.iter("class"):
            name = cls.get("filename", "").replace("\\", "/")
            if "OddMaster.Core/" not in name or ".Tests" in name:
                continue
            key = name.split("OddMaster.Core/")[-1]
            lines = cls.find("lines")
            if lines is None:
                continue
            bucket = merged.setdefault(key, {})
            for line in lines:
                try:
                    number = int(line.get("number", "0"))
                    hits = int(line.get("hits", "0"))
                except ValueError:
                    continue
                if number <= 0:
                    continue
                bucket[number] = max(bucket.get(number, 0), hits)

    hit = sum(1 for lines in merged.values() for h in lines.values() if h > 0)
    total = sum(len(lines) for lines in merged.values())
    rate = 100.0 * hit / total if total else 0.0
    worst = sorted(merged.items(), key=lambda kv: sum(1 for h in kv[1].values() if h == 0),
                   reverse=True)[:12]
    breakdown = "\n".join(
        f"- `{name}`: {sum(1 for h in lines.values() if h > 0)}/{len(lines)} "
        f"({sum(1 for h in lines.values() if h == 0)} непокрыто)"
        for name, lines in worst)
    text = (f"# Покрытие OddMaster.Core\n\n"
            f"Файлов в отчёте: {len(files)}; файлов ядра: {len(merged)}; "
            f"строк: {hit}/{total}; **{rate:.1f} %**.\n\n"
            f"Цель по CR-03: не ниже 80 %.\n\n"
            f"## Топ-12 файлов по числу непокрытых строк\n\n{breakdown}\n")
    with open("coverage-summary.md", "w", encoding="utf-8") as fh:
        fh.write(text)
    print(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
