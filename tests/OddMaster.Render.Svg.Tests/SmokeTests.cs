using Xunit;
using OddMaster.TestKit;

namespace OddMaster.Render.Svg.Tests
{
    /// <summary>Дымовой тест проекта рендера.</summary>
    public class SmokeTests
    {
        /// <summary>Сборка рендера загружается.</summary>
        [Fact(DisplayName = "SMOKE-03: сборка Render.Svg загружается")]
        public void RenderAssemblyLoads()
        {
            TestAssert.NotNull(typeof(OddMaster.Render.Svg.Marker).Assembly);
        }
    }
}
