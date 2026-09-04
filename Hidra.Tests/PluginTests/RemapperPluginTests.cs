using Hidra.Core.Utilities;
using Hidra.Plugins.Remapper;
using NUnit.Framework;

namespace Hidra.Tests.PluginTests
{
    // These plugins only touch their own Outputs (via WriteOutput), so they can be exercised
    // directly without a Profile/Mapping/Context, unlike the Filter plugins covered in
    // FilterPluginTests.cs, which route through RuntimeMapping.FilterState. Each plugin here
    // calls InitializeCacheValues() before Update(), matching how SubscriptionsManager drives a
    // real plugin (see SubscriptionsManager.ActivateProfile), since that's what seeds the
    // dead zone/sensitivity helpers from the plugin's own properties.
    [TestFixture]
    internal class RemapperPluginTests
    {
        [Test]
        public void AxisToAxis_PassesThroughUnchangedByDefault()
        {
            var plugin = new AxisToAxis();
            plugin.InitializeCacheValues();

            plugin.Update(1000);

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(1000));
        }

        [Test]
        public void AxisToAxis_InvertsWhenInvertIsSet()
        {
            var plugin = new AxisToAxis { Invert = true };
            plugin.InitializeCacheValues();

            plugin.Update(1000);

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(-1000));
        }

        [Test]
        public void AxesToAxes_PassesThroughUnchangedByDefault()
        {
            var plugin = new AxesToAxes();
            plugin.InitializeCacheValues();

            plugin.Update(1000, -2000);

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(1000));
            Assert.That(plugin.Outputs[1].CurrentValue, Is.EqualTo(-2000));
        }

        [Test]
        public void AxesToAxes_InvertsEachAxisIndependently()
        {
            var plugin = new AxesToAxes { InvertX = true, InvertY = false };
            plugin.InitializeCacheValues();

            plugin.Update(1000, 2000);

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(-1000));
            Assert.That(plugin.Outputs[1].CurrentValue, Is.EqualTo(2000));
        }

        [TestCase(AxisMerger.AxisMergerMode.Average, (short)100, (short)50, (short)75, TestName = "AxisMerger: Average combines both axes")]
        [TestCase(AxisMerger.AxisMergerMode.Sum, (short)100, (short)50, (short)150, TestName = "AxisMerger: Sum adds both axes")]
        [TestCase(AxisMerger.AxisMergerMode.Greatest, (short)100, (short)-200, (short)-200, TestName = "AxisMerger: Greatest picks the larger-magnitude axis")]
        public void AxisMerger_CombinesTwoAxesByMode(AxisMerger.AxisMergerMode mode, short high, short low, short expected)
        {
            var plugin = new AxisMerger { Mode = mode };
            plugin.InitializeCacheValues();

            plugin.Update(high, low);

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(expected));
        }

        [Test]
        public void AxisSplitter_SplitsAPositiveValueIntoHighAndLow()
        {
            var plugin = new AxisSplitter();
            plugin.InitializeCacheValues();

            plugin.Update(Constants.AxisMaxValue);

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(Constants.AxisMaxValue));
            Assert.That(plugin.Outputs[1].CurrentValue, Is.EqualTo(Constants.AxisMinValue));
        }

        [Test]
        public void AxisInitializer_WritesOutputFromPercentageWhenActivated()
        {
            var plugin = new AxisInitializer { Percentage = 50 };

            // Unlike the other plugins here, AxisInitializer's Update() is a deliberate no-op;
            // it only ever writes through InitializeCacheValues (see AxisInitializer.cs).
            plugin.InitializeCacheValues();

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(16384));
        }

        [Test]
        public void AxisToAxisWithModifier_UsesModifierSensitivityOnlyWhileModifierIsHeld()
        {
            var plugin = new AxisToAxisWithModifier { Sensitivity = 100, ModifierSensitivity = 50 };
            plugin.InitializeCacheValues();

            plugin.Update(1000, 0);
            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(1000), "Sensitivity is 100 (a no-op) while the modifier is not held");

            plugin.Update(1000, 1);
            Assert.That(plugin.Outputs[0].CurrentValue, Is.Not.EqualTo(1000), "ModifierSensitivity (50) should actually transform the value once the modifier is held");
        }

        [TestCase(Constants.AxisMaxValue, (short)1, (short)0, TestName = "AxisToButton: Max sets the high button")]
        [TestCase(Constants.AxisMinValue, (short)0, (short)1, TestName = "AxisToButton: Min sets the low button")]
        [TestCase((short)0, (short)0, (short)0, TestName = "AxisToButton: centered sets neither button")]
        public void AxisToButton_MapsAxisSignToTwoButtons(short input, short expectedHigh, short expectedLow)
        {
            var plugin = new AxisToButton();
            plugin.InitializeCacheValues();

            plugin.Update(input);

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(expectedHigh));
            Assert.That(plugin.Outputs[1].CurrentValue, Is.EqualTo(expectedLow));
        }

        [TestCase((short)1, (short)0, Constants.AxisMaxValue, TestName = "ButtonsToAxis: only the first button pressed returns Max")]
        [TestCase((short)0, (short)1, Constants.AxisMinValue, TestName = "ButtonsToAxis: only the second button pressed returns Min")]
        [TestCase((short)0, (short)0, (short)0, TestName = "ButtonsToAxis: neither button pressed returns 0")]
        [TestCase((short)1, (short)1, (short)0, TestName = "ButtonsToAxis: both buttons pressed returns 0")]
        public void ButtonsToAxis_MapsButtonPairToAxisExtremes(short button0, short button1, short expected)
        {
            var plugin = new ButtonsToAxis();

            plugin.Update(button0, button1);

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(expected));
        }

        [Test]
        public void ButtonToAxis_OutputsRangeAndRangePressedByButtonState()
        {
            var plugin = new ButtonToAxis(); // Range = 0 (released), RangePressed = 100 (pressed) by default

            plugin.Update(0);
            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(0));

            plugin.Update(1);
            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(Constants.AxisMaxValue));
        }

        [Test]
        public void ButtonToAxis_OnActivateInitializesOutputWhenRequested()
        {
            var plugin = new ButtonToAxis { Initialize = true, Range = -100 };

            plugin.OnActivate();

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(Constants.AxisMinValue));
        }

        [TestCase((short)1, (short)1, true, false, (short)1, TestName = "ButtonToButtonWithModifier: modifier held and must be held: passes through")]
        [TestCase((short)1, (short)0, true, false, (short)0, TestName = "ButtonToButtonWithModifier: modifier not held but must be held: gated closed")]
        [TestCase((short)1, (short)0, false, false, (short)1, TestName = "ButtonToButtonWithModifier: modifier not held and must NOT be held: gate open")]
        [TestCase((short)0, (short)1, true, false, (short)0, TestName = "ButtonToButtonWithModifier: button not held: no output regardless of modifier")]
        [TestCase((short)1, (short)1, true, true, (short)0, TestName = "ButtonToButtonWithModifier: Invert flips an otherwise-passed-through press")]
        public void ButtonToButtonWithModifier_GatesOutputByModifierState(short button, short modifier, bool modifierMustBeHeld, bool invert, short expected)
        {
            var plugin = new ButtonToButtonWithModifier { ModifierMustBeHeld = modifierMustBeHeld, Invert = invert };

            plugin.Update(button, modifier);

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(expected));
        }

        [Test]
        public void ButtonToEvent_FiresOnPressWhenModeIsPress()
        {
            var plugin = new ButtonToEvent(); // Mode = Press by default

            plugin.Update(1);

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(1));
        }

        [Test]
        public void ButtonToEvent_DoesNotFireOnReleaseWhenModeIsPress()
        {
            var plugin = new ButtonToEvent();

            plugin.Update(0);

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(0));
        }

        [Test]
        public void ButtonToEvent_FiresOnReleaseWhenModeIsRelease()
        {
            var plugin = new ButtonToEvent { Mode = ButtonToEvent.ButtonToEventMode.Release };

            plugin.Update(0);

            Assert.That(plugin.Outputs[0].CurrentValue, Is.EqualTo(1));
        }
    }
}
