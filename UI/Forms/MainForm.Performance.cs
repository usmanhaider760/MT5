using System.Drawing;
using System.Windows.Forms;
using MT5TradingBot.Data;
using MT5TradingBot.Services;

namespace MT5TradingBot.UI
{
    public sealed partial class MainForm
    {
        private TabPage? _tabPerformance;
        private EquityCurvePanel? _perfChart;
        private Label? _lblPerfStats;
        private Button? _btnPerfLast20;
        private Button? _btnPerfLast100;
        private Button? _btnPerfAll;
        private int _perfLimit = 50;

        private void EnsurePerformanceTab()
        {
            if (_tabPerformance != null && _tabControl.TabPages.Contains(_tabPerformance))
                return;

            _tabPerformance = new TabPage("  📈 Performance  ")
            {
                BackColor = Color.FromArgb(18, 18, 28)
            };

            _btnPerfLast20 = CreatePerformanceButton("Last 20");
            _btnPerfLast100 = CreatePerformanceButton("Last 100");
            _btnPerfAll = CreatePerformanceButton("All Time");

            _btnPerfLast20.Click += (_, _) => { _perfLimit = 20; _ = RefreshPerformanceAsync(); };
            _btnPerfLast100.Click += (_, _) => { _perfLimit = 100; _ = RefreshPerformanceAsync(); };
            _btnPerfAll.Click += (_, _) => { _perfLimit = 0; _ = RefreshPerformanceAsync(); };

            _perfChart = new EquityCurvePanel { Dock = DockStyle.Fill };
            _lblPerfStats = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Consolas", 9f),
                ForeColor = Color.FromArgb(200, 200, 220),
                Text = "Select a period to load performance data."
            };

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(4, 6, 4, 0)
            };
            toolbar.Controls.Add(_btnPerfLast20);
            toolbar.Controls.Add(_btnPerfLast100);
            toolbar.Controls.Add(_btnPerfAll);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                ColumnCount = 1,
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(_perfChart, 0, 1);
            layout.Controls.Add(_lblPerfStats, 0, 2);

            _tabPerformance.Controls.Add(layout);

            int insertAt = _tabControl.TabPages.IndexOf(_tabClaude);
            if (insertAt < 0)
                insertAt = _tabControl.TabPages.Count;
            _tabControl.TabPages.Insert(insertAt, _tabPerformance);
        }

        private static Button CreatePerformanceButton(string text)
        {
            var button = new Button
            {
                Text = text,
                Width = 86,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(35, 35, 55),
                ForeColor = Color.FromArgb(200, 200, 220),
                Font = new Font("Segoe UI Semibold", 9f)
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 100);
            return button;
        }

        private async Task RefreshPerformanceAsync()
        {
            if (_lblPerfStats == null)
                return;

            if (_tradeDb == null)
            {
                _lblPerfStats.Text = "Database not available.";
                return;
            }

            _lblPerfStats.Text = "Loading...";
            _lblPerfStats.ForeColor = Color.Gray;

            try
            {
                IReadOnlyList<TradeRecord> records = _perfLimit > 0
                    ? await _tradeDb.GetRecentClosedAsync(_perfLimit).ConfigureAwait(true)
                    : await _tradeDb.GetByDateRangeAsync(
                        DateTime.UtcNow.AddYears(-10), DateTime.UtcNow).ConfigureAwait(true);

                if (records.Count == 0)
                {
                    _lblPerfStats.Text = "No closed trades in database.";
                    _lblPerfStats.ForeColor = Color.FromArgb(130, 130, 160);
                    _perfChart?.SetData([]);
                    return;
                }

                var summary = PerformanceCalculator.Calculate(records);
                _perfChart?.SetData(summary.EquityCurve);

                string sign = summary.NetProfitUsd >= 0 ? "+" : "";
                _lblPerfStats.Text =
                    $"Trades: {summary.TotalTrades}  |  " +
                    $"Win Rate: {summary.WinRatePct:F1}%  " +
                    $"({summary.WinCount}W / {summary.LossCount}L)  |  " +
                    $"Net P&L: {sign}{summary.NetProfitUsd:F2} USD  |  " +
                    $"Max DD: {summary.MaxDrawdownPct:F1}%  |  " +
                    $"Sharpe: {summary.SharpeRatio:F2}";
                _lblPerfStats.ForeColor = summary.NetProfitUsd >= 0
                    ? Color.FromArgb(0, 200, 120)
                    : Color.FromArgb(220, 60, 60);
            }
            catch (Exception ex)
            {
                _lblPerfStats.Text = $"Error: {ex.Message}";
                _lblPerfStats.ForeColor = Color.OrangeRed;
            }
        }
    }
}
