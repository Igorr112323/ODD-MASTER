using System;
using OddMaster.Core.Norms;
using OddMaster.TestKit;

namespace OddMaster.Core.Tests
{
    /// <summary>Тесты семантики номеров и диапазонов (раздел 8.6 промпта, ADR-0005).</summary>
    public static class NormTests
    {
        [OddFact("M1-N01: разбор валидных номеров 3.24, 2.3.1, 8.21.3")]
        public static void ParseValidNumbers()
        {
            Assert.Equal("3.24", SignNumber.Parse("3.24").ToString());
            Assert.Equal("2.3.1", SignNumber.Parse("2.3.1").ToString());
            Assert.Equal("8.21.3", SignNumber.Parse("8.21.3").ToString());
        }

        [OddFact("M1-N02: «3..24», «3.24.», «a.1», «» — ошибка")]
        public static void ParseInvalidNumbers()
        {
            foreach (var bad in new[] { "3..24", "3.24.", "a.1", "", "3", "9.1", "  .1" })
                Assert.Throws<FormatException>(() => SignNumber.Parse(bad));
        }

        [OddFact("M1-N03: порядок 3.2 < 3.10 < 3.24 и 2.3.7 < 2.4")]
        public static void OrderIsComponentwise()
        {
            var n32 = SignNumber.Parse("3.2");
            var n310 = SignNumber.Parse("3.10");
            var n324 = SignNumber.Parse("3.24");
            Assert.True(n32.CompareTo(n310) < 0, "3.2 должно быть меньше 3.10");
            Assert.True(n310.CompareTo(n324) < 0, "3.10 должно быть меньше 3.24");
            Assert.True(SignNumber.Parse("2.3.7").CompareTo(SignNumber.Parse("2.4")) < 0, "2.3.7 < 2.4");
        }

        [OddFact("M1-N04: одиночный номер «6.2» не совпадает с 6.2.1")]
        public static void SingleNumberDoesNotMatchVariant()
        {
            var set = SignNumberSet.Parse("6.2");
            Assert.True(set.Contains(SignNumber.Parse("6.2")));
            Assert.False(set.Contains(SignNumber.Parse("6.2.1")));
        }

        [OddFact("M1-N05: дефис, en dash, em dash, с пробелами и без")]
        public static void RangeSeparators()
        {
            foreach (var text in new[] { "1.5-1.7", "1.5 - 1.7", "1.5–1.7", "1.5 — 1.7" })
            {
                var set = SignNumberSet.Parse(text);
                Assert.True(set.Contains(SignNumber.Parse("1.6")), text + " должен содержать 1.6");
            }
        }

        [OddFact("M1-N06: «6.14.2 - 6.16» не содержит 6.16.1")]
        public static void UpperBoundaryExcludesVariants()
        {
            var set = SignNumberSet.Parse("6.14.2 - 6.16");
            Assert.True(set.Contains(SignNumber.Parse("6.15")));
            Assert.True(set.Contains(SignNumber.Parse("6.16")));
            Assert.False(set.Contains(SignNumber.Parse("6.16.1")));
        }

        [OddFact("M1-N07: «1.1 - 2.4» пересекает группы и содержит 2.3.5")]
        public static void RangeCrossesGroups()
        {
            var set = SignNumberSet.Parse("1.1 - 2.4");
            Assert.True(set.Contains(SignNumber.Parse("2.3.5")));
            Assert.True(set.Contains(SignNumber.Parse("1.31")));
            Assert.False(set.Contains(SignNumber.Parse("2.4.1")));
        }

        [OddFact("M1-N08: список из одиночных номеров и диапазонов")]
        public static void MixedList()
        {
            var set = SignNumberSet.Parse("1.5 - 1.7, 2.4");
            Assert.True(set.Contains(SignNumber.Parse("1.6")));
            Assert.True(set.Contains(SignNumber.Parse("2.4")));
            Assert.False(set.Contains(SignNumber.Parse("1.8")));
            Assert.Equal(1, set.Singles.Count);
            Assert.Equal(1, set.Ranges.Count);
        }

        [OddFact("M1-N09: некорректный список даёт FormatException, а не тихое ignore")]
        public static void BadListThrows()
        {
            Assert.Throws<FormatException>(() => SignNumberSet.Parse("1.5 - , 2.4"));
            Assert.Throws<FormatException>(() => new SignNumberRange(SignNumber.Parse("3.24"), SignNumber.Parse("3.2")));
        }

        [OddFact("M1-N10: статусы проверки разбираются и StudentVerified — единственный проверенный")]
        public static void VerificationStatuses()
        {
            Assert.Equal(Verification.Placeholder, VerificationParser.Parse("Placeholder"));
            Assert.Equal(Verification.CuratorExtracted, VerificationParser.Parse("CuratorExtracted"));
            Assert.True(VerificationParser.IsQuestionable(Verification.CuratorExtracted));
            Assert.False(VerificationParser.IsQuestionable(Verification.StudentVerified));
            Assert.Throws<FormatException>(() => VerificationParser.Parse("VerifiedByAI"));
        }

        [OddFact("M1-N11: VerificationSummary детерминирован и помечает черновик")]
        public static void VerificationSummaryIsDeterministic()
        {
            var s = new VerificationSummary();
            s.Add("catalog", Verification.CuratorExtracted);
            s.Add("pictogram", Verification.Placeholder);
            s.Add("catalog", Verification.StudentVerified); // худший статус побеждает
            Assert.True(s.IsDraft);
            Assert.Equal("catalog=CuratorExtracted; pictogram=Placeholder", s.Describe());
        }
    }
}
