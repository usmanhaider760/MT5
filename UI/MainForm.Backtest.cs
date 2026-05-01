using System.Drawing;
using System.Windows.Forms;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.Backtesting;

namespace MT5TradingBot.UI
{
    public sealed partial class MainForm
    {
        // ── fields ──────────────────────────────────────────────────────
        private TabPage?          _tabBacktest;
        private EquityCurvePanel? _btChart;
        private Label?            _lblBtStats;
        private Label?            _lblBtSource;
        private RadioButton?      _rbBtDb;
        private RadioButton?      _rbBtCsv;
        private Button?           _btnBtBrowse;
        private Button?           _btnBtRun;
        private string            _btCsvPath = "";

        // ── tab setup ────────────────────────────────────────────────────
        private void EnsureBacktestTab()
        {
            if (_tabBacktest != null && _tabControl.TabPages.Contains(_tabBacktest)) return;

            _tabBacktest = new TabPage("  📊 Backtest  ");

            // Source selectors
            _rbBtDb  = new RadioButton { Text = "Trade History (DB)", Checked = true, AutoSize = true,
                                         ForeColor = Color.FromArgb(200, 200, 220), BackColor = Color.FromArgb(18, 18, 28) };
            _rbBtCsv = new RadioButton { Text = "CSV File",           Checked = false, AutoSize = true,
                                         ForeColor = Color.FromArgb(200, 200, 220), BackColor = Color.FromArgb(18, 18, 28) };
            _rbBtCsv.CheckedChanged += (_, _) => _btnBtBrowse!.Enabled = _rbBtCsv.Checked;

            _lblBtSource = new Label
            {
                Text      = "No CSV selected.",
                AutoSize  = false,
                Width     = 320,
                ForeColor = Color.FromArgb(130, 130, 160),
                Font      = new Font("Consolas", 8.5f)
            };

            _btnBtBrowse = new Button
            {
                Text      = "Browse…",
                Width     = 80,
                Height    = 26,
                Enabled   = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(35, 35, 55),
                ForeColor = Color.FromArgb(200, 200, 220)
            };
            _btnBtBrowse.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 100);
            _btnBtBrowse.Click += BtnBtBrowse_Click;

            _btnBtRun = new Button
            {
                Text      = "▶  Run Backtest",
                Width     = 130,
                Height    = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 200, 120),
                ForeColor = Color.FromArgb(10, 10, 20),
                Font      = new Font("Segoe UI Semibold", 9.5f)
            };
            _btnBtRun.FlatAppearance.BorderSize = 0;
            _btnBtRun.Click += async (_, _) => await RunBacktestAsync();

            // Stats label
            _lblBtStats = new Label
            {
                Dock      = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Font      = new Font("Consolas", 9f),
                ForeColor = Color.FromArgb(200, 200, 220),
                Text      = "Run a backtest to see results."
            };

            // Chart
            _btChart = new EquityCurvePanel { Dock = DockStyle.Fill };

            // Top toolbar
            var toolbar = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                Padding       = new Padding(4, 6, 4, 0)
            };
            toolbar.Controls.Add(_rbBtDb);
            toolbar.Controls.Add(_rbBtCsv);
            toolbar.Controls.Add(_btnBtBrowse);
            toolbar.Controls.Add(_lblBtSource);
            toolbar.Controls.Add(_btnBtRun);

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                Padding     = new Padding(8),
                ColumnCount = 1,
                RowCount    = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // toolbar
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // chart
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));   // stats

            layout.Controls.Add(toolbar,      0, 0);
            layout.Controls.Add(_btChart,     0, 1);
            layout.Controls.Add(_lblBtStats,  0, 2);

            _tabBacktest.BackColor = Color.FromArgb(18, 18, 28);
            _tabBacktest.Controls.Add(layout);

            int insertAt = _tabControl.TabPages.IndexOf(_tabClaude);
            if (insertAt < 0) insertAt = _tabControl.TabPages.Count;
            _tabControl.TabPages.Insert(insertAt, _tabBacktest);
        }

        // ── Browse CSV ───────────────────────────────────────────────────
        private void BtnBtBrowse_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "Select Trade History CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _btCsvPath          = dlg.FileName;
                _lblBtSource!.Text  = Path.GetFileName(_btCsvPath);
            }
        }

        // ── Run ──────────────────────────────────────────────────────────
        private async Task RunBacktestAsync()
        {
            if (_btnBtRun == null) return;
            _btnBtRun.Enabled = false;
            _lblBtStats!.Text      = "Running…";
            _lblBtStats.ForeColor  = Color.Gray;

            try
            {
                IBacktestDataLoader loader;

                if (_rbBtCsv!.Checked)
                {
                    if (string.IsNullOrWhiteSpace(_btCsvPath) || !File.Exists(_btCsvPath))
                    {
                        _lblBtStats.Text      = "No CSV file selected or file not found.";
                        _lblBtStats.ForeColor = Color.OrangeRed;
                        return;
                    }
                    loader = new CsvBacktestLoader(_btCsvPath);
                }
                else
                {
                    if (_tradeDb == null)
                    {
                        _lblBtStats.Text      = "Trade database not available.";
                        _lblBtStats.ForeColor = Color.OrangeRed;
                        return;
                    }
                    loader = new DbBacktestLoader(_tradeDb);
                }

                var trades = await loader.LoadAsync().ConfigureAwait(true);

                if (trades.Count == 0)
                {
                    _lblBtStats.Text      = "No closed trades found in selected source.";
                    _lblBtStats.ForeColor = Color.OrangeRed;
                    _btChart?.SetData([]);
                    return;
                }

                var svc    = new BacktestingService();
                var result = await svc.RunAsync(trades).ConfigureAwait(true);

                _btChart?.SetData(result.EquityCurve);
                ShowBacktestStats(result);
            }
            catch (Exception ex)
            {
                _lblBtStats!.Text      = $"Error: {ex.Message}";
                _lblBtStats.ForeColor  = Color.OrangeRed;
            }
            finally
            {
                _btnBtRun.Enabled = true;
            }
        }

        private void ShowBacktestStats(BacktestResult r)
        {
            string sign = r.NetProfitUsd >= 0 ? "+" : "";
            _lblBtStats!.Text =
                $"Trades: {r.TotalTrades}  |  " +
                $"Win Rate: {r.WinRatePercent:F1}%  ({r.WinningTrades}W / {r.LosingTrades}L)  |  " +
                $"Net P&L: {sign}{r.NetProfitUsd:F2} USD ({r.NetProfitPips:+0.0;-0.0} pips)  |  " +
                $"PF: {r.ProfitFactor:F2}  |  " +
                $"Max DD: {r.MaxDrawdownPct:F1}%  |  " +
                $"Sharpe: {r.SharpeRatio:F2}  |  " +
                $"Avg W: +{r.AvgWinUsd:F2}  Avg L: -{r.AvgLossUsd:F2}";

            _lblBtStats.ForeColor = r.NetProfitUsd >= 0
                ? Color.FromArgb(0, 200, 120)
                : Color.FromArgb(220, 60, 60);

            foreach (var note in r.Notes)
                Log($"[Backtest] {note}");
        }
    }
}
