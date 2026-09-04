using System;
using Hidra.Core.Models.Binding;

namespace Hidra.Core.Models.Subscription
{
    public class InputSubscription
    {
        public DeviceBinding DeviceBinding { get; }
        public Profile Profile { get; }
        public Guid SubscriptionStateGuid { get; set; }
        public Guid DeviceBindingSubscriptionGuid { get; set; }
        public bool IsOverwritten { get; set; }
        // Null when GetDeviceConfiguration() can't find the device configuration this binding
        // refers to (e.g. a device that's no longer connected); the constructor returns early in
        // that case rather than constructing a DeviceSubscription.
        public DeviceSubscription? DeviceSubscription { get; }

        public InputSubscription(Mapping mapping, DeviceBinding deviceBinding, Profile profile, Guid subscriptionStateGuid)
        {
            DeviceBinding = deviceBinding;
            Profile = profile;
            SubscriptionStateGuid = subscriptionStateGuid;
            DeviceBindingSubscriptionGuid = Guid.NewGuid();
            IsOverwritten = false;

            var deviceConfiguration = GetDeviceConfiguration();
            if (deviceConfiguration == null) return;

            var device = mapping.IsShadowMapping
                ? deviceConfiguration.ShadowDevices[mapping.ShadowDeviceNumber]
                : deviceConfiguration.Device;

            DeviceSubscription = new DeviceSubscription(device);
        }

        private DeviceConfiguration? GetDeviceConfiguration()
        {
            return Profile.GetDeviceConfiguration(DeviceBinding.DeviceIoType, DeviceBinding.DeviceConfigurationGuid);
        }
    }
}
