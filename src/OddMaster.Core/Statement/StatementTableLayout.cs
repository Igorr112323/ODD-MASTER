using System;
using System.Collections.Generic;
using OddMaster.Core.Geometry;
using OddMaster.Core.Typography;

namespace OddMaster.Core.Statement
{
    /// <summary>Ячейка таблицы: текст, её габарит и готовая раскладка базовой линии.</summary>
    public sealed class TableCell
    {
        /// <summary>Создаёт ячейку.</summary>
        public TableCell(int row, int column, string text, BoundingBox box, TextLayout layout)
        {
            Row = row; Column = column; Text = text ?? string.Empty; Box = box; Layout = layout;
        }

        /// <summary>Строка (0 — шапка).</summary>
        public int Row { get; }
        /// <summary>Колонка.</summary>
        public int Column { get; }
        /// <summary>Текст.</summary>
        public string Text { get; }
        /// <summary>Габарит ячейки в координатах чертежа.</summary>
        public BoundingBox Box { get; }
        /// <summary>Раскладка: кегль, ширина, начало базовой линии.</summary>
        public TextLayout Layout { get; }
    }

    /// <summary>Отрезок сетки таблицы в координатах чертежа.</summary>
    public readonly struct TableLine
    {
        /// <summary>Создаёт отрезок.</summary>
        public TableLine(Point2 start, Point2 end) { Start = start; End = end; }

        /// <summary>Начало.</summary>
        public Point2 Start { get; }
        /// <summary>Конец.</summary>
        public Point2 End { get; }
    }

    /// <summary>
    /// Геометрия таблицы ведомости: сетка + подписи, в миллиметрах чертежа.
    /// Один и тот же объект уходит и в DXF-стенд, и в будущую таблицу nanoCAD.
    /// </summary>
    public sealed class TableLayout
    {
        private TableLayout(int rowCount, int columnCount, IReadOnlyList<TableCell> cells,
            IReadOnlyList<TableLine> lines, BoundingBox bounds, string title, TextLayout titleLayout)
        {
            RowCount = rowCount; ColumnCount = columnCount; Cells = cells; Lines = lines;
            Bounds = bounds; Title = title; TitleLayout = titleLayout;
        }

        /// <summary>Число строк, включая шапку.</summary>
        public int RowCount { get; }
        /// <summary>Число колонок.</summary>
        public int ColumnCount { get; }
        /// <summary>Ячейки (шапка + данные).</summary>
        public IReadOnlyList<TableCell> Cells { get; }
        /// <summary>Отрезки сетки.</summary>
        public IReadOnlyList<TableLine> Lines { get; }
        /// <summary>Габарит таблицы (без заголовка над ней).</summary>
        public BoundingBox Bounds { get; }
        /// <summary>Заголовок над таблицей (может быть пустым).</summary>
        public string Title { get; }
        /// <summary>Раскладка заголовка.</summary>
        public TextLayout TitleLayout { get; }

        /// <summary>
        /// Строит таблицу: сетку и подписи. Текст вписывается в ячейку через TextEngine,
        /// поэтому кегль не может выйти за поля (инвариант проверяет тест M13-D08).
        /// </summary>
        public static TableLayout Build(StatementTableSpec spec, IReadOnlyList<string> headers,
            IReadOnlyList<IReadOnlyList<string>> rows, FontMetrics font, Point2 origin)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (headers == null) throw new ArgumentNullException(nameof(headers));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (font == null) throw new ArgumentNullException(nameof(font));
            if (headers.Count != spec.Columns.Count)
                throw new ArgumentException("Число заголовков (" + headers.Count +
                    ") не совпадает с числом колонок формы (" + spec.Columns.Count + ").", nameof(headers));
            foreach (var row in rows)
                if (row.Count != spec.Columns.Count)
                    throw new ArgumentException("Строка таблицы содержит " + row.Count +
                        " ячеек вместо " + spec.Columns.Count + ".", nameof(rows));

            var width = spec.TotalWidthMm;
            var height = spec.TitleRowHeightMm + spec.RowHeightMm * rows.Count;
            var bounds = new BoundingBox(origin.X, origin.Y, origin.X + width, origin.Y + height);

            var cells = new List<TableCell>(headers.Count + rows.Count * headers.Count);
            for (var column = 0; column < headers.Count; column++)
                cells.Add(MakeCell(spec, font, 0, rows.Count, column, headers[column], origin));
            for (var r = 0; r < rows.Count; r++)
                for (var column = 0; column < headers.Count; column++)
                    cells.Add(MakeCell(spec, font, r + 1, rows.Count, column, rows[r][column], origin));

            var lines = new List<TableLine>(rows.Count + 2 + headers.Count + 1);
            var y = origin.Y + height;
            lines.Add(new TableLine(new Point2(origin.X, y), new Point2(origin.X + width, y)));
            y -= spec.TitleRowHeightMm;
            lines.Add(new TableLine(new Point2(origin.X, y), new Point2(origin.X + width, y)));
            for (var r = 0; r < rows.Count; r++)
            {
                y -= spec.RowHeightMm;
                lines.Add(new TableLine(new Point2(origin.X, y), new Point2(origin.X + width, y)));
            }
            var x = origin.X;
            lines.Add(new TableLine(new Point2(x, origin.Y), new Point2(x, origin.Y + height)));
            foreach (var column in spec.Columns)
            {
                x += column.WidthMm;
                lines.Add(new TableLine(new Point2(x, origin.Y), new Point2(x, origin.Y + height)));
            }

            var titleLayout = TextEngine.Layout(string.IsNullOrEmpty(spec.Title) ? " " : spec.Title,
                width * (1 - 2 * spec.CellPaddingRatio), spec.RowHeightMm * spec.TextFillRatio, font,
                new Point2(origin.X + width / 2, origin.Y + height + spec.RowHeightMm / 2));
            return new TableLayout(rows.Count + 1, headers.Count, cells, lines, bounds, spec.Title, titleLayout);
        }

        private static TableCell MakeCell(StatementTableSpec spec, FontMetrics font, int row, int dataRows,
            int column, string text, Point2 origin)
        {
            var left = origin.X;
            for (var c = 0; c < column; c++) left += spec.Columns[c].WidthMm;
            var width = spec.Columns[column].WidthMm;
            var height = row == 0 ? spec.TitleRowHeightMm : spec.RowHeightMm;
            var totalHeight = spec.TitleRowHeightMm + spec.RowHeightMm * dataRows;
            var top = row == 0
                ? origin.Y + totalHeight
                : origin.Y + totalHeight - spec.TitleRowHeightMm - spec.RowHeightMm * (row - 1);
            var box = new BoundingBox(left, top - height, left + width, top);
            // TextFillRatio — доля высоты ячейки под кегль; внутри TextEngine ещё раз
            // применяется MaxHeightRatio из метрик шрифта, поэтому запас двойной.
            var layout = TextEngine.Layout(text ?? string.Empty, width * (1 - 2 * spec.CellPaddingRatio),
                height * spec.TextFillRatio, font, new Point2(left + width / 2, top - height / 2));
            return new TableCell(row, column, text ?? string.Empty, box, layout);
        }
    }
}
