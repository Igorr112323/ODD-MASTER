using System;
using System.Collections.Generic;

namespace OddMaster.Core.Ports
{
    /// <summary>Исключение: объект с таким дескриптором не существует (или уже удалён).</summary>
    public sealed class CadObjectMissingException : InvalidOperationException
    {
        /// <summary>Создаёт исключение.</summary>
        public CadObjectMissingException(CadHandle handle)
            : base("Объект " + handle + " не существует или удалён.")
        {
            Handle = handle;
        }

        /// <summary>Дескриптор, по которому не нашли объект.</summary>
        public CadHandle Handle { get; }
    }

    /// <summary>Исключение: попытка менять документ вне транзакции или внутри уведомления.</summary>
    public sealed class CadMutationRejectedException : InvalidOperationException
    {
        /// <summary>Создаёт исключение.</summary>
        public CadMutationRejectedException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Документ САПР глазами ядра (§1.1). Чтение разрешено всегда, изменения — только внутри
    /// <see cref="BeginTransaction"/>. Так правило «изменение вне транзакции — ошибка» нельзя нарушить
    /// случайно: метода изменения у документа просто нет.
    /// </summary>
    public interface ICadDocument
    {
        /// <summary>Читает объект. Для удалённого дескриптора бросает <see cref="CadObjectMissingException"/>.</summary>
        CadObjectSnapshot Read(CadHandle handle);

        /// <summary>Все объекты типа в порядке дескрипторов (детерминированно).</summary>
        IReadOnlyList<CadHandle> Query(string type);

        /// <summary>Открывает транзакцию — одну группу отмены на одну доменную команду.</summary>
        ICadTransaction BeginTransaction(string commandName);

        /// <summary>Можно ли отменить.</summary>
        bool CanUndo { get; }

        /// <summary>Можно ли повторить.</summary>
        bool CanRedo { get; }

        /// <summary>Отменяет последнюю группу.</summary>
        void Undo();

        /// <summary>Повторяет отменённую группу.</summary>
        void Redo();

        /// <summary>
        /// Нормализованный снимок документа: объекты по возрастанию дескриптора, свойства по имени,
        /// числа инвариантно. Одинаковый сценарий обязан давать одинаковую строку (правило 10).
        /// </summary>
        string Snapshot();
    }

    /// <summary>Транзакция: единственный способ менять документ.</summary>
    public interface ICadTransaction
    {
        /// <summary>Создаёт объект и возвращает его дескриптор.</summary>
        CadHandle Create(string type, string layer, IReadOnlyList<CadProperty> properties);

        /// <summary>Меняет или добавляет свойство.</summary>
        void Set(CadHandle handle, string name, string value);

        /// <summary>Удаляет объект.</summary>
        void Delete(CadHandle handle);

        /// <summary>Фиксирует изменения и рассылает уведомления.</summary>
        void Commit();

        /// <summary>Отменяет изменения: документ возвращается к состоянию до транзакции.</summary>
        void Abort();

        /// <summary>Транзакция уже завершена (зафиксирована или отменена).</summary>
        bool IsFinished { get; }
    }
}
