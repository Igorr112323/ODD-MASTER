#!/usr/bin/env python3
"""Минимальный растеризатор сгенерированных SVG-знаков (только для самопроверки агента).

Зачем: в песочнице нет ни .NET, ни cairo/rsvg, а глазами надо увидеть, что цвета и состав
совпадают с прил. А. Скрипт читает ровно тот SVG, который пишет ядро: path с M/L/A/Z,
fill/stroke и текстовые элементы. В репозиторий не входит как продукт: это инструмент
проверки, не часть «ОДД-Мастер».

Использование:
    python3 tools/preview/svg_raster.py docs/demo/signs/3-24_II.svg out.png [px_per_mm]
    python3 tools/preview/svg_raster.py --sheet out.png docs/demo/signs/*.svg
"""

import math
import re
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

PATH_RE = re.compile(r'<path d="([^"]+)"([^/]*)/>')
TEXT_RE = re.compile(r'<text x="([-0-9.]+)" y="([-0-9.]+)"[^>]*font-size="([-0-9.]+)"[^>]*fill="([^"]+)"[^>]*>([^<]*)</text>')
GROUP_RE = re.compile(r'<g id="([^"]+)">')
VIEWBOX_RE = re.compile(r'viewBox="([-0-9. ]+)"')
TOKEN_RE = re.compile(r"([MLAZmlaz])|([-0-9.]+)")


def parse_viewbox(svg):
    m = VIEWBOX_RE.search(svg)
    if not m:
        raise ValueError("нет viewBox")
    x, y, w, h = (float(v) for v in m.group(1).split())
    return x, y, w, h


def tokenize(d):
    tokens = []
    for m in TOKEN_RE.finditer(d):
        tokens.append(m.group(1) if m.group(1) else float(m.group(2)))
    return tokens


