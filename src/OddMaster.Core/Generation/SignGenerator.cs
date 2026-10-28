using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Geometry;
using OddMaster.Core.Norms;
using OddMaster.Core.Typography;

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

            // CR-04: внутренняя кайма рисуется только там, где она объявлена в каталоге.
            // Красное кольцо на синем 6.4, на табличке 8.2.1 и на 3.25 больше не появляется.
            var hasInnerBorder = definition.Colors.ContainsKey("innerBorder");
            double innerWidth = 0;
            bool innerIsPlaceholder = false;
            if (hasInnerBorder)
            {
                var configuredInnerWidth = _config.Borders.InnerBorderWidthMm;
                innerIsPlaceholder = !configuredInnerWidth.HasValue;
                innerWidth = configuredInnerWidth ?? _config.Drawing.PlaceholderInnerBorderWidthMm;
                if (innerIsPlaceholder)
                {
                    verification.Add("innerBorder", Verification.Placeholder);
                    findings.Add(Finding.Warning("G-001",
                        "Ширина внутренней каймы неизвестна: использована заглушка из config/conventions/drawing.json.",
                        definition.Number.ToString(), _config.Borders.NormRef,
                        Verification.Placeholder, "GOST-001"));
                }
                else verification.Add("innerBorder", _config.Borders.Verification);
            }

            // CR-16: кайма — это ПОЛОСА между двумя контурами носителя, а не линия по центру.
            // Раньше внутренняя кайма рисовалась обводкой толщиной innerWidth: в DXF у отрезка
            // нет ширины, и красная полоса исчезала. Теперь это две заливки:
            //   поле до каймы (цвет каймы) + поле после каймы (цвет поля).
            var outer = builder.Build(0.0);
            var fieldEdge = builder.Build(borderWidth);
            var fieldInset = hasInnerBorder ? builder.Build(borderWidth + innerWidth) : fieldEdge;
            if (!outer.IsSuccess || !fieldEdge.IsSuccess || !fieldInset.IsSuccess)
                return Fail(findings, verification, "R007",
                    $"Не удалось построить контур знака {definition.Number}: " +
                    (outer.Problem ?? fieldEdge.Problem ?? fieldInset.Problem),
                    definition.Number.ToString(), profile.NormRef);

            var layers = BuildLayers(definition, outer.Path!, fieldEdge.Path!, fieldInset.Path!,
                innerWidth, builder, findings, verification);
            var texts = BuildTexts(definition, parameterValue, builder, borderWidth + innerWidth,
                findings, verification);

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

        private List<SignLayer> BuildLayers(SignDefinition definition, Path outer, Path fieldEdge,
            Path fieldInset, double innerWidth, CarrierBuilder builder, List<Finding> findings,
            VerificationSummary verification)
        {
            var layers = new List<SignLayer>();
            AddLayer(layers, findings, definition, "outerBorder", SignLayerKind.Outline, outer, true, 0);
            // Полоса каймы: заливается контур поля до каймы, затем сверху — поле после каймы.
            if (innerWidth > 0)
                AddLayer(layers, findings, definition, "innerBorder", SignLayerKind.InnerBorder,
                    fieldEdge, true, 0);
            AddLayer(layers, findings, definition, "field", SignLayerKind.Field, fieldInset, true, 0);

            if (definition.Pictogram != null && definition.Pictogram.Placeholder)
            {
                var bounds = builder.NominalBounds();
                var minSide = Math.Min(bounds.Width, bounds.Height);
                var symbolColor = Color(definition.Colors, "symbol");
                if (symbolColor != null)
                {
                    var stroke = minSide * 0.012;
                    if (string.Equals(definition.Pictogram.PlaceholderShape, "diagonalBand", StringComparison.Ordinal))
                    {
                        // CR-05: 3.25 — контурные цифры и диагональная полоса из тонких линий.
                        // Пока это схематичная заглушка: оцифровка прил. Б — за студентом.
                        for (int i = -1; i <= 1; i++)
                        {
                            var offset = i * minSide * 0.055;
                            var path = new Path(new Point2(-bounds.Width * 0.30, -bounds.Height * 0.30 + offset))
                                .LineTo(new Point2(bounds.Width * 0.30, bounds.Height * 0.30 + offset));
                            layers.Add(new SignLayer(SignLayerKind.Pictogram, symbolColor, path, false, stroke));
                        }
                    }
                    else
                    {
                        var size = minSide * _config.Drawing.PlaceholderPictogramRadiusRatio * 2;
                        layers.Add(new SignLayer(SignLayerKind.Pictogram, symbolColor,
                            GeometryPrimitives.Arrow(size, definition.MirrorOf != null), false, stroke));
                    }
                }
                verification.Add("pictogram", Verification.Placeholder);
                findings.Add(Finding.Warning("G-019",
                    "Пиктограмма — заглушка («" + (definition.Pictogram.PlaceholderShape ?? "circle")
                    + "»); реальная геометрия по прил. Б не сверена.",
                    definition.Number.ToString(), definition.NormRef,
                    Verification.Placeholder, "GOST-019"));
            }
            return layers;
        }

        private void AddLayer(List<SignLayer> layers, List<Finding> findings, SignDefinition definition,
            string colorKey, SignLayerKind kind, Path path, bool filled, double strokeWidth)
        {
            var color = Color(definition.Colors, colorKey);
            if (color == null)
            {
                findings.Add(Finding.Warning("G-004",
                    "В каталоге знака " + definition.Number + " нет цвета «" + colorKey + "»: слой " + kind + " пропущен.",
                    definition.Number.ToString(), definition.NormRef, Verification.Unverified, "GOST-012"));
                return;
            }
            layers.Add(new SignLayer(kind, color, path, filled, strokeWidth));
        }

        private List<SignText> BuildTexts(SignDefinition definition, string? parameterValue,
            CarrierBuilder builder, double borderWidth, List<Finding> findings, VerificationSummary verification)
        {
            var result = new List<SignText>();
            if (definition.Text == null) return result;
            var spec = definition.Text;
            string value;
            if (spec.Kind == "Fixed") value = spec.Value;
            else
            {
                if (parameterValue == null) return result;
                var template = _config.Drawing.ParameterTextTemplates.TryGetValue(spec.Value, out var t)
                    ? t : "{value}";
                value = template.Replace("{value}", parameterValue);
            }
            var color = Color(definition.Colors, "text");
            if (color == null)
            {
                findings.Add(Finding.Warning("G-004",
                    "В каталоге знака " + definition.Number + " нет цвета «text»: надпись пропущена.",
                    definition.Number.ToString(), definition.NormRef, Verification.Unverified, "GOST-012"));
                return result;
            }

            // CR-07: кегль и якорь считает TextEngine по метрикам шрифта из конфига,
            // а не text-anchor/dominant-baseline (их в САПР нет).
            var metrics = _config.Typography;
            var fieldBox = builder.NominalBounds(borderWidth);
            var padW = fieldBox.Width * metrics.PaddingRatio;
            var padH = fieldBox.Height * metrics.PaddingRatio;
            var layout = TextEngine.Layout(value, fieldBox.Width - 2 * padW, fieldBox.Height - 2 * padH,
                metrics, new Point2(0, 0));
            verification.Add("textMetrics", metrics.Verification);
            findings.Add(Finding.Warning("G-002",
                "Метрики шрифта измерены по файлу (config/typography/fonts.json, measuredFrom), " +
                "но требуемая гарнитура и высота надписей по ГОСТ не сверены: снять с масштабной сетки прил. Б.",
                definition.Number.ToString(), definition.NormRef,
                metrics.Verification, "GOST-002"));
            result.Add(new SignText(value, new Point2(0, 0), layout.HeightMm,
                color, layout.FontFamily, metrics.Verification, layout));
            return result;
        }

        /// <summary>Цвет только из каталога. Никаких цветовых литералов в генераторе (CR-04).</summary>
        private static string? Color(IReadOnlyDictionary<string, string> colors, string key) =>
            colors.TryGetValue(key, out var value) && value.Length > 0 ? value : null;
    }
}
