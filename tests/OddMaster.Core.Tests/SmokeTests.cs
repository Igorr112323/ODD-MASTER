using Xunit;
using OddMaster.TestKit;

namespace OddMaster.Core.Tests
{
    /// <summary>Дымовой тест: раннер и инфраструктура репозитория живы.</summary>
    public class SmokeTests
    {
        /// <summary>Решение и промпт доступны из теста.</summary>
        [Fact(DisplayName = "SMOKE-01: репозиторий доступен из теста")]
        public void RepositoryIsReachable()
        {
            TestAssert.True(Repo.Exists("OddMaster.sln"), "нет OddMaster.sln");
            TestAssert.True(Repo.Exists("docs/prompts/NIGHT_RUN_PROMPT.md"), "нет сохранённого промпта");
            TestAssert.Contains("НОЧНОЙ ПРОГОН", Repo.ReadText("docs/prompts/NIGHT_RUN_PROMPT.md"));
        }

        /// <summary>Сборка ядра загружается.</summary>
        [Fact(DisplayName = "SMOKE-02: сборка Core загружается")]
        public void CoreAssemblyLoads()
        {
            TestAssert.NotNull(typeof(OddMaster.Core.Marker).Assembly);
        }
    }
}
