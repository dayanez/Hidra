using System.Collections.Generic;
using Hidra.Core;
using Hidra.Core.Managers;
using NUnit.Framework;

namespace Hidra.Tests.ManagerTests
{
    // ProcessProfileSwitcher itself polls the real foreground window on a DispatcherTimer, which
    // needs a live WPF message pump and isn't something to drive from a unit test. FindProfileFor
    // is the pure matching logic behind that poll (see ProcessProfileSwitcher.cs), so it's what's
    // covered here.
    [TestFixture]
    internal class ProcessProfileSwitcherTests
    {
        private Context _context;

        [SetUp]
        public void Setup()
        {
            _context = new Context();
        }

        [Test]
        public void FindProfileFor_MatchesByExecutableCaseInsensitively()
        {
            var profile = _context.ProfilesManager.CreateProfile("Game", null, null);
            profile.SetAutoSwitchExecutable("game.exe");
            _context.ProfilesManager.AddProfile(profile);

            var match = ProcessProfileSwitcher.FindProfileFor("GAME.EXE", _context.Profiles);

            Assert.That(match, Is.Not.Null);
            Assert.That(match.Guid, Is.EqualTo(profile.Guid));
        }

        [Test]
        public void FindProfileFor_MatchesANestedChildProfile()
        {
            var parentProfile = _context.ProfilesManager.CreateProfile("Parent", null, null);
            _context.ProfilesManager.AddProfile(parentProfile);
            var childProfile = _context.ProfilesManager.CreateProfile("Child", null, null);
            childProfile.SetAutoSwitchExecutable("game.exe");
            parentProfile.AddChildProfile(childProfile);

            var match = ProcessProfileSwitcher.FindProfileFor("game.exe", _context.Profiles);

            Assert.That(match, Is.Not.Null);
            Assert.That(match.Guid, Is.EqualTo(childProfile.Guid));
        }

        [Test]
        public void FindProfileFor_ReturnsNullWhenNoProfileMatches()
        {
            var profile = _context.ProfilesManager.CreateProfile("Game", null, null);
            profile.SetAutoSwitchExecutable("game.exe");
            _context.ProfilesManager.AddProfile(profile);

            var match = ProcessProfileSwitcher.FindProfileFor("other.exe", _context.Profiles);

            Assert.That(match, Is.Null);
        }

        [Test]
        public void FindProfileFor_ReturnsNullWhenNoProfileHasAnAutoSwitchExecutable()
        {
            var profile = _context.ProfilesManager.CreateProfile("Manual profile", null, null);
            _context.ProfilesManager.AddProfile(profile);

            var match = ProcessProfileSwitcher.FindProfileFor("game.exe", _context.Profiles);

            Assert.That(match, Is.Null);
        }
    }
}
