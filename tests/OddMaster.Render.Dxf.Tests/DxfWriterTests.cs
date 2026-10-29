using System;
using System.IO;
using System.Linq;
using OddMaster.Core.Config;
using OddMaster.Core.Generation;
using OddMaster.Core.Geometry;
using OddMaster.Demo;
using OddMaster.Render.Dxf;
using OddMaster.TestKit;
using Xunit;
using IoPath = System.IO.Path;

namespace OddMaster.Render.Dxf.Tests
{
    /// <summary>Общие помощники стенда: конфиг из репозитория и знак из генератора.</summary>
    internal static class Stand
    {
        private static NormativeConfigSet? _config;

        /// <summary>Конфигурация репозитория (загружается один раз).</summary>
        public static NormativeConfigSet Config => _config ??= ConfigLoader.Load(Repo.Root());

        /// <summary>Генерирует знак по номеру и типоразмеру или падает с понятным сообщением.</summary>
        public static SignGeometry Sign(string number, string sizeClass)
        {
            var definition = Config.Catalog.Find(number);
            if (definition == null) throw new InvalidOperationException("Нет знака " + number + " в каталоге.");
            var generator = new SignGenerator(Config);
            var result = generator.Generate(definition, sizeClass, DemoRunner.DefaultParameters(definition));
            if (!result.IsSuccess) throw new InvalidOperationException("Не сгенерирован знак " + number + " " + sizeClass);
            return result.Geometry!;
        }

        /// <summary>Строит DXF одного знака.</summary>
        public static DxfBackend Build(string number, string sizeClass) =>
            SignDxfWriter.Build(Sign(number, sizeClass), Config.Drawing, Config.Typography);

