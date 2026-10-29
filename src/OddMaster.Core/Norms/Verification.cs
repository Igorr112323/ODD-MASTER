using System;

namespace OddMaster.Core.Norms
{
    /// <summary>Статус проверки нормативного значения. Статус StudentVerified ставит только человек.</summary>
    public enum Verification
    {
        /// <summary>Заглушка только для отрисовки. Выход обязательно помечается «ЧЕРНОВИК».</summary>
        Placeholder = 0,

        /// <summary>Предположение агента или данные из вторичных источников.</summary>
        Unverified = 1,

        /// <summary>Извлечено куратором из PDF-копии стандарта; студент обязан сверить.</summary>
        CuratorExtracted = 2,

        /// <summary>Сверено студентом с официальным текстом. Агент не имеет права ставить этот статус.</summary>
        StudentVerified = 3
    }

    /// <summary>Разбор и сравнение статусов проверки.</summary>
    public static class VerificationParser
    {
        /// <summary>Разбирает строковый статус. Неизвестное значение — ошибка конфигурации.</summary>
        public static Verification Parse(string value)
        {
            switch (value)
            {
                case "Placeholder": return Verification.Placeholder;
                case "Unverified": return Verification.Unverified;
                case "CuratorExtracted": return Verification.CuratorExtracted;
                case "StudentVerified": return Verification.StudentVerified;
                default:
                    throw new FormatException(
                        $"Неизвестный статус проверки «{value}». Допустимо: Placeholder, Unverified, CuratorExtracted, StudentVerified.");
            }
        }

        /// <summary>Пытается разобрать статус без исключения.</summary>
        public static bool TryParse(string? value, out Verification status)
        {
            status = Verification.Unverified;
            if (value == null) return false;
            try { status = Parse(value); return true; }
            catch (FormatException) { return false; }
        }

        /// <summary>Истина, если данные ещё не сверены с официальным текстом.</summary>
        public static bool IsQuestionable(Verification status) => status != Verification.StudentVerified;
    }
}
