using System;
using System.Collections.Generic;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Statement
{
    /// <summary>Колонка таблицы ведомости: заголовок и ширина, мм чертежа.</summary>
    public sealed class StatementTableColumn
    {
        /// <summary>Создаёт колонку.</summary>
        public StatementTableColumn(string title, double widthMm, bool numeric)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            WidthMm = widthMm;
            Numeric = numeric;
        }

        /// <summary>Заголовок колонки.</summary>
        public string Title { get; }
        /// <summary>Ширина, мм.</summary>
        public double WidthMm { get; }
        /// <summary>Числовая колонка (выравнивание по правому краю — позже, см. GOST-011).</summary>
        public bool Numeric { get; }
    }

    /// <summary>
    /// Форма таблицы ведомости ТС ОДД на чертеже: колонки, высоты строк, поля.
    /// Форма — черновик (GOST-011), поэтому живёт в конфиге, а не в коде.
    /// </summary>
    public sealed class StatementTableSpec
    {
        /// <summary>Создаёт описание формы таблицы.</summary>
        public StatementTableSpec(NormRef normRef, Verification verification, string title,
            IReadOnlyList<StatementTableColumn> columns, double titleRowHeightMm, double rowHeightMm,
            double cellPaddingRatio, double textFillRatio, IReadOnlyList<string> markers, string note)
        {
            NormRef = normRef ?? throw new ArgumentNullException(nameof(normRef));
            Verification = verification;
            Title = title ?? string.Empty;
            Columns = columns ?? throw new ArgumentNullException(nameof(columns));
            TitleRowHeightMm = titleRowHeightMm;
            RowHeightMm = rowHeightMm;
            CellPaddingRatio = cellPaddingRatio;
            TextFillRatio = textFillRatio;
            Markers = markers ?? new List<string>();
            Note = note ?? string.Empty;
        }

        /// <summary>Ссылка на норму формы (черновик).</summary>
        public NormRef NormRef { get; }
        /// <summary>Статус проверки формы.</summary>
        public Verification Verification { get; }
        /// <summary>Заголовок таблицы (над шапкой).</summary>
        public string Title { get; }
        /// <summary>Колонки.</summary>
        public IReadOnlyList<StatementTableColumn> Columns { get; }
        /// <summary>Высота строки шапки, мм.</summary>
        public double TitleRowHeightMm { get; }
        /// <summary>Высота строки данных, мм.</summary>
        public double RowHeightMm { get; }
        /// <summary>Поле вокруг текста в ячейке как доля её размера.</summary>
        public double CellPaddingRatio { get; }
        /// <summary>Максимальная доля высоты ячейки, занимаемая кеглем.</summary>
        public double TextFillRatio { get; }
        /// <summary>Маркеры GOST-NNN.</summary>
        public IReadOnlyList<string> Markers { get; }
        /// <summary>Пояснение, почему форма — черновик.</summary>
        public string Note { get; }

        /// <summary>Суммарная ширина таблицы, мм.</summary>
        public double TotalWidthMm
        {
            get
            {
                var sum = 0.0;
                foreach (var column in Columns) sum += column.WidthMm;
                return sum;
            }
        }
    }
}
