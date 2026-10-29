using System;
using OddMaster.Core.Ports;

namespace OddMaster.Host.Emulator
{
    /// <summary>
    /// Linux-хост: строгий эмулятор САПР в памяти (кольцо 2 программы «LINUX-ПОЛИГОН»).
    /// Он не рисует «как в nanoCAD» и не заменяет проверку в САПР: его задача — ловить нарушения
    /// контракта раньше, чем они дойдут до Windows.
    /// </summary>
    public sealed class EmulatorHost : ICadHost
    {
        private readonly EmulatorDocument _document;
        private readonly EmulatorPersistence _persistence;
        private readonly ScriptedInteraction _interaction;

        /// <summary>Создаёт хост с порогами по умолчанию.</summary>
        public EmulatorHost() : this(EmulatorBudget.Default)
        {
        }

        /// <summary>Создаёт хост с заданными порогами.</summary>
        public EmulatorHost(EmulatorBudget budget)
        {
            Budget = budget ?? throw new ArgumentNullException(nameof(budget));
            _document = new EmulatorDocument();
            _persistence = new EmulatorPersistence(_document);
            _interaction = new ScriptedInteraction();
        }

        /// <summary>Инженерные пороги: допуск и бюджет скорости.</summary>
        public EmulatorBudget Budget { get; }

        /// <inheritdoc />
        public ICadDocument Document => _document;

        /// <inheritdoc />
        public ICadEvents Events => _document;

        /// <inheritdoc />
        public ICadPersistence Persistence => _persistence;

        /// <inheritdoc />
        public ICadInteraction Interaction => _interaction;

        /// <summary>Сценарий ввода для доменных команд.</summary>
        public ScriptedInteraction Script => _interaction;

        /// <summary>Документ как таковой — нужен стендам и контрактным кейсам.</summary>
        public EmulatorDocument EmulatorDocument => _document;
    }
}
