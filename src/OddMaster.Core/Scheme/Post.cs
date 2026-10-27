using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OddMaster.Core.Scheme
{
    /// <summary>Опора/стойка с упорядоченными сверху вниз знаками и табличками.</summary>
    public sealed class Post
    {
        private readonly List<SignInstance> _mounts;

        /// <summary>Создаёт опору.</summary>
        public Post(string id, double stationMeters, double offsetMeters, IEnumerable<SignInstance>? mounts = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            StationMeters = stationMeters;
            OffsetMeters = offsetMeters;
            _mounts = mounts == null ? new List<SignInstance>() : new List<SignInstance>(mounts);
        }

        /// <summary>Идентификатор опоры.</summary>
        public string Id { get; }
        /// <summary>Пикет установки, м.</summary>
        public double StationMeters { get; }
        /// <summary>Смещение от оси, м: справа «+» (ADR-0015).</summary>
        public double OffsetMeters { get; }
        /// <summary>Сторона: справа/слева по ходу пикетажа.</summary>
        public string Side => OffsetMeters >= 0 ? "справа" : "слева";
        /// <summary>Знаки сверху вниз (Post.Mounts).</summary>
        public IReadOnlyList<SignInstance> Mounts => _mounts;

        /// <summary>Добавляет знак в конец списка (ниже остальных).</summary>
        public void Add(SignInstance sign) => _mounts.Add(sign ?? throw new ArgumentNullException(nameof(sign)));

        /// <summary>Список знаков через запятую в порядке сверху вниз.</summary>
        public string SignsTopDown() => string.Join(", ", _mounts.Select(m => m.Display));

        /// <summary>Ключ опоры для журнала.</summary>
        public override string ToString() => Id + "@" + StationMeters.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Модель схемы: ось и опоры.</summary>
    public sealed class SchemeModel
    {
        /// <summary>Создаёт схему.</summary>
        public SchemeModel(string id, Alignment.Alignment alignment, IEnumerable<Post>? posts = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Alignment = alignment ?? throw new ArgumentNullException(nameof(alignment));
            Posts = posts == null ? new List<Post>() : new List<Post>(posts);
        }

        /// <summary>Идентификатор схемы.</summary>
        public string Id { get; }
        /// <summary>Ось дороги.</summary>
        public Alignment.Alignment Alignment { get; }
        /// <summary>Опоры в порядке задания.</summary>
        public IReadOnlyList<Post> Posts { get; }
    }
}
