using System;
using System.Collections.Generic;
using System.Linq;
using OddMaster.Core.Alignment;

namespace OddMaster.Core.Scheme
{
    /// <summary>Реактор в ядре: пересчёт мировых координат и журнала при изменении оси.</summary>
    public static class SchemeReactor
    {
        /// <summary>Вычисляет журнал координат для схемы.</summary>
        public static CoordinateLog Compute(SchemeModel scheme, StationFormat? format = null)
        {
            if (scheme == null) throw new ArgumentNullException(nameof(scheme));
            var rows = new List<Placement>();
            foreach (var post in scheme.Posts)
            {
                var point = scheme.Alignment.PointAt(post.StationMeters, post.OffsetMeters);
                rows.Add(new Placement(post.Id, post.StationMeters, post.OffsetMeters, post.Side,
                    point.Point.X, point.Point.Y, post.SignsTopDown(), point.Outside, format));
            }
            return new CoordinateLog(rows);
        }

        /// <summary>Возвращает находки по геометрии оси (сегмент нулевой длины и т. п.).</summary>
        public static IReadOnlyList<OddMaster.Core.Norms.Finding> Validate(SchemeModel scheme) =>
            scheme.Alignment.Validate();

        /// <summary>Считает diff журналов до и после изменения оси.</summary>
        public static string Diff(SchemeModel before, SchemeModel after, StationFormat? format = null) =>
            Compute(before, format).Diff(Compute(after, format));
    }
}
