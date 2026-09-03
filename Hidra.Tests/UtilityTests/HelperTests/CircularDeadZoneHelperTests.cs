using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hidra.Core.Utilities;
using Hidra.Core.Utilities.AxisHelpers;
using NUnit.Framework;

namespace Hidra.Tests.UtilityTests.HelperTests
{
    [TestFixture]
    public class CircularDeadZoneHelperTests
    {
        [TestCase(Constants.AxisMaxValue, 0, ExpectedResult = new long[] { Constants.AxisMaxValue, 0 }, TestName = "CircularDeadZoneHelper (Init): Max returns Max")]
        [TestCase(Constants.AxisMinValue, 0, ExpectedResult = new long[] { Constants.AxisMinValue, 0 }, TestName = "CircularDeadZoneHelper (Init): Min returns Min")]
        [TestCase(0, 0, ExpectedResult = new long[] { 0, 0 }, TestName = "CircularDeadZoneHelper (Init): 0 returns 0")]
        public short[] CircularDeadZoneInitTests(short x, short y)
        {
            var helper = new CircularDeadZoneHelper();
            return helper.ApplyRangeDeadZone(new[] { x, y });
        }

        [TestCase(Constants.AxisMaxValue, 0, 50, ExpectedResult = new short[] { Constants.AxisMaxValue, 0 },
            TestName = "CircularDeadZoneHelper (50): Max X returns Max")]

        [TestCase(Constants.AxisMinValue, 0, 50, ExpectedResult = new short[] { Constants.AxisMinValue, 0 },
            TestName = "CircularDeadZoneHelper (50): Min X returns Min")]

        [TestCase(0, Constants.AxisMaxValue, 50, ExpectedResult = new short[] { 0, Constants.AxisMaxValue },
            TestName = "CircularDeadZoneHelper (50): Max Y returns Max")]

        [TestCase(0, Constants.AxisMinValue, 50, ExpectedResult = new short[] { 0, Constants.AxisMinValue },
            TestName = "CircularDeadZoneHelper (50): Min Y returns Min")]

        [TestCase(0, 0, 50, ExpectedResult = new short[] { 0, 0 },
            TestName = "CircularDeadZoneHelper (50): 0 returns 0")]

        [TestCase(Constants.AxisMaxValue, 0, 20, ExpectedResult = new short[] { Constants.AxisMaxValue, 0 },
            TestName = "CircularDeadZoneHelper (20): Max X returns Max")]

        [TestCase(Constants.AxisMinValue, 0, 20, ExpectedResult = new short[] { Constants.AxisMinValue, 0 },
            TestName = "CircularDeadZoneHelper (20): Min X returns Min")]

        [TestCase(0, Constants.AxisMaxValue, 20, ExpectedResult = new short[] { 0, Constants.AxisMaxValue },
            TestName = "CircularDeadZoneHelper (20): Max Y returns Max")]

        [TestCase(0, Constants.AxisMinValue, 20, ExpectedResult = new short[] { 0, Constants.AxisMinValue },
            TestName = "CircularDeadZoneHelper (20): Min Y returns Min")]

        [TestCase(0, 0, 20, ExpectedResult = new short[] { 0, 0 },
            TestName = "CircularDeadZoneHelper (20): 0 returns 0")]

        [TestCase(16384, 0, 50, ExpectedResult = new short[] { 1, 0 },
            TestName = "CircularDeadZoneHelper (50): Positive X values above 16383 are outside DZ")]

        [TestCase(-16384, 0, 50, ExpectedResult = new short[] { -1, 0 },
            TestName = "CircularDeadZoneHelper (50): Negative X values below 16383 are outside DZ")]

        [TestCase(16383, 0, 50, ExpectedResult = new short[] { 0, 0 },
            TestName = "CircularDeadZoneHelper (50): Positive X values below 16384 are inside DZ")]

