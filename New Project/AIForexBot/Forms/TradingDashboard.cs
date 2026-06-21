using System.Drawing;
using System.Windows.Forms;

namespace AIForexBot.Forms
{
    public partial class TradingDashboard : Form
    {
        // ── Palette ───────────────────────────────────────────────────
        internal static readonly Color BgDark        = Color.FromArgb(14,  14,  21);
        internal static readonly Color BgCard        = Color.FromArgb(25,  25,  42);
        internal static readonly Color BgCardHeader  = Color.FromArgb(18,  18,  32);
        internal static readonly Color BgHeader      = Color.FromArgb(10,  10,  18);
        internal static readonly Color BgButton      = Color.FromArgb(30,  30,  55);
        internal static readonly Color BgInput       = Color.FromArgb(20,  20,  36);
        internal static readonly Color BorderCol     = Color.FromArgb(46,  48,  85);
        internal static readonly Color TextPrimary   = Color.FromArgb(221, 224, 240);
        internal static readonly Color TextSecondary = Color.FromArgb(128, 136, 192);
        internal static readonly Color TextMuted     = Color.FromArgb(80,  88,  140);
        internal static readonly Color ColGreen      = Color.FromArgb(61,  221, 128);
        internal static readonly Color ColRed        = Color.FromArgb(240, 85,  85);
        internal static readonly Color ColYellow     = Color.FromArgb(240, 176, 48);

        public TradingDashboard()
        {
            InitializeComponent();
            ConfigureTabDrawing();
            LoadSampleData();
        }

        // ── Dark tab header owner-draw ────────────────────────────────
        private void ConfigureTabDrawing()
        {
            tabMain.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabMain.DrawItem += TabMain_DrawItem;
        }

        private void TabMain_DrawItem(object? sender, DrawItemEventArgs e)
        {
            var tab = tabMain.TabPages[e.Index];
            bool selected = (tabMain.SelectedIndex == e.Index);

            Color bg = selected ? BgCard : BgHeader;
            Color fg = selected ? TextPrimary : TextSecondary;

            using var bgBrush = new SolidBrush(bg);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            if (selected)
            {
                using var accentPen = new Pen(ColGreen, 2);
                e.Graphics.DrawLine(accentPen, e.Bounds.Left, e.Bounds.Top,
                                               e.Bounds.Right, e.Bounds.Top);
            }

            using var fgBrush = new SolidBrush(fg);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(tab.Text, tabMain.Font, fgBrush, e.Bounds, sf);
        }

