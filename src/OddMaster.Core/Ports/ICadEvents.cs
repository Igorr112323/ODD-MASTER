using System;
using System.Collections.Generic;

namespace OddMaster.Core.Ports
{
    /// <summary>Вид изменения объекта.</summary>
    public enum CadChangeKind
    {
        /// <summary>Создан.</summary>
        Created,

        /// <summary>Изменён.</summary>
        Changed,

        /// <summary>Удалён.</summary>
        Deleted,

        /// <summary>Восстановлен отменой.</summary>
        Undo,

        /// <summary>Применён повторно.</summary>
        Redo,
    }

    /// <summary>Одно уведомление: что и как изменилось.</summary>
    public sealed class CadChange
    {
        /// <summary>Создаёт уведомление.</summary>
        public CadChange(CadHandle handle, string type, CadChangeKind kind)
        {
            Handle = handle;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Kind = kind;
        }

        /// <summary>Дескриптор объекта.</summary>
        public CadHandle Handle { get; }

        /// <summary>Тип объекта.</summary>
        public string Type { get; }

        /// <summary>Вид изменения.</summary>
        public CadChangeKind Kind { get; }
    }

    /// <summary>Подписчик уведомлений. Менять документ внутри уведомления нельзя (правило 4).</summary>
    public interface ICadEventSink
    {
        /// <summary>Получено уведомление.</summary>
        void OnChanged(CadChange change);
    }

    /// <summary>
    /// События документа (§1.1). Уведомления приходят после фиксации транзакции, по одному на объект
    /// (правило 5). Пересчёты не выполняются внутри уведомления: они попадают в отложенную очередь
    /// <see cref="Deferred"/> и выполняются после фиксации (правило 4, кейс C-EVT-003).
    /// </summary>
    public interface ICadEvents
    {
        /// <summary>Подписка.</summary>
        void Subscribe(ICadEventSink sink);

        /// <summary>Отписка.</summary>
        void Unsubscribe(ICadEventSink sink);

        /// <summary>Отложенная очередь изменений: то, что ждёт пересчёта после фиксации.</summary>
        IReadOnlyList<CadChange> Deferred { get; }

        /// <summary>Очищает отложенную очередь (пересчёт выполнен).</summary>
        void ClearDeferred();
    }
}