        [TestCase(-16383, 0, 50, ExpectedResult = new short[] { 0, 0 },
            TestName = "CircularDeadZoneHelper (50): Negative X values below 16384 are inside DZ")]

        [TestCase(0, 16384, 50, ExpectedResult = new short[] { 0, 1 },
            TestName = "CircularDeadZoneHelper (50): Positive Y values above 16383 are outside DZ")]

        [TestCase(0, -16384, 50, ExpectedResult = new short[] { 0, -1 },
            TestName = "CircularDeadZoneHelper (50): Negative Y values below 16383 are outside DZ")]

        [TestCase(0, 16383, 50, ExpectedResult = new short[] { 0, 0 },
            TestName = "CircularDeadZoneHelper (50): Positive Y values below 16384 are inside DZ")]

        [TestCase(0, -16383, 50, ExpectedResult = new short[] { 0, 0 },
            TestName = "CircularDeadZoneHelper (50): Negative Y values below 16384 are inside DZ")]

        [TestCase(0, -16383, 50, ExpectedResult = new short[] { 0, 0 },
            TestName = "CircularDeadZoneHelper (50): Negative Y values below 16384 are inside DZ")]

        public short[] CircularDeadZoneValueTests(short x, short y, int percentage)
        {
            var helper = new CircularDeadZoneHelper { Percentage = percentage };
            return helper.ApplyRangeDeadZone(new[] { x, y });
        }

        // These test check that the deadzone is somewhat circular...
        // If passing +/- 16383 was INSIDE the DZ when passed for one axis...
        // (As tested by other tests)
        // ... but OUTSIDE the DZ when passed for both axes...
        // ... then the DZ cannot be square.
        [TestCase(-16383, -16383, 50,
            TestName = "CircularDeadZoneHelper (50): Deadzone appears Circular (Negative)")]
        [TestCase(16383, 16383, 50,
            TestName = "CircularDeadZoneHelper (50): Deadzone appears Circular (Positive)")]
        public void CircularDzOutsideTests(short x, short y, int percentage)
        {
            var helper = new CircularDeadZoneHelper { Percentage = percentage };
            var result = helper.ApplyRangeDeadZone(new short[] { -16383, -16383 });
            Assert.AreNotEqual(new short[] { 0, 0 }, result);
        }

        [TestCase(short.MinValue, short.MinValue, 50,
            TestName = "CircularDeadZoneHelper (50): Max negative range no overflow")]
        [TestCase(short.MaxValue, short.MaxValue, 50,
            TestName = "CircularDeadZoneHelper (50): Max positive range no overflow")]
        public void CircularDzMaxRange(short x, short y, int percentage)
        {
            var helper = new CircularDeadZoneHelper { Percentage = percentage };
            var result = helper.ApplyRangeDeadZone(new short[] { x, y });
            Assert.AreEqual(new short[] { x, y }, result);
        }

        [TestCase(101, ExpectedResult = 100, TestName = "CircularDeadZoneHelper (101): Percentages over 100 should clamp to 100")]
        [TestCase(-1, ExpectedResult = 0, TestName = "CircularDeadZoneHelper (-1): Percentages below 0 should clamp to 0")]
        public long CircularDeadZoneValidationTest(int percentage)
        {
            var helper = new CircularDeadZoneHelper();
            helper.Percentage = percentage;
            return helper.Percentage;
        }

        // Regression test: before Percentage was clamped, a negative value made _deadzoneRadius
        // negative, and for a centered stick (length 0) the scale calculation hit 0 * Infinity =
        // NaN, which could report a phantom deflection for input that was actually centered.
        [Test]
        public void CircularDeadZone_NegativePercentage_CenteredStickStaysZero()
        {
            var helper = new CircularDeadZoneHelper { Percentage = -50 };
            var result = helper.ApplyRangeDeadZone(new short[] { 0, 0 });
            Assert.AreEqual(new short[] { 0, 0 }, result);
        }
    }
}
