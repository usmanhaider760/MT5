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
        private readonly NumericUpDown _nudMaxConcurrent = new()
        {
            Minimum  = 0,
            Maximum  = 20,
            DecimalPlaces = 0,
            Increment = 1,
            Value    = 3
        };
        private readonly Label _lblMaxConcurrentLabel = new()
        {
            Text = "Max Concurrent Positions"
        };
        public ReviewTradeForm(BotConfig config)
        {
            _cfg = config;
            InitializeComponent();
            ConfigureAdditionalRows();
            ApplyTheme();
            PopulateFromConfig();
        }

        // ==========================================================
        //  POPULATE / SAVE
        // ==========================================================
        private void PopulateFromConfig()
        {
            SetNud(_nudRisk,        (decimal)_cfg.MaxRiskPercent);
            SetNud(_nudMaxConcurrent, _cfg.MaxConcurrentPositions);
            SetNud(_nudPollMs,      _cfg.PollIntervalMs);
            SetNud(_nudRetry,       _cfg.RetryCount);
            SetNud(_nudDrawdownPct, (decimal)_cfg.EmergencyCloseDrawdownPct);
            _chkDrawdown.Checked  = _cfg.DrawdownProtectionEnabled;
            _chkAutoStart.Checked = _cfg.AutoStartOnLaunch;

        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            _cfg.MaxRiskPercent            = (double)_nudRisk.Value;
            _cfg.MaxConcurrentPositions    = (int)_nudMaxConcurrent.Value;
            _cfg.PollIntervalMs            = (int)_nudPollMs.Value;
            _cfg.RetryCount                = (int)_nudRetry.Value;
            _cfg.EmergencyCloseDrawdownPct = (double)_nudDrawdownPct.Value;
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
            foreach (var lbl in new[] { _lblRiskLabel,
                                         _lblMaxConcurrentLabel,
                                         _lblPollMsLabel, _lblRetryLabel, _lblDrawdownLabel })
            {
                lbl.Font      = new Font("Segoe UI", 9F);
                lbl.ForeColor = C_MUTED;
            }

            // Draw horizontal separator lines between rows inside panel
            int[] separatorYs = { 38, 76, 114, 152, 190 };
            _pnlSettings.Paint += (_, e) =>
            {
                using var pen = new Pen(Color.FromArgb(38, 40, 56));
                foreach (int y in separatorYs)
                    e.Graphics.DrawLine(pen, 16, y, _pnlSettings.Width - 16, y);
            };

            // NumericUpDowns
            foreach (var nud in new[] { _nudRisk, _nudMaxConcurrent, _nudPollMs, _nudRetry, _nudDrawdownPct })
            {
                nud.BackColor    = C_SURFACE;
                nud.ForeColor    = C_TEXT;
                nud.BorderStyle  = BorderStyle.FixedSingle;
                nud.Font         = new Font("Consolas", 9.5F);
            }

            // Checkboxes
            foreach (var chk in new[] { _chkDrawdown, _chkAutoStart })
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
        private void ConfigureAdditionalRows()
        {
            const int rowHeight = 38;
            const int fieldX = 210;
            const int fieldW = 110;
            const int addedRows = 1;
            int delta = rowHeight * addedRows;

            ClientSize = new Size(ClientSize.Width, ClientSize.Height + delta);
            _pnlSettings.Size = new Size(_pnlSettings.Width, _pnlSettings.Height + delta);
            _btnSave.Location = new Point(_btnSave.Left, _btnSave.Top + delta);
            _btnCancel.Location = new Point(_btnCancel.Left, _btnCancel.Top + delta);

            PlaceRow(_lblMaxConcurrentLabel, _lblMaxConcurrentLabel.Text, _nudMaxConcurrent, 130, fieldX, fieldW);
            PlaceRow(_lblPollMsLabel, _lblPollMsLabel.Text, _nudPollMs, 168, fieldX, fieldW);
            PlaceRow(_lblRetryLabel, _lblRetryLabel.Text, _nudRetry, 206, fieldX, fieldW);
            PlaceRow(_lblDrawdownLabel, _lblDrawdownLabel.Text, _nudDrawdownPct, 244, fieldX, fieldW);
            _chkDrawdown.Location = new Point(_chkDrawdown.Left, _chkDrawdown.Top + delta);
            _chkAutoStart.Location = new Point(_chkAutoStart.Left, _chkAutoStart.Top + delta);

            _pnlSettings.Controls.Add(_lblMaxConcurrentLabel);
            _pnlSettings.Controls.Add(_nudMaxConcurrent);
        }

        private static void SetNud(NumericUpDown nud, decimal value)
            => nud.Value = Math.Max(nud.Minimum, Math.Min(nud.Maximum, value));
    }
}
