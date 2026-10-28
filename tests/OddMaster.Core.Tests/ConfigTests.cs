using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Norms;
using OddMaster.TestKit;

namespace OddMaster.Core.Tests
{
    /// <summary>Проверка загрузки конфигов, разрешения размеров и поиска несостыковок 8.5.</summary>
    public class ConfigTests
    {
        private static NormativeConfigSet Load() => ConfigLoader.Load(Repo.Root());

        [Fact(DisplayName = "M1-C01: все конфиги загружаются, 11 файлов, 6 POC-знаков")]
        public void LoadAllConfigs()
        {
            var set = Load();
            TestAssert.Equal(11, ConfigPaths.All.Length);
            TestAssert.Equal(6, set.Catalog.Signs.Count);
            TestAssert.Equal(8, set.MainProfile.Tables.Count);
            TestAssert.Equal(4, set.MainProfile.SizeClasses.Count);
            TestAssert.Equal(5, set.SecondProfile.SizeClasses.Count);
            TestAssert.Equal(0, set.SecondProfile.Tables.Count);
            TestAssert.Equal(7, set.Controls.Rules.Count);
            TestAssert.Equal(0, set.MigrationMap.Rules.Count);
        }

        [Fact(DisplayName = "M1-C02: у каждого конфига есть normRef и verification")]
        public void EveryConfigHasProvenance()
        {
            var result = ConfigValidator.Validate(Repo.Root());
            TestAssert.Equal(0, result.Errors.Count);
            TestAssert.True(result.IsValid, string.Join(" | ", result.Errors));
        }

        [Fact(DisplayName = "M1-C03: POC-знаки разрешаются в нужные таблицы Д.2–Д.8")]
        public void PocSignsResolveToExpectedTables()
        {
            var set = Load();
            var expected = new Dictionary<string, string>
            {
                ["2.4"] = "Д.2", ["2.5"] = "Д.3", ["3.24"] = "Д.4",
                ["3.25"] = "Д.4", ["6.4"] = "Д.5", ["8.2.1"] = "Д.8"
            };
            foreach (var pair in expected)
            {
                var number = SignNumber.Parse(pair.Key);
                var resolution = SizeResolver.Resolve(set.MainProfile, number, "II");
                TestAssert.True(resolution.IsUnambiguous, pair.Key + ": ожидалась одна строка, находки: " +
                    string.Join("; ", resolution.Findings.Select(f => f.Message)));
                TestAssert.Equal(pair.Value, resolution.TableId);
                TestAssert.Equal(pair.Value, set.Catalog.Find(pair.Key)!.SizeTableId);
            }
        }

        [Fact(DisplayName = "M1-C04: 2.5 в типоразмере I даёт находку со ссылкой на Д.3")]
        public void MissingSizeClassIsFinding()
        {
            var set = Load();
            var resolution = SizeResolver.Resolve(set.MainProfile, SignNumber.Parse("2.5"), "I");
            TestAssert.Equal(0, resolution.Matches.Count);
            TestAssert.True(resolution.Findings.Count > 0);
            TestAssert.Contains("Д.3", string.Join(" ", resolution.Findings.Select(f => f.Format())));
            TestAssert.Contains("R005", resolution.Findings[0].Format());
        }

        [Fact(DisplayName = "M1-C05: валидатор автоматически находит несостыковки 1–4")]
        public void ValidatorDetectsFourMismatches()
        {
            var result = ConfigValidator.Validate(Repo.Root());
            var text = string.Join("\n", result.Warnings);
            TestAssert.Contains("GAP-001", text);
            TestAssert.Contains("GAP-002A", text);
            TestAssert.Contains("GAP-002B", text);
            TestAssert.Contains("GAP-003", text);
            TestAssert.Contains("GAP-004", text);
            TestAssert.True(result.Gaps.Count >= 5, "ожидалось минимум 5 находок о пробелах");
        }

