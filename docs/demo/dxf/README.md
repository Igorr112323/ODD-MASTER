# DXF-стенд (ДЕМО, ЧЕРНОВИК)

Сгенерировано командой `dotnet run --project src/OddMaster.Demo -- dxf`. Формат — DXF R12 (AC1009), единицы в HEADER, кириллица через `\U+XXXX` (файл ASCII). Проверка — независимая перечитка `scripts/dxf_verify.py` на ezdxf: файлы читаются заново, сверяются слои, число сущностей, габарит и тексты с `expected.json`.

**Что это доказывает:** ядро отдаёт примитивы через `ICadBackend`/`IDrawingPort` и получает настоящий чертёж, который открывается сторонним ПО. **Чего это не доказывает:** работу в nanoCAD (G1/G2 закрыты), колориметрию ГОСТ, соответствие пиктограмм прил. Б.

| Файл | Тип | Что внутри | Сущностей | Слоёв |
|---|---|---|---|---|
| `dxf/live-before.dxf` | scenario | до правки | 469 | 45 |
| `dxf/live-after.dxf` | scenario | после правки | 518 | 51 |
| `dxf/control.dxf` | scenario | контроль | 59 | 4 |
| `dxf/hello-3.1-I.dxf` | sign | 3.1 I | 188 | 4 |
| `dxf/sign-2-1-I.dxf` | sign | 2.1 I | 24 | 4 |
| `dxf/sign-2-3-1-I.dxf` | sign | 2.3.1 I | 92 | 5 |
| `dxf/sign-2-4-I.dxf` | sign | 2.4 I | 92 | 5 |
| `dxf/sign-2-5-II.dxf` | sign | 2.5 II | 39 | 5 |
| `dxf/sign-5-5-I.dxf` | sign | 5.5 I | 100 | 5 |
| `dxf/sign-5-7-1-II.dxf` | sign | 5.7.1 II | 100 | 5 |
| `dxf/sign-8-1-1-I.dxf` | sign | 8.1.1 I | 91 | 4 |
| `dxf/scheme.dxf` | scheme | DEMO-SCHEME-01 | 384 | 42 |
| `dxf/statement.dxf` | statement | ТС ОДД | 84 | 2 |

## Состав

- сценарий: live-before / live-after / control (см. README-scenario.md)
- hello-3.1-I.dxf: знак 3.1, типоразмер I, 600×600 мм
- dxf/sign-2-1-I.dxf: знак 2.1, форма Diamond, типоразмер I
- dxf/sign-2-3-1-I.dxf: знак 2.3.1, форма TriangleUp, типоразмер I
- dxf/sign-2-4-I.dxf: знак 2.4, форма TriangleDown, типоразмер I
- dxf/sign-2-5-II.dxf: знак 2.5, форма Octagon, типоразмер II
- dxf/sign-5-5-I.dxf: знак 5.5, форма Square, типоразмер I
- dxf/sign-5-7-1-II.dxf: знак 5.7.1, форма RectangleVertical, типоразмер II
- dxf/sign-8-1-1-I.dxf: знак 8.1.1, форма RectangleHorizontal, типоразмер I
- scheme.dxf: ось 900 м, опор 6, знаков 7
- statement.dxf: строк 7, колонок 7

## Ограничения стенда

- Дуги заменяются хордами с допуском `dxfFlattenSagittaMm` из `config/conventions/drawing.json` (0,2 мм): это отображение, а не норма.
- Заливка — веер треугольников `SOLID`, потому что в DXF R12 нет `HATCH`.
- Цвета — индексы ACI, это палитра САПР, а не колориметрия ГОСТ (`DISPLAY_ONLY`).
- Пиктограммы остаются заглушками: реальная графика прил. Б ждёт оцифровки студентом.
