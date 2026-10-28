using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Generation;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Checks
{
    /// <summary>Аудитор схемы: проверки R001–R007 берутся из config/rules/controls.json.</summary>
    public sealed class SchemeAuditor
    {
        private readonly NormativeConfigSet _config;
        private readonly SignGenerator _generator;

        /// <summary>Создаёт аудитора.</summary>
        public SchemeAuditor(NormativeConfigSet config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _generator = new SignGenerator(config);
        }

        /// <summary>Проверяет схему и возвращает отсортированный отчёт.</summary>
        public AuditReport Audit(AuditRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var findings = new List<AuditFinding>();
            foreach (var entry in request.Entries)
            {
                var sizeOk = CheckSize(entry, findings);   // R005
                CheckParameter(entry, findings, sizeOk);   // R006
                CheckGaps(entry, findings);                // R007
                CheckCorridor(entry, findings);            // R004
                CheckPlateWithoutSign(entry, request, findings); // R002
            }
            CheckDuplicates(request, findings);  // R001
            CheckPairing(request, findings);     // R003
            CheckGlobalGaps(findings);           // R007
            return new AuditReport(request.Name, findings);
        }

        private ControlRule? Rule(string id)
        {
            var rule = _config.Controls.Find(id);
            return rule != null && rule.Enabled ? rule : null;
        }

        private void Add(List<AuditFinding> findings, string ruleId, SchemeEntry entry, string message)
        {
            var rule = Rule(ruleId);
            if (rule == null) return;
            findings.Add(new AuditFinding(ruleId, rule.Severity, entry.StationMeters,
                entry.Location, message, rule.NormRef, rule.Verification));
        }

        private bool CheckSize(SchemeEntry entry, List<AuditFinding> findings)
        {
            var resolution = SizeResolver.Resolve(_config.MainProfile, entry.Number, entry.SizeClass);
            if (resolution.IsUnambiguous) return true;
            var details = string.Join("; ", resolution.Findings.Select(f => f.Message));
            Add(findings, "R005", entry,
                "Типоразмер «" + entry.SizeClass + "» для знака " + entry.Number + " не разрешён: " + details);
            return false;
        }

        private void CheckParameter(SchemeEntry entry, List<AuditFinding> findings, bool sizeOk)
        {
            var definition = _config.Catalog.Find(entry.Number);
            // CR-12: одна находка — одна запись. Если типоразмер уже не разрешён (R005)
            // или знака нет в каталоге (R007), генератор не дёргаем: он добавил бы дубль R006.
            if (!sizeOk || definition == null) return;
            var kind = entry.ParameterKind ?? definition?.ParameterKind;
            if (kind != null && !entry.ParameterValue.HasValue)
            {
                Add(findings, "R006", entry, "Не задано значение параметра «" + kind + "».");
                return;
            }
            if (entry.ParameterValue.HasValue)
            {
                var check = _config.Parameters.Validate(entry.Number, entry.ParameterValue.Value);
                if (!check.IsValid)
                    Add(findings, "R006", entry, check.Reason);
                else if (!check.HasRule)
                    Add(findings, "R007", entry, check.Reason);
            }
            var parameters = entry.ParameterValue.HasValue && kind != null
                ? new Dictionary<string, decimal> { [kind] = entry.ParameterValue.Value }
                : null;
            var generated = _generator.Generate(entry.Number.ToString(), entry.SizeClass, parameters);
            if (!generated.IsSuccess)
                Add(findings, "R006", entry,
                    "Генератор не построил знак: " + string.Join("; ", generated.Findings.Select(f => f.Message)));
        }

        private void CheckGaps(SchemeEntry entry, List<AuditFinding> findings)
        {
            var definition = _config.Catalog.Find(entry.Number);
            if (definition == null)
            {
                Add(findings, "R007", entry, "Знак " + entry.Number + " отсутствует в POC-каталоге.");
                return;
            }
            if (_config.Variants.IndividualDesign.Contains(entry.Number))
                Add(findings, "R007", entry, "Знак индивидуального проектирования — генератор его не строит.");
            var borders = definition.IsPlate
                ? _config.Borders.MatchPlate(entry.Number)
                : _config.Borders.MatchOuter(entry.Number);
            if (borders.Count != 1)
            {
                var rule = Rule("R007");
                if (rule != null)
                    findings.Add(new AuditFinding("R007", rule.Severity, entry.StationMeters, entry.Location,
                        "Для знака " + entry.Number + " найдено " + borders.Count + " правил каймы вместо одного.",
                        rule.NormRef, _config.Borders.Verification, "GOST-008"));
            }
            if (!_config.MainProfile.Tables.Any(t => t.Id == definition.SizeTableId))
                Add(findings, "R007", entry, "В профиле нет таблицы " + definition.SizeTableId + ".");
        }

        private void CheckCorridor(SchemeEntry entry, List<AuditFinding> findings)
        {
            var rule = Rule("R004");
            if (rule == null) return;
            var corridor = rule.Param("corridorM", 50.0);
            if (Math.Abs(entry.OffsetMeters) > corridor)
                Add(findings, "R004", entry,
                    "Смещение " + entry.OffsetMeters.ToString("0.##", CultureInfo.InvariantCulture) +
                    " м вне коридора ±" + corridor.ToString("0.##", CultureInfo.InvariantCulture) + " м.");
            var right = string.Equals(entry.Side, "справа", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Side, "right", StringComparison.OrdinalIgnoreCase);
            if (right && entry.OffsetMeters < 0 || !right && entry.OffsetMeters > 0)
                Add(findings, "R004", entry,
                    "Противоречие: сторона «" + entry.Side + "» и смещение " +
                    entry.OffsetMeters.ToString("0.##", CultureInfo.InvariantCulture) + " м.");
        }

        private void CheckPlateWithoutSign(SchemeEntry entry, AuditRequest request, List<AuditFinding> findings)
        {
            if (entry.Number.Group != 8) return;
            var tolerance = Rule("R002")?.Param("toleranceM", 0.001) ?? 0.001;
            var hasMain = request.Entries.Any(e => e != entry && e.Number.Group != 8
                && string.Equals(e.Side, entry.Side, StringComparison.Ordinal)
                && Math.Abs(e.StationMeters - entry.StationMeters) <= tolerance);
            if (!hasMain)
                Add(findings, "R002", entry,
                    "Табличка " + entry.Number + " без основного знака на той же опоре.");
        }

        private void CheckDuplicates(AuditRequest request, List<AuditFinding> findings)
        {
            var rule = Rule("R001");
            if (rule == null) return;
            var tolerance = rule.Param("toleranceM", 5.0);
            for (int i = 0; i < request.Entries.Count; i++)
                for (int j = i + 1; j < request.Entries.Count; j++)
                {
                    var a = request.Entries[i];
                    var b = request.Entries[j];
                    if (a.Number.CompareTo(b.Number) != 0) continue;
                    if (!string.Equals(a.Side, b.Side, StringComparison.Ordinal)) continue;
                    if (a.ParameterValue != b.ParameterValue) continue;
                    if (Math.Abs(a.StationMeters - b.StationMeters) > tolerance) continue;
                    Add(findings, "R001", b,
                        "Дубль записи " + b.Id + " совпадает с " + a.Id + ": тот же знак " + a.Number + " на расстоянии " +
                        Math.Abs(a.StationMeters - b.StationMeters).ToString("0.##", CultureInfo.InvariantCulture) +
                        " м (допуск " + tolerance.ToString("0.##", CultureInfo.InvariantCulture) + " м).");
                }
        }

        private void CheckPairing(AuditRequest request, List<AuditFinding> findings)
        {
            foreach (var pairing in _config.Controls.Pairing)
            {
                var starts = request.Entries
                    .Where(e => e.Number.ToString() == pairing.Start)
                    .OrderBy(e => e.StationMeters).ToList();
                foreach (var start in starts)
                {
                    var hasEnd = request.Entries.Any(e =>
                        e.StationMeters > start.StationMeters + 1e-9
                        && string.Equals(e.Side, start.Side, StringComparison.Ordinal)
                        && (e.Number.ToString() == pairing.End
                            || e.Number.ToString() == pairing.Repeat
                            || pairing.Plates.Contains(e.Number.ToString(), StringComparer.Ordinal)));
                    if (hasEnd) continue;
                    var rule = Rule("R003");
                    if (rule == null) continue;
                    findings.Add(new AuditFinding("R003", rule.Severity, start.StationMeters,
                        start.Location, "Начало зоны " + pairing.Start + " без конца " + pairing.End
                        + " / повтора / таблички.", rule.NormRef, pairing.Verification,
                        pairing.Marker ?? "GOST-017"));
                }
            }
        }

        private void CheckGlobalGaps(List<AuditFinding> findings)
        {
            if (_config.Borders.InnerBorderWidthMm.HasValue) return;
            var rule = Rule("R007");
            if (rule == null) return;
            findings.Add(new AuditFinding("R007", FindingSeverity.Info, -1,
                "config/norms/borders.json",
                "Ширина внутренней каймы неизвестна: " + _config.Borders.InnerBorderWidthNote,
                _config.Borders.NormRef, Verification.Unverified, "GOST-001"));
        }
    }
}