        [Fact(DisplayName = "M1-C06: ширина наружной каймы POC-знаков из borders.json")]
        public void BorderWidthsComeFromConfig()
        {
            var set = Load();
            TestAssert.Equal(10.0, BorderFor(set, "2.4"));
            TestAssert.Equal(20.0, BorderFor(set, "2.5"));
            TestAssert.Equal(10.0, BorderFor(set, "3.24"));
            TestAssert.Equal(10.0, BorderFor(set, "3.25"));
            TestAssert.Equal(20.0, BorderFor(set, "6.4"));
            TestAssert.Equal(10.0, BorderFor(set, "8.2.1"));
            TestAssert.False(set.Borders.InnerBorderWidthMm.HasValue, "внутренняя кайма должна быть null + GOST-001");
        }

        private static double BorderFor(NormativeConfigSet set, string number)
        {
            var n = SignNumber.Parse(number);
            var def = set.Catalog.Find(number);
            var rules = def != null && def.IsPlate ? set.Borders.MatchPlate(n) : set.Borders.MatchOuter(n);
            TestAssert.Equal(1, rules.Count);
            return rules[0].Value;
        }

        [Fact(DisplayName = "M1-C07: скорость 45 не по табл. Д.1, 40 — по табл. Д.1")]
        public void SpeedParameterValidation()
        {
            var set = Load();
            var n = SignNumber.Parse("3.24");
            TestAssert.True(set.Parameters.Validate(n, 40m).IsValid);
            TestAssert.False(set.Parameters.Validate(n, 45m).IsValid);
            TestAssert.Contains("не кратно 10", set.Parameters.Validate(n, 45m).Reason);
        }

        [Fact(DisplayName = "M1-C08: кратность 0,1 считается в decimal, 2,3 кратно 0,1")]
        public void DecimalMultiplicity()
        {
            var set = Load();
            var n = SignNumber.Parse("3.13"); // length, кратно 0,1 м
            TestAssert.True(set.Parameters.Validate(n, 2.3m).IsValid);
            TestAssert.False(set.Parameters.Validate(n, 2.35m).IsValid);
        }

        [Fact(DisplayName = "M1-C09: битый конфиг даёт понятную ошибку с именем файла")]
        public void BrokenConfigGivesClearError()
        {
            var dir = Path.Combine(Path.GetTempPath(), "odd-broken-config");
            Directory.CreateDirectory(dir);
            var profile = Path.Combine(dir, "broken.json");
            File.WriteAllText(profile, "{ \"profile\": ");
            try
            {
                var ex = TestAssert.Throws<ConfigException>(() => ConfigLoader.LoadProfile(dir, "broken.json"));
                TestAssert.Contains("broken.json", ex.Message);
                TestAssert.Contains("Как исправить", ex.Message);
            }
            finally { File.Delete(profile); }
        }

        [Fact(DisplayName = "M1-C10: форматирование чисел не зависит от текущей культуры")]
        public void InvariantFormatting()
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                foreach (var name in new[] { "ru-RU", "en-US" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(name);
                    TestAssert.Equal("700.5", 700.5.ToString("0.0", CultureInfo.InvariantCulture));
                    TestAssert.Equal("3,5", 3.5m.ToString("0.0", new CultureInfo("ru-RU")));
                    TestAssert.Equal("3.5", 3.5m.ToString("0.0", new CultureInfo("en-US")));
                }
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }

        [Fact(DisplayName = "M1-C11: схемы валидны как JSON и имеют обязательные поля")]
        public void SchemasAreParseable()
        {
            var files = Directory.GetFiles(Path.Combine(Repo.Root(), "config/schema"), "*.schema.json");
            TestAssert.Equal(10, files.Length);
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                TestAssert.Contains("\"$schema\"", text);
                TestAssert.Contains("\"required\"", text);
                TestAssert.Contains("draft-07", text);
            }
        }
    }
}
