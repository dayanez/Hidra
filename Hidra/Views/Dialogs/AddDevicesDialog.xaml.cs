using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Hidra.Core.Models;
using Hidra.ViewModels.Dashboard;

namespace Hidra.Views.Dialogs
{
    public partial class AddDevicesDialog : UserControl
    {
        public AddDevicesDialog(List<Device> devices, DeviceIoType deviceIoType)
        {
            DataContext = new AddDevicesDialogViewModel(devices, deviceIoType);

            InitializeComponent();
        }
    }
}
