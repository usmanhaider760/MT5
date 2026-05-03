namespace MT5TradingBot.Services
{
    public readonly record struct EdgeStatus(
        double WinRatePct,
        int    ConsecutiveLosses,
        int    SampleSize,
        bool   IsDegraded);

    public sealed class EdgeHealthMonitor
    {
        private readonly int    _windowSize;
        private readonly double _minWinRatePct;
        private readonly int    _maxConsecutiveLosses;

        // Circular window: true = win (profit > 0), false = loss
        private readonly Queue<bool> _window;
        private int _consecutiveLosses;

        public bool IsEdgeDegraded { get; private set; }

        public EdgeHealthMonitor(
            int windowSize,
            double minWinRatePct,
            int maxConsecutiveLosses)
        {
            _windowSize           = Math.Max(windowSize, 5);
            _minWinRatePct        = minWinRatePct;
            _maxConsecutiveLosses = maxConsecutiveLosses;
            _window               = new Queue<bool>(_windowSize + 1);
        }

        // Called once on startup to hydrate from persisted history.
        public void Seed(IEnumerable<double> recentProfits)
        {
            foreach (double p in recentProfits)
                RecordInternal(p > 0);
        }

        // Called each time a position closes.
        public EdgeStatus Record(double profit)
        {
            RecordInternal(profit > 0);
            return GetStatus();
        }

        public EdgeStatus GetStatus()
        {
            if (_window.Count == 0)
                return new EdgeStatus(100.0, 0, 0, false);

            int wins = _window.Count(w => w);
            double winPct = wins * 100.0 / _window.Count;

            bool degraded = (_window.Count >= _windowSize &&
                             winPct < _minWinRatePct)
                         || _consecutiveLosses >= _maxConsecutiveLosses;

            IsEdgeDegraded = degraded;
            return new EdgeStatus(winPct, _consecutiveLosses, _window.Count, degraded);
        }

        // -- internals --------------------------------------------

        private void RecordInternal(bool win)
        {
            if (_window.Count >= _windowSize)
                _window.Dequeue();
            _window.Enqueue(win);

            _consecutiveLosses = win ? 0 : _consecutiveLosses + 1;
        }
    }
}
