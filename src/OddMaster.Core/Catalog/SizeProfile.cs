using System;
using System.Collections.Generic;
using System.Linq;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Catalog
{
    /// <summary>Значения габаритов для одного типоразмера, например A=700 или H=350, B=700.</summary>
    public sealed class SizeSpec
    {
        private readonly Dictionary<string, double> _values;
        private readonly Dictionary<string, List<double>> _options;

        /// <summary>Создаёт набор размеров.</summary>
        public SizeSpec(Dictionary<string, double> values, Dictionary<string, List<double>> options)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>Все скалярные размеры (ключи A, B, D, H) в порядке следования в JSON.</summary>
        public IReadOnlyDictionary<string, double> Values => _values;

        /// <summary>Варианты размера, например B_options.</summary>
        public IReadOnlyDictionary<string, List<double>> Options => _options;

        /// <summary>Пытается получить размер по имени.</summary>
        public bool TryGet(string key, out double value) => _values.TryGetValue(key, out value);

        /// <summary>Получает размер по имени. Отсутствие — ошибка данных, не нормативная проблема.</summary>
        public double Get(string key)
        {
            if (!_values.TryGetValue(key, out var value))
                throw new InvalidOperationException("В строке таблицы размеров нет размерности «" + key + "».");
            return value;
        }
    }

    /// <summary>Строка таблицы размеров: список номеров и размеры по типоразмерам.</summary>
    public sealed class SizeRow
    {
        /// <summary>Создаёт строку таблицы.</summary>
        public SizeRow(int index, SignNumberSet signs, Dictionary<string, SizeSpec> sizes,
            double? cornerRadiusMm, Dictionary<string, double> classless)
        {
            Index = index;
            Signs = signs ?? throw new ArgumentNullException(nameof(signs));
            Sizes = sizes ?? throw new ArgumentNullException(nameof(sizes));
            CornerRadiusMm = cornerRadiusMm;
            Classless = classless ?? throw new ArgumentNullException(nameof(classless));
        }

        /// <summary>Номер строки (0-based) — для сообщений.</summary>
        public int Index { get; }

        /// <summary>Номера знаков, попадающих в строку.</summary>
        public SignNumberSet Signs { get; }

        /// <summary>Размеры по типоразмерам I–IV (или 1–5 во втором профиле).</summary>
        public IReadOnlyDictionary<string, SizeSpec> Sizes { get; }

        /// <summary>Радиус скругления r из объединённой ячейки (GOST-006).</summary>
        public double? CornerRadiusMm { get; }

        /// <summary>Размеры вне типоразмерной сетки (classless), например B=350.</summary>
        public IReadOnlyDictionary<string, double> Classless { get; }
    }

    /// <summary>Таблица прил. Д: Д.2–Д.9.</summary>
    public sealed class SizeTable
    {
        /// <summary>Создаёт таблицу.</summary>
        public SizeTable(string id, string figure, string shapeHint, List<string> dims, List<SizeRow> rows, List<string> notes)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Figure = figure;
            ShapeHint = shapeHint;
            Dims = dims ?? throw new ArgumentNullException(nameof(dims));
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            Notes = notes ?? throw new ArgumentNullException(nameof(notes));
        }

        /// <summary>Идентификатор таблицы, например «Д.4».</summary>
        public string Id { get; }

        /// <summary>Ссылка на рисунок, например «Д.4».</summary>
        public string Figure { get; }

        /// <summary>Подсказка о форме из выписки куратора.</summary>
        public string ShapeHint { get; }

        /// <summary>Имена размерностей: A, B, D, H, r.</summary>
        public IReadOnlyList<string> Dims { get; }

        /// <summary>Строки таблицы.</summary>
        public IReadOnlyList<SizeRow> Rows { get; }

        /// <summary>Примечания куратора к таблице.</summary>
        public IReadOnlyList<string> Notes { get; }
    }

    /// <summary>Профиль типоразмеров: 52290-2024 (I–IV) или 32945-2014 (1–5).</summary>
    public sealed class SizeProfile
    {
        /// <summary>Создаёт профиль.</summary>
        public SizeProfile(string id, string title, List<string> sizeClasses, Dictionary<string, string> sizeClassNames,
            NormRef normRef, Verification verification, List<string> clauseReferencedTables,
            List<string> markers, List<string> notes, List<SizeTable> tables)
        {
            Id = id;
            Title = title;
            SizeClasses = sizeClasses ?? throw new ArgumentNullException(nameof(sizeClasses));
            SizeClassNames = sizeClassNames ?? throw new ArgumentNullException(nameof(sizeClassNames));
            NormRef = normRef ?? throw new ArgumentNullException(nameof(normRef));
            Verification = verification;
            ClauseReferencedTables = clauseReferencedTables ?? throw new ArgumentNullException(nameof(clauseReferencedTables));
            Markers = markers ?? throw new ArgumentNullException(nameof(markers));
            Notes = notes ?? throw new ArgumentNullException(nameof(notes));
            Tables = tables ?? throw new ArgumentNullException(nameof(tables));
        }

        /// <summary>Идентификатор профиля.</summary>
        public string Id { get; }

        /// <summary>Название профиля.</summary>
        public string Title { get; }

        /// <summary>Типоразмеры профиля.</summary>
        public IReadOnlyList<string> SizeClasses { get; }

        /// <summary>Русские названия типоразмеров.</summary>
        public IReadOnlyDictionary<string, string> SizeClassNames { get; }

        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }

        /// <summary>Статус проверки данных профиля.</summary>
        public Verification Verification { get; }

        /// <summary>Таблицы, на которые ссылается пункт стандарта.</summary>
        public IReadOnlyList<string> ClauseReferencedTables { get; }

        /// <summary>Идентификаторы GOST-NNN, связанные с профилем.</summary>
        public IReadOnlyList<string> Markers { get; }

        /// <summary>Примечания.</summary>
        public IReadOnlyList<string> Notes { get; }

        /// <summary>Таблицы размеров.</summary>
        public IReadOnlyList<SizeTable> Tables { get; }

        /// <summary>Проверяет, что типоразмер существует в профиле.</summary>
        public bool HasSizeClass(string sizeClass) => SizeClasses.Any(x => string.Equals(x, sizeClass, StringComparison.Ordinal));
    }
}