        /// <summary>Временная папка для файловых проверок.</summary>
        public static string TempDir()
        {
            var path = IoPath.Combine(IoPath.GetTempPath(), "odd-dxf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <summary>Структура DXF-файла: заголовок, таблицы, сущности (M13-D01…D06, D11, D12, D14).</summary>
    public sealed class DxfWriterTests
    {
        [Fact(DisplayName = "M13-D01 DXF R12: HEADER с AC1009, ENDSEC, EOF, файл ASCII")]
        public void D01_Structure()
        {
            var text = Stand.Build("3.1", "I").Document.Build();
            TestAssert.True(text.StartsWith("0\nSECTION\n2\nHEADER\n", StringComparison.Ordinal),
                "DXF должен начинаться секцией HEADER");
            TestAssert.True(text.EndsWith("0\nEOF\n", StringComparison.Ordinal), "DXF должен кончаться EOF");
            TestAssert.Equal("AC1009", DxfPairs.HeaderVariable(text, "$ACADVER"));
            TestAssert.Equal("ANSI_1251", DxfPairs.HeaderVariable(text, "$DWGCODEPAGE"));
            var nonAscii = text.Count(c => c > 127);
            TestAssert.Equal(0, nonAscii, "файл должен быть ASCII: кириллица идёт через \\U+XXXX");
            var pairs = DxfPairs.Parse(text);
            TestAssert.True(pairs.Count > 20, "в файле должно быть больше 20 групповых пар");
            foreach (var pair in pairs) TestAssert.True(pair.Value != null, "значение пары не должно быть null");
        }

        [Fact(DisplayName = "M13-D02 Каждый слой сущностей объявлен в таблице LAYER")]
        public void D02_LayersDeclared()
        {
            var text = Stand.Build("2.1", "II").Document.Build();
            var declared = DxfPairs.LayerNames(text);
            var used = DxfPairs.UsedLayers(text);
            TestAssert.True(declared.Count >= 4, "у знака должно быть минимум 4 слоя, получено " + declared.Count);
            TestAssert.Contains("SIGN.Field", string.Join(",", declared), "поле знака — отдельный слой");
            foreach (var layer in used)
                TestAssert.True(declared.Contains(layer), "слой «" + layer + "» используется, но не объявлен");
            TestAssert.Contains("SIGN.Outline", string.Join(",", declared), "слой контура с префиксом знака");
        }

        [Fact(DisplayName = "M13-D03 Знак уходит в DXF заливкой, дугами и текстом; цвета из каталога")]
        public void D03_EntitiesAndColors()
        {
            var backend = Stand.Build("3.1", "I");
            var types = DxfPairs.EntityTypes(backend.Document.Build());
            TestAssert.True(types.Contains("SOLID"), "поле и кайма должны быть залиты (SOLID)");
            TestAssert.True(types.Contains("ARC"), "круг знака 3.1 должен содержать дуги");
            TestAssert.True(types.Contains("TEXT"), "надпись параметра должна быть текстом");
            var layers = backend.Layers.Select(l => l.Name).ToList();
            TestAssert.True(layers.Contains("SIGN.Field"), "слой поля объявлен");
            var field = backend.Layers.First(l => l.Name == "SIGN.Field");
            TestAssert.Equal(DxfColorMap.Aci(Stand.Sign("3.1", "I").Sign.Colors["field"]), field.ColorIndex,
                "цвет поля берётся из каталога через палитру ACI");
            TestAssert.True(DxfColorMap.IsKnown("Red"), "красный цвет должен быть в палитре стенда");
            TestAssert.Equal(255, DxfColorMap.Aci("White"), "белое поле — ACI 255");
        }

        [Fact(DisplayName = "M13-D04 Вся геометрия DXF внутри габарита знака")]
        public void D04_InsideBounds()
        {
            foreach (var pair in new[] { ("3.1", "I"), ("2.1", "II"), ("2.5", "II"), ("2.4", "II") })
            {
                var geometry = Stand.Sign(pair.Item1, pair.Item2);
                var backend = SignDxfWriter.Build(geometry, Stand.Config.Drawing, Stand.Config.Typography);
                var bounds = backend.Document.Bounds();
                TestAssert.NotNull(bounds, "у непустого чертежа есть габарит");
                const double tolerance = 0.01;
                var nominal = geometry.Bounds;
                TestAssert.True(bounds!.Value.MinX >= nominal.MinX - tolerance,
                    "X слева выходит за габарит: " + pair.Item1);
                TestAssert.True(bounds.Value.MinY >= nominal.MinY - tolerance,
                    "Y снизу выходит за габарит: " + pair.Item1);
                TestAssert.True(bounds.Value.MaxX <= nominal.MaxX + tolerance,
                    "X справа выходит за габарит: " + pair.Item1);
                TestAssert.True(bounds.Value.MaxY <= nominal.MaxY + tolerance,
                    "Y сверху выходит за габарит: " + pair.Item1);
                if (pair.Item1 == "3.1")
                {
                    // Круг: контур совпадает с номинальным габаритом с точностью аппроксимации.
                    TestAssert.Close(nominal.Width, bounds.Value.Width, 0.5, "ширина круга");
                    TestAssert.Close(nominal.Height, bounds.Value.Height, 0.5, "высота круга");
                }
            }
        }

        [Fact(DisplayName = "M13-D05 Детерминизм: два прогона дают побайтно одинаковый DXF")]
        public void D05_Determinism()
        {
            var first = Stand.Build("3.24", "II").Document.Build();
            var second = Stand.Build("3.24", "II").Document.Build();
            TestAssert.Equal(first.Length, second.Length, "длина файла должна совпадать");
            TestAssert.True(string.Equals(first, second, StringComparison.Ordinal),
                "два прогона должны давать одинаковый текст");
            var dir = Stand.TempDir();
            try
            {
                var pathA = IoPath.Combine(dir, "a.dxf");
                var pathB = IoPath.Combine(dir, "b.dxf");
                var backendA = Stand.Build("3.24", "II");
                var backendB = Stand.Build("3.24", "II");
                backendA.SaveDocument(pathA);
                backendB.SaveDocument(pathB);
                var bytesA = File.ReadAllBytes(pathA);
                var bytesB = File.ReadAllBytes(pathB);
                TestAssert.Equal(bytesA.Length, bytesB.Length, "размеры файлов должны совпадать");
                TestAssert.True(bytesA.SequenceEqual(bytesB), "байты файлов должны совпадать");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact(DisplayName = "M13-D06 Кириллица кодируется \\U+XXXX и декодируется обратно")]
        public void D06_TextCodec()
        {
            var encoded = DxfTextCodec.Encode("ЧЕРНОВИК: 40 м");
            TestAssert.Contains("\\U+0427", encoded, "Ч кодируется как \\U+0427");
            TestAssert.NotContains("Ч", encoded, "в файле не должно быть не-ASCII символов");
            TestAssert.Equal("ЧЕРНОВИК: 40 м", DxfTextCodec.Decode(encoded));
            TestAssert.Equal("plain", DxfTextCodec.Encode("plain"));
            TestAssert.Equal("a  b", DxfTextCodec.Encode("a\r\nb"), "каждый перевод строки становится пробелом");

            var text = Stand.Build("3.1", "I").Document.Build();
            var texts = DxfPairs.Texts(text);
            TestAssert.True(texts.All(t => t.All(c => c < 128)), "все тексты в файле — ASCII");
            var decoded = texts.Select(DxfTextCodec.Decode).ToList();
            TestAssert.True(decoded.Any(t => t.Contains("ЧЕРНОВИК", StringComparison.Ordinal)),
                "баннер ЧЕРНОВИК должен быть в чертеже: " + string.Join(" | ", decoded));
        }

        [Fact(DisplayName = "M13-D11 Развёртка дуги любого знака превращается в пару 50/51 против часовой")]
        public void D11_ArcSweep()
        {
            var document = new DxfDocument();
            document.AddArc(new Point2(0, 0), 10, 90, -45, "T");
            var text = document.Build();
            var pairs = DxfPairs.Parse(text);
            var start = pairs.First(p => p.Key == 50).Value;
            var end = pairs.First(p => p.Key == 51).Value;
            TestAssert.Equal("45", start, "начало дуги с отрицательной развёрткой: 90 + (-45)");
            TestAssert.Equal("90", end, "конец дуги — исходный угол");
        }

        [Fact(DisplayName = "M13-D12 Недопустимое имя слоя — исключение, а не битый DXF")]
        public void D12_LayerNameValidation()
        {
            var document = new DxfDocument();
            TestAssert.Throws<ArgumentException>(() => document.AddLayer("плохой слой", 7));
            TestAssert.Throws<ArgumentException>(() => document.AddLine(new Point2(0, 0), new Point2(1, 1), "a/b"));
            TestAssert.Throws<ArgumentException>(() => document.AddLayer("  ", 7));
        }

        [Fact(DisplayName = "M13-D14 Ошибочные допуски и радиусы отсекаются исключениями")]
        public void D14_InvalidArguments()
        {
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new DxfBackend(0));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new DxfBackend(-1));
            var document = new DxfDocument();
            TestAssert.Throws<ArgumentOutOfRangeException>(() => document.AddArc(new Point2(0, 0), 0, 0, 90, "T"));
            TestAssert.Throws<ArgumentOutOfRangeException>(() =>
                document.AddText("x", new Point2(0, 0), 0, "T"));
        }
    }
}
