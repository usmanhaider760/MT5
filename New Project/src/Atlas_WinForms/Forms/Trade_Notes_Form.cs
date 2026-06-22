namespace Atlas_WinForms.Forms;

public class Trade_Notes_Form : Form
{
    public string Notes => txt_notes.Text.Trim();

    private readonly TextBox txt_notes;

    public Trade_Notes_Form(string trade_info, string existing_notes)
    {
        Text            = "ATLAS — Trade Notes";
        ClientSize      = new Size(480, 280);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        BackColor       = Color.FromArgb(14, 14, 21);
        ForeColor       = Color.FromArgb(221, 224, 240);
        Font            = new Font("Segoe UI", 9);

        var lbl_title = new Label
        {
            Text      = "📝  TRADE NOTES",
            Location  = new Point(14, 14),
            Size      = new Size(452, 24),
            Font      = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(221, 224, 240),
            BackColor = Color.Transparent
        };

        var lbl_trade = new Label
        {
            Text      = trade_info,
            Location  = new Point(14, 42),
            Size      = new Size(452, 20),
            ForeColor = Color.FromArgb(128, 136, 192),
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 8)
        };

        var sep = new Panel
        {
            Location  = new Point(14, 66),
            Size      = new Size(452, 1),
            BackColor = Color.FromArgb(46, 48, 85)
        };

        txt_notes = new TextBox
        {
            Location    = new Point(14, 76),
            Size        = new Size(452, 144),
            Multiline   = true,
            ScrollBars  = ScrollBars.Vertical,
            BackColor   = Color.FromArgb(20, 20, 36),
            ForeColor   = Color.FromArgb(221, 224, 240),
            BorderStyle = BorderStyle.FixedSingle,
            Font        = new Font("Segoe UI", 9),
            Text        = existing_notes,
            AcceptsReturn = true
        };

        var btn_cancel = MkBtn("CANCEL",    14, 234, 170, 34, Color.FromArgb(80, 88, 140));
        var btn_save   = MkBtn("SAVE NOTE", 296, 234, 170, 34, Color.FromArgb(61, 221, 128));
        btn_cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        btn_save.Click   += (_, _) => { DialogResult = DialogResult.OK;     Close(); };

        AcceptButton = btn_save;
        CancelButton = btn_cancel;

        Controls.AddRange([lbl_title, lbl_trade, sep, txt_notes, btn_cancel, btn_save]);
    }

    private static Button MkBtn(string text, int x, int y, int w, int h, Color fg)
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
        btn.FlatAppearance.BorderColor = fg;
        return btn;
    }
}
