#!/usr/bin/env python3
"""Независимая перечитка DXF-стенда «ОДД-Мастер».

Читает файлы, которые сделало C#-ядро, СТОРОННИМ инструментом (ezdxf), сравнивает
состав и габариты с манифестом docs/demo/dxf/expected.json, пересчитывает номинальные
размеры знаков из config/ (не из C#-кода) и рендерит PNG для визуальной проверки.

Запуск:
    python3 scripts/dxf_verify.py            # проверка + PNG
    python3 scripts/dxf_verify.py --no-render

Код возврата 1 — есть расхождения. Отчёт: artifacts/dxf/verification.md.
"""
from __future__ import annotations

import argparse
import json
import math
import re
import sys
from pathlib import Path

try:
    import ezdxf
except ImportError:  # pragma: no cover
    print("Нет ezdxf. Установите: pip install ezdxf matplotlib", file=sys.stderr)
    raise SystemExit(2)

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_DIR = ROOT / "docs" / "demo" / "dxf"
ESCAPE = re.compile(r"\\U\+([0-9A-Fa-f]{4})")

SHAPES = {
    # Как CarrierBuilder в ядре считает габарит носителя; здесь — независимый пересчёт.
    "TriangleUp": lambda d: (d["A"], d["A"] * math.sqrt(3.0) / 2),
    "TriangleDown": lambda d: (d["A"], d["A"] * math.sqrt(3.0) / 2),
    "Circle": lambda d: (d["D"], d["D"]),
    "Octagon": lambda d: (d["B"], d["B"]),
    "Square": lambda d: (d["B"], d["B"]),
    "Diamond": lambda d: (d["B"] * math.sqrt(2.0), d["B"] * math.sqrt(2.0)),
    "RoundedRect": lambda d: (d["B"], d["H"]),
}

failures: list[str] = []
checks = 0


def check(condition: bool, message: str) -> None:
    global checks
    checks += 1
    if not condition:
        failures.append(message)


def decode(text: str) -> str:
    return ESCAPE.sub(lambda m: chr(int(m.group(1), 16)), text)


def parse_number(token: str) -> tuple[int, ...]:
    return tuple(int(part) for part in token.strip().split("."))


def token_matches(token: str, number: tuple[int, ...]) -> bool:
    """Семантика ADR-0005: точное совпадение или диапазон 'a - b' включительно,
    причём более длинный номер (6.16.1) в диапазон, кончающийся на 6.16, не входит."""
    token = token.strip()
    if " - " in token:
        low_text, high_text = token.split(" - ", 1)
        return parse_number(low_text) <= number <= parse_number(high_text)
    return parse_number(token) == number


def find_size(profile: dict, number: str, size_class: str) -> dict[str, float] | None:
    wanted = parse_number(number)
    for table in profile.get("tables", []):
        for row in table.get("rows", []):
            signs = [t for t in row.get("signs", "").split(",") if token_matches(t, wanted)]
            if not signs:
                continue
            sizes = row.get("sizes", {}).get(size_class)
            if not sizes:
                continue
            dims = {k: float(v) for k, v in sizes.items() if not isinstance(v, list)}
            for key, values in sizes.items():
                if isinstance(values, list) and values:
                    dims[key] = float(values[0])
            options = row.get("B_options")
            if "B" not in dims and isinstance(options, dict):
                first = options.get(size_class) or options.get("any")
                if first:
                    dims["B"] = float(first[0] if isinstance(first, list) else first)
            for common in ("r", "cornerRadius"):
                if common in row:
                    dims[common] = float(row[common])
            return dims
    return None


def nominal_size(root: Path, number: str, size_class: str) -> tuple[float, float, str, float] | None:
    catalog = json.loads((root / "config/catalog/signs.poc.json").read_text(encoding="utf-8"))
    profile = json.loads((root / "config/profiles/gost-r-52290-2024.sizes.json").read_text(encoding="utf-8"))
    shape = None
    for sign in catalog.get("signs", []):
        if sign.get("number") == number:
            shape = sign.get("shape")
            break
    if shape is None or shape not in SHAPES:
        return None
    dims = find_size(profile, number, size_class)
    if dims is None:
        return None
    try:
        width, height = SHAPES[shape](dims)
    except KeyError:
        return None
    return width, height, shape, float(dims.get("r", 0.0))


