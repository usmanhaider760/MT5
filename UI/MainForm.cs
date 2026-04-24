using MT5TradingBot.Core;
using MT5TradingBot.Models;
using MT5TradingBot.Services;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MT5TradingBot.UI
{
    public sealed partial class MainForm : Form
    {
        private MT5Bridge? _bridge;
        private AutoBotService? _bot;
        private ClaudeSignalService? _claude;

        private readonly SettingsManager _settings = new();
        private AppSettings _cfg = new();

        private readonly System.Windows.Forms.Timer _refreshTimer = new();

        private static readonly Color C_GREEN = Color.FromArgb(72, 199, 142);
        private static readonly Color C_RED = Color.FromArgb(252, 95, 95);
        private static readonly Color C_ACCENT = Color.FromArgb(99, 179, 237);

        public MainForm()
        {
            InitializeComponent();
            WireEvents();
            InitTimers();
        }

        private void InitTimers()
        {
            _refreshTimer.Interval = 2000;
            _refreshTimer.Tick += async (_, __) => await RefreshAsync();
        }

        // ───────────────── EVENTS ─────────────────
        private void WireEvents()
        {
            _btnConnect.Click += async (_, __) => await ConnectAsync();
            _btnDisconnect.Click += (_, __) => DisconnectAsync();
            _btnStartBot.Click += async (_, __) => await StartBotAsync();
            _btnStopBot.Click += async (_, __) => await StopBotAsync();

            _btnBuy.Click += async (_, __) => await SubmitTradeAsync(TradeType.BUY);
            _btnSell.Click += async (_, __) => await SubmitTradeAsync(TradeType.SELL);

            _btnRefreshPos.Click += async (_, __) => await RefreshPositionsAsync();
        }

        // ───────────────── CONNECTION ─────────────────
        private async Task ConnectAsync()
        {
            _bridge = new MT5Bridge(new MT5Settings
            {
                PipeName = _txtPipeName.Text
            });

            bool ok = await _bridge.PingAsync();

            SetConnectedUI(ok);

            if (ok)
            {
                _refreshTimer.Start();
                Log("Connected");
                await RefreshAsync();
            }
            else
            {
                Log("Connection failed", C_RED);
            }
        }

        private void DisconnectAsync()
        {
            _refreshTimer.Stop();
            _bridge?.Dispose();
            _bridge = null;
            SetConnectedUI(false);
        }

        // ───────────────── TRADE ─────────────────
        private async Task SubmitTradeAsync(TradeType type)
        {
            if (_bridge == null) return;

            var req = new TradeRequest
            {
                Pair = _cmbPair.Text,
                TradeType = type,
                StopLoss = double.Parse(_txtSL.Text),
                TakeProfit = double.Parse(_txtTP.Text),
                LotSize = 0.01
            };

            var result = await _bridge.OpenTradeAsync(req);
            AddHistoryRow(req, result);

            Log(result.IsSuccess ? "Trade OK" : "Trade FAIL",
                result.IsSuccess ? C_GREEN : C_RED);
        }

        // ───────────────── BOT ─────────────────
        private async Task StartBotAsync()
        {
            if (_bridge == null) return;

            _bot = new AutoBotService(_bridge, new BotConfig());
            await _bot.StartAsync();

            UpdateBotBadge(true);
        }

        private async Task StopBotAsync()
        {
            if (_bot == null) return;

            await _bot.DisposeAsync();
            _bot = null;

            UpdateBotBadge(false);
        }

        // ───────────────── REFRESH ─────────────────
        private async Task RefreshAsync()
        {
            if (_bridge == null) return;

            var acc = await _bridge.GetAccountInfoAsync();
            if (acc != null)
                UpdateAccountUI(acc);

            await RefreshPositionsAsync();
        }

        private async Task RefreshPositionsAsync()
        {
            if (_bridge == null) return;

            var pos = await _bridge.GetPositionsAsync();

            _gridPos.Rows.Clear();

            foreach (var p in pos)
            {
                _gridPos.Rows.Add(p.Ticket, p.Symbol, p.Profit);
            }
        }

        // ───────────────── UI HELPERS ─────────────────
        private void SetConnectedUI(bool connected)
        {
            _pnlDot.BackColor = connected ? C_GREEN : C_RED;
            _lblConnStatus.Text = connected ? "Connected" : "Disconnected";
        }

        private void UpdateBotBadge(bool running)
        {
            _lblBotBadge.Text = running ? "BOT RUNNING" : "BOT STOPPED";
            _lblBotBadge.ForeColor = running ? C_GREEN : C_RED;
        }

        private void AddHistoryRow(TradeRequest req, TradeResult res)
        {
            _gridHistory.Rows.Insert(0,
                DateTime.Now.ToString("HH:mm:ss"),
                req.Pair,
                req.TradeType,
                res.Status
            );
        }

        private void UpdateAccountUI(AccountInfo a)
        {
            _lblBalance.Text = $"Balance: {a.Balance}";
            _lblEquity.Text = $"Equity: {a.Equity}";
        }

        private void Log(string msg, Color? c = null)
        {
            _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        }

        private bool AssertConnected()
        {
            if (_bridge?.IsConnected == true) return true;
            Log("Not connected", C_RED);
            return false;
        }

        // ───────────────── PLACEHOLDERS ─────────────────
        private void ApplySettingsToUI() { }
        private void ReadBotConfigFromUI() { }
    }
}