using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hidra.Core.Annotations;
using Hidra.Core.Models;

namespace Hidra.ViewModels.Dashboard
{
    public class DeviceItem : INotifyPropertyChanged
    {
        public string Title => DeviceConfiguration.GetFullTitleForProfile(Profile);

        public string ProviderName => DeviceConfiguration.Device.ProviderName;

        public DeviceConfiguration DeviceConfiguration { get; set; }
        private Profile Profile { get; set; }

        public DeviceItem(DeviceConfiguration deviceConfiguration, Profile profile)
        {
            DeviceConfiguration = deviceConfiguration;
            Profile = profile;
        }

        public void TitleChanged()
        {
            OnPropertyChanged(nameof(Title));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}