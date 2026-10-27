using System.Collections.Generic;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Config
{
    /// <summary>Источник, реквизиты и область применения нормативного документа.</summary>
    public sealed class NormativeSource
    {
        /// <summary>Создаёт запись реестра.</summary>
        public NormativeSource(string id, string doc, string title, string approvedBy,
            string? effectiveFrom, string? replaces, List<string> usedFor, Verification verification)
        {
            Id = id; Doc = doc; Title = title; ApprovedBy = approvedBy; EffectiveFrom = effectiveFrom;
            Replaces = replaces; UsedFor = usedFor; Verification = verification;
        }

        /// <summary>Идентификатор.</summary>
        public string Id { get; }
        /// <summary>Обозначение документа.</summary>
        public string Doc { get; }
        /// <summary>Полное название.</summary>
        public string Title { get; }
        /// <summary>Реквизиты утверждения.</summary>
        public string ApprovedBy { get; }
        /// <summary>Дата введения.</summary>
        public string? EffectiveFrom { get; }
        /// <summary>Что заменяет.</summary>
        public string? Replaces { get; }
        /// <summary>Где используется в проекте.</summary>
        public IReadOnlyList<string> UsedFor { get; }
        /// <summary>Статус проверки реквизитов.</summary>
        public Verification Verification { get; }
    }

    /// <summary>Реестр нормативных документов (config/norms/sources.json).</summary>
    public sealed class SourceRegistry
    {
        /// <summary>Создаёт реестр.</summary>
        public SourceRegistry(NormRef normRef, Verification verification, List<NormativeSource> sources, List<string> markers)
        {
            NormRef = normRef; Verification = verification; Sources = sources; Markers = markers;
        }

        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }
        /// <summary>Статус проверки.</summary>
        public Verification Verification { get; }
        /// <summary>Документы.</summary>
        public IReadOnlyList<NormativeSource> Sources { get; }
        /// <summary>Маркеры GOST-NNN.</summary>
        public IReadOnlyList<string> Markers { get; }
    }

    /// <summary>Варианты исполнения знаков (жёлтый фон, индивидуальное проектирование).</summary>
    public sealed class VariantNorms
    {
        /// <summary>Создаёт набор вариантов.</summary>
        public VariantNorms(NormRef normRef, Verification verification, SignNumberSet yellowBackgroundAllowed,
            SignNumberSet individualDesign, string sign64OptionalSymbols, List<string> markers)
        {
            NormRef = normRef; Verification = verification; YellowBackgroundAllowed = yellowBackgroundAllowed;
            IndividualDesign = individualDesign; Sign64OptionalSymbols = sign64OptionalSymbols; Markers = markers;
        }

        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }
        /// <summary>Статус проверки.</summary>
        public Verification Verification { get; }
        /// <summary>Знаки, допускаемые с жёлтым фоном (п. 8.4.4 / разд. 4).</summary>
        public SignNumberSet YellowBackgroundAllowed { get; }
        /// <summary>Знаки индивидуального проектирования: генератор геометрию не строит.</summary>
        public SignNumberSet IndividualDesign { get; }
        /// <summary>Необязательные символы на знаке 6.4 (п. 3.11).</summary>
        public string Sign64OptionalSymbols { get; }
        /// <summary>Маркеры GOST-NNN.</summary>
        public IReadOnlyList<string> Markers { get; }
    }

    /// <summary>Условное изображение знака на схеме (ADR-0007, статус Placeholder).</summary>
    public sealed class DrawingConventions
    {
        /// <summary>Создаёт соглашения отрисовки.</summary>
        public DrawingConventions(NormRef normRef, Verification verification, double miniSignOnPaperMm,
            double sheetScaleDenominator, double stationTickEveryM, double postSymbolRadiusMm,
            double leaderLineMm, string draftBanner, string note, List<string> markers)
        {
            NormRef = normRef; Verification = verification; MiniSignOnPaperMm = miniSignOnPaperMm;
            SheetScaleDenominator = sheetScaleDenominator; StationTickEveryM = stationTickEveryM;
            PostSymbolRadiusMm = postSymbolRadiusMm; LeaderLineMm = leaderLineMm;
            DraftBanner = draftBanner; Note = note; Markers = markers;
        }

        /// <summary>Ссылка на норму (внутреннее соглашение).</summary>
        public NormRef NormRef { get; }
        /// <summary>Статус проверки (Placeholder).</summary>
        public Verification Verification { get; }
        /// <summary>Размер миниатюры знака на бумаге, мм.</summary>
        public double MiniSignOnPaperMm { get; }
        /// <summary>Знаменатель масштаба листа.</summary>
        public double SheetScaleDenominator { get; }
        /// <summary>Шаг отметок пикетов, м.</summary>
        public double StationTickEveryM { get; }
        /// <summary>Радиус условного обозначения опоры на бумаге, мм.</summary>
        public double PostSymbolRadiusMm { get; }
        /// <summary>Длина выноски на бумаге, мм.</summary>
        public double LeaderLineMm { get; }
        /// <summary>Текст баннера «ЧЕРНОВИК/ДЕМО».</summary>
        public string DraftBanner { get; }
        /// <summary>Пояснение, почему это не норма ГОСТ.</summary>
        public string Note { get; }
        /// <summary>Маркеры GOST-NNN.</summary>
        public IReadOnlyList<string> Markers { get; }
    }
}
