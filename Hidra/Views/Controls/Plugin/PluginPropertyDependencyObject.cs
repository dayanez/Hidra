using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Hidra.Core.Models;
using Hidra.ViewModels.ProfileViewModels;

namespace Hidra.Views.Controls.Plugin
{
    public class PluginPropertyDependencyObject : DependencyObject
    {
        public static readonly DependencyProperty PluginPropertyProperty = DependencyProperty.Register("PluginProperty", typeof(PluginProperty), typeof(PluginPropertyDependencyObject), new PropertyMetadata(default(PluginProperty)));

        public PluginProperty PluginProperty
        {
            get { return (PluginProperty) GetValue(PluginPropertyProperty); }
            set { SetValue(PluginPropertyProperty, value); }
        }
    }
}
