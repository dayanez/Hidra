using Hidra.Core;
using Hidra.Core.Models;
using Hidra.Core.Models.Subscription;
using Hidra.Core.Utilities;
using Hidra.Plugins.Filter;
using NUnit.Framework;

namespace Hidra.Tests.PluginTests
{
    // Unlike RemapperPluginTests, these plugins write to a filter (WriteFilterState /
    // ToggleFilterState) rather than to their own Outputs, which needs a real Mapping wired up
    // to a FilterState via PrepareMapping, matching how SubscriptionsManager.ActivateProfile
    // does it for a live profile.
    [TestFixture]
    internal class FilterPluginTests
    {
        private Context _context;
        private Profile _profile;
        private Mapping _mapping;
        private FilterState _filterState;
        private const string FilterName = "test-filter";

        [SetUp]
        public void Setup()
        {
            _context = new Context();
            var profile = _context.ProfilesManager.CreateProfile("Base Profile", null, null);
            _context.ProfilesManager.AddProfile(profile);
            _profile = _context.Profiles[0];
            _mapping = _profile.AddMapping("Test mapping");
            _filterState = new FilterState();
            // FilterState.SetFilterState reads the dictionary before writing it, so the filter
            // needs an initial value seeded here the same way SubscriptionsManager seeds every
            // filter a profile declares (via Profile.GetFilters()) before activating it.
            _filterState.FilterRuntimeDictionary[FilterName] = false;
        }

        private T AddPlugin<T>(T plugin) where T : Plugin
        {
            _profile.AddPlugin(_mapping, plugin);
            _mapping.PrepareMapping(_filterState);
            return plugin;
        }

        [Test]
        public void ButtonToFilter_SetsActiveOnPressAndInactiveOnRelease()
        {
            var plugin = AddPlugin(new ButtonToFilter { FilterName = FilterName });

            plugin.Update(1);
            Assert.That(_filterState.FilterRuntimeDictionary[FilterName], Is.True);

            plugin.Update(0);
            Assert.That(_filterState.FilterRuntimeDictionary[FilterName], Is.False);
        }

        [Test]
        public void AxisToFilter_ChangesStateOnlyWhenCrossingTheBoundary()
        {
            // Default bounds are -50%..50%; FilterStateExiting = Active, FilterStateEntering = Inactive.
            var plugin = AddPlugin(new AxisToFilter { FilterName = FilterName });

            plugin.Update(Constants.AxisMaxValue);
            Assert.That(_filterState.FilterRuntimeDictionary[FilterName], Is.True, "Leaving the bounds should apply FilterStateExiting (Active)");

            plugin.Update(0);
            Assert.That(_filterState.FilterRuntimeDictionary[FilterName], Is.False, "Re-entering the bounds should apply FilterStateEntering (Inactive)");
        }
    }
}
