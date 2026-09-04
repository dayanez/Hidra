using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Hidra.Core;
using Hidra.Core.Managers;
using Hidra.Core.Models;
using Hidra.Core.Models.Binding;
using Hidra.Plugins.Remapper;
using Hidra.Tests.Factory;
using NUnit.Framework;

namespace Hidra.Tests.ModelTests
{
    [TestFixture]
    internal class ProfileTests
    {
        private Context _context;
        private Profile _profile;
        private Mapping _mapping;
        private string _profileName;

        [SetUp]
        public void Setup()
        {
            _context = new Context();
            var profile = _context.ProfilesManager.CreateProfile("Base Profile", null, null);
            _context.ProfilesManager.AddProfile(profile);
            _profile = _context.Profiles[0];
            _mapping = _profile.AddMapping("Test mapping");
            _profileName = "Test";
        }

        [Test]
        public void AddChildProfile()
        {
            Assert.That(_profile.ChildProfiles.Count, Is.EqualTo(0));
            var childProfile = _context.ProfilesManager.CreateProfile(_profileName, null, null);
            _profile.AddChildProfile(childProfile);
            Assert.That(_profile.ChildProfiles.Count, Is.EqualTo(1));
            Assert.That(_profile.ChildProfiles[0].Title, Is.EqualTo(_profileName));
            Assert.That(_profile.ChildProfiles[0].ParentProfile, Is.EqualTo(_profile));
            Assert.That(_profile.ChildProfiles[0].Guid, Is.Not.EqualTo(Guid.Empty));
            Assert.That(_profile.IsActive, Is.Not.True);
            Assert.That(_context.IsNotSaved, Is.True);
        }
        
        [Test]
        public void RemoveChildProfile()
        {
            Assert.That(_profile.ChildProfiles.Count, Is.EqualTo(0));
            var childProfile = _context.ProfilesManager.CreateProfile(_profileName, null, null);
            _profile.AddChildProfile(childProfile);
            Assert.That(_profile.ChildProfiles.Count, Is.EqualTo(1));
            Assert.That(_profile.ChildProfiles[0].Title, Is.EqualTo(_profileName));
            _profile.ChildProfiles[0].Remove();
            Assert.That(_profile.ChildProfiles.Count, Is.EqualTo(0));
            Assert.That(_context.IsNotSaved, Is.True);
        }

        [Test]
        public void RenameProfile()
        {
            var newName = "Renamed Profile";
            Assert.That(_profile.Rename(newName), Is.True);
            Assert.That(_profile.Title, Is.EqualTo(newName));
            Assert.That(_context.IsNotSaved, Is.True);
        }

        [Test]
        public void AddPlugin()
        {
            _profile.AddPlugin(_mapping, new ButtonToButton());
            var plugin = _mapping.Plugins[0];

            Assert.That(plugin, Is.Not.Null);
            Assert.That(plugin.Outputs, Is.Not.Null);
            Assert.That(plugin.Profile, Is.EqualTo(_profile));
            Assert.That(_context.IsNotSaved, Is.True);
        }

        [Test]
        public void CopyProfile()
        {
            var profileManager = new ProfilesManager(_context, _context.Profiles);
            var profile = _context.Profiles[0];
            profileManager.CopyProfile(profile, "Copy");
            var newProfile = _context.Profiles[1];

            Assert.That(newProfile.Guid, Is.Not.EqualTo(profile.Guid));
            Assert.That(newProfile.Title, Is.EqualTo("Copy"));
            Assert.That(newProfile.ParentProfile, Is.Null);
            Assert.That(newProfile.Context, Is.Not.Null);
        }

        [Test]
        public void CopyChildProfile()
        {
            var profileManager = new ProfilesManager(_context, _context.Profiles);
            var parentProfile = _context.Profiles[0];
            var childProfile = _context.ProfilesManager.CreateProfile("Child", null, null);
            parentProfile.AddChildProfile(childProfile);
            var profile = parentProfile.ChildProfiles[0];
            profileManager.CopyProfile(profile, "Copy");
            var newProfile = parentProfile.ChildProfiles[1];

            Assert.That(newProfile.Guid, Is.Not.EqualTo(profile.Guid));
            Assert.That(newProfile.Title, Is.EqualTo("Copy"));
            Assert.That(newProfile.ParentProfile.Guid, Is.EqualTo(parentProfile.Guid));
            Assert.That(newProfile.Context, Is.Not.Null);
        }

