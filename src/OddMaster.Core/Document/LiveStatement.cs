using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OddMaster.Core.Catalog;
using OddMaster.Core.Norms;
using OddMaster.Core.Scheme;
using OddMaster.Core.Statement;

namespace OddMaster.Core.Document
{
    /// <summary>Изменение одной строки ведомости.</summary>
    public sealed class StatementChange
    {
        /// <summary>Создаёт изменение.</summary>
        public StatementChange(string key, int before, int after)
        {
            Key = key; Before = before; After = after;
        }

        /// <summary>Ключ группировки.</summary>
        public string Key { get; }
        /// <summary>Было.</summary>
        public int Before { get; }
        /// <summary>Стало.</summary>
        public int After { get; }
    }

    /// <summary>Дельта пересчёта ведомости на одно событие.</summary>
    public sealed class StatementDelta
    {
        /// <summary>Создаёт дельту.</summary>
        public StatementDelta(List<StatementChange> changes) { Changes = changes; }

        /// <summary>Изменённые строки.</summary>
        public IReadOnlyList<StatementChange> Changes { get; }

        /// <summary>Текстовое представление для отчёта.</summary>
        public string ToMarkdown()
        {
            var lines = new List<string> { "| Ключ | Было | Стало |", "|---|---|---|" };
            foreach (var c in Changes)
                lines.Add("| " + c.Key + " | " + c.Before.ToString(CultureInfo.InvariantCulture)
                    + " | " + c.After.ToString(CultureInfo.InvariantCulture) + " |");
            return string.Join("\n", lines) + "\n";
        }
    }

    /// <summary>Живая ведомость: инкрементальный пересчёт по событиям (P3).</summary>
    public sealed class LiveStatement
    {
        private readonly SignCatalog _catalog;
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, SignInstance> _samples = new Dictionary<string, SignInstance>(StringComparer.Ordinal);

        private LiveStatement(SignCatalog catalog) { _catalog = catalog; }

        /// <summary>Строит ведомость из схемы.</summary>
        public static LiveStatement Build(SchemeModel scheme, SignCatalog catalog)
        {
            if (scheme == null) throw new ArgumentNullException(nameof(scheme));
            var result = new LiveStatement(catalog);
            foreach (var post in scheme.Posts)
                foreach (var sign in post.Mounts)
                    result.Add(sign, 1);
            return result;
        }

        /// <summary>Применяет событие «добавлен / изменён / удалён». Сложность O(1) на ключ.</summary>
        public StatementDelta Apply(SignInstance? before, SignInstance? after)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (before != null) keys.Add(before.StatementKey);
            if (after != null) keys.Add(after.StatementKey);
            var changes = new List<StatementChange>();
            foreach (var key in keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                _counts.TryGetValue(key, out var was);
                if (before != null && before.StatementKey == key) _counts[key] = was - 1;
                if (after != null && after.StatementKey == key)
                {
                    _counts.TryGetValue(key, out var current);
                    _counts[key] = current + 1;
                    _samples[key] = after;
                }
                _counts.TryGetValue(key, out var now);
                if (now <= 0) _counts.Remove(key);
                changes.Add(new StatementChange(key, was, Math.Max(0, now)));
            }
            return new StatementDelta(changes);
        }

        private void Add(SignInstance sign, int delta)
        {
            var key = sign.StatementKey;
            _counts.TryGetValue(key, out var current);
            _counts[key] = current + delta;
            _samples[key] = sign;
        }

        /// <summary>Строки ведомости в натуральном порядке номеров.</summary>
        public IReadOnlyList<StatementRow> Rows()
        {
            var rows = new List<StatementRow>();
            int index = 1;
            foreach (var pair in _counts.OrderBy(p => SignNumber.Parse(p.Key.Split('|')[0]))
                .ThenBy(p => p.Key, StringComparer.Ordinal))
            {
                var sample = _samples[pair.Key];
                var definition = _catalog.Find(sample.Number);
                rows.Add(new StatementRow(index++, sample.Number,
                    definition?.Name ?? "Нет в каталоге", sample.SizeClass,
                    sample.ParameterDisplay, pair.Value,
                    definition == null ? "Нет в каталоге — требуется сверка" : "Данные не сверены (ЧЕРНОВИК)"));
            }
            return rows;
        }

        /// <summary>CSV для Excel.</summary>
        public string ToCsv()
        {
            var rows = new List<IEnumerable<string>>
            {
                new[] { "№", "Номер знака", "Наименование", "Типоразмер", "Параметр", "Количество", "Примечание" }
            };
            foreach (var r in Rows())
                rows.Add(new[] { r.Index.ToString(CultureInfo.InvariantCulture), r.Number.ToString(),
                    r.Name, r.SizeClass, r.Parameter, r.Count.ToString(CultureInfo.InvariantCulture), r.Note });
            return CsvWriter.Build(rows);
        }

        /// <summary>Markdown ведомости.</summary>
        public string ToMarkdown()
        {
            var lines = new List<string>
            {
                "# Ведомость ТС ОДД (живая, ЧЕРНОВИК)", "",
                "| № | Номер знака | Наименование | Типоразмер | Параметр | Количество | Примечание |",
                "|---|---|---|---|---|---|---|"
            };
            foreach (var r in Rows())
                lines.Add("| " + r.Index.ToString(CultureInfo.InvariantCulture) + " | " + r.Number
                    + " | " + r.Name + " | " + r.SizeClass + " | " + r.Parameter + " | "
                    + r.Count.ToString(CultureInfo.InvariantCulture) + " | " + r.Note + " |");
            return string.Join("\n", lines) + "\n";
        }
    }
}
