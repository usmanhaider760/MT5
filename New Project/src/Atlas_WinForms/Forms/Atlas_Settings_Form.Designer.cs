namespace Atlas_WinForms.Forms;

partial class Atlas_Settings_Form
{
    private System.ComponentModel.IContainer components = null;

    private TextBox         txt_host;
    private NumericUpDown   num_port;
    private CheckBox        chk_demo;
    private NumericUpDown   num_interval;
    private TextBox         txt_telegram_token;
    private TextBox         txt_telegram_chatid;
    private TextBox         txt_email_smtp_host;
    private NumericUpDown   num_email_smtp_port;
    private TextBox         txt_email_username;
    private TextBox         txt_email_password;
    private TextBox         txt_email_to;
    private NumericUpDown   num_forex_risk;
    private NumericUpDown   num_daily_loss;
    private NumericUpDown   num_dd_breaker;
    private NumericUpDown   num_spread_mult;
    private Button          btn_save;
    private Button          btn_cancel;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();

        Text            = "ATLAS Settings";
        ClientSize      = new Size(410, 772);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        BackColor       = Color.FromArgb(14, 14, 21);
        ForeColor       = Color.FromArgb(221, 224, 240);
        Font            = new Font("Segoe UI", 9);

        // ── Title ────────────────────────────────────────────────────
        var lbl_title = new Label
        {
            Text      = "⚙  ATLAS SETTINGS",
            Location  = new Point(14, 14),
            Size      = new Size(382, 28),
            Font      = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(221, 224, 240),
            BackColor = Color.Transparent
        };

        var sep1 = Sep(14, 48);

        // ── MT5 Bridge ───────────────────────────────────────────────
        var lbl_mt5 = Section("MT5 BRIDGE", 14, 58);

