using System;
using OddMaster.Core.Geometry;
using OddMaster.Core.Statement;

namespace OddMaster.Render.Dxf
{
    /// <summary>
    /// Запись ведомости ТС ОДД в DXF как настоящей таблицы чертежа: линии сетки + текст в ячейках.
    /// Форма таблицы приходит из config/tables/statement-table.json (черновик, GOST-011).
    /// </summary>
    public static class StatementDxfWriter
    {
        /// <summary>
        /// Записывает таблицу в существующий приёмник (например, в чертёж схемы).
        /// scale переводит миллиметры таблицы в единицы чертежа: 0,001 для чертежа в метрах.
        /// </summary>
        public static void Write(DxfBackend backend, TableLayout table, double scale = 1.0)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (!(scale > 0)) throw new ArgumentOutOfRangeException(nameof(scale));
            backend.AddLayer("TABLE", DxfColorMap.Aci("Black"));
            backend.AddLayer("TABLE_TEXT", DxfColorMap.Aci("Black"));
            foreach (var line in table.Lines)
                backend.AddLine(Scale(line.Start, scale), Scale(line.End, scale), "TABLE");
            foreach (var cell in table.Cells)
                foreach (var line in cell.Lines)
                    backend.AddText(line.Value, Scale(line.BaselineStart, scale),
                        line.CapHeightMm * scale, "TABLE_TEXT");
            if (!string.IsNullOrEmpty(table.Title))
                backend.AddText(table.Title, Scale(table.TitleLayout.BaselineStart, scale),
                    table.TitleLayout.CapHeightMm * scale, "TABLE_TEXT");
        }

        private static Point2 Scale(Point2 point, double scale) => new Point2(point.X * scale, point.Y * scale);

        /// <summary>Строит документ таблицы. table должен быть построен в координатах origin.</summary>
        public static DxfBackend Build(TableLayout table, StatementTableSpec spec, Point2 origin,
            double flattenSagittaMm)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            var backend = new DxfBackend(flattenSagittaMm) { InsUnits = 4 };
            backend.AddLayer("TABLE", DxfColorMap.Aci("Black"));
            backend.AddLayer("TABLE_TEXT", DxfColorMap.Aci("Black"));

            foreach (var line in table.Lines) backend.AddLine(line.Start, line.End, "TABLE");
            foreach (var cell in table.Cells)
                foreach (var line in cell.Lines)
                    backend.AddText(line.Value, line, "TABLE_TEXT");
            if (!string.IsNullOrEmpty(table.Title))
                backend.AddText(table.Title, table.TitleLayout, "TABLE_TEXT");
            return backend;
        }
    }
}
