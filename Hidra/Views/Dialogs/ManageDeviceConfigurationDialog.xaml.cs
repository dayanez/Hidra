using System.Windows;
using System.Windows.Controls;
using Hidra.Core.Managers;
using Hidra.Core.Models;
using Hidra.ViewModels.Dashboard;
using Hidra.ViewModels.Dialogs;

namespace Hidra.Views.Dialogs
{
    public partial class ManageDeviceConfigurationDialog : UserControl
    {
        private ManageDeviceConfigurationViewModel ViewModel { get; set; }

        public ManageDeviceConfigurationDialog(DeviceConfiguration deviceConfiguration, DeviceIoType deviceIoType)
        {
            ViewModel = new ManageDeviceConfigurationViewModel(deviceConfiguration, deviceIoType);
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
