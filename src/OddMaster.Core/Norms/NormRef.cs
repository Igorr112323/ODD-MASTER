using System;

namespace OddMaster.Core.Norms
{
    /// <summary>Ссылка на норму: документ, пункт, таблица, рисунок.</summary>
    public sealed class NormRef
    {
        /// <summary>Создаёт ссылку на норму.</summary>
        public NormRef(string doc, string clause, string? table = null, string? figure = null)
        {
            Doc = doc ?? throw new ArgumentNullException(nameof(doc));
            Clause = clause ?? throw new ArgumentNullException(nameof(clause));
            Table = table;
            Figure = figure;
        }

        /// <summary>Обозначение документа, например «ГОСТ Р 52290-2024».</summary>
        public string Doc { get; }

        /// <summary>Пункт стандарта, например «п. 3.13; прил. Д».</summary>
        public string Clause { get; }

        /// <summary>Таблица, если есть.</summary>
        public string? Table { get; }

        /// <summary>Рисунок, если есть.</summary>
        public string? Figure { get; }

        /// <summary>Читаемое описание ссылки для сообщений пользователю.</summary>
        public string Describe()
        {
            var text = Doc + ", " + Clause;
            if (Table != null) text += ", табл. " + Table;
            if (Figure != null) text += ", рис. " + Figure;
            return text;
        }
    }
}
