using System.Collections.Generic;

namespace OddMaster.Core.Ports
{
    /// <summary>
    /// Ввод для доменных команд (§1.1). В тестах реализуется сценарием ввода: команда просит точку —
    /// получает заранее записанную. Если сценарий кончился, честная ошибка, а не «случайная» точка.
    /// </summary>
    public interface ICadInteraction
    {
        /// <summary>Точка.</summary>
        CadPoint ReadPoint(string prompt);

        /// <summary>Объект указанного типа (выбор мышью в САПР).</summary>
        CadHandle ReadObject(string prompt, string type);

        /// <summary>Выбор строки из списка.</summary>
        string ReadChoice(string prompt, IReadOnlyList<string> options);
    }

    /// <summary>Набор портов одного хоста: эмулятора на Linux или настоящей САПР в nanoCAD.</summary>
    public interface ICadHost
    {
        /// <summary>Документ.</summary>
        ICadDocument Document { get; }

        /// <summary>События.</summary>
        ICadEvents Events { get; }

        /// <summary>Хранение.</summary>
        ICadPersistence Persistence { get; }

        /// <summary>Ввод.</summary>
        ICadInteraction Interaction { get; }
    }
}
