using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hidra.Core;
using Hidra.ViewModels.Dashboard;

namespace Hidra.Views.Dialogs
{
    public partial class ActivityMonitorDialog : UserControl
    {
        private readonly ActivityMonitorViewModel _viewModel;

        public ActivityMonitorDialog(Context context)
        {
            _viewModel = new ActivityMonitorViewModel(context);
            DataContext = _viewModel;
            InitializeComponent();

            _viewModel.History.CollectionChanged += History_OnCollectionChanged;
            Unloaded += ActivityMonitorDialog_OnUnloaded;
        }

        private void History_OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RedrawGraph();
        }

        private void GraphCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawGraph();
        }

        private void RedrawGraph()
        {
            var width = GraphCanvas.ActualWidth;
            var height = GraphCanvas.ActualHeight;
            if (width <= 0 || height <= 0) return;

            var history = _viewModel.History;
            var max = Math.Max(_viewModel.PeakRate, 1) * 1.2;
            var stepX = width / (history.Count - 1);

            var points = new PointCollection();
            for (var i = 0; i < history.Count; i++)
            {
                var x = i * stepX;
                var y = height - (history[i] / max) * height;
                points.Add(new Point(x, y));
            }

            GraphLine.Points = points;
        }

        private void ActivityMonitorDialog_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.History.CollectionChanged -= History_OnCollectionChanged;
            _viewModel.Dispose();
        }
    }
}
