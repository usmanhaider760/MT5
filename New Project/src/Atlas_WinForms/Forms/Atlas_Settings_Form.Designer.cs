namespace Atlas_WinForms.Forms;

partial class Atlas_Settings_Form
{
    private System.ComponentModel.IContainer components = null;

    private TextBox         txt_host;
    private NumericUpDown   num_port;
    private CheckBox        chk_demo;
    private NumericUpDown   num_interval;
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
        ClientSize      = new Size(410, 370);
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

        // ── Note ─────────────────────────────────────────────────────
        var lbl_note = new Label
        {
            Text      = "Changes to MT5 Host/Port and Cycle Interval take effect after restarting the application.",
            Location  = new Point(14, 262),
            Size      = new Size(382, 40),
            ForeColor = Color.FromArgb(80, 88, 140),
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 8)
        };

        // ── Buttons ──────────────────────────────────────────────────
        btn_cancel = MkBtn("CANCEL",        14,  318, 170, 36, Color.FromArgb(80,  88, 140), Color.FromArgb(46, 48, 85));
        btn_save   = MkBtn("SAVE SETTINGS", 220, 318, 176, 36, Color.FromArgb(61, 221, 128), Color.FromArgb(61, 221, 128));
        btn_cancel.Click += btn_cancel_Click;
        btn_save.Click   += btn_save_Click;

        AcceptButton = btn_save;
        CancelButton = btn_cancel;

        Controls.AddRange([
            lbl_title, sep1,
            lbl_mt5, lbl_host, txt_host, lbl_port, num_port,
            sep2,
            lbl_bot, lbl_demo, chk_demo, lbl_interval, num_interval, lbl_seconds,
            sep3, lbl_note,
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