        var lbl_host = Field("Host:", 14, 84);
        txt_host = new TextBox
        {
            Location    = new Point(150, 82),
            Size        = new Size(240, 22),
            BackColor   = Color.FromArgb(20, 20, 36),
            ForeColor   = Color.FromArgb(221, 224, 240),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lbl_port = Field("Port:", 14, 114);
        num_port = new NumericUpDown
        {
            Location    = new Point(150, 112),
            Size        = new Size(100, 22),
            BackColor   = Color.FromArgb(20, 20, 36),
            ForeColor   = Color.FromArgb(221, 224, 240),
            BorderStyle = BorderStyle.FixedSingle,
            Minimum     = 1,
            Maximum     = 65535,
            Value       = 9090
        };

        var sep2 = Sep(14, 150);

        // ── Bot ──────────────────────────────────────────────────────
        var lbl_bot = Section("BOT", 14, 160);

        var lbl_demo = Field("Demo Mode:", 14, 186);
        chk_demo = new CheckBox
        {
            Location  = new Point(150, 184),
            Size      = new Size(22, 22),
            Checked   = true,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(61, 221, 128)
        };

        var lbl_interval = Field("Cycle Interval:", 14, 216);
        num_interval = new NumericUpDown
        {
            Location    = new Point(150, 214),
            Size        = new Size(80, 22),
            BackColor   = Color.FromArgb(20, 20, 36),
            ForeColor   = Color.FromArgb(221, 224, 240),
            BorderStyle = BorderStyle.FixedSingle,
            Minimum     = 10,
            Maximum     = 3600,
            Value       = 60
        };
        var lbl_seconds = new Label
        {
            Text      = "seconds",
            Location  = new Point(238, 217),
            Size      = new Size(70, 20),
            ForeColor = Color.FromArgb(80, 88, 140),
            BackColor = Color.Transparent
        };

        var sep3 = Sep(14, 252);

        // ── Telegram ─────────────────────────────────────────────────
        var lbl_telegram = Section("TELEGRAM ALERTS  (optional)", 14, 262);

        var lbl_token = Field("Bot Token:", 14, 286);
        txt_telegram_token = new TextBox
        {
            Location    = new Point(150, 284),
            Size        = new Size(240, 22),
            BackColor   = Color.FromArgb(20, 20, 36),
            ForeColor   = Color.FromArgb(221, 224, 240),
            BorderStyle = BorderStyle.FixedSingle,
            PasswordChar = '•'
        };

        var lbl_chatid = Field("Chat ID:", 14, 316);
        txt_telegram_chatid = new TextBox
        {
            Location    = new Point(150, 314),
            Size        = new Size(160, 22),
            BackColor   = Color.FromArgb(20, 20, 36),
            ForeColor   = Color.FromArgb(221, 224, 240),
            BorderStyle = BorderStyle.FixedSingle
        };

        var sep4 = Sep(14, 350);

        // ── Email Alerts ─────────────────────────────────────────────
        var lbl_email = Section("EMAIL ALERTS  (optional)", 14, 358);

        var lbl_smtp_host = Field("SMTP Host:", 14, 380);
        txt_email_smtp_host = new TextBox
        {
            Location    = new Point(150, 378),
            Size        = new Size(240, 22),
            BackColor   = Color.FromArgb(20, 20, 36),
            ForeColor   = Color.FromArgb(221, 224, 240),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lbl_smtp_port = Field("SMTP Port:", 14, 410);
        num_email_smtp_port = new NumericUpDown
        {
            Location    = new Point(150, 408),
            Size        = new Size(80, 22),
            BackColor   = Color.FromArgb(20, 20, 36),
            ForeColor   = Color.FromArgb(221, 224, 240),
            BorderStyle = BorderStyle.FixedSingle,
            Minimum     = 1,
            Maximum     = 65535,
            Value       = 587
        };

        var lbl_email_user = Field("Username:", 14, 440);
        txt_email_username = new TextBox
        {
            Location    = new Point(150, 438),
            Size        = new Size(240, 22),
            BackColor   = Color.FromArgb(20, 20, 36),
            ForeColor   = Color.FromArgb(221, 224, 240),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lbl_email_pass = Field("Password:", 14, 470);
        txt_email_password = new TextBox
        {
            Location     = new Point(150, 468),
            Size         = new Size(240, 22),
            BackColor    = Color.FromArgb(20, 20, 36),
            ForeColor    = Color.FromArgb(221, 224, 240),
            BorderStyle  = BorderStyle.FixedSingle,
            PasswordChar = '•'
        };

        var lbl_email_to = Field("Send Alerts To:", 14, 500);
        txt_email_to = new TextBox
        {
            Location    = new Point(150, 498),
            Size        = new Size(240, 22),
            BackColor   = Color.FromArgb(20, 20, 36),
            ForeColor   = Color.FromArgb(221, 224, 240),
            BorderStyle = BorderStyle.FixedSingle
        };

        var sep5 = Sep(14, 530);

        // ── Risk Controls ────────────────────────────────────────────
        var lbl_risk = Section("RISK CONTROLS  (restarts bot to apply)", 14, 538);

        var lbl_forex_risk = Field("Forex Risk/Trade %:", 14, 560);
        num_forex_risk = new NumericUpDown
        {
            Location      = new Point(210, 558),
            Size          = new Size(80, 22),
            BackColor     = Color.FromArgb(20, 20, 36),
            ForeColor     = Color.FromArgb(221, 224, 240),
            BorderStyle   = BorderStyle.FixedSingle,
            Minimum       = 0.01m,
            Maximum       = 5.00m,
            Increment     = 0.05m,
            DecimalPlaces = 2,
            Value         = 0.25m
        };

        var lbl_daily_loss = Field("Max Daily Loss %:", 14, 590);
        num_daily_loss = new NumericUpDown
        {
            Location      = new Point(210, 588),
            Size          = new Size(80, 22),
            BackColor     = Color.FromArgb(20, 20, 36),
            ForeColor     = Color.FromArgb(221, 224, 240),
            BorderStyle   = BorderStyle.FixedSingle,
            Minimum       = 0.10m,
            Maximum       = 10.0m,
            Increment     = 0.25m,
            DecimalPlaces = 2,
            Value         = 0.75m
        };

        var lbl_dd_breaker = Field("Drawdown Breaker %:", 14, 620);
        num_dd_breaker = new NumericUpDown
        {
            Location      = new Point(210, 618),
            Size          = new Size(80, 22),
            BackColor     = Color.FromArgb(20, 20, 36),
            ForeColor     = Color.FromArgb(221, 224, 240),
            BorderStyle   = BorderStyle.FixedSingle,
            Minimum       = 0.50m,
            Maximum       = 30.0m,
            Increment     = 0.50m,
            DecimalPlaces = 2,
            Value         = 5.00m
        };

        var lbl_spread_mult = Field("Spread Multiplier:", 14, 650);
        num_spread_mult = new NumericUpDown
        {
            Location      = new Point(210, 648),
            Size          = new Size(80, 22),
            BackColor     = Color.FromArgb(20, 20, 36),
            ForeColor     = Color.FromArgb(221, 224, 240),
            BorderStyle   = BorderStyle.FixedSingle,
            Minimum       = 0.50m,
            Maximum       = 3.00m,
            Increment     = 0.10m,
            DecimalPlaces = 2,
            Value         = 1.00m
        };

        var sep6 = Sep(14, 686);

        // ── Note ─────────────────────────────────────────────────────
        var lbl_note = new Label
        {
            Text      = "MT5 Host/Port and Cycle Interval take effect after restart. " +
                        "Leave Telegram/Email blank to disable alerts.",
            Location  = new Point(14, 694),
            Size      = new Size(382, 30),
            ForeColor = Color.FromArgb(80, 88, 140),
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 8)
        };

        // ── Buttons ──────────────────────────────────────────────────
        btn_cancel = MkBtn("CANCEL",        14,  728, 170, 36, Color.FromArgb(80,  88, 140), Color.FromArgb(46, 48, 85));
        btn_save   = MkBtn("SAVE SETTINGS", 220, 728, 176, 36, Color.FromArgb(61, 221, 128), Color.FromArgb(61, 221, 128));
        btn_cancel.Click += btn_cancel_Click;
        btn_save.Click   += btn_save_Click;

        AcceptButton = btn_save;
        CancelButton = btn_cancel;

        Controls.AddRange([
            lbl_title, sep1,
            lbl_mt5, lbl_host, txt_host, lbl_port, num_port,
            sep2,
            lbl_bot, lbl_demo, chk_demo, lbl_interval, num_interval, lbl_seconds,
            sep3,
            lbl_telegram, lbl_token, txt_telegram_token, lbl_chatid, txt_telegram_chatid,
            sep4,
            lbl_email, lbl_smtp_host, txt_email_smtp_host, lbl_smtp_port, num_email_smtp_port,
            lbl_email_user, txt_email_username, lbl_email_pass, txt_email_password,
            lbl_email_to, txt_email_to,
            sep5,
            lbl_risk, lbl_forex_risk, num_forex_risk, lbl_daily_loss, num_daily_loss, lbl_dd_breaker, num_dd_breaker,
            lbl_spread_mult, num_spread_mult,
            sep6, lbl_note,
            btn_cancel, btn_save
        ]);

        ResumeLayout(false);
    }

    // ── Builder helpers ───────────────────────────────────────────────
    private static Label Section(string text, int x, int y) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        Size      = new Size(382, 20),
        Font      = new Font("Segoe UI", 8, FontStyle.Bold),
        ForeColor = Color.FromArgb(128, 136, 192),
        BackColor = Color.Transparent
    };

    private static Label Field(string text, int x, int y) => new()
    {
        Text      = text,
        Location  = new Point(x, y),
        Size      = new Size(130, 22),
        ForeColor = Color.FromArgb(221, 224, 240),
        BackColor = Color.Transparent,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Panel Sep(int x, int y) => new()
    {
        Location  = new Point(x, y),
        Size      = new Size(382, 1),
        BackColor = Color.FromArgb(46, 48, 85)
    };

    private static Button MkBtn(string text, int x, int y, int w, int h, Color fg, Color border)
    {
        var btn = new Button
        {
            Text      = text,
            Location  = new Point(x, y),
            Size      = new Size(w, h),
            ForeColor = fg,
            BackColor = Color.FromArgb(30, 30, 55),
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor    = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = border;
        return btn;
    }
}
