using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Generation
{
    /// <summary>
    /// Параметрический генератор знаков: «каталог + профиль + типоразмер + параметры → геометрия».
    /// Нормативные проблемы возвращаются находками, а не исключениями (ADR-0016).
    /// </summary>
    public sealed class SignGenerator
    {
        private readonly NormativeConfigSet _config;

        /// <summary>Создаёт генератор. Все нормативные данные приходят через конструктор.</summary>
        public SignGenerator(NormativeConfigSet config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>Генерирует знак по номеру из каталога.</summary>
        public GenerationResult Generate(string number, string sizeClass,
            IReadOnlyDictionary<string, decimal>? parameters = null, string? profileId = null)
        {
            if (number == null) throw new ArgumentNullException(nameof(number));
            if (sizeClass == null) throw new ArgumentNullException(nameof(sizeClass));
            var findings = new List<Finding>();
            var verification = new VerificationSummary();
            var definition = _config.Catalog.Find(number);
            if (definition == null)
                return Fail(findings, verification, "R000",
                    $"Знак {number} отсутствует в каталоге {_config.Catalog.Id}.", number);
            return Generate(definition, sizeClass, parameters, profileId);
        }

        /// <summary>Генерирует знак по описанию из каталога.</summary>
        public GenerationResult Generate(SignDefinition definition, string sizeClass,
            IReadOnlyDictionary<string, decimal>? parameters = null, string? profileId = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (sizeClass == null) throw new ArgumentNullException(nameof(sizeClass));
            var findings = new List<Finding>();
            var verification = new VerificationSummary();
            verification.Add("catalog", definition.Verification);
            verification.Add("colors", _config.Catalog.ColorStatus);

            var profile = profileId == null ? _config.MainProfile : _config.ProfileById(profileId);
            if (profile == null)
                return Fail(findings, verification, "R000",
                    $"Профиль типоразмеров «{profileId}» не найден.", definition.Number.ToString());
            verification.Add("sizeProfile", profile.Verification);

            if (_config.Variants.IndividualDesign.Contains(definition.Number))
                return Fail(findings, verification, "R007",
                    $"Знак {definition.Number} индивидуального проектирования: генератор по типоразмеру его не строит.",
                    definition.Number.ToString(), _config.Variants.NormRef);

            var resolution = SizeResolver.Resolve(profile, definition.Number, sizeClass);
            findings.AddRange(resolution.Findings);
            if (!resolution.IsUnambiguous)
                return new GenerationResult(null, findings, verification);
            var match = resolution.Matches[0];
            if (match.Table.Id != definition.SizeTableId)
                findings.Add(Finding.Warning("R007",
                    $"Знак {definition.Number}: в каталоге указана таблица {definition.SizeTableId}, а найдено совпадение в {match.Table.Id}.",
                    definition.Number.ToString(), profile.NormRef, profile.Verification));

            var borderRules = definition.IsPlate
                ? _config.Borders.MatchPlate(definition.Number)
                : _config.Borders.MatchOuter(definition.Number);
            if (borderRules.Count != 1)
            {
                findings.Add(Finding.Warning("R007",
                    $"Для знака {definition.Number} найдено {borderRules.Count} правил каймы вместо одного.",
                    definition.Number.ToString(), _config.Borders.NormRef,
                    _config.Borders.Verification, "GOST-008"));
                return new GenerationResult(null, findings, verification);
            }
            var borderWidth = borderRules[0].Value;
            verification.Add("borders", _config.Borders.Verification);

            var parameterValue = ValidateParameter(definition, parameters, findings, verification);
            if (findings.Any(f => f.Severity == FindingSeverity.Error))
                return new GenerationResult(null, findings, verification);

            var builder = CarrierBuilder.FromSpec(definition.Shape, match.Spec, match.Row.CornerRadiusMm, out var problem);
            if (problem != null)
                return Fail(findings, verification, "R007",
                    $"Не удалось прочитать размеры знака {definition.Number}: {problem}.", definition.Number.ToString());

            var outer = builder.Build(0.0);
            var field = builder.Build(borderWidth);
            if (!outer.IsSuccess || !field.IsSuccess)
                return Fail(findings, verification, "R007",
                    $"Не удалось построить контур знака {definition.Number}: {outer.Problem ?? field.Problem}",
                    definition.Number.ToString(), profile.NormRef);

            var configuredInnerWidth = _config.Borders.InnerBorderWidthMm;
            var innerIsPlaceholder = !configuredInnerWidth.HasValue;
            var innerWidth = configuredInnerWidth ?? _config.Drawing.PlaceholderInnerBorderWidthMm;
            if (innerIsPlaceholder)
            {
                verification.Add("innerBorder", Verification.Placeholder);
                findings.Add(Finding.Warning("G-001",
                    "Ширина внутренней каймы неизвестна: использована заглушка из config/conventions/drawing.json.",
                    definition.Number.ToString(), _config.Borders.NormRef,
                    Verification.Placeholder, "GOST-001"));
            }
            else verification.Add("innerBorder", _config.Borders.Verification);

            var inner = builder.Build(borderWidth + innerWidth / 2);
            var layers = BuildLayers(definition, outer.Path!, field.Path!, inner.IsSuccess ? inner.Path : null,
                innerWidth, builder, findings, verification);
            var texts = BuildTexts(definition, parameterValue, builder, findings, verification);

            var bounds = builder.NominalBounds();
            var geometry = new SignGeometry(definition, sizeClass, profile.Id, match.Table.Id, bounds,
                outer.Path!, layers, texts, borderWidth, innerWidth, innerIsPlaceholder,
                parameterValue, verification, findings);
            return new GenerationResult(geometry, findings, verification);
        }

        private GenerationResult Fail(List<Finding> findings, VerificationSummary verification,
            string ruleId, string message, string location, NormRef? normRef = null)
        {
            findings.Add(Finding.Error(ruleId, message, location, normRef));
            return new GenerationResult(null, findings, verification);
        }

        private string? ValidateParameter(SignDefinition definition,
            IReadOnlyDictionary<string, decimal>? parameters, List<Finding> findings,
            VerificationSummary verification)
        {
            if (definition.ParameterKind == null) return null;
            var kind = definition.ParameterKind;
            if (parameters == null || !parameters.TryGetValue(kind, out var value))
            {
                findings.Add(Finding.Error("R006",
                    $"Для знака {definition.Number} не задан параметр «{kind}».", definition.Number.ToString(),
                    _config.Parameters.NormRef, _config.Parameters.Verification));
                return null;
            }
            verification.Add("parameters", _config.Parameters.Verification);
            var check = _config.Parameters.Validate(definition.Number, value);
            if (!check.IsValid)
            {
                findings.Add(Finding.Error("R006",
                    $"Знак {definition.Number}: {check.Reason}", definition.Number.ToString(),
                    _config.Parameters.NormRef, _config.Parameters.Verification));
            }
            else if (!check.HasRule)
            {
                findings.Add(Finding.Warning("R007", check.Reason, definition.Number.ToString(),
                    _config.Parameters.NormRef, _config.Parameters.Verification, "GOST-021"));
            }
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private List<SignLayer> BuildLayers(SignDefinition definition, Path outer, Path field,
            Path? inner, double innerWidth, CarrierBuilder builder, List<Finding> findings,
            VerificationSummary verification)
        {
            var colors = definition.Colors;
            var layers = new List<SignLayer>
            {
                new SignLayer(SignLayerKind.Outline, Color(colors, "outerBorder", "Black"), outer, true, 0),
                new SignLayer(SignLayerKind.OuterBorder, Color(colors, "field", "White"), field, true, 0)
            };
            if (inner != null)
                layers.Add(new SignLayer(SignLayerKind.InnerBorder, "Red", inner, false, innerWidth));
            else
                findings.Add(Finding.Warning("G-002",
                    "Не удалось построить контур внутренней каймы (габарит меньше каймы).",
                    definition.Number.ToString(), _config.Borders.NormRef,
                    Verification.Placeholder, "GOST-001"));

            if (definition.Pictogram != null && definition.Pictogram.Placeholder)
            {
                var minSide = Math.Min(builder.NominalBounds().Width, builder.NominalBounds().Height);
                var radius = minSide * _config.Drawing.PlaceholderPictogramRadiusRatio;
                layers.Add(new SignLayer(SignLayerKind.Pictogram,
                    Color(colors, "pictogram", "Black"), GeometryPrimitives.Circle(radius * 2), false, 1.0));
                verification.Add("pictogram", Verification.Placeholder);
                findings.Add(Finding.Warning("G-019",
                    "Пиктограмма — заглушка (окружность), реальная геометрия по прил. Б не сверена.",
                    definition.Number.ToString(), definition.NormRef,
                    Verification.Placeholder, "GOST-019"));
            }
            return layers;
        }

        private List<SignText> BuildTexts(SignDefinition definition, string? parameterValue,
            CarrierBuilder builder, List<Finding> findings, VerificationSummary verification)
        {
            var result = new List<SignText>();
            if (definition.Text == null) return result;
            var spec = definition.Text;
            if (spec == null) return result;
            string value;
            if (spec.Kind == "Fixed") value = spec.Value;
            else
            {
                if (parameterValue == null) return result;
                var template = _config.Drawing.ParameterTextTemplates.TryGetValue(spec.Value, out var t)
                    ? t : "{value}";
                value = template.Replace("{value}", parameterValue, StringComparison.Ordinal);
            }
            var minSide = Math.Min(builder.NominalBounds().Width, builder.NominalBounds().Height);
            var height = minSide * _config.Drawing.PlaceholderTextHeightRatio;
            verification.Add("textMetrics", Verification.Placeholder);
            findings.Add(Finding.Warning("G-002",
                "Высота надписи — заглушка из config/conventions/drawing.json; сверить с прил. В/Г.",
                definition.Number.ToString(), definition.NormRef,
                Verification.Placeholder, "GOST-002"));
            result.Add(new SignText(value, new Point2(0, 0), height,
                Color(definition.Colors, "text", "Black"), "Arial Bold, Liberation Sans, sans-serif",
                Verification.Placeholder));
            return result;
        }

        private static string Color(IReadOnlyDictionary<string, string> colors, string key, string fallback) =>
            colors.TryGetValue(key, out var value) && value.Length > 0 ? value : fallback;
    }
}
