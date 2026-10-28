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
                backend.AddText(cell.Text, cell.Layout, "TABLE_TEXT");
            if (!string.IsNullOrEmpty(table.Title))
                backend.AddText(table.Title, table.TitleLayout, "TABLE_TEXT");
            return backend;
        }
    }
}
