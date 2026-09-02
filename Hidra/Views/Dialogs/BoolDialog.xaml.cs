using System.Windows.Controls;
using Hidra.ViewModels.Dialogs;

namespace Hidra.Views.Dialogs
{
    public partial class BoolDialog : UserControl
    {
        public BoolDialog(string title, string description)
        {
            DataContext = new BoolDialogViewModel()
            {
                Title = title,
                Description = description
            };
            InitializeComponent();
        }
    }
}
