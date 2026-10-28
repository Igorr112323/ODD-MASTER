using Xunit;
using System;
using OddMaster.Core.Norms;
using OddMaster.TestKit;

namespace OddMaster.Core.Tests
{
    /// <summary>Тесты семантики номеров и диапазонов (раздел 8.6 промпта, ADR-0005).</summary>
    public class NormTests
    {
        [Fact(DisplayName = "M1-N01: разбор валидных номеров 3.24, 2.3.1, 8.21.3")]
        public void ParseValidNumbers()
        {
            TestAssert.Equal("3.24", SignNumber.Parse("3.24").ToString());
            TestAssert.Equal("2.3.1", SignNumber.Parse("2.3.1").ToString());
            TestAssert.Equal("8.21.3", SignNumber.Parse("8.21.3").ToString());
        }

        [Fact(DisplayName = "M1-N02: «3..24», «3.24.», «a.1», «» — ошибка")]
        public void ParseInvalidNumbers()
        {
            foreach (var bad in new[] { "3..24", "3.24.", "a.1", "", "3", "9.1", "  .1" })
                TestAssert.Throws<FormatException>(() => SignNumber.Parse(bad));
        }

        [Fact(DisplayName = "M1-N03: порядок 3.2 < 3.10 < 3.24 и 2.3.7 < 2.4")]
        public void OrderIsComponentwise()
        {
            var n32 = SignNumber.Parse("3.2");
            var n310 = SignNumber.Parse("3.10");
            var n324 = SignNumber.Parse("3.24");
            TestAssert.True(n32.CompareTo(n310) < 0, "3.2 должно быть меньше 3.10");
            TestAssert.True(n310.CompareTo(n324) < 0, "3.10 должно быть меньше 3.24");
            TestAssert.True(SignNumber.Parse("2.3.7").CompareTo(SignNumber.Parse("2.4")) < 0, "2.3.7 < 2.4");
        }

        [Fact(DisplayName = "M1-N04: одиночный номер «6.2» не совпадает с 6.2.1")]
        public void SingleNumberDoesNotMatchVariant()
        {
            var set = SignNumberSet.Parse("6.2");
            TestAssert.True(set.Contains(SignNumber.Parse("6.2")));
            TestAssert.False(set.Contains(SignNumber.Parse("6.2.1")));
        }

        [Fact(DisplayName = "M1-N05: дефис, en dash, em dash, с пробелами и без")]
        public void RangeSeparators()
        {
            foreach (var text in new[] { "1.5-1.7", "1.5 - 1.7", "1.5–1.7", "1.5 — 1.7" })
            {
                var set = SignNumberSet.Parse(text);
                TestAssert.True(set.Contains(SignNumber.Parse("1.6")), text + " должен содержать 1.6");
            }
        }

        [Fact(DisplayName = "M1-N06: «6.14.2 - 6.16» не содержит 6.16.1")]
        public void UpperBoundaryExcludesVariants()
        {
            var set = SignNumberSet.Parse("6.14.2 - 6.16");
            TestAssert.True(set.Contains(SignNumber.Parse("6.15")));
            TestAssert.True(set.Contains(SignNumber.Parse("6.16")));
            TestAssert.False(set.Contains(SignNumber.Parse("6.16.1")));
        }

        [Fact(DisplayName = "M1-N07: «1.1 - 2.4» пересекает группы и содержит 2.3.5")]
        public void RangeCrossesGroups()
        {
            var set = SignNumberSet.Parse("1.1 - 2.4");
            TestAssert.True(set.Contains(SignNumber.Parse("2.3.5")));
            TestAssert.True(set.Contains(SignNumber.Parse("1.31")));
            TestAssert.False(set.Contains(SignNumber.Parse("2.4.1")));
        }

        [Fact(DisplayName = "M1-N08: список из одиночных номеров и диапазонов")]
        public void MixedList()
        {
            var set = SignNumberSet.Parse("1.5 - 1.7, 2.4");
            TestAssert.True(set.Contains(SignNumber.Parse("1.6")));
            TestAssert.True(set.Contains(SignNumber.Parse("2.4")));
            TestAssert.False(set.Contains(SignNumber.Parse("1.8")));
            TestAssert.Equal(1, set.Singles.Count);
            TestAssert.Equal(1, set.Ranges.Count);
        }

        [Fact(DisplayName = "M1-N09: некорректный список даёт FormatException, а не тихое ignore")]
        public void BadListThrows()
        {
            TestAssert.Throws<FormatException>(() => SignNumberSet.Parse("1.5 - , 2.4"));
            TestAssert.Throws<FormatException>(() => new SignNumberRange(SignNumber.Parse("3.24"), SignNumber.Parse("3.2")));
        }

        [Fact(DisplayName = "M1-N10: статусы проверки разбираются и StudentVerified — единственный проверенный")]
        public void VerificationStatuses()
        {
            TestAssert.Equal(Verification.Placeholder, VerificationParser.Parse("Placeholder"));
            TestAssert.Equal(Verification.CuratorExtracted, VerificationParser.Parse("CuratorExtracted"));
            TestAssert.True(VerificationParser.IsQuestionable(Verification.CuratorExtracted));
            TestAssert.False(VerificationParser.IsQuestionable(Verification.StudentVerified));
            TestAssert.Throws<FormatException>(() => VerificationParser.Parse("VerifiedByAI"));
        }

        [Fact(DisplayName = "M1-N11: VerificationSummary детерминирован и помечает черновик")]
        public void VerificationSummaryIsDeterministic()
        {
            var s = new VerificationSummary();
            s.Add("catalog", Verification.CuratorExtracted);
            s.Add("pictogram", Verification.Placeholder);
            s.Add("catalog", Verification.StudentVerified); // худший статус побеждает
            TestAssert.True(s.IsDraft);
            TestAssert.Equal("catalog=CuratorExtracted; pictogram=Placeholder", s.Describe());
        }
    }
}
