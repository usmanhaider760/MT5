using MT5TradingBot.Models;

namespace MT5TradingBot.UI
{
    public sealed partial class ReviewTradeForm : Form
    {
        private static readonly Color C_BG      = Color.FromArgb(13,  13,  19);
        private static readonly Color C_SURFACE = Color.FromArgb(22,  22,  32);
        private static readonly Color C_CARD    = Color.FromArgb(28,  29,  42);
        private static readonly Color C_TEXT    = Color.FromArgb(218, 218, 230);
        private static readonly Color C_MUTED   = Color.FromArgb(110, 110, 130);
        private static readonly Color C_ACCENT  = Color.FromArgb(99,  179, 237);
        private static readonly Color C_GREEN   = Color.FromArgb(72,  199, 142);
        private static readonly Color C_BORDER  = Color.FromArgb(45,  48,  64);

        private readonly BotConfig _cfg;

        public ReviewTradeForm(BotConfig config)
        {
            _cfg = config;
            InitializeComponent();
            ApplyTheme();
            PopulateFromConfig();
        }

        // ==========================================================
        //  POPULATE / SAVE
        // ==========================================================
        private void PopulateFromConfig()
        {
            SetNud(_nudRisk,        (decimal)_cfg.MaxRiskPercent);
            SetNud(_nudMinRR,       (decimal)_cfg.MinRRRatio);
            SetNud(_nudMaxTrades,   _cfg.MaxTradesPerDay);
            SetNud(_nudPollMs,      _cfg.PollIntervalMs);
            SetNud(_nudRetry,       _cfg.RetryCount);
            SetNud(_nudDrawdownPct, (decimal)_cfg.EmergencyCloseDrawdownPct);
            _chkAutoLot.Checked   = _cfg.AutoLotCalculation;
            _chkEnforceRR.Checked = _cfg.EnforceRR;
            _chkDrawdown.Checked  = _cfg.DrawdownProtectionEnabled;
            _chkAutoStart.Checked = _cfg.AutoStartOnLaunch;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            _cfg.MaxRiskPercent            = (double)_nudRisk.Value;
            _cfg.MinRRRatio                = (double)_nudMinRR.Value;
            _cfg.MaxTradesPerDay           = (int)_nudMaxTrades.Value;
            _cfg.PollIntervalMs            = (int)_nudPollMs.Value;
            _cfg.RetryCount                = (int)_nudRetry.Value;
            _cfg.EmergencyCloseDrawdownPct = (double)_nudDrawdownPct.Value;
            _cfg.AutoLotCalculation        = _chkAutoLot.Checked;
            _cfg.EnforceRR                 = _chkEnforceRR.Checked;
            _cfg.DrawdownProtectionEnabled = _chkDrawdown.Checked;
            _cfg.AutoStartOnLaunch         = _chkAutoStart.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        // ==========================================================
        //  THEME
        // ==========================================================
        private void ApplyTheme()
        {
            BackColor = C_BG;
            ForeColor = C_TEXT;
            Font      = new Font("Segoe UI", 9F);

            _lblHeader.Font      = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
            _lblHeader.ForeColor = Color.FromArgb(200, 210, 240);
            _lblSubHeader.Font      = new Font("Segoe UI", 9F);
            _lblSubHeader.ForeColor = C_MUTED;

            _pnlSettings.BackColor = C_CARD;

            // Draw a top border on the settings panel
            _pnlSettings.Paint += (_, e) =>
            {
                using var pen = new Pen(C_BORDER);
                e.Graphics.DrawLine(pen, 0, 0, _pnlSettings.Width, 0);
            };

            // Labels
            foreach (var lbl in new[] { _lblRiskLabel, _lblMinRRLabel, _lblMaxTradesLabel,
                                         _lblPollMsLabel, _lblRetryLabel, _lblDrawdownLabel })
            {
                lbl.Font      = new Font("Segoe UI", 9F);
                lbl.ForeColor = C_MUTED;
            }

            // Draw horizontal separator lines between rows inside panel
            int[] separatorYs = { 38, 76, 114, 152, 190, 232 };
            _pnlSettings.Paint += (_, e) =>
            {
                using var pen = new Pen(Color.FromArgb(38, 40, 56));
                foreach (int y in separatorYs)
                    e.Graphics.DrawLine(pen, 16, y, _pnlSettings.Width - 16, y);
            };

            // NumericUpDowns
            foreach (var nud in new[] { _nudRisk, _nudMinRR, _nudMaxTrades, _nudPollMs, _nudRetry, _nudDrawdownPct })
            {
                nud.BackColor    = C_SURFACE;
                nud.ForeColor    = C_TEXT;
                nud.BorderStyle  = BorderStyle.FixedSingle;
                nud.Font         = new Font("Consolas", 9.5F);
            }

            // Checkboxes
            foreach (var chk in new[] { _chkAutoLot, _chkEnforceRR, _chkDrawdown, _chkAutoStart })
            {
                chk.Font                  = new Font("Segoe UI", 9F);
                chk.ForeColor             = C_TEXT;
                chk.BackColor             = C_CARD;
                chk.UseVisualStyleBackColor = false;
            }

            // Save button
            _btnSave.BackColor             = C_GREEN;
            _btnSave.ForeColor             = Color.FromArgb(10, 10, 20);
            _btnSave.FlatStyle             = FlatStyle.Flat;
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Font                  = new Font("Segoe UI Semibold", 10F);

            // Cancel button
            _btnCancel.BackColor             = Color.FromArgb(50, 52, 68);
            _btnCancel.ForeColor             = C_TEXT;
            _btnCancel.FlatStyle             = FlatStyle.Flat;
            _btnCancel.FlatAppearance.BorderColor = C_BORDER;
            _btnCancel.FlatAppearance.BorderSize = 1;
            _btnCancel.Font                  = new Font("Segoe UI Semibold", 9F);
        }

        // ==========================================================
        //  HELPERS
        // ==========================================================
        private static void SetNud(NumericUpDown nud, decimal value)
            => nud.Value = Math.Max(nud.Minimum, Math.Min(nud.Maximum, value));
    }
}
