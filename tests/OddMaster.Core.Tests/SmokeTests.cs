using OddMaster.TestKit;

namespace OddMaster.Core.Tests
{
    /// <summary>Дымовой тест: раннер и инфраструктура репозитория живы.</summary>
    public static class SmokeTests
    {
        /// <summary>Решение и промпт доступны из теста.</summary>
        [OddFact("SMOKE-01: репозиторий доступен из теста")]
        public static void RepositoryIsReachable()
        {
            Assert.True(Repo.Exists("OddMaster.sln"), "нет OddMaster.sln");
            Assert.True(Repo.Exists("docs/prompts/NIGHT_RUN_PROMPT.md"), "нет сохранённого промпта");
            Assert.Contains("НОЧНОЙ ПРОГОН", Repo.ReadText("docs/prompts/NIGHT_RUN_PROMPT.md"));
        }

        /// <summary>Сборка ядра загружается.</summary>
        [OddFact("SMOKE-02: сборка Core загружается")]
        public static void CoreAssemblyLoads()
        {
            Assert.NotNull(typeof(OddMaster.Core.Marker).Assembly);
        }
    }
}
