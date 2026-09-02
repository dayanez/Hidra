using System.Windows;
using System.Windows.Controls;
using Hidra.ViewModels.ProfileViewModels;

namespace Hidra.Views.Controls
{
    public partial class MappingCardControl : UserControl
    {
        public MappingCardControl()
        {
            InitializeComponent();
        }

        private void Remove_OnClick(object sender, RoutedEventArgs e)
        {
            var mappingViewModel = DataContext as MappingViewModel;
            mappingViewModel?.Remove();
        }

        private void Rename_OnClick(object sender, RoutedEventArgs e)
        {
            var mappingViewModel = DataContext as MappingViewModel;
            mappingViewModel?.Rename();
        }

        private void AddPlugin_OnClick(object sender, RoutedEventArgs e)
        {
            var mappingViewModel = DataContext as MappingViewModel;
            mappingViewModel?.AddPlugin();
        }
    }
}