def bbox_of(msp) -> tuple[float, float, float, float] | None:
    min_x = min_y = math.inf
    max_x = max_y = -math.inf
    found = False
    for entity in msp:
        points = []
        kind = entity.dxftype()
        if kind == "LINE":
            points = [entity.dxf.start, entity.dxf.end]
        elif kind == "ARC":
            points = _arc_points(entity)
        elif kind in ("TEXT", "MTEXT"):
            insert = entity.dxf.insert
            points = [insert]
        elif kind == "SOLID":
            points = [entity.dxf.vtx0, entity.dxf.vtx1, entity.dxf.vtx2]
        elif kind == "POLYLINE":
            points = [v.dxf.location for v in entity.vertices]
        elif kind == "LWPOLYLINE":
            points = [entity.dxf.elevation and (x, y, 0) for x, y in entity.get_points("xy")]
        for point in points:
            x, y = float(point[0]), float(point[1])
            min_x, min_y = min(min_x, x), min(min_y, y)
            max_x, max_y = max(max_x, x), max(max_y, y)
            found = True
    return (min_x, min_y, max_x, max_y) if found else None


def _arc_points(entity) -> list[tuple[float, float, float]]:
    center = entity.dxf.center
    radius = float(entity.dxf.radius)
    start = math.radians(float(entity.dxf.start_angle))
    end = math.radians(float(entity.dxf.end_angle))
    if end < start:
        end += 2 * math.pi
    points = [(center[0] + radius * math.cos(start), center[1] + radius * math.sin(start), 0)]
    points.append((center[0] + radius * math.cos(end), center[1] + radius * math.sin(end), 0))
    for quadrant in range(4):
        angle = quadrant * math.pi / 2
        while angle < start:
            angle += 2 * math.pi
        if angle <= end:
            points.append((center[0] + radius * math.cos(angle), center[1] + radius * math.sin(angle), 0))
    return points


