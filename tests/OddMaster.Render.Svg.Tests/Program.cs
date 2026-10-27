using System.Reflection;
using OddMaster.TestKit;

namespace OddMaster.Render.Svg.Tests
{
    /// <summary>Точка входа тестового раннера SVG.</summary>
    public static class Program
    {
        /// <summary>Запускает тесты рендера.</summary>
        public static int Main() => TestRunner.Run(Assembly.GetExecutingAssembly());
    }
}
