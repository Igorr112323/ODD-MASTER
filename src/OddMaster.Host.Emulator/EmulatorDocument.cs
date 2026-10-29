using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OddMaster.Core.Ports;

namespace OddMaster.Host.Emulator
{
    /// <summary>
    /// Строгий эмулятор документа САПР в памяти (LINUX_FIRST_PROGRAM §2). Он намеренно строже настоящей
    /// САПР: изменение вне транзакции, обращение к удалённому дескриптору и правка объекта внутри его же
    /// уведомления дают исключение, а не «тихо проходит».
    ///
    /// Один класс реализует и <see cref="ICadDocument"/>, и <see cref="ICadEvents"/>: события обязаны
    /// приходить из той же транзакции, что и изменения, иначе легко получить рассинхрон.
    /// </summary>
    public sealed class EmulatorDocument : ICadDocument, ICadEvents
    {
        private SortedDictionary<int, EmulatorObject> _objects = new SortedDictionary<int, EmulatorObject>();
        private readonly Stack<UndoGroup> _undo = new Stack<UndoGroup>();
        private readonly Stack<UndoGroup> _redo = new Stack<UndoGroup>();
        private readonly List<ICadEventSink> _sinks = new List<ICadEventSink>();
        private readonly List<CadChange> _deferred = new List<CadChange>();
        private int _nextHandle = 1;
        private bool _dispatching;

        /// <inheritdoc />
        public bool CanUndo => _undo.Count > 0;

        /// <inheritdoc />
        public bool CanRedo => _redo.Count > 0;

        /// <inheritdoc />
        public CadObjectSnapshot Read(CadHandle handle)
        {
            if (!_objects.TryGetValue(handle.Value, out var stored))
                throw new CadObjectMissingException(handle);
            return stored.ToSnapshot(handle);
        }

        /// <inheritdoc />
        public IReadOnlyList<CadHandle> Query(string type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var result = new List<CadHandle>();
            foreach (var pair in _objects)
            {
                if (string.Equals(pair.Value.Type, type, StringComparison.Ordinal))
                    result.Add(new CadHandle(pair.Key));
            }
            return result;
        }

        /// <inheritdoc />
        public ICadTransaction BeginTransaction(string commandName)
        {
            if (commandName == null) throw new ArgumentNullException(nameof(commandName));
            if (_dispatching)
                throw new CadMutationRejectedException(
                    "Нельзя открывать транзакцию внутри уведомления: пересчёт идёт после фиксации.");
            return new EmulatorTransaction(this, commandName, Clone());
        }

        /// <inheritdoc />
        public void Undo()
        {
            GuardDispatch("отмена");
            if (_undo.Count == 0) throw new InvalidOperationException("Нечего отменять.");
            var group = _undo.Pop();
            _redo.Push(group);
            var changes = Diff(_objects, group.Before, CadChangeKind.Undo);
            _objects = group.Before;
            Dispatch(changes);
        }

        /// <inheritdoc />
        public void Redo()
        {
            GuardDispatch("повтор");
            if (_redo.Count == 0) throw new InvalidOperationException("Нечего повторять.");
            var group = _redo.Pop();
            _undo.Push(group);
            var changes = Diff(_objects, group.After, CadChangeKind.Redo);
            _objects = group.After;
            Dispatch(changes);
        }

