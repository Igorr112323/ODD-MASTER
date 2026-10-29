using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;
using OddMaster.Core.Scheme;
using IoPath = System.IO.Path;

namespace OddMaster.Demo
{
    /// <summary>Загружает синтетическую ДЕМО-схему из tests/fixtures/demo/scheme.json.</summary>
    public static class DemoSchemeLoader
    {
        /// <summary>Читает модель схемы.</summary>
        public static (SchemeModel Model, string Title) Load(string root)
        {
            var path = IoPath.Combine(root, "tests/fixtures/demo/scheme.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var e = doc.RootElement;
            var vertices = new List<Point2>();
            foreach (var v in e.GetProperty("vertices").EnumerateArray())
            {
                var x = v[0].GetDouble();
                var y = v[1].GetDouble();
                vertices.Add(new Point2(x, y));
            }
            var axis = new OddMaster.Core.Alignment.Alignment(vertices, e.GetProperty("initialStation").GetDouble());
            var posts = new List<Post>();
            foreach (var p in e.GetProperty("posts").EnumerateArray())
            {
                var post = new Post(p.GetProperty("id").GetString() ?? "",
                    p.GetProperty("station").GetDouble(), p.GetProperty("offset").GetDouble());
                int index = 0;
                foreach (var s in p.GetProperty("signs").EnumerateArray())
                {
                    index++;
                    var number = SignNumber.Parse(s.GetProperty("number").GetString() ?? "");
                    var sizeClass = s.GetProperty("sizeClass").GetString() ?? "II";
                    string? kind = s.TryGetProperty("parameter", out var k) ? k.GetString() : null;
                    decimal? value = s.TryGetProperty("value", out var v) ? v.GetDecimal() : (decimal?)null;
                    post.Add(new SignInstance(post.Id + "-S" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        number, sizeClass, kind, value));
                }
                posts.Add(post);
            }
            return (new SchemeModel(e.GetProperty("id").GetString() ?? "demo", axis, posts),
                e.GetProperty("title").GetString() ?? "ДЕМО");
        }
    }
}
