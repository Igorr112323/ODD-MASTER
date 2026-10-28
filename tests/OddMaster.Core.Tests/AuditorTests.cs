using Xunit;
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
    public class AuditorTests
    {
        private static (AuditRequest Request, AuditReport Report) Fixture()
        {
            var csv = Repo.ReadText("tests/fixtures/checks/legacy-scheme.csv");
            var request = AuditCsvParser.Parse(csv, "ДЕМО: чужая схема 2004");
            var report = new SchemeAuditor(ConfigLoader.Load(Repo.Root())).Audit(request);
            return (request, report);
        }

        [Fact(DisplayName = "M6-C01: CSV-фикстура разбирается: 11 записей, BOM и запятая в дроби")]
        public void FixtureParses()
        {
            var (request, _) = Fixture();
            TestAssert.Equal(12, request.Entries.Count);
            TestAssert.Equal(2.35m, request.Entries.First(e => e.Id == "E09").ParameterValue!.Value);
            TestAssert.Equal("справа", request.Entries.First(e => e.Id == "E01").Side);
        }

        [Fact(DisplayName = "M6-C02: отчёт содержит находки всех правил R001–R007")]
        public void AllRulesProduceFindings()
        {
            var (_, report) = Fixture();
            var ids = report.Findings.Select(f => f.RuleId).ToHashSet();
            foreach (var id in new[] { "R001", "R002", "R003", "R004", "R005", "R006", "R007" })
                TestAssert.True(ids.Contains(id), "нет находок правила " + id);
        }

        [Fact(DisplayName = "M6-C03: находки отсортированы по пикету, затем по месту и правилу")]
        public void FindingsAreSorted()
        {
            var (_, report) = Fixture();
            var sorted = report.Findings
                .OrderBy(f => f.StationMeters)
                .ThenBy(f => f.Location, StringComparer.Ordinal)
                .ThenBy(f => f.RuleId, StringComparer.Ordinal)
                .ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                TestAssert.Equal(sorted[i].StationMeters, report.Findings[i].StationMeters);
                TestAssert.Equal(sorted[i].RuleId, report.Findings[i].RuleId);
            }
        }

        [Fact(DisplayName = "M6-C04: баннер «ЧЕРНОВИК» и пометка «не проверено по ГОСТ»")]
        public void DraftBannerAndMarker()
        {
            var (_, report) = Fixture();
            TestAssert.True(report.HasUnverified);
            TestAssert.Contains("ЧЕРНОВИК", report.Banner);
            TestAssert.Contains("не проверено по ГОСТ", report.ToMarkdown());
            TestAssert.Contains("не проверено по ГОСТ", report.ToCsv());
            TestAssert.NotContains("OddMaster.Core.Norms.NormRef", report.ToMarkdown());
            TestAssert.Contains("ГОСТ Р 52290-2024", report.ToMarkdown());
            TestAssert.True(report.Findings.Any(f => f.RuleId == "R003" && f.IsDraft && f.Marker == "GOST-017"));
        }

        [Fact(DisplayName = "M6-C05: CSV-отчёт с BOM, «;», CRLF и без времени/GUID")]
        public void AuditCsvFormat()
        {
            var (_, report) = Fixture();
            var csv = report.ToCsv();
            TestAssert.True(CsvWriter.HasBom(csv));
            TestAssert.Contains(";", csv);
            TestAssert.Contains("\r\n", csv);
            TestAssert.NotContains("2026-", csv);
            TestAssert.NotContains("guid", csv.ToLowerInvariant());
        }

        [Fact(DisplayName = "M6-C06: golden-файлы отчёта аудитора")]
        public void GoldenAuditReport()
        {
            var (_, report) = Fixture();
            Golden.CheckText("tests/OddMaster.Core.Tests/Approved/audit_report.csv", report.ToCsv());
            Golden.CheckText("tests/OddMaster.Core.Tests/Approved/audit_report.md", report.ToMarkdown());
        }

        [Fact(DisplayName = "M6-C07: дубли R001 учитывают допуск из controls.json")]
        public void DuplicateTolerance()
        {
            var (_, report) = Fixture();
            var duplicate = report.Findings.First(f => f.RuleId == "R001");
            TestAssert.Contains("3 м", duplicate.Message);
            TestAssert.Contains("допуск 5 м", duplicate.Message);
        }

        [Fact(DisplayName = "M6-C08: пустая схема даёт только глобальный пробел по кайме")]
        public void EmptySchemeGivesGlobalGap()
        {
            var report = new SchemeAuditor(ConfigLoader.Load(Repo.Root()))
                .Audit(new AuditRequest("пустая", new List<SchemeEntry>()));
            TestAssert.Equal(1, report.Findings.Count);
            TestAssert.Equal("R007", report.Findings[0].RuleId);
            TestAssert.Equal("GOST-001", report.Findings[0].Marker);
        }

        [Fact(DisplayName = "M6-C10: неверный заголовок CSV даёт понятную русскую ошибку")]
        public void BadFixtureHeaderFailsClearly()
        {
            var ex = TestAssert.Throws<Config.ConfigException>(() =>
                AuditCsvParser.Parse("id;number;sizeClass\r\nE1;3.24;II\r\n", "плохая"));
            TestAssert.Contains("Как исправить", ex.Message);
        }

        [Fact(DisplayName = "M6-C09: отчёт не зависит от текущей культуры")]
        public void CultureInvariantReport()
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
                TestAssert.Equal(results[0], results[1]);
            }
            finally { CultureInfo.CurrentCulture = saved; }
        }
    }
}
