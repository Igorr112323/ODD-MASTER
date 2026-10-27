using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OddMaster.Core.Alignment;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;
using OddMaster.Core.Scheme;
using OddMaster.Core.Statement;
using OddMaster.TestKit;

namespace OddMaster.Core.Tests
{
    /// <summary>Ведомость ТС ОДД (M5).</summary>
    public static class StatementTests
    {
        private static SchemeModel Scheme()
        {
            var axis = new Alignment.Alignment(new[] { new Point2(0, 0), new Point2(1000, 0) });
            var posts = new List<Post>();
            var p1 = new Post("P1", 100, 5);
            p1.Add(new SignInstance("s1", SignNumber.Parse("3.24"), "II", "speed", 40m));
            p1.Add(new SignInstance("s2", SignNumber.Parse("3.24"), "II", "speed", 40m));
            p1.Add(new SignInstance("s3", SignNumber.Parse("8.2.1"), "II", "distance", 50m));
            posts.Add(p1);
            var p2 = new Post("P2", 400, -5);
            p2.Add(new SignInstance("s4", SignNumber.Parse("3.24"), "III", "speed", 60m));
            p2.Add(new SignInstance("s5", SignNumber.Parse("3.2"), "II"));
            p2.Add(new SignInstance("s6", SignNumber.Parse("3.10"), "II"));
            posts.Add(p2);
            return new SchemeModel("statement-test", axis, posts);
        }

        private static TsoddStatement Statement() =>
            TsoddStatement.Build(Scheme(), ConfigLoader.Load(Repo.Root()).Catalog);

        [OddFact("M5-S01: группировка по номер + типоразмер + параметр")]
        public static void GroupingByNumberSizeAndParameter()
        {
            var rows = Statement().Rows;
            var s40 = rows.FirstOrDefault(r => r.Number.ToString() == "3.24" && r.Parameter == "40" && r.SizeClass == "II");
            Assert.NotNull(s40);
            Assert.Equal(2, s40!.Count);
            var s60 = rows.FirstOrDefault(r => r.Number.ToString() == "3.24" && r.Parameter == "60");
            Assert.NotNull(s60);
            Assert.Equal(1, s60!.Count);
            Assert.Equal(5, rows.Count);
        }

        [OddFact("M5-S02: натуральная сортировка 3.2 < 3.10 < 3.24")]
        public static void NaturalSort()
        {
            var numbers = new List<string>();
            foreach (var row in Statement().Rows) numbers.Add(row.Number.ToString());
            var i2 = numbers.IndexOf("3.2");
            var i10 = numbers.IndexOf("3.10");
            var i24 = numbers.IndexOf("3.24");
            Assert.True(i2 < i10 && i10 < i24, string.Join(",", numbers));
        }

        [OddFact("M5-S03: экранирование CSV: кавычки и «;» в наименовании")]
        public static void CsvEscaping()
        {
            var definition = new SignDefinition(SignNumber.Parse("3.2"),
                "Знак \"особый\"; правый", 3, CarrierShape.Circle, "Д.4", false,
                new Dictionary<string, string>(), null, null, null, null,
                new NormRef("ГОСТ Р 52290-2024", "п. 3.2"),
                Verification.CuratorExtracted, new List<string>());
            var catalog = new SignCatalog("test", "test", definition.NormRef,
                Verification.CuratorExtracted, Verification.CuratorExtracted, "", new List<string>(),
                new List<SignDefinition> { definition });
            var statement = TsoddStatement.Build(Scheme(), catalog);
            var csv = statement.ToCsv();
            Assert.Contains("\"Знак \"\"особый\"\"; правый\"", csv);
            Assert.Contains(";", csv);
        }

        [OddFact("M5-S04: CSV начинается с BOM и использует CRLF")]
        public static void CsvBomAndLineEndings()
        {
            var csv = Statement().ToCsv();
            Assert.True(CsvWriter.HasBom(csv));
            Assert.Contains("\r\n", csv);
            Assert.Contains("Номер знака по ГОСТ Р 52290-2024", csv);
        }

        [OddFact("M5-S05: пустой вход → только шапка")]
        public static void EmptyInputHeaderOnly()
        {
            var axis = new Alignment.Alignment(new[] { new Point2(0, 0), new Point2(10, 0) });
            var empty = new SchemeModel("empty", axis);
            var statement = TsoddStatement.Build(empty, ConfigLoader.Load(Repo.Root()).Catalog);
            Assert.Equal(0, statement.Rows.Count);
            Assert.Equal(7, statement.ToTableModel()[0].Count);
            Assert.Equal(1, statement.ToCsv().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).Length);
        }

        [OddFact("M5-S06: golden-файлы ведомости CSV и MD")]
        public static void GoldenStatement()
        {
            var statement = Statement();
            Golden.CheckText("tests/OddMaster.Core.Tests/Approved/statement.csv", statement.ToCsv());
            Golden.CheckText("tests/OddMaster.Core.Tests/Approved/statement.md", statement.ToMarkdown());
        }

        [OddFact("M5-S07: перечень по опорам в порядке задания")]
        public static void PostListOrder()
        {
            var posts = Statement().Posts;
            Assert.Equal(2, posts.Count);
            Assert.Equal("P1", posts[0].PostId);
            Assert.Equal("ПК1+00,00", posts[0].StationText);
            Assert.Equal("слева", posts[1].Side);
            Assert.Contains("3.24 (40)", posts[0].Signs);
        }

        [OddFact("M5-S08: CSV и Markdown не зависят от текущей культуры")]
        public static void CultureInvariantStatement()
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                var results = new List<string>();
                foreach (var name in new[] { "ru-RU", "en-US" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(name);
                    var statement = Statement();
                    results.Add(statement.ToCsv() + "|" + statement.ToMarkdown());
                }
                Assert.Equal(results[0], results[1]);
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }

        [OddFact("M5-S09: табличная модель содержит 7 колонок и все строки")]
        public static void TableModel()
        {
            var table = Statement().ToTableModel();
            Assert.Equal(7, table[0].Count);
            Assert.Equal(6, table.Count); // шапка + 5 строк
            Assert.Equal("1", table[1][0]);
        }
    }
}
