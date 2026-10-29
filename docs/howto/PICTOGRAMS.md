# Как оцифровать пиктограмму знака

Инструмент `odd-tools` (P2) превращает SVG-трассировку страницы прил. Б в JSON в клетках
масштабной сетки. Агент **не оцифровывал ГОСТ**: он не видит стандарт. Это делает студент.

## Подготовка

1. Открыть PDF-копию ГОСТ Р 52290-2024, страницы прил. Б из `docs/prompts/PRODUCT_PROMPT` §7
   (например: 2.4 — стр. 78, 3.24/3.25 — стр. 84, 5.19.1 — стр. 103, 6.4 — стр. 111–112).
2. В Inkscape: импортировать страницу, выделить знак, **Path → Trace Bitmap**, сохранить как
   `Plain SVG` в `tools/trace/<номер>.svg`. В другой копии PDF номера страниц могут отличаться.
3. Убедиться, что в SVG есть `viewBox` и контуры представлены `path`/`polyline`/`polygon`.
   Кривые (`C`, `Q`, `A`) импортёр пока пропускает — перевести их в отрезки (Inkscape: Path → Flatten Beziers).

## Импорт

```bash
dotnet run --project tools/OddMaster.Tools -- pictogram import 3.19 tools/trace/3.19.svg \
  config/pictograms/3-19.json "ГОСТ Р 52290-2024, прил. Б, стр. 83 PDF"
```

Импортёр: читает `viewBox`, переводит координаты в клетки сетки (`cellsAcross=12` на размер D),
упрощает полилинии Дугласом–Пекером с допуском 0,25 клетки, привязывает цвет к палитре каталога,
ставит статус `StudentTranscribed`. Проверить формат: `config/schema/pictogram.schema.json`.

## Проверка наложением

```bash
dotnet run --project tools/OddMaster.Tools -- pictogram overlay 3.19 tools/trace/3.19.png \
  docs/visual/overlay-3-19.svg
```

Выходной SVG: растр страницы внизу, сгенерированный знак сверху с прозрачностью 0,55.
Расхождения видны сразу. Итог — новая запись в `docs/visual/REVIEW-NN.md` и статус
`StudentVerified` **только после ручной сверки** (AGENT не имеет его ставить).

## Синтетическая проверка конвейера

```bash
dotnet run --project tools/OddMaster.Tools -- selftest
```

Создаёт стрелку в памяти, импортирует, упрощает, строит накладку — без файлов ГОСТ.
Этот шаг выполняется в CI.

## Номенклатура

`config/catalog/nomenclature-2024.csv` содержит текущие строки (`номер;наименование;группа;статус`).
Полный список готовит куратор; агент названия не выдумывает. Импорт:

```bash
dotnet run --project tools/OddMaster.Tools -- nomenclature import <файл.csv> stubs.json
```
