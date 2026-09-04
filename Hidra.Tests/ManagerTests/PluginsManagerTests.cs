using System.Linq;
using Hidra.Core.Managers;
using Hidra.Plugins.Remapper;
using NUnit.Framework;

namespace Hidra.Tests.ManagerTests
{
    // Relies on the Plugins\Hidra.Plugins\Hidra.Plugins.dll folder that Hidra.Tests.csproj's
    // CopyPluginsForTests target copies into the test output, the same MEF discovery mechanism
    // Hidra.csproj's build sets up for the real app (see Hidra.Tests.csproj for the full
    // explanation, and ProfileTests.cs for the other test area this same setup unblocked).
    [TestFixture]
    internal class PluginsManagerTests
    {
        [Test]
        public void Constructor_DiscoversBuiltInPluginsViaMef()
        {
            var pluginsManager = new PluginsManager("Plugins");

            Assert.That(pluginsManager.Plugins, Is.Not.Empty);
            Assert.That(pluginsManager.Plugins.Any(p => p is ButtonToButton), Is.True);
        }

        [Test]
        public void Constructor_ReturnsNoPluginsForAMissingDirectory()
        {
            var pluginsManager = new PluginsManager("NoSuchPluginsDirectory");

            Assert.That(pluginsManager.Plugins, Is.Null.Or.Empty);
        }

        [Test]
        public void GetNewPlugin_ReturnsAFreshInstanceOfTheSameType()
        {
            var pluginsManager = new PluginsManager("Plugins");
            var existingPlugin = pluginsManager.Plugins.First(p => p is ButtonToButton);

            var newPlugin = pluginsManager.GetNewPlugin(existingPlugin);

            Assert.That(newPlugin, Is.Not.SameAs(existingPlugin));
            Assert.That(newPlugin, Is.InstanceOf<ButtonToButton>());
        }
    }
}
