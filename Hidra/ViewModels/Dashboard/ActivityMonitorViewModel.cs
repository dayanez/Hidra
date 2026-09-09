using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Hidra.Core;
using Hidra.Core.Annotations;

namespace Hidra.ViewModels.Dashboard
{
    public class ActivityMonitorViewModel : INotifyPropertyChanged, IDisposable
    {
        private const int HistoryLength = 60;
        private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(500);

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<double> History { get; } = new ObservableCollection<double>();

        private double _currentRate;
        public double CurrentRate
        {
            get => _currentRate;
            private set { _currentRate = value; OnPropertyChanged(); }
        }

        private double _peakRate;
        public double PeakRate
        {
            get => _peakRate;
            private set { _peakRate = value; OnPropertyChanged(); }
        }

        private readonly Context _context;
        private readonly DispatcherTimer _timer;

        public ActivityMonitorViewModel(Context context)
        {
            _context = context;
            for (var i = 0; i < HistoryLength; i++) History.Add(0);

            _timer = new DispatcherTimer { Interval = SampleInterval };
            _timer.Tick += (sender, args) => Sample();
            _timer.Start();
        }

        private void Sample()
        {
            var eventsPerSample = _context.ActivityMonitor.TakeEventCount();
            var rate = eventsPerSample / SampleInterval.TotalSeconds;

            CurrentRate = rate;
            if (rate > PeakRate) PeakRate = rate;

            History.RemoveAt(0);
            History.Add(rate);
        }

        public void Dispose()
        {
            _timer.Stop();
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
