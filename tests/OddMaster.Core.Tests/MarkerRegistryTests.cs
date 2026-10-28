using Xunit;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OddMaster.TestKit;

namespace OddMaster.Core.Tests
{
    /// <summary>Реестры маркеров GOST-NNN / SDK-NNN совпадают с кодом, конфигами и документами.</summary>
    public class MarkerRegistryTests
    {
        private static readonly Regex Gost = new Regex(@"GOST-[0-9]{3}", RegexOptions.Compiled);
        private static readonly Regex Sdk = new Regex(@"SDK-[0-9]{3}", RegexOptions.Compiled);

        [Fact(DisplayName = "M1-M01: все GOST-NNN из конфигов и кода есть в VERIFICATION_QUEUE.md")]
        public void GostMarkersAreRegistered()
        {
            var registry = Repo.ReadText("docs/VERIFICATION_QUEUE.md");
            var registered = Gost.Matches(registry).Select(m => m.Value).ToHashSet();
            var used = CollectMarkers(Gost);
            foreach (var marker in used)
                TestAssert.True(registered.Contains(marker), marker + " отсутствует в docs/VERIFICATION_QUEUE.md");
            TestAssert.True(registered.Count >= 20, "реестр GOST-маркеров пуст или почти пуст");
        }

        [Fact(DisplayName = "M1-M02: все SDK-NNN из адаптера есть в SDK_ASSUMPTIONS.md")]
        public void SdkMarkersAreRegistered()
        {
            var registry = Repo.ReadText("docs/SDK_ASSUMPTIONS.md");
            var registered = Sdk.Matches(registry).Select(m => m.Value).ToHashSet();
            foreach (var marker in CollectMarkers(Sdk))
                TestAssert.True(registered.Contains(marker), marker + " отсутствует в docs/SDK_ASSUMPTIONS.md");
        }

        private static System.Collections.Generic.HashSet<string> CollectMarkers(Regex regex)
        {
            var result = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            var root = Repo.Root();
            foreach (var dir in new[] { "config", "src", "tests", "docs" })
            {
                var path = Path.Combine(root, dir);
                if (!Directory.Exists(path)) continue;
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    if (file.Contains("/obj/") || file.Contains("/bin/")) continue;
                    if (!file.EndsWith(".cs") && !file.EndsWith(".json") && !file.EndsWith(".md")
                        && !file.EndsWith(".csproj") && !file.EndsWith(".yml")) continue;
                    foreach (var marker in regex.Matches(File.ReadAllText(file)).Cast<Match>()
                                 .Select(m => m.Value)) result.Add(marker);
                }
            }
            return result;
        }
    }
}