        // Regression test for the Guid-collision bug fixed alongside ProfilesManager.RemapGuids:
        // a copy used to keep the exact same Profile/DeviceConfiguration Guids as the original
        // throughout the whole copied subtree, which broke Profile.IsActive() (matches by Guid)
        // for nested child profiles, and left device bindings pointing at the original's device
        // configurations instead of the copy's own.
        [Test]
        public void CopyProfile_RemapsNestedChildProfileGuid()
        {
            var profileManager = new ProfilesManager(_context, _context.Profiles);
            var profile = _context.Profiles[0];
            var originalChildProfile = _context.ProfilesManager.CreateProfile("Child", null, null);
            profile.AddChildProfile(originalChildProfile);
            var originalChildGuid = originalChildProfile.Guid;

            profileManager.CopyProfile(profile, "Copy");
            var newProfile = _context.Profiles[1];

            Assert.That(newProfile.ChildProfiles[0].Guid, Is.Not.EqualTo(originalChildGuid));
            Assert.That(newProfile.ChildProfiles[0].Guid, Is.Not.EqualTo(Guid.Empty));
        }

        [Test]
        public void CopyProfile_RewritesDeviceBindingsToTheCopiedDeviceConfigurations()
        {
            var profileManager = new ProfilesManager(_context, _context.Profiles);
            var profile = _context.Profiles[0];

            var inputConfiguration = new DeviceConfiguration(DeviceFactory.CreateDevice("Keyboard", "Core_RawInputHook", "0", 0));
            profile.AddDeviceConfigurations(new List<DeviceConfiguration> { inputConfiguration }, DeviceIoType.Input);

            var outputConfiguration = new DeviceConfiguration(DeviceFactory.CreateDevice("Keyboard", "Core_RawInputHook", "0", 0));
            profile.AddDeviceConfigurations(new List<DeviceConfiguration> { outputConfiguration }, DeviceIoType.Output);

            profile.AddPlugin(_mapping, new ButtonToButton());
            _mapping.DeviceBindings[0].SetDeviceConfigurationGuid(inputConfiguration.Guid);
            _mapping.Plugins[0].Outputs[0].SetDeviceConfigurationGuid(outputConfiguration.Guid);

            profileManager.CopyProfile(profile, "Copy");
            var newProfile = _context.Profiles[1];
            var newInputConfiguration = newProfile.InputDeviceConfigurations[0];
            var newOutputConfiguration = newProfile.OutputDeviceConfigurations[0];

            // The copy's own device configurations get new Guids...
            Assert.That(newInputConfiguration.Guid, Is.Not.EqualTo(inputConfiguration.Guid));
            Assert.That(newOutputConfiguration.Guid, Is.Not.EqualTo(outputConfiguration.Guid));

            // ...and every binding that referenced the original Guid follows along to the new one,
            // on both the input side (Mapping.DeviceBindings) and the output side (Plugin.Outputs).
            Assert.That(newProfile.Mappings[0].DeviceBindings[0].DeviceConfigurationGuid, Is.EqualTo(newInputConfiguration.Guid));
            Assert.That(newProfile.Mappings[0].Plugins[0].Outputs[0].DeviceConfigurationGuid, Is.EqualTo(newOutputConfiguration.Guid));
        }

        [Test]
        public void CopyChildProfile_LeavesBindingsToAnAncestorDeviceConfigurationUnchanged()
        {
            var parentProfile = _context.Profiles[0];

            var inputConfiguration = new DeviceConfiguration(DeviceFactory.CreateDevice("Keyboard", "Core_RawInputHook", "0", 0));
            parentProfile.AddDeviceConfigurations(new List<DeviceConfiguration> { inputConfiguration }, DeviceIoType.Input);

            var childProfile = _context.ProfilesManager.CreateProfile("Child", null, null);
            parentProfile.AddChildProfile(childProfile);
            var childMapping = childProfile.AddMapping("Child mapping");
            childProfile.AddPlugin(childMapping, new ButtonToButton());
            // The child profile binds to a device configuration inherited from its parent, which
            // is outside the subtree CopyProfile duplicates, so it must keep pointing at the
            // original parent configuration rather than being rewritten.
            childMapping.DeviceBindings[0].SetDeviceConfigurationGuid(inputConfiguration.Guid);

            var profileManager = new ProfilesManager(_context, _context.Profiles);
            profileManager.CopyProfile(childProfile, "Copy");
            var newChildProfile = parentProfile.ChildProfiles[1];

            Assert.That(newChildProfile.Mappings[0].DeviceBindings[0].DeviceConfigurationGuid, Is.EqualTo(inputConfiguration.Guid));
        }
    }
}
