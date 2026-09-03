using System.Windows.Controls;
using Hidra.ViewModels.Dialogs;

namespace Hidra.Views.Dialogs
{
    public partial class AlertDialog : UserControl
    {
        public AlertDialog(string title, string description)
        {
            DataContext = new AlertDialogViewModel()
            {
                Title = title,
                Description = description
            };
            InitializeComponent();
        }
    }
}
