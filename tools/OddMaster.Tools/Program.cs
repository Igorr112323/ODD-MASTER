using System;
using System.IO;
using System.Linq;
using System.Text;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Pictograms;

namespace OddMaster.Tools
{
    /// <summary>odd-tools: конвейер оцифровки пиктограмм и импорт номенклатуры (P2).</summary>
    public static class Program
    {
        private const string PixelPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==";

        /// <summary>Точка входа.</summary>
        public static int Main(string[] args)
        {
            if (args.Length == 0) { Usage(); return 2; }
            try
            {
                switch (args[0])
                {
                    case "selftest": return SelfTest();
                    case "pictogram": return Pictogram(args);
                    case "nomenclature": return Nomenclature(args);
                    default: Usage(); return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("odd-tools: " + ex.Message);
                return 1;
            }
        }

        private static int Pictogram(string[] args)
        {
            if (args.Length < 4) { Usage(); return 2; }
            var command = args[1];
            var sign = args[2];
            var input = args[3];
            if (command == "import")
            {
                var svg = File.ReadAllText(input);
                var source = args.Length > 5 ? args[5] : "трассировка студента Inkscape";
                var definition = PictogramImporter.Import(svg, sign, source);
                var output = args.Length > 4 ? args[4] : "config/pictograms/" + sign.Replace('.', '-') + ".json";
                File.WriteAllText(output, definition.ToJson(), new UTF8Encoding(false));
                Console.WriteLine("Импортировано: " + output + " (статус " + definition.Status + ")");
                return 0;
            }
            if (command == "overlay")
            {
                var root = FindRoot();
                var cfg = ConfigLoader.Load(root);
                var definition = cfg.Catalog.Find(sign);
                if (definition == null) throw new InvalidOperationException("Знак " + sign + " не найден в каталоге.");
                var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 800 800\">"
                    + "<rect x=\"100\" y=\"100\" width=\"600\" height=\"600\" fill=\"none\" stroke=\"black\"/></svg>";
                var png = File.Exists(input)
                    ? Convert.ToBase64String(File.ReadAllBytes(input)) : PixelPng;
                var output = args.Length > 4 ? args[4] : "docs/visual/overlay-" + sign.Replace('.', '-') + ".svg";
                File.WriteAllText(output, PictogramOverlay.Overlay(png, svg), new UTF8Encoding(false));
                Console.WriteLine("Накладка: " + output);
                return 0;
            }
            Usage();
            return 2;
        }

        private static int Nomenclature(string[] args)
        {
            if (args.Length < 3 || args[1] != "import") { Usage(); return 2; }
            var rows = NomenclatureImporter.Parse(File.ReadAllText(args[2]));
            var output = args.Length > 3 ? args[3] : "nomenclature.stubs.json";
            File.WriteAllText(output, NomenclatureImporter.ToCatalogStubs(rows), new UTF8Encoding(false));
            Console.WriteLine("Строк: " + rows.Count + " → " + output);
            return 0;
        }

        /// <summary>Синтетическая стрелка: проверяет import → упрощение → overlay без файлов ГОСТ.</summary>
        private static int SelfTest()
        {
            const string arrow = "<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 120 120\">"
                + "<polyline points=\"10,60 30,60 30,50 60,60 30,70 30,60 90,60\"/></svg>";
            var definition = PictogramImporter.Import(arrow, "3.19.test", "синтетическая стрелка");
            var json = definition.ToJson();
            Require(definition.Status == "StudentTranscribed", "статус импорта");
            Require(json.Contains("\"polyline\""), "в JSON нет полилинии");
            Require(json.Contains("\"cellsAcross\": 12"), "в JSON нет сетки");
            Require(json.Contains("\"sign\": \"3.19.test\""), "в JSON нет номера знака");
            var simplified = PictogramImporter.DouglasPeucker(
                new[] { new OddMaster.Core.Geometry.Point2(0, 0), new OddMaster.Core.Geometry.Point2(1, 0.01),
                        new OddMaster.Core.Geometry.Point2(2, 0) }, 0.1);
            Require(simplified.Count == 2, "Дуглас–Пекер должен убрать почти collinear точку");
            var overlay = PictogramOverlay.Overlay(PixelPng, arrow);
            Require(overlay.Contains("data:image/png;base64,"), "в накладке нет растра");
            Require(overlay.Contains("opacity=\"0.55\""), "в накладке нет полупрозрачного знака");
            var rows = NomenclatureImporter.Parse("номер;наименование;группа;статус\n3.19;Разворот запрещен;3;CuratorExtracted\n");
            Require(rows.Count == 1 && rows[0].Group == 3, "импорт номенклатуры");
            var root = FindRoot();
            var cfg = ConfigLoader.Load(root);
            var registryErrors = PictogramRegistry.Validate(root, cfg.Catalog);
            Require(registryErrors.Count == 0, "реестр пиктограмм: " + string.Join("; ", registryErrors));
            Console.WriteLine("odd-tools selftest: OK");
            return 0;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("selftest: " + message);
        }

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "OddMaster.sln"))) dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("Не найден OddMaster.sln");
            return dir.FullName;
        }

        private static void Usage()
        {
            Console.WriteLine("odd-tools — инструменты оцифровки (не входят в продукт).");
            Console.WriteLine("  odd-tools pictogram import <знак> <файл.svg> [выход.json] [источник]");
            Console.WriteLine("  odd-tools pictogram overlay <знак> <png> [выход.svg]");
            Console.WriteLine("  odd-tools nomenclature import <файл.csv> [выход.json]");
            Console.WriteLine("  odd-tools selftest");
        }
    }
}
