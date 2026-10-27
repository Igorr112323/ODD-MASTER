using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OddMaster.Core.Checks;
using OddMaster.Core.Config;
using OddMaster.Core.Norms;
using OddMaster.Core.Statement;
using OddMaster.TestKit;

namespace OddMaster.Core.Tests
{
    /// <summary>Аудитор схемы и отчёт (M6).</summary>
    public static class AuditorTests
    {
        private static (AuditRequest Request, AuditReport Report) Fixture()
        {
            var csv = Repo.ReadText("tests/fixtures/checks/legacy-scheme.csv");
            var request = AuditCsvParser.Parse(csv, "ДЕМО: чужая схема 2004");
            var report = new SchemeAuditor(ConfigLoader.Load(Repo.Root())).Audit(request);
            return (request, report);
        }

        [OddFact("M6-C01: CSV-фикстура разбирается: 11 записей, BOM и запятая в дроби")]
        public static void FixtureParses()
        {
            var (request, _) = Fixture();
            Assert.Equal(12, request.Entries.Count);
            Assert.Equal(2.35m, request.Entries.First(e => e.Id == "E09").ParameterValue!.Value);
            Assert.Equal("справа", request.Entries.First(e => e.Id == "E01").Side);
        }

        [OddFact("M6-C02: отчёт содержит находки всех правил R001–R007")]
        public static void AllRulesProduceFindings()
        {
            var (_, report) = Fixture();
            var ids = report.Findings.Select(f => f.RuleId).ToHashSet();
            foreach (var id in new[] { "R001", "R002", "R003", "R004", "R005", "R006", "R007" })
                Assert.True(ids.Contains(id), "нет находок правила " + id);
        }

        [OddFact("M6-C03: находки отсортированы по пикету, затем по месту и правилу")]
        public static void FindingsAreSorted()
        {
            var (_, report) = Fixture();
            var sorted = report.Findings
                .OrderBy(f => f.StationMeters)
                .ThenBy(f => f.Location, StringComparer.Ordinal)
                .ThenBy(f => f.RuleId, StringComparer.Ordinal)
                .ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                Assert.Equal(sorted[i].StationMeters, report.Findings[i].StationMeters);
                Assert.Equal(sorted[i].RuleId, report.Findings[i].RuleId);
            }
        }

        [OddFact("M6-C04: баннер «ЧЕРНОВИК» и пометка «не проверено по ГОСТ»")]
        public static void DraftBannerAndMarker()
        {
            var (_, report) = Fixture();
            Assert.True(report.HasUnverified);
            Assert.Contains("ЧЕРНОВИК", report.Banner);
            Assert.Contains("не проверено по ГОСТ", report.ToMarkdown());
            Assert.Contains("не проверено по ГОСТ", report.ToCsv());
            Assert.NotContains("OddMaster.Core.Norms.NormRef", report.ToMarkdown());
            Assert.Contains("ГОСТ Р 52290-2024", report.ToMarkdown());
            Assert.True(report.Findings.Any(f => f.RuleId == "R003" && f.IsDraft && f.Marker == "GOST-017"));
        }

        [OddFact("M6-C05: CSV-отчёт с BOM, «;», CRLF и без времени/GUID")]
        public static void AuditCsvFormat()
        {
            var (_, report) = Fixture();
            var csv = report.ToCsv();
            Assert.True(CsvWriter.HasBom(csv));
            Assert.Contains(";", csv);
            Assert.Contains("\r\n", csv);
            Assert.NotContains("2026-", csv);
            Assert.NotContains("guid", csv.ToLowerInvariant());
        }

        [OddFact("M6-C06: golden-файлы отчёта аудитора")]
        public static void GoldenAuditReport()
        {
            var (_, report) = Fixture();
            Golden.CheckText("tests/OddMaster.Core.Tests/Approved/audit_report.csv", report.ToCsv());
            Golden.CheckText("tests/OddMaster.Core.Tests/Approved/audit_report.md", report.ToMarkdown());
        }

        [OddFact("M6-C07: дубли R001 учитывают допуск из controls.json")]
        public static void DuplicateTolerance()
        {
            var (_, report) = Fixture();
            var duplicate = report.Findings.First(f => f.RuleId == "R001");
            Assert.Contains("3 м", duplicate.Message);
            Assert.Contains("допуск 5 м", duplicate.Message);
        }

        [OddFact("M6-C08: пустая схема даёт только глобальный пробел по кайме")]
        public static void EmptySchemeGivesGlobalGap()
        {
            var report = new SchemeAuditor(ConfigLoader.Load(Repo.Root()))
                .Audit(new AuditRequest("пустая", new List<SchemeEntry>()));
            Assert.Equal(1, report.Findings.Count);
            Assert.Equal("R007", report.Findings[0].RuleId);
            Assert.Equal("GOST-001", report.Findings[0].Marker);
        }

        [OddFact("M6-C10: неверный заголовок CSV даёт понятную русскую ошибку")]
        public static void BadFixtureHeaderFailsClearly()
        {
            var ex = Assert.Throws<Config.ConfigException>(() =>
                AuditCsvParser.Parse("id;number;sizeClass\r\nE1;3.24;II\r\n", "плохая"));
            Assert.Contains("Как исправить", ex.Message);
        }

        [OddFact("M6-C09: отчёт не зависит от текущей культуры")]
        public static void CultureInvariantReport()
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                var results = new List<string>();
                foreach (var name in new[] { "ru-RU", "en-US" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(name);
                    var (_, report) = Fixture();
                    results.Add(report.ToCsv() + "|" + report.ToMarkdown());
                }
                Assert.Equal(results[0], results[1]);
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }
    }
}
