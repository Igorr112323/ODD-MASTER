#!/usr/bin/env python3
"""Сводка покрытия OddMaster.Core по Cobertura. Без внешних зависимостей."""
import glob
import os
import sys
import xml.etree.ElementTree as ET

def main() -> int:
    root = sys.argv[1] if len(sys.argv) > 1 else "coverage"
    files = glob.glob(os.path.join(root, "**", "coverage.cobertura.xml"), recursive=True)
    seen: dict[str, tuple[int, int]] = {}
    for path in files:
        tree = ET.parse(path)
        for cls in tree.iter("class"):
            name = cls.get("filename", "")
            normalized = name.replace("\\", "/")
            if "OddMaster.Core/" not in normalized or ".Tests" in normalized:
                continue
            lines = cls.find("lines")
            if lines is None:
                continue
            hit = total = 0
            for line in lines:
                total += 1
                if int(line.get("hits", "0")) > 0:
                    hit += 1
            prev = seen.get(normalized, (0, 0))
            seen[normalized] = (prev[0] + hit, prev[1] + total)
    hit = sum(v[0] for v in seen.values())
    total = sum(v[1] for v in seen.values())
    rate = 100.0 * hit / total if total else 0.0
    worst = sorted(seen.items(), key=lambda kv: (kv[1][1] - kv[1][0]), reverse=True)[:12]
    breakdown = "\n".join(
        f"- `{name.split('OddMaster.Core/')[-1]}`: {v[0]}/{v[1]} ({v[1] - v[0]} непокрыто)"
        for name, v in worst)
    text = (f"# Покрытие OddMaster.Core\n\n"
            f"Файлов в отчёте: {len(files)}; классов: {len(seen)}; "
            f"строк: {hit}/{total}; **{rate:.1f} %**.\n\n"
            f"Цель по CR-03: не ниже 80 %.\n\n"
            f"## Топ-12 файлов по числу непокрытых строк\n\n{breakdown}\n")
    with open("coverage-summary.md", "w", encoding="utf-8") as fh:
        fh.write(text)
    print(text)
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