def arc_points(x0, y0, rx, ry, phi_deg, large, sweep, x1, y1, step_deg=6.0):
    """Точки дуги SVG (A) от (x0,y0) до (x1,y1). Углы — в системе SVG (Y вниз)."""
    if rx == 0 or ry == 0:
        return [(x1, y1)]
    phi = math.radians(phi_deg)
    dx2 = (x0 - x1) / 2.0
    dy2 = (y0 - y1) / 2.0
    cos_phi, sin_phi = math.cos(phi), math.sin(phi)
    x1p = cos_phi * dx2 + sin_phi * dy2
    y1p = -sin_phi * dx2 + cos_phi * dy2
    rx, ry = abs(rx), abs(ry)
    lam = (x1p ** 2) / (rx ** 2) + (y1p ** 2) / (ry ** 2)
    if lam > 1:
        rx *= math.sqrt(lam)
        ry *= math.sqrt(lam)
    num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p
    den = rx * rx * y1p * y1p + ry * ry * x1p * x1p
    coef = math.sqrt(max(0.0, num / den)) if den else 0.0
    if large == sweep:
        coef = -coef
    cxp = coef * rx * y1p / ry
    cyp = -coef * ry * x1p / rx
    cx = cos_phi * cxp - sin_phi * cyp + (x0 + x1) / 2
    cy = sin_phi * cxp + cos_phi * cyp + (y0 + y1) / 2

    def angle(ux, uy, vx, vy):
        dot = ux * vx + uy * vy
        length = math.hypot(ux, uy) * math.hypot(vx, vy)
        a = math.degrees(math.acos(max(-1.0, min(1.0, dot / length)))) if length else 0.0
        return -a if (ux * vy - uy * vx) < 0 else a

    start = angle(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry)
    delta = angle((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry)
    if not sweep and delta > 0:
        delta -= 360
    elif sweep and delta < 0:
        delta += 360
    count = max(2, int(abs(delta) / step_deg) + 1)
    points = []
    for i in range(1, count + 1):
        a = math.radians(start + delta * i / count)
        px = cx + rx * math.cos(a) * cos_phi - ry * math.sin(a) * sin_phi
        py = cy + rx * math.cos(a) * sin_phi + ry * math.sin(a) * cos_phi
        points.append((px, py))
    return points


def path_to_polygons(d, flip_y=False):
    tokens = tokenize(d)
    polygons, current = [], []
    x = y = 0.0
    start = None
    i = 0
    command = None
    while i < len(tokens):
        if isinstance(tokens[i], str):
            command = tokens[i]
            i += 1
            if command in "Zz":
                if current:
                    current.append(current[0])
                    polygons.append(current)
                    current = []
                if start:
                    x, y = start
                continue
            continue
        if command in "Mm":
            nx, ny = tokens[i], tokens[i + 1]
            i += 2
            if current:
                polygons.append(current)
                current = []
            x, y = nx, ny
            start = (x, y)
            current = [(x, y)]
        elif command in "Ll":
            nx, ny = tokens[i], tokens[i + 1]
            i += 2
            x, y = nx, ny
            current.append((x, y))
        elif command in "Aa":
            rx, ry, rot, large, sweep, nx, ny = tokens[i:i + 7]
            i += 7
            current.extend(arc_points(x, y, rx, ry, rot, int(large), int(sweep), nx, ny))
            x, y = nx, ny
        else:
            raise ValueError("неизвестная команда пути: " + str(command))
    if current:
        polygons.append(current)
    if flip_y:
        polygons = [[(px, -py) for px, py in polygon] for polygon in polygons]
    return polygons


def color_of(text):
    m = re.search(r'fill="([^"]+)"', text)
    return m.group(1) if m else None


def render(svg_text, px_per_mm=2.0):
    x0, y0, w, h = parse_viewbox(svg_text)
    width = max(8, int(round(w * px_per_mm)))
    height = max(8, int(round(h * px_per_mm)))
    image = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(image)

    def to_px(p):
        return ((p[0] - x0) * px_per_mm, (p[1] - y0) * px_per_mm)

    # Группа геометрии пишется с transform="scale(1,-1)": ось Y модели направлена вверх.
    flip = 'scale(1,-1)' in svg_text
    for m in PATH_RE.finditer(svg_text):
        attrs = m.group(2)
        polygons = path_to_polygons(m.group(1), flip)
        fill = color_of(attrs)
        stroke_m = re.search(r'stroke="([^"]+)"', attrs)
        stroke = stroke_m.group(1) if stroke_m else None
        stroke_w = re.search(r'stroke-width="([-0-9.]+)"', attrs)
        width_px = max(1, int(round(float(stroke_w.group(1)) * px_per_mm))) if stroke_w else 1
        for polygon in polygons:
            points = [to_px(p) for p in polygon]
            if len(points) < 3:
                continue
            if fill and fill != "none":
                draw.polygon(points, fill=fill)
            if stroke and stroke != "none":
                draw.line(points + [points[0]], fill=stroke, width=width_px, joint="curve")

    for m in TEXT_RE.finditer(svg_text):
        tx, ty, size, color, body = m.groups()
        size_px = max(6, int(round(float(size) * px_per_mm)))
        try:
            font = ImageFont.truetype("DejaVuSans-Bold.ttf", size_px)
        except Exception:
            font = ImageFont.load_default()
        draw.text(to_px((float(tx), float(ty))), body, fill=color, font=font)
    return image


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return 2
    if args[0] == "--sheet":
        out = Path(args[1])
        files = [Path(p) for p in args[2:]]
        thumbs = [render(Path(f).read_text(encoding="utf-8"), 0.8) for f in files]
        cols = 6
        cell_w = max(t.width for t in thumbs) + 8
        cell_h = max(t.height for t in thumbs) + 8
        rows = (len(thumbs) + cols - 1) // cols
        sheet = Image.new("RGB", (cols * cell_w, rows * cell_h), "#eeeeee")
        for index, thumb in enumerate(thumbs):
            sheet.paste(thumb, ((index % cols) * cell_w + 4, (index // cols) * cell_h + 4))
        sheet.save(out)
        print("контактный лист: %s (%d знаков)" % (out, len(thumbs)))
        return 0
    source, target = Path(args[0]), Path(args[1])
    scale = float(args[2]) if len(args) > 2 else 2.0
    render(source.read_text(encoding="utf-8"), scale).save(target)
    print("готово: " + str(target))
    return 0


if __name__ == "__main__":
    sys.exit(main())
