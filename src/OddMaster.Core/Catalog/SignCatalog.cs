using System;
using System.Collections.Generic;
using System.Linq;
using OddMaster.Core.Norms;

namespace OddMaster.Core.Catalog
{
    /// <summary>Каталог знаков, загруженный из JSON. Источник истины — config/catalog/.</summary>
    public sealed class SignCatalog
    {
        private readonly Dictionary<string, SignDefinition> _byNumber;

        /// <summary>Создаёт каталог.</summary>
        public SignCatalog(string id, string title, NormRef normRef, Verification verification,
            Verification colorStatus, string colorNote, List<string> markers, List<SignDefinition> signs)
        {
            Id = id;
            Title = title;
            NormRef = normRef;
            Verification = verification;
            ColorStatus = colorStatus;
            ColorNote = colorNote;
            Markers = markers ?? throw new ArgumentNullException(nameof(markers));
            Signs = signs ?? throw new ArgumentNullException(nameof(signs));
            _byNumber = signs.ToDictionary(s => s.Number.ToString(), StringComparer.Ordinal);
        }

        /// <summary>Идентификатор каталога.</summary>
        public string Id { get; }

        /// <summary>Название.</summary>
        public string Title { get; }

        /// <summary>Ссылка на норму.</summary>
        public NormRef NormRef { get; }

        /// <summary>Статус проверки каталога.</summary>
        public Verification Verification { get; }

        /// <summary>Статус проверки цветов.</summary>
        public Verification ColorStatus { get; }

        /// <summary>Примечание о цветах.</summary>
        public string ColorNote { get; }

        /// <summary>Маркеры GOST-NNN каталога.</summary>
        public IReadOnlyList<string> Markers { get; }

        /// <summary>Все записи в порядке файла.</summary>
        public IReadOnlyList<SignDefinition> Signs { get; }

        /// <summary>Ищет знак по номеру. Отсутствие — null, а не исключение.</summary>
        public SignDefinition? Find(string number) =>
            _byNumber.TryGetValue(number, out var sign) ? sign : null;

        /// <summary>Ищет знак по номеру. Отсутствие — null, а не исключение.</summary>
        public SignDefinition? Find(SignNumber number) => Find(number.ToString());
    }
}
