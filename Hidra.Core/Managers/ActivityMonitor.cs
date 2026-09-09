using System.Threading;

namespace Hidra.Core.Managers
{
    /// <summary>
    /// Counts remap events as they fire through the active profile, for the UI's live activity
    /// graph. RecordEvent() runs on whichever thread the input hook delivers on, not the UI
    /// thread, so the counter has to be thread-safe.
    /// </summary>
    public sealed class ActivityMonitor
    {
        private long _eventCount;

        public void RecordEvent()
        {
            Interlocked.Increment(ref _eventCount);
        }

        public long TakeEventCount()
        {
            return Interlocked.Exchange(ref _eventCount, 0);
        }
    }
}
