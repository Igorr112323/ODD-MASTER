using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OddMaster.Core.Generation;
using OddMaster.Core.Geometry;
using OddMaster.Core.Ports;
using OddMaster.TestKit;
using IoPath = System.IO.Path;

namespace OddMaster.Core.Tests
{
    /// <summary>Изоляция адаптера и порт отрисовки (M9).</summary>
    public class AdapterTests
    {
        [Fact(DisplayName = "M9-A01: адаптер не входит в OddMaster.sln, но есть в OddMaster.NanoCAD.sln")]
        public void AdapterIsSeparate()
        {
            var main = Repo.ReadText("OddMaster.sln");
            var nano = Repo.ReadText("OddMaster.NanoCAD.sln");
            TestAssert.NotContains("OddMaster.NanoCAD", main);
            TestAssert.Contains("OddMaster.NanoCAD", nano);
            TestAssert.Contains("OddMaster.Core", nano);
        }

        [Fact(DisplayName = "M9-A02: в репозитории нет DLL, EXE и PDF")]
        public void NoBinaries()
        {
            var root = Repo.Root();
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains("/.git/") && !f.Contains("/obj/") && !f.Contains("/bin/"));
            foreach (var file in files)
            {
                var ext = IoPath.GetExtension(file).ToLowerInvariant();
                TestAssert.True(ext != ".dll" && ext != ".exe" && ext != ".pdf",
                    "запрещённый бинарный файл: " + file);
            }
        }

        [Fact(DisplayName = "M9-A03: все SDK-NNN вызовы в адаптере зарегистрированы")]
        public void SdkMarkersInAdapter()
        {
            var dir = IoPath.Combine(Repo.Root(), "src/OddMaster.NanoCAD");
            var csproj = Repo.ReadText("src/OddMaster.NanoCAD/OddMaster.NanoCAD.csproj");
            TestAssert.Contains("SDK-001", csproj);
            foreach (var file in Directory.GetFiles(dir, "*.cs"))
                TestAssert.Contains("ПРОВЕРИТЬ В SDK", File.ReadAllText(file), IoPath.GetFileName(file));
        }

        [Fact(DisplayName = "M9-A04: GeometryPortWriter отдаёт примитивы без знания о САПР")]
        public void PortWriterProducesPrimitives()
        {
            var cfg = OddMaster.Core.Config.ConfigLoader.Load(Repo.Root());
            var sign = cfg.Catalog.Find("3.24")!;
            var geometry = new SignGenerator(cfg).Generate(sign, "II",
                new Dictionary<string, decimal> { ["speed"] = 40m }).Geometry!;
            var port = new RecordingPort();
            GeometryPortWriter.Write(port, geometry, new Point2(10, 20), 0.5);
            TestAssert.True(port.Lines.Count + port.Arcs.Count > 0, "нет отрезков и дуг");
            TestAssert.True(port.Arcs.Count > 0, " circle sign: ожидались дуги");
            TestAssert.True(port.Texts.Count > 0, "нет надписей");
            TestAssert.True(port.Fills.Count > 0, "нет заливок");
            TestAssert.Close(10 + geometry.Bounds.MinX * 0.5, port.Fills[0], 0.01);
        }

        private sealed class RecordingPort : IDrawingPort
        {
            public List<string> Lines { get; } = new List<string>();
            public List<string> Arcs { get; } = new List<string>();
            public List<string> Texts { get; } = new List<string>();
            public List<double> Fills { get; } = new List<double>();

            public void DrawLine(Point2 start, Point2 end, string layer) => Lines.Add(start + "->" + end);
            public void DrawArc(Point2 center, double radius, double startAngleDeg, double sweepDeg, string layer) =>
                Arcs.Add(center + " r=" + radius);
            public void DrawFilledPath(OddMaster.Core.Geometry.Path path, string layer) => Fills.Add(path.Bounds().MinX);
            public void DrawText(string text, Point2 anchor, double heightMm, string layer) => Texts.Add(text);
        }
    }
}
