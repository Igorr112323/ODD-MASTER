using System;
using System.Collections.Generic;
using OddMaster.Core.Ports;

namespace OddMaster.Host.Emulator
{
    /// <summary>
    /// Сценарный ввод (правило «в тестах — сценарий ввода»). Команда просит точку — получает заранее
    /// записанную. Кончился сценарий — исключение с подсказкой, а не выдуманная точка.
    /// </summary>
    public sealed class ScriptedInteraction : ICadInteraction
    {
        private readonly Queue<CadPoint> _points = new Queue<CadPoint>();
        private readonly Queue<CadHandle> _objects = new Queue<CadHandle>();
        private readonly Queue<string> _choices = new Queue<string>();

        /// <summary>Добавляет точку в сценарий.</summary>
        public ScriptedInteraction AddPoint(double x, double y)
        {
            _points.Enqueue(new CadPoint(x, y));
            return this;
        }

        /// <summary>Добавляет выбор объекта в сценарий.</summary>
        public ScriptedInteraction AddObject(CadHandle handle)
        {
            _objects.Enqueue(handle);
            return this;
        }

        /// <summary>Добавляет ответ на выбор из списка.</summary>
        public ScriptedInteraction AddChoice(string choice)
        {
            _choices.Enqueue(choice);
            return this;
        }

        /// <summary>Сколько шагов сценария осталось.</summary>
        public int Remaining => _points.Count + _objects.Count + _choices.Count;

        /// <inheritdoc />
        public CadPoint ReadPoint(string prompt)
        {
            if (_points.Count == 0) throw new InvalidOperationException("Сценарий ввода исчерпан: " + prompt);
            return _points.Dequeue();
        }

        /// <inheritdoc />
        public CadHandle ReadObject(string prompt, string type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (_objects.Count == 0) throw new InvalidOperationException("Сценарий ввода исчерпан: " + prompt);
            return _objects.Dequeue();
        }

        /// <inheritdoc />
        public string ReadChoice(string prompt, IReadOnlyList<string> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (_choices.Count == 0) throw new InvalidOperationException("Сценарий ввода исчерпан: " + prompt);
            var choice = _choices.Dequeue();
            if (!Contains(options, choice))
                throw new InvalidOperationException("В сценарии «" + choice + "», а допустимо: " +
                                                    string.Join(", ", options));
            return choice;
        }

        private static bool Contains(IReadOnlyList<string> options, string value)
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i], value, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
