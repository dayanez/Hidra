using Hidra.Core.Managers;
using Hidra.Core.Utilities;
using Hidra.IOWrapper.DataTransferObjects;
using NUnit.Framework;

namespace Hidra.Tests.ManagerTests
{
    // BindingManager's bind-mode flow (BeginBindMode, InputChanged, ...) is glue between a WPF
    // Dispatcher and the native IOController and needs a live message pump to drive, so it isn't
    // a unit test target. IsInputValid is the one piece of real decision logic in that flow: it
    // decides whether a captured input is "deliberate enough" to accept as a new binding, so
    // that's what's covered here.
    [TestFixture]
    internal class BindingManagerTests
    {
        [TestCase(BindingCategory.Event, (short)0, true, TestName = "IsInputValid: an Event binding is always valid")]
        [TestCase(BindingCategory.Delta, (short)0, true, TestName = "IsInputValid: a Delta binding is always valid")]
        [TestCase(BindingCategory.Momentary, (short)0, false, TestName = "IsInputValid: a released Momentary binding is not valid")]
        [TestCase(BindingCategory.Momentary, (short)1, true, TestName = "IsInputValid: a pressed Momentary binding is valid")]
        [TestCase(BindingCategory.Signed, (short)0, false, TestName = "IsInputValid: a centered axis is not valid (too close to rest)")]
        [TestCase(BindingCategory.Signed, Constants.AxisMaxValue, false, TestName = "IsInputValid: a maxed-out axis is not valid (too close to the extreme)")]
        [TestCase(BindingCategory.Signed, (short)16384, true, TestName = "IsInputValid: a roughly half-deflected axis is valid")]
        public void IsInputValid_AcceptsOnlyDeliberateInput(BindingCategory category, short value, bool expected)
        {
            var result = BindingManager.IsInputValid(category, value);

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
