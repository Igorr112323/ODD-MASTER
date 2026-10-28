using System;
using OddMaster.Core.Actualizer;
using OddMaster.Core.Catalog;
using OddMaster.Core.Checks;

namespace OddMaster.Core.Config
{
    /// <summary>Все нормативные данные, загруженные из config/. Ядро не знает ни одного «числа из ГОСТ».</summary>
    public sealed class NormativeConfigSet
    {
        /// <summary>Создаёт набор.</summary>
        public NormativeConfigSet(
            SizeProfile mainProfile, SizeProfile secondProfile, BordersNorms borders,
            ParameterNorms parameters, VariantNorms variants, SourceRegistry sources,
            SignCatalog catalog, ControlSettings controls, DrawingConventions drawing,
            MigrationMap migrationMap, OddMaster.Core.Typography.FontMetrics typography,
            Statement.StatementTableSpec statementTable)
        {
            MainProfile = mainProfile ?? throw new ArgumentNullException(nameof(mainProfile));
            SecondProfile = secondProfile;
            Borders = borders; Parameters = parameters; Variants = variants;
            Sources = sources; Catalog = catalog; Controls = controls;
            Drawing = drawing; MigrationMap = migrationMap;
            Typography = typography ?? throw new ArgumentNullException(nameof(typography));
            StatementTable = statementTable ?? throw new ArgumentNullException(nameof(statementTable));
        }

        /// <summary>Профиль по умолчанию — ГОСТ Р 52290-2024.</summary>
        public SizeProfile MainProfile { get; }
        /// <summary>Второй профиль — ГОСТ 32945-2014 (пока пустой).</summary>
        public SizeProfile SecondProfile { get; }
        /// <summary>Каймы.</summary>
        public BordersNorms Borders { get; }
        /// <summary>Параметры на знаках.</summary>
        public ParameterNorms Parameters { get; }
        /// <summary>Варианты исполнения.</summary>
        public VariantNorms Variants { get; }
        /// <summary>Реестр документов.</summary>
        public SourceRegistry Sources { get; }
        /// <summary>Каталог знаков.</summary>
        public SignCatalog Catalog { get; }
        /// <summary>Правила контроля.</summary>
        public ControlSettings Controls { get; }
        /// <summary>Условные изображения.</summary>
        public DrawingConventions Drawing { get; }
        /// <summary>Боевая карта замен.</summary>
        public MigrationMap MigrationMap { get; }
        /// <summary>Метрики шрифта для вписывания надписей.</summary>
        public OddMaster.Core.Typography.FontMetrics Typography { get; }
        /// <summary>Форма таблицы ведомости на чертеже (черновик, GOST-011).</summary>
        public Statement.StatementTableSpec StatementTable { get; }

        /// <summary>Возвращает профиль по идентификатору.</summary>
        public SizeProfile? ProfileById(string id) =>
            MainProfile.Id == id ? MainProfile : SecondProfile.Id == id ? SecondProfile : null;
    }
}
