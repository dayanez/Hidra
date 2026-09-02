using System;

namespace Hidra.Core.Models.Subscription
{
    public class DeviceSubscription
    {
        public Guid DeviceSubscriptionGuid { get; }
        public Device Device { get; }

        public DeviceSubscription(Device device)
        {
            DeviceSubscriptionGuid = Guid.NewGuid();
            Device = device;
        }
    }
}
