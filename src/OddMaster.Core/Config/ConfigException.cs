using System;

namespace OddMaster.Core.Config
{
    /// <summary>Ошибка конфигурации с понятным русским сообщением: файл, JSON Pointer, что исправить.</summary>
    public sealed class ConfigException : Exception
    {
        /// <summary>Создаёт ошибку конфигурации.</summary>
        public ConfigException(string file, string pointer, string problem, string howToFix)
            : base($"Ошибка конфигурации {file}{pointer}: {problem}. Как исправить: {howToFix}.")
        {
            File = file;
            Pointer = pointer;
            Problem = problem;
            HowToFix = howToFix;
        }

        /// <summary>Файл конфигурации.</summary>
        public string File { get; }

        /// <summary>JSON Pointer к проблемному месту.</summary>
        public string Pointer { get; }

        /// <summary>Что не так.</summary>
        public string Problem { get; }

        /// <summary>Как исправить.</summary>
        public string HowToFix { get; }
    }
}
