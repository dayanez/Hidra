using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Hidra.Core.Models
{
    public class DeviceConfiguration
    {
        [XmlAttribute]
        public Guid Guid { get; set; }
        // Null only in the gap between XmlSerializer using the parameterless constructor below
        // and the rest of deserialization populating it, same as Profile.Context.
        public Device Device { get; set; } = null!;
        [XmlAttribute]
        public string? ConfigurationName { get; set; }
        public List<Device> ShadowDevices { get; set; } = new List<Device>();

        [XmlIgnore]
        public int DeviceCount => 1 + ShadowDevices.Count;

        public DeviceConfiguration()
        {
            Guid = Guid.NewGuid();
        }

        public DeviceConfiguration(Device device) : this()
        {
            Device = device;
            ConfigurationName = null;
        }

        public void ChangeConfigurationName(string name)
        {
            // A DeviceConfiguration only has ChangeConfigurationName called on it once it's part
            // of a loaded profile, at which point Profile.GetDeviceConfigurationList has already
            // set Device.Profile (see Device.cs).
            Device.Profile!.Context.ContextChanged();
            if (string.IsNullOrEmpty(name))
            {
                ConfigurationName = null;
                return;
            }

            ConfigurationName = name;
        }

        public void ChangeShadowDevices(List<Device> shadowDevices)
        {
            Device.Profile!.Context.ContextChanged();
            ShadowDevices = shadowDevices;
        }

        public List<Device> getAvailableShadowDevices(DeviceIoType deviceIoType)
        {
            var availableDevices = Device.Profile!.Context.DevicesManager.GetAvailableDevicesListFromSameProvider(deviceIoType, Device);
            return availableDevices.Where(d => !d.Equals(Device)).ToList();
        }

        public string GetFullTitleForProfile(Profile? profile)
        {
            var title = ConfigurationName ?? Device.Title;
            if (profile == null || Device.Profile!.Guid == profile.Guid) return ConfigurationName ?? Device.Title;

            return $"{title} (Inherited from {Device.Profile.Title})";
        }
    }
}
