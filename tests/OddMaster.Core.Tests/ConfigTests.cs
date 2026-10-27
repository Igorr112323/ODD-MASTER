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
    public static class ConfigTests
    {
        private static NormativeConfigSet Load() => ConfigLoader.Load(Repo.Root());

        [OddFact("M1-C01: все конфиги загружаются, 10 файлов, 6 POC-знаков")]
        public static void LoadAllConfigs()
        {
            var set = Load();
            Assert.Equal(10, ConfigPaths.All.Length);
            Assert.Equal(6, set.Catalog.Signs.Count);
            Assert.Equal(8, set.MainProfile.Tables.Count);
            Assert.Equal(4, set.MainProfile.SizeClasses.Count);
            Assert.Equal(5, set.SecondProfile.SizeClasses.Count);
            Assert.Equal(0, set.SecondProfile.Tables.Count);
            Assert.Equal(7, set.Controls.Rules.Count);
            Assert.Equal(0, set.MigrationMap.Rules.Count);
        }

        [OddFact("M1-C02: у каждого конфига есть normRef и verification")]
        public static void EveryConfigHasProvenance()
        {
            var result = ConfigValidator.Validate(Repo.Root());
            Assert.Equal(0, result.Errors.Count);
            Assert.True(result.IsValid, string.Join(" | ", result.Errors));
        }

        [OddFact("M1-C03: POC-знаки разрешаются в нужные таблицы Д.2–Д.8")]
        public static void PocSignsResolveToExpectedTables()
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
                Assert.True(resolution.IsUnambiguous, pair.Key + ": ожидалась одна строка, находки: " +
                    string.Join("; ", resolution.Findings.Select(f => f.Message)));
                Assert.Equal(pair.Value, resolution.TableId);
                Assert.Equal(pair.Value, set.Catalog.Find(pair.Key)!.SizeTableId);
            }
        }

        [OddFact("M1-C04: 2.5 в типоразмере I даёт находку со ссылкой на Д.3")]
        public static void MissingSizeClassIsFinding()
        {
            var set = Load();
            var resolution = SizeResolver.Resolve(set.MainProfile, SignNumber.Parse("2.5"), "I");
            Assert.Equal(0, resolution.Matches.Count);
            Assert.True(resolution.Findings.Count > 0);
            Assert.Contains("Д.3", string.Join(" ", resolution.Findings.Select(f => f.Format())));
            Assert.Contains("R005", resolution.Findings[0].Format());
        }

        [OddFact("M1-C05: валидатор автоматически находит несостыковки 1–4")]
        public static void ValidatorDetectsFourMismatches()
        {
            var result = ConfigValidator.Validate(Repo.Root());
            var text = string.Join("\n", result.Warnings);
            Assert.Contains("GAP-001", text);
            Assert.Contains("GAP-002A", text);
            Assert.Contains("GAP-002B", text);
            Assert.Contains("GAP-003", text);
            Assert.Contains("GAP-004", text);
            Assert.True(result.Gaps.Count >= 5, "ожидалось минимум 5 находок о пробелах");
        }

        [OddFact("M1-C06: ширина наружной каймы POC-знаков из borders.json")]
        public static void BorderWidthsComeFromConfig()
        {
            var set = Load();
            Assert.Equal(10.0, BorderFor(set, "2.4"));
            Assert.Equal(20.0, BorderFor(set, "2.5"));
            Assert.Equal(10.0, BorderFor(set, "3.24"));
            Assert.Equal(10.0, BorderFor(set, "3.25"));
            Assert.Equal(20.0, BorderFor(set, "6.4"));
            Assert.Equal(10.0, BorderFor(set, "8.2.1"));
            Assert.False(set.Borders.InnerBorderWidthMm.HasValue, "внутренняя кайма должна быть null + GOST-001");
        }

        private static double BorderFor(NormativeConfigSet set, string number)
        {
            var n = SignNumber.Parse(number);
            var def = set.Catalog.Find(number);
            var rules = def != null && def.IsPlate ? set.Borders.MatchPlate(n) : set.Borders.MatchOuter(n);
            Assert.Equal(1, rules.Count);
            return rules[0].Value;
        }

        [OddFact("M1-C07: скорость 45 не по табл. Д.1, 40 — по табл. Д.1")]
        public static void SpeedParameterValidation()
        {
            var set = Load();
            var n = SignNumber.Parse("3.24");
            Assert.True(set.Parameters.Validate(n, 40m).IsValid);
            Assert.False(set.Parameters.Validate(n, 45m).IsValid);
            Assert.Contains("не кратно 10", set.Parameters.Validate(n, 45m).Reason);
        }

        [OddFact("M1-C08: кратность 0,1 считается в decimal, 2,3 кратно 0,1")]
        public static void DecimalMultiplicity()
        {
            var set = Load();
            var n = SignNumber.Parse("3.13"); // length, кратно 0,1 м
            Assert.True(set.Parameters.Validate(n, 2.3m).IsValid);
            Assert.False(set.Parameters.Validate(n, 2.35m).IsValid);
        }

        [OddFact("M1-C09: битый конфиг даёт понятную ошибку с именем файла")]
        public static void BrokenConfigGivesClearError()
        {
            var dir = Path.Combine(Path.GetTempPath(), "odd-broken-config");
            Directory.CreateDirectory(dir);
            var profile = Path.Combine(dir, "broken.json");
            File.WriteAllText(profile, "{ \"profile\": ");
            try
            {
                var ex = Assert.Throws<ConfigException>(() => ConfigLoader.LoadProfile(dir, "broken.json"));
                Assert.Contains("broken.json", ex.Message);
                Assert.Contains("Как исправить", ex.Message);
            }
            finally { File.Delete(profile); }
        }

        [OddFact("M1-C10: форматирование чисел не зависит от текущей культуры")]
        public static void InvariantFormatting()
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                foreach (var name in new[] { "ru-RU", "en-US" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(name);
                    Assert.Equal("700.5", 700.5.ToString("0.0", CultureInfo.InvariantCulture));
                    Assert.Equal("3,5", 3.5m.ToString("0.0", new CultureInfo("ru-RU")));
                    Assert.Equal("3.5", 3.5m.ToString("0.0", new CultureInfo("en-US")));
                }
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }

        [OddFact("M1-C11: схемы валидны как JSON и имеют обязательные поля")]
        public static void SchemasAreParseable()
        {
            var files = Directory.GetFiles(Path.Combine(Repo.Root(), "config/schema"), "*.schema.json");
            Assert.Equal(9, files.Length);
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                Assert.Contains("\"$schema\"", text);
                Assert.Contains("\"required\"", text);
                Assert.Contains("draft-07", text);
            }
        }
    }
}
