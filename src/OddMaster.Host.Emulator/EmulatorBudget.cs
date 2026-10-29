using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace OddMaster.Host.Emulator
{
    /// <summary>
    /// Инженерные пороги эмулятора: допуск сравнения длин и бюджет скорости (правило 11).
    /// Это НЕ нормативка ГОСТ: значения лежат в `config/host/emulator.json` с пометкой
    /// <c>verification: Placeholder</c>, чтобы их можно было менять без правки кода.
    /// </summary>
    public sealed class EmulatorBudget
    {
        /// <summary>Порог по умолчанию, если файл конфигурации недоступен.</summary>
        public EmulatorBudget(double lengthToleranceMm, int budgetObjects, int budgetMilliseconds)
        {
            LengthToleranceMm = lengthToleranceMm;
            BudgetObjects = budgetObjects;
            BudgetMilliseconds = budgetMilliseconds;
        }

        /// <summary>Допуск сравнения длин, мм (правило 9: никаких == для double).</summary>
        public double LengthToleranceMm { get; }

        /// <summary>Сколько объектов в сценарии скорости.</summary>
        public int BudgetObjects { get; }

        /// <summary>Предельное время сценария, мс.</summary>
        public int BudgetMilliseconds { get; }

        /// <summary>Значения по умолчанию (файла нет — работаем, но сообщаем об этом вызывающему).</summary>
        public static EmulatorBudget Default => new EmulatorBudget(0.001, 1000, 5000);

        /// <summary>Читает конфигурацию эмулятора.</summary>
        public static EmulatorBudget LoadFromFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) return Default;
            using (var document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                var root = document.RootElement;
                var tolerance = Default.LengthToleranceMm;
                if (root.TryGetProperty("tolerance", out var toleranceElement) &&
                    toleranceElement.TryGetProperty("lengthMm", out var lengthElement))
                {
                    tolerance = lengthElement.GetDouble();
                }
                var objects = Default.BudgetObjects;
                var milliseconds = Default.BudgetMilliseconds;
                if (root.TryGetProperty("performanceBudget", out var budgetElement))
                {
                    if (budgetElement.TryGetProperty("objects", out var objectsElement))
                        objects = objectsElement.GetInt32();
                    if (budgetElement.TryGetProperty("maxMilliseconds", out var timeElement))
                        milliseconds = timeElement.GetInt32();
                }
                return new EmulatorBudget(tolerance, objects, milliseconds);
            }
        }

        /// <summary>Сравнение длин с допуском (правило 9).</summary>
        public bool AreEqual(double left, double right) =>
            Math.Abs(left - right) <= LengthToleranceMm;

        /// <inheritdoc />
        public override string ToString() =>
            "допуск " + LengthToleranceMm.ToString("0.###", CultureInfo.InvariantCulture) + " мм, бюджет " +
            BudgetObjects.ToString(CultureInfo.InvariantCulture) + " объектов / " +
            BudgetMilliseconds.ToString(CultureInfo.InvariantCulture) + " мс";
    }
}
