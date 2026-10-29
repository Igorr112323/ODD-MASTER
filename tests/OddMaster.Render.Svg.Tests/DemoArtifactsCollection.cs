using Xunit;

namespace OddMaster.Render.Svg.Tests
{
    /// <summary>
    /// Тесты, читающие и перегенерирующие docs/demo, должны идти последовательно:
    /// на Windows параллельная запись даёт IOException (E-032).
    /// </summary>
    [CollectionDefinition("Demo artifacts", DisableParallelization = true)]
    public sealed class DemoArtifactsCollection { }
}
