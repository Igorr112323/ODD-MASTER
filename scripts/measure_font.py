#!/usr/bin/env python3
"""Измерение метрик гарнитуры для config/typography/fonts.json.

Зачем: TextEngine вписывает надписи в поле знака по модели метрик. Если модель
уже реального шрифта, надпись вылезает за поле (это было видно на STOP).
Модуль читает НАСТОЯЩИЙ файл шрифта (fontTools) и выдаёт:
  - capHeightRatio = OS/2.sCapHeight / unitsPerEm,
  - advanceRatios = hmtx.advance / unitsPerEm для нужных символов.

Важно: это измерение конкретного файла шрифта, а не норма ГОСТ. Какую гарнитуру
требует ГОСТ для надписей — по-прежнему GOST-002 и решает студент.

Запуск: python3 scripts/measure_font.py <путь.ttf> [--json out.json]
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

try:
    from fontTools.ttLib import TTFont
except ImportError:  # pragma: no cover
    print("Нет fontTools. Установите: pip install fonttools", file=sys.stderr)
    raise SystemExit(2)

ALPHABET = (
    "0123456789"
    "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ"
    "абвгдеёжзийклмнопрстуфхцчшщъыьэюя"
    " .,;:()-–—/№ кмчминт"
)


def measure(path: Path) -> dict:
    font = TTFont(str(path), lazy=True)
    units = font["head"].unitsPerEm
    # OS/2.sCapHeight в некоторых файлах равен ascender (DejaVu), поэтому высоту прописных
    # берём из контура буквы H: yMax глифа — это и есть кегль прописной.
    cap_height = None
    cmap = font.getBestCmap()
    if "glyf" in font and ord("H") in cmap:
        cap_height = font["glyf"][cmap[ord("H")]].yMax
    if not cap_height:
        cap_height = getattr(font["OS/2"], "sCapHeight", None) or font["hhea"].ascent
    advances = {}
    for char in dict.fromkeys(ALPHABET):
        glyph = font.getBestCmap().get(ord(char))
        if glyph is None:
            continue
        advances[char] = round(font["hmtx"][glyph][0] / units, 4)
    raw = path.read_bytes()
    return {
        "source": path.name,
        "sha256": hashlib.sha256(raw).hexdigest(),
        "unitsPerEm": units,
        "capHeightRatio": round(cap_height / units, 4),
        "ascenderRatio": round(font["hhea"].ascent / units, 4),
        "descenderRatio": round(font["hhea"].descent / units, 4),
        "glyphCountMeasured": len(advances),
        "advanceRatios": dict(sorted(advances.items())),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Метрики шрифта для TextEngine")
    parser.add_argument("font")
    parser.add_argument("--json", default="")
    args = parser.parse_args()
    path = Path(args.font)
    if not path.exists():
        print(f"Нет файла шрифта: {path}", file=sys.stderr)
        return 2
    data = measure(path)
    text = json.dumps(data, ensure_ascii=False, indent=2) + "\n"
    if args.json:
        Path(args.json).write_text(text, encoding="utf-8")
    print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
