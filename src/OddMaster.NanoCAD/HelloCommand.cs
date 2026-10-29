// ВНИМАНИЕ: файл НЕ компилировался — SDK nanoCAD 26 отсутствует.
using System;
using OddMaster.Core.Catalog;
using OddMaster.Core.Config;
using OddMaster.Core.Generation;
using OddMaster.Core.Norms;

namespace OddMaster.NanoCAD
{
    /// <summary>Команда ODD_HELLO: минимальная проверка загрузки плагина.</summary>
    public static class HelloCommand
    {
        /// <summary>
        /// Точка входа команды. [ПРОВЕРИТЬ В SDK: SDK-002, SDK-003]
        /// Атрибут CommandMethod и пространство имён Multicad.Runtime — предположения.
        /// </summary>
        public static void Run()
        {
            // [ПРОВЕРИТЬ В SDK: SDK-002] using Multicad.Runtime; [ПРОВЕРИТЬ В SDK: SDK-003] [CommandMethod("ODD_HELLO")]
            Console.WriteLine("ОДД-Мастер: плагин загружен (проверка SDK не выполнялась).");
        }

        /// <summary>Пример: геометрия знака 3.24 из конфигурации, без вызовов SDK.</summary>
        public static SignGeometry? BuildSampleGeometry(string repositoryRoot)
        {
            var config = ConfigLoader.Load(repositoryRoot);
            var result = new SignGenerator(config).Generate("3.24", "II",
                new System.Collections.Generic.Dictionary<string, decimal> { ["speed"] = 40m });
            return result.Geometry;
        }
    }
}
