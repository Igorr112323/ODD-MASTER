using System.Reflection;
using OddMaster.TestKit;

namespace OddMaster.Core.Tests
{
    /// <summary>Точка входа тестового раннера.</summary>
    public static class Program
    {
        /// <summary>Запускает тесты ядра.</summary>
        public static int Main() => TestRunner.Run(Assembly.GetExecutingAssembly());
    }
}
