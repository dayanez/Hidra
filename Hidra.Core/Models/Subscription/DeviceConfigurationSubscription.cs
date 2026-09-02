using System;
using System.Collections.Generic;

namespace Hidra.Core.Models.Subscription
{
    public class DeviceConfigurationSubscription
    {
        public DeviceConfiguration DeviceConfiguration { get; }

        public DeviceSubscription DeviceSubscription { get; }
        public List<DeviceSubscription> ShadowDeviceSubscriptions { get; }

        public DeviceConfigurationSubscription(DeviceConfiguration deviceConfiguration)
        {
            DeviceConfiguration = deviceConfiguration;

            DeviceSubscription = new DeviceSubscription(deviceConfiguration.Device);

            ShadowDeviceSubscriptions = new List<DeviceSubscription>();
            deviceConfiguration.ShadowDevices.ForEach(shadowDevice => ShadowDeviceSubscriptions.Add(new DeviceSubscription(shadowDevice)));
        }
    }
}