        /// <inheritdoc />
        public string Snapshot()
        {
            var builder = new StringBuilder();
            foreach (var pair in _objects)
            {
                builder.Append('h').Append(pair.Key.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('|').Append(pair.Value.Type)
                    .Append('|').Append(pair.Value.Layer).Append('|');
                var first = true;
                foreach (var property in pair.Value.Properties)
                {
                    if (!first) builder.Append(';');
                    builder.Append(property.Key).Append('=').Append(property.Value);
                    first = false;
                }
                builder.Append('\n');
            }
            return builder.ToString();
        }

        /// <inheritdoc />
        public void Subscribe(ICadEventSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (!_sinks.Contains(sink)) _sinks.Add(sink);
        }

        /// <inheritdoc />
        public void Unsubscribe(ICadEventSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            _sinks.Remove(sink);
        }

        /// <inheritdoc />
        public IReadOnlyList<CadChange> Deferred => _deferred;

        /// <inheritdoc />
        public void ClearDeferred() => _deferred.Clear();

        /// <summary>Заменяет содержимое документа (загрузка из хранилища). Отмены и повторы сбрасываются.</summary>
        public void Import(IReadOnlyList<CadObjectSnapshot> objects)
        {
            if (objects == null) throw new ArgumentNullException(nameof(objects));
            var next = new SortedDictionary<int, EmulatorObject>();
            var max = 0;
            foreach (var snapshot in objects)
            {
                var stored = new EmulatorObject(snapshot.Type, snapshot.Layer);
                foreach (var property in snapshot.Properties) stored.Properties[property.Name] = property.Value;
                next[snapshot.Handle.Value] = stored;
                if (snapshot.Handle.Value > max) max = snapshot.Handle.Value;
            }
            _objects = next;
            _nextHandle = max + 1;
            _undo.Clear();
            _redo.Clear();
            _deferred.Clear();
        }

        /// <summary>Все объекты документа в порядке дескрипторов (для хранения).</summary>
        public IReadOnlyList<CadObjectSnapshot> Export()
        {
            var result = new List<CadObjectSnapshot>(_objects.Count);
            foreach (var pair in _objects) result.Add(pair.Value.ToSnapshot(new CadHandle(pair.Key)));
            return result;
        }

        internal void Commit(string commandName, SortedDictionary<int, EmulatorObject> work)
        {
            GuardDispatch("фиксация транзакции");
            var changes = Diff(_objects, work, CadChangeKind.Changed);
            _undo.Push(new UndoGroup(_objects, work, commandName));
            _redo.Clear();
            _objects = work;
            Dispatch(changes);
        }

        internal int NextHandle() => _nextHandle++;

        private void GuardDispatch(string action)
        {
            if (_dispatching)
                throw new CadMutationRejectedException(
                    "Запрещено: " + action + " внутри уведомления об изменении (правило 4 эмулятора).");
        }

        private SortedDictionary<int, EmulatorObject> Clone()
        {
            var copy = new SortedDictionary<int, EmulatorObject>();
            foreach (var pair in _objects) copy[pair.Key] = pair.Value.Clone();
            return copy;
        }

        private void Dispatch(List<CadChange> changes)
        {
            if (changes.Count == 0) return;
            _dispatching = true;
            try
            {
                foreach (var sink in _sinks.ToArray())
                {
                    foreach (var change in changes) sink.OnChanged(change);
                }
            }
            finally
            {
                _dispatching = false;
            }
            _deferred.AddRange(changes);
        }

        /// <summary>
        /// Разница двух состояний: одна запись на объект (правило 5). Порядок — по дескриптору.
        /// </summary>
        private static List<CadChange> Diff(SortedDictionary<int, EmulatorObject> before,
            SortedDictionary<int, EmulatorObject> after, CadChangeKind changedKind)
        {
            var handles = new SortedSet<int>(before.Keys);
            foreach (var key in after.Keys) handles.Add(key);
            var result = new List<CadChange>();
            foreach (var handle in handles)
            {
                before.TryGetValue(handle, out var old);
                after.TryGetValue(handle, out var fresh);
                if (old == null && fresh != null)
                {
                    result.Add(new CadChange(new CadHandle(handle), fresh.Type, CadChangeKind.Created));
                }
                else if (old != null && fresh == null)
                {
                    result.Add(new CadChange(new CadHandle(handle), old.Type, CadChangeKind.Deleted));
                }
                else if (old != null && fresh != null && !old.SameAs(fresh))
                {
                    result.Add(new CadChange(new CadHandle(handle), fresh.Type, changedKind));
                }
            }
            return result;
        }

        private sealed class UndoGroup
        {
            public UndoGroup(SortedDictionary<int, EmulatorObject> before,
                SortedDictionary<int, EmulatorObject> after, string name)
            {
                Before = before;
                After = after;
                Name = name;
            }

            public SortedDictionary<int, EmulatorObject> Before { get; }
            public SortedDictionary<int, EmulatorObject> After { get; }
            public string Name { get; }
        }

        private sealed class EmulatorObject
        {
            public EmulatorObject(string type, string layer)
            {
                Type = type;
                Layer = layer;
            }

            public string Type { get; }
            public string Layer { get; set; }
            public SortedDictionary<string, string> Properties { get; } =
                new SortedDictionary<string, string>(StringComparer.Ordinal);

            public EmulatorObject Clone()
            {
                var copy = new EmulatorObject(Type, Layer);
                foreach (var pair in Properties) copy.Properties[pair.Key] = pair.Value;
                return copy;
            }

            public bool SameAs(EmulatorObject other)
            {
                if (!string.Equals(Type, other.Type, StringComparison.Ordinal)) return false;
                if (!string.Equals(Layer, other.Layer, StringComparison.Ordinal)) return false;
                if (Properties.Count != other.Properties.Count) return false;
                foreach (var pair in Properties)
                {
                    if (!other.Properties.TryGetValue(pair.Key, out var value)) return false;
                    if (!string.Equals(value, pair.Value, StringComparison.Ordinal)) return false;
                }
                return true;
            }

            public CadObjectSnapshot ToSnapshot(CadHandle handle)
            {
                var properties = new List<CadProperty>(Properties.Count);
                foreach (var pair in Properties) properties.Add(new CadProperty(pair.Key, pair.Value));
                return new CadObjectSnapshot(handle, Type, Layer, properties);
            }
        }

        private sealed class EmulatorTransaction : ICadTransaction
        {
            private readonly EmulatorDocument _document;
            private readonly string _name;
            private SortedDictionary<int, EmulatorObject> _work;

            public EmulatorTransaction(EmulatorDocument document, string name,
                SortedDictionary<int, EmulatorObject> work)
            {
                _document = document;
                _name = name;
                _work = work;
            }

            public bool IsFinished => _work == null;

            public CadHandle Create(string type, string layer, IReadOnlyList<CadProperty> properties)
            {
                var work = Require();
                if (type == null) throw new ArgumentNullException(nameof(type));
                if (layer == null) throw new ArgumentNullException(nameof(layer));
                var handle = _document.NextHandle();
                var stored = new EmulatorObject(type, layer);
                if (properties != null)
                {
                    foreach (var property in properties)
                        stored.Properties[property.Name] = property.Value;
                }
                work[handle] = stored;
                return new CadHandle(handle);
            }

            public void Set(CadHandle handle, string name, string value)
            {
                var work = Require();
                if (name == null) throw new ArgumentNullException(nameof(name));
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (!work.TryGetValue(handle.Value, out var stored))
                    throw new CadObjectMissingException(handle);
                stored.Properties[name] = value;
            }

            public void Delete(CadHandle handle)
            {
                var work = Require();
                if (!work.Remove(handle.Value)) throw new CadObjectMissingException(handle);
            }

            public void Commit()
            {
                var work = Require();
                _work = null;
                _document.Commit(_name, work);
            }

            public void Abort() => _work = null;

            private SortedDictionary<int, EmulatorObject> Require()
            {
                if (_work == null)
                    throw new CadMutationRejectedException(
                        "Транзакция «" + _name + "» уже завершена: изменение после Commit/Abort запрещено.");
                return _work;
            }
        }
    }
}
