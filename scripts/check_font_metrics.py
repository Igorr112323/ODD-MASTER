#!/usr/bin/env python3
"""Сверка config/typography/fonts.json с файлом шрифта.

TextEngine вписывает надписи по модели метрик из конфига. Если конфиг разойдётся
с реальным файлом шрифта, надписи начнут вылезать за поле. Этот скрипт
перемеряет шрифт (scripts/measure_font.py) и сравнивает с конфигом.

Запуск: python3 scripts/check_font_metrics.py [--font путь.ttf]
Код возврата 1 — расхождение.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import measure_font  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent


def default_font() -> Path:
    import matplotlib

    return Path(matplotlib.__file__).parent / "mpl-data" / "fonts" / "ttf" / "DejaVuSans-Bold.ttf"


def main() -> int:
    parser = argparse.ArgumentParser(description="Сверка метрик шрифта с конфигом")
    parser.add_argument("--font", default="")
    parser.add_argument("--config", default=str(ROOT / "config" / "typography" / "fonts.json"))
    args = parser.parse_args()

    font_path = Path(args.font) if args.font else default_font()
    if not font_path.exists():
        print(f"Нет файла шрифта: {font_path}", file=sys.stderr)
        return 2

    measured = measure_font.measure(font_path)
    config = json.loads(Path(args.config).read_text(encoding="utf-8"))
    source = config.get("measuredFrom", {})
    problems: list[str] = []

    if source.get("sha256") != measured["sha256"]:
        problems.append(f"sha256 файла {measured['sha256']} не совпал с measuredFrom.sha256 {source.get('sha256')}")
    if abs(float(config["capHeightRatio"]) - measured["capHeightRatio"]) > 1e-4:
        problems.append(f"capHeightRatio: конфиг {config['capHeightRatio']}, файл {measured['capHeightRatio']}")
    ratios = config["advanceRatios"]
    for char, ratio in ratios.items():
        actual = measured["advanceRatios"].get(char)
        if actual is None:
            problems.append(f"символ {char!r} есть в конфиге, но не измерен в файле")
        elif abs(actual - float(ratio)) > 1e-4:
            problems.append(f"ширина {char!r}: конфиг {ratio}, файл {actual}")
    if float(config["defaultAdvanceRatio"]) < max(float(v) for v in ratios.values()) - 1e-9:
        problems.append("defaultAdvanceRatio уже самого широкого символа: текст может вылезти за поле")

    if problems:
        print("Расхождения метрик шрифта:")
        for problem in problems:
            print(" - " + problem)
        print("Как исправить: python3 scripts/measure_font.py <файл.ttf> --json config/typography/fonts.json "
              "(затем вернуть normRef/verification/markers).")
        return 1
    print(f"Метрики шрифта совпадают с файлом {measured['source']}: "
          f"{len(ratios)} символов, capHeightRatio={config['capHeightRatio']}, "
          f"sha256={measured['sha256'][:12]}…")
    return 0


if __name__ == "__main__":
    sys.exit(main())