def verify_file(path: Path, record: dict, root: Path) -> dict:
    info = {"name": path.name, "kind": record.get("kind"), "ok": True, "notes": []}
    doc = ezdxf.readfile(path)
    msp = doc.modelspace()

    check(doc.dxfversion == "AC1009", f"{path.name}: версия {doc.dxfversion}, ожидалась AC1009")
    units = 6 if record.get("units") == "m" else 4
    check(int(doc.header.get("$INSUNITS", 0)) == units,
          f"{path.name}: $INSUNITS={doc.header.get('$INSUNITS')}, ожидалось {units}")

    # ezdxf добавляет служебные слои (0, Defpoints) сам — сверяем только объявленные в файле.
    layers = sorted(layer.dxf.name for layer in doc.layers
                    if layer.dxf.name not in ("0", "Defpoints"))
    check(layers == sorted(record.get("layers", [])),
          f"{path.name}: слои не совпали с манифестом: {layers} != {sorted(record.get('layers', []))}")

    entities = list(msp)
    check(len(entities) == record.get("entityCount"),
          f"{path.name}: сущностей {len(entities)}, в манифесте {record.get('entityCount')}")

    bbox = bbox_of(msp)
    expected = record.get("bounds")
    if expected and bbox:
        tolerance = 0.02
        for got, key in zip(bbox, ("minX", "minY", "maxX", "maxY")):
            check(abs(got - expected[key]) <= tolerance,
                  f"{path.name}: габарит {key}={got:.3f}, в манифесте {expected[key]:.3f}")

    # Инварианты CR-09 прямо в готовом чертеже.
    for entity in entities:
        if entity.dxftype() == "LINE":
            start, end = entity.dxf.start, entity.dxf.end
            length = math.dist((start[0], start[1]), (end[0], end[1]))
            check(length > 1e-9, f"{path.name}: нулевой отрезок на слое {entity.dxf.layer}")
        if entity.dxftype() in ("POLYLINE", "LWPOLYLINE"):
            if entity.dxftype() == "POLYLINE":
                points = [(v.dxf.location[0], v.dxf.location[1]) for v in entity.vertices]
            else:
                points = list(entity.get_points("xy"))
            for a, b in zip(points, points[1:]):
                check(math.dist(a, b) > 1e-9, f"{path.name}: повторная вершина в полилинии {entity.dxf.layer}")
        if entity.dxftype() == "ARC":
            check(float(entity.dxf.radius) > 0, f"{path.name}: дуга с нулевым радиусом")
        if entity.dxftype() in ("TEXT", "MTEXT"):
            check(float(entity.dxf.height) > 0, f"{path.name}: текст нулевой высоты")

    # Цвета: только ByLayer или ACI.
    for entity in entities:
        color = int(entity.dxf.get("color", 256))
        check(color in (0, 256) or 1 <= color <= 255, f"{path.name}: странный цвет {color}")
        check(entity.dxf.layer in [layer.dxf.name for layer in doc.layers],
              f"{path.name}: сущность на необъявленном слое {entity.dxf.layer}")

    texts = [decode(e.dxf.text) for e in entities if e.dxftype() == "TEXT"]
    info["texts"] = len(texts)
    check(all(t.strip() for t in texts), f"{path.name}: пустой текст в чертеже")

    if record.get("kind") == "sign":
        check(any("ЧЕРНОВИК" in t for t in texts),
              f"{path.name}: нет баннера ЧЕРНОВИК — чертёж нельзя принимать за норму")
        size = nominal_size(root, record.get("sign", ""), record.get("sizeClass", ""))
        if size and bbox:
            width, height, shape, radius = size
            got_width = bbox[2] - bbox[0]
            got_height = bbox[3] - bbox[1]
            # Допуск 1 мм: аппроксимация дуг хордами и округление координат в файле.
            if radius <= 1e-9:
                check(abs(got_width - width) <= 1.0,
                      f"{path.name}: ширина {got_width:.2f} мм, по прил. Д {width:.2f} мм ({shape})")
                check(abs(got_height - height) <= 1.0,
                      f"{path.name}: высота {got_height:.2f} мм, по прил. Д {height:.2f} мм ({shape})")
                info["notes"].append(f"{shape}: {got_width:.2f}×{got_height:.2f} мм = прил. Д")
            else:
                # Скругление вершин вписано внутрь номинального контура, поэтому габарит
                # меньше номинального; вопрос «что считать A» вынесен в GOST-024.
                check(got_width <= width + 1.0 and got_height <= height + 1.0,
                      f"{path.name}: габарит {got_width:.2f}×{got_height:.2f} больше номинального "
                      f"{width:.2f}×{height:.2f} ({shape})")
                check(got_width >= width - 3 * radius and got_height >= height - 3 * radius,
                      f"{path.name}: габарит {got_width:.2f}×{got_height:.2f} усох больше чем на 3r "
                      f"(r={radius:.2f}) от номинального {width:.2f}×{height:.2f} ({shape})")
                info["notes"].append(f"{shape}: {got_width:.2f}×{got_height:.2f} в номинальном "
                                     f"{width:.2f}×{height:.2f}, скругление r={radius:.0f} (GOST-024)")
    elif record.get("kind") == "scheme":
        check(any("ЧЕРНОВИК" in t for t in texts), f"{path.name}: нет баннера ЧЕРНОВИК")
        check(any(t.startswith("P1:") for t in texts), f"{path.name}: нет подписи опоры P1")
    elif record.get("kind") == "statement":
        check(any(t.strip() == "№" for t in texts), f"{path.name}: нет шапки ведомости")
    return info


def render(path: Path, out_dir: Path) -> str | None:
    """PNG для глаза: белый фон, кириллица декодирована, заливка без швов между треугольниками."""
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
        from ezdxf.addons.drawing import Frontend, RenderContext
        from ezdxf.addons.drawing.config import BackgroundPolicy, Configuration
        from ezdxf.addons.drawing.matplotlib import MatplotlibBackend
    except Exception as exc:  # noqa: BLE001
        return f"нет рендера: {exc}"
    doc = ezdxf.readfile(path)
    for entity in doc.modelspace().query("TEXT MTEXT"):
        entity.dxf.text = decode(entity.dxf.text)
    fig = plt.figure(figsize=(8, 8))
    ax = fig.add_axes([0, 0, 1, 1])
    ax.set_axis_off()
    configuration = Configuration(background_policy=BackgroundPolicy.WHITE)
    Frontend(RenderContext(doc), MatplotlibBackend(ax), config=configuration).draw_layout(
        doc.modelspace(), finalize=True)
    # Заливка SOLID — веер треугольников; убираем обводку, иначе видны «швы» (это отображение, не геометрия).
    for patch in ax.patches:
        patch.set_edgecolor(patch.get_facecolor())
        patch.set_linewidth(0.3)
    target = out_dir / (path.stem + ".png")
    fig.savefig(target, dpi=110, facecolor="white")
    plt.close(fig)
    return None


