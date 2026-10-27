using OddMaster.TestKit;

namespace OddMaster.Render.Svg.Tests
{
    /// <summary>Дымовой тест проекта рендера.</summary>
    public static class SmokeTests
    {
        /// <summary>Сборка рендера загружается.</summary>
        [OddFact("SMOKE-03: сборка Render.Svg загружается")]
        public static void RenderAssemblyLoads()
        {
            Assert.NotNull(typeof(OddMaster.Render.Svg.Marker).Assembly);
        }
    }
}
