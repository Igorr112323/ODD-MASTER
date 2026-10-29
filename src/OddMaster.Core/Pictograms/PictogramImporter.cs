using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OddMaster.Core.Geometry;

namespace OddMaster.Core.Pictograms
{
    /// <summary>Импорт SVG-трассировки прил. Б в пиктограмму в клетках сетки (P2).</summary>
    public static class PictogramImporter
    {
        private static readonly Regex Number = new Regex(@"-?[0-9]+(?:\.[0-9]+)?", RegexOptions.Compiled);

        /// <summary>Преобразует SVG (полилинии/полигоны/простые path) в JSON пиктограммы.</summary>
        public static PictogramDefinition Import(string svg, string sign, string source,
            int cellsAcross = 12, double toleranceCells = 0.25, string color = "Black")
        {
            if (svg == null) throw new ArgumentNullException(nameof(svg));
            var viewBox = ParseViewBox(svg);
            var width = viewBox.Item3 > 0 ? viewBox.Item3 : 100.0;
            var scale = width / cellsAcross;
            var raw = ExtractPolylines(svg);
            var shapes = new List<string>();
            foreach (var line in raw)
            {
                var grid = line.Select(p => new Point2(
                    (p.X - (viewBox.Item1 + width / 2)) / scale,
                    ((viewBox.Item2 + viewBox.Item4 / 2) - p.Y) / scale)).ToList();
                var simplified = DouglasPeucker(grid, toleranceCells);
                if (simplified.Count < 2) continue;
                shapes.Add("{\"type\":\"polyline\",\"width\":0.5,\"points\":["
                    + string.Join(",", simplified.Select(p => "[" + Num(p.X) + "," + Num(p.Y) + "]")) + "]}");
            }
            if (shapes.Count == 0)
                throw new FormatException("В SVG не найдено ни одной полилинии для импорта.");
            var layer = "{\"role\":\"symbol\",\"color\":\"" + color + "\",\"shapes\":["
                + string.Join(",", shapes) + "]}";
            return new PictogramDefinition("pict." + sign + ".transcribed", sign,
                "StudentTranscribed", source, new PictogramGrid(cellsAcross, "D"),
                new List<string> { layer },
                "Импортировано из SVG-трассировки; требуется визуальная сверка и StudentVerified.");
        }

        private static Tuple<double, double, double, double> ParseViewBox(string svg)
        {
            var match = Regex.Match(svg, "viewBox\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (!match.Success) return Tuple.Create(0.0, 0.0, 100.0, 100.0);
            var nums = Number.Matches(match.Groups[1].Value).Cast<Match>()
                .Select(m => double.Parse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture)).ToList();
            if (nums.Count < 4) return Tuple.Create(0.0, 0.0, 100.0, 100.0);
            return Tuple.Create(nums[0], nums[1], nums[2], nums[3]);
        }

        private static List<List<Point2>> ExtractPolylines(string svg)
        {
            var result = new List<List<Point2>>();
            foreach (var m in Regex.Matches(svg,
                "<(?:polyline|polygon)[^>]*points\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase).Cast<Match>())
                result.Add(ParsePointPairs(m.Groups[1].Value));
            foreach (var m in Regex.Matches(svg, "<path[^>]*\\sd\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase).Cast<Match>())
            {
                var d = m.Groups[1].Value;
                if (Regex.IsMatch(d, "[AaCcQqSsTtHhVv]", RegexOptions.None)) continue; // только M/L
                var points = new List<Point2>();
                var tokens = Number.Matches(d).Cast<Match>()
                    .Select(x => double.Parse(x.Value, NumberStyles.Float, CultureInfo.InvariantCulture)).ToList();
                for (int i = 0; i + 1 < tokens.Count; i += 2) points.Add(new Point2(tokens[i], tokens[i + 1]));
                if (points.Count >= 2) result.Add(points);
            }
            return result;
        }

        private static List<Point2> ParsePointPairs(string text)
        {
            var nums = Number.Matches(text).Cast<Match>()
                .Select(x => double.Parse(x.Value, NumberStyles.Float, CultureInfo.InvariantCulture)).ToList();
            var points = new List<Point2>();
            for (int i = 0; i + 1 < nums.Count; i += 2) points.Add(new Point2(nums[i], nums[i + 1]));
            return points;
        }

        /// <summary>Упрощение Дугласа–Пекера с допуском в клетках сетки.</summary>
        public static List<Point2> DouglasPeucker(IReadOnlyList<Point2> points, double tolerance)
        {
            if (points.Count <= 2) return points.ToList();
            var keep = new bool[points.Count];
            keep[0] = keep[points.Count - 1] = true;
            Simplify(points, 0, points.Count - 1, tolerance, keep);
            return points.Where((p, i) => keep[i]).ToList();
        }

        private static void Simplify(IReadOnlyList<Point2> points, int first, int last,
            double tolerance, bool[] keep)
        {
            if (last <= first + 1) return;
            double maxDistance = -1;
            int index = -1;
            var a = points[first];
            var b = points[last];
            var ab = b - a;
            var length = ab.Length;
            for (int i = first + 1; i < last; i++)
            {
                var ap = points[i] - a;
                var distance = length < 1e-12
                    ? ap.Length
                    : Math.Abs(ab.X * ap.Y - ab.Y * ap.X) / length;
                if (distance > maxDistance) { maxDistance = distance; index = i; }
            }
            if (maxDistance <= tolerance || index < 0) return;
            keep[index] = true;
            Simplify(points, first, index, tolerance, keep);
            Simplify(points, index, last, tolerance, keep);
        }

        private static string Num(double value) =>
            Math.Round(value, 3, MidpointRounding.AwayFromZero)
                .ToString("0.###", CultureInfo.InvariantCulture);
    }
}