def contact_sheet(pngs: list[Path], target: Path) -> str | None:
    """Контактный лист из готовых PNG средствами matplotlib (без ImageMagick)."""
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.image as mpimg
        import matplotlib.pyplot as plt
    except Exception as exc:  # noqa: BLE001
        return f"нет контактного листа: {exc}"
    if not pngs:
        return "нечего склеивать"
    columns = min(4, len(pngs))
    rows = math.ceil(len(pngs) / columns)
    fig, axes = plt.subplots(rows, columns, figsize=(4 * columns, 4 * rows))
    axes = axes.flatten() if hasattr(axes, "flatten") else [axes]
    for axis in axes:
        axis.set_axis_off()
    for axis, png in zip(axes, pngs):
        axis.imshow(mpimg.imread(png))
        axis.set_title(png.stem, fontsize=9)
    fig.tight_layout()
    fig.savefig(target, dpi=100, facecolor="white")
    plt.close(fig)
    return None


def main() -> int:
    parser = argparse.ArgumentParser(description="Независимая проверка DXF-стенда")
    parser.add_argument("--dir", default=str(DEFAULT_DIR))
    parser.add_argument("--out", default=str(ROOT / "artifacts" / "dxf"))
    parser.add_argument("--no-render", action="store_true")
    parser.add_argument("--summary", default="", help="куда дополнительно записать отчёт (например, docs/demo/dxf/verification.md)")
    args = parser.parse_args()

    directory = Path(args.dir)
    manifest_path = directory / "expected.json"
    if not manifest_path.exists():
        print(f"Нет манифеста {manifest_path}: сначала запустите демо с командой dxf.", file=sys.stderr)
        return 2
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    out_dir = Path(args.out)
    (out_dir / "png").mkdir(parents=True, exist_ok=True)

    infos = []
    for record in manifest.get("files", []):
        path = directory / Path(record["path"]).name
        if not path.exists():
            failures.append(f"{record['path']}: файл отсутствует")
            continue
        try:
            infos.append(verify_file(path, record, ROOT))
        except Exception as exc:  # noqa: BLE001
            failures.append(f"{path.name}: не читается сторонним читателем: {type(exc).__name__}: {exc}")

    render_notes = {}
    if not args.no_render:
        for record in manifest.get("files", []):
            path = directory / Path(record["path"]).name
            if path.exists():
                render_notes[path.name] = render(path, out_dir / "png")
        pngs = sorted((out_dir / "png").glob("*.png"))
        sheet_note = contact_sheet(pngs, out_dir / "png" / "contact-sheet.png")
        if sheet_note:
            render_notes["contact-sheet"] = sheet_note

    report = [
        "# Проверка DXF-стенда (независимая перечитка ezdxf)",
        "",
        f"Файлов: {len(infos)}; проверок: {checks}; расхождений: {len(failures)}.",
        "",
        "| Файл | Тип | Сущностей | Текстов | Примечание |",
        "|---|---|---|---|---|",
    ]
    for record, info in zip(manifest.get("files", []), infos):
        report.append(f"| `{info['name']}` | {info['kind']} | {record.get('entityCount')} | "
                      f"{info.get('texts', 0)} | {'; '.join(info['notes'])} |")
    if render_notes:
        report.append("")
        report.append("PNG: `artifacts/dxf/png/*.png` и `contact-sheet.png`. Для картинки кириллица декодирована из `\\U+XXXX`; "
                      "сам DXF-файл остаётся ASCII.")
        for name, note in render_notes.items():
            if note:
                report.append(f"- {name}: {note}")
    if failures:
        report.append("")
        report.append("## Расхождения")
        report.append("")
        for failure in failures:
            report.append(f"- ❌ {failure}")
    else:
        report.append("")
        report.append("## Итог")
        report.append("")
        report.append("✅ Все проверки прошли: DXF читается сторонним инструментом, слои и сущности совпадают "
                      "с манифестом, габариты знаков совпадают с прил. Д, нулевых отрезков и повторных вершин нет.")
    text = "\n".join(report) + "\n"
    (out_dir / "verification.md").write_text(text, encoding="utf-8")
    if args.summary:
        Path(args.summary).write_text(text, encoding="utf-8")

    print("\n".join(report))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
