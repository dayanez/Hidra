using System.Windows;
using System.Windows.Controls;
using Hidra.Core.Managers;
using Hidra.ViewModels.Dashboard;
using Hidra.ViewModels.Dialogs;

namespace Hidra.Views.Dialogs
{
    public partial class CreateProfileDialog : UserControl
    {
        private CreateProfileDialogViewModel ViewModel { get; set; }

        public CreateProfileDialog(string title, DevicesManager devicesManager)
        {
            ViewModel = new CreateProfileDialogViewModel(title, devicesManager);
            DataContext = ViewModel;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TextValue.SelectAll();
        }
    }
}
