namespace OddMaster.Core.Config
{
    /// <summary>Пути к файлам конфигурации относительно корня репозитория.</summary>
    public static class ConfigPaths
    {
        /// <summary>Профиль ГОСТ Р 52290-2024.</summary>
        public const string MainProfile = "config/profiles/gost-r-52290-2024.sizes.json";
        /// <summary>Профиль ГОСТ 32945-2014 (заготовка).</summary>
        public const string SecondProfile = "config/profiles/gost-32945-2014.sizes.json";
        /// <summary>Каймы.</summary>
        public const string Borders = "config/norms/borders.json";
        /// <summary>Параметры табл. Д.1.</summary>
        public const string Parameters = "config/norms/parameters.json";
        /// <summary>Варианты исполнения.</summary>
        public const string Variants = "config/norms/variants.json";
        /// <summary>Реестр источников.</summary>
        public const string Sources = "config/norms/sources.json";
        /// <summary>POC-каталог знаков.</summary>
        public const string Catalog = "config/catalog/signs.poc.json";
        /// <summary>Правила контроля.</summary>
        public const string Controls = "config/rules/controls.json";
        /// <summary>Условные изображения.</summary>
        public const string Drawing = "config/conventions/drawing.json";
        /// <summary>Боевая карта замен (пустая).</summary>
        public const string MigrationMap = "config/actualizer/map-2004-2024.json";

        /// <summary>Все пути в устойчивом порядке (для валидатора).</summary>
        public static readonly string[] All =
        {
            MainProfile, SecondProfile, Borders, Parameters, Variants, Sources,
            Catalog, Controls, Drawing, MigrationMap
        };
    }
}