        // ── Sample data (UI preview only) ─────────────────────────────
        private void LoadSampleData()
        {
            // Header
            lblConnectionStatus.Text      = "● CONNECTED";
            lblConnectionStatus.ForeColor = ColGreen;

            // Account Summary
            lblBalance.Text         = "$10,000.00";
            lblEquity.Text          = "$10,250.00";
            lblMargin.Text          = "$500.00";
            lblFreeMargin.Text      = "$9,750.00";
            lblDailyProfitLoss.Text = "+$250.00";
            lblDailyProfitLoss.ForeColor = ColGreen;

            // Signal Panel
            lblAIScore.Text              = "87";
            lblSignalDirection.Text      = "▲  BUY";
            lblSignalDirection.ForeColor = ColGreen;
            lblSignalConfidence.Text     = "HIGH";
            lblSignalConfidence.ForeColor= ColGreen;
            lblSignalReason.Text         = "Trend + RSI + BB Breakout";

            // Risk Summary
            lblRiskPercent.Text       = "1.0 %";
            lblLotSize.Text           = "0.10";
            lblMaxDailyLossValue.Text = "$300.00";
            lblTradesToday.Text       = "3 / 10";

            // Symbol Watch grid
            gridSymbolWatch.Rows.Add("XAUUSD", "1920.50", "1920.70", "20", "▲ UP",   "BUY");
            gridSymbolWatch.Rows.Add("EURUSD", "1.0850",  "1.0852",  "2",  "▼ DOWN", "SELL");
            gridSymbolWatch.Rows.Add("GBPUSD", "1.2650",  "1.2653",  "3",  "→ FLAT", "WAIT");
            gridSymbolWatch.Rows.Add("USDJPY", "154.20",  "154.23",  "3",  "▲ UP",   "BUY");
            ColorGridRows(gridSymbolWatch, 5, new[] { ColGreen, ColRed, TextSecondary, ColGreen });

            // Open Trades grid
            gridOpenTrades.Rows.Add("#12001", "XAUUSD", "BUY",  "0.10", "1919.00", "1915.00", "1925.00", "+$24.50");
            gridOpenTrades.Rows.Add("#12002", "EURUSD", "SELL", "0.05", "1.0870",  "1.0890",  "1.0840",  "-$8.00");
            ColorGridRows(gridOpenTrades, 7, new[] { ColGreen, ColRed });

            // Signals grid
            gridSignals.Rows.Add("09:15:32", "XAUUSD", "BUY",  "87", "HIGH",   "Trend + RSI + BB",     "Taken");
            gridSignals.Rows.Add("08:45:10", "GBPUSD", "SELL", "72", "MEDIUM", "MACD Cross + SR Level", "Skipped");
            gridSignals.Rows.Add("08:30:00", "EURUSD", "BUY",  "55", "LOW",    "RSI Oversold",          "Pending");
            gridSignals.Rows.Add("08:00:00", "USDJPY", "BUY",  "91", "HIGH",   "Breakout + Volume",     "Taken");
            ColorGridRows(gridSignals, 2, new[] { ColGreen, ColRed, ColYellow, ColGreen });

            // Backtest results placeholder
            gridBacktestResults.Rows.Add("2024-01-01", "2024-12-31", "XAUUSD", "342", "68%", "+$4,120", "1.87");

            // Logs
            AppendLog("[09:15:33] [INFO]  Trade placed: #12001  XAUUSD BUY 0.10 @ 1920.50", TextPrimary);
            AppendLog("[09:15:32] [INFO]  Signal generated: XAUUSD BUY | Score 87 | HIGH confidence", ColGreen);
            AppendLog("[08:45:10] [WARN]  Signal skipped: Spread 25 pips exceeds limit of 20",        ColYellow);
            AppendLog("[08:30:05] [ERROR] Connection lost — retrying in 5s...",                        ColRed);
            AppendLog("[08:30:00] [INFO]  Bot started — monitoring XAUUSD, EURUSD, GBPUSD",           TextPrimary);
        }

        private static void ColorGridRows(DataGridView grid, int profitCol, Color[] rowColors)
        {
            for (int i = 0; i < grid.Rows.Count && i < rowColors.Length; i++)
            {
                if (grid.Rows[i].Cells.Count > profitCol)
                    grid.Rows[i].Cells[profitCol].Style.ForeColor = rowColors[i];

                if (grid.Columns.Count > 2)
                {
                    var dirCell = grid.Rows[i].Cells[2];
                    if (dirCell.Value?.ToString()?.Contains("BUY")  == true) dirCell.Style.ForeColor = ColGreen;
                    if (dirCell.Value?.ToString()?.Contains("SELL") == true) dirCell.Style.ForeColor = ColRed;
                }
            }
        }

        private void AppendLog(string message, Color color)
        {
            txtBotLogs.SelectionStart  = txtBotLogs.TextLength;
            txtBotLogs.SelectionLength = 0;
            txtBotLogs.SelectionColor  = color;
            txtBotLogs.AppendText(message + "\n");
            txtBotLogs.SelectionColor  = TextPrimary;
        }

        // ── Button stubs (logic wired later) ─────────────────────────
        private void btnStartBot_Click(object sender, EventArgs e) { }
        private void btnStopBot_Click(object sender, EventArgs e) { }
        private void btnPauseBot_Click(object sender, EventArgs e) { }
        private void btnEmergencyStop_Click(object sender, EventArgs e) { }
        private void btnClearLogs_Click(object sender, EventArgs e) => txtBotLogs.Clear();
        private void btnRunBacktest_Click(object sender, EventArgs e) { }
    }
}
