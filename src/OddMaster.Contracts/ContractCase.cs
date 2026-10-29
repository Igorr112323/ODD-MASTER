using System;
using OddMaster.Core.Ports;

namespace OddMaster.Contracts
{
    /// <summary>
    /// Хост, против которого гоняются контрактные кейсы: на Linux — эмулятор, в nanoCAD — настоящий
    /// адаптер. Кейсы не знают, кто именно за портами (мост М1 программы «LINUX-ПОЛИГОН»).
    /// </summary>
    public interface IHostUnderTest : ICadHost
    {
        /// <summary>Имя хоста для отчёта: emulator, nanocad.</summary>
        string Name { get; }
    }

    /// <summary>
    /// Проверки, которыми пользуется кейс. Реализации две: xUnit на Linux/Windows и писатель отчёта
    /// внутри nanoCAD (`ODD_SELFTEST`), где xUnit недоступен. Поэтому библиотека не тянет xUnit.
    /// </summary>
    public interface IContractCheck
    {
        /// <summary>Условие истинно.</summary>
        void True(bool condition, string message);

        /// <summary>Строки равны.</summary>
        void Equal(string expected, string actual, string message);

        /// <summary>Действие бросает исключение указанного типа.</summary>
        void Throws<TException>(Action action, string message) where TException : Exception;
    }

    /// <summary>Один контрактный кейс: идентификатор, порт, заголовок и сценарий проверки.</summary>
    public sealed class ContractCase
    {
        private readonly Action<IHostUnderTest, IContractCheck> _run;

        /// <summary>Создаёт кейс.</summary>
        public ContractCase(string id, string port, string title, Action<IHostUnderTest, IContractCheck> run)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Port = port ?? throw new ArgumentNullException(nameof(port));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            _run = run ?? throw new ArgumentNullException(nameof(run));
        }

        /// <summary>Идентификатор вида C-DOC-001.</summary>
        public string Id { get; }

        /// <summary>Порт, который проверяется.</summary>
        public string Port { get; }

        /// <summary>Что доказывает кейс.</summary>
        public string Title { get; }

        /// <summary>Выполняет кейс против хоста.</summary>
        public void Run(IHostUnderTest host, IContractCheck check)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (check == null) throw new ArgumentNullException(nameof(check));
            _run(host, check);
        }

        /// <inheritdoc />
        public override string ToString() => Id + " · " + Port + " · " + Title;
    }
}
