using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Hidra.Core.Models;
using Hidra.ViewModels.Dashboard;
using Hidra.ViewModels.ProfileViewModels;

namespace Hidra.Views.Dialogs
{
    public partial class AddMappingPluginDialog : UserControl
    {
        private AddMappingPluginDialogViewModel ViewModel { get; }

        public AddMappingPluginDialog(MappingViewModel mappingViewModel)
        {
            ViewModel = new AddMappingPluginDialogViewModel(mappingViewModel);
            DataContext = ViewModel;

            InitializeComponent();
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.SelectedPlugin = (SimplePluginViewModel)PluginList.SelectedItem;
            ViewModel.SelectionChanged();
        }
    }
}
