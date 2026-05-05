
namespace MT5TradingBot.UI
{
    partial class ReviewTradeForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        // ── Header ─────────────────────────────────────────────────────────
        private Label _lblHeader;
        private Label _lblSubHeader;

        // ── Settings panel ─────────────────────────────────────────────────
        private Panel _pnlSettings;

        // ── Numeric fields ──────────────────────────────────────────────────
        private Label         _lblRiskLabel;
        private NumericUpDown _nudRisk;
        private Label         _lblPollMsLabel;
        private NumericUpDown _nudPollMs;
        private Label         _lblRetryLabel;
        private NumericUpDown _nudRetry;
        private Label         _lblDrawdownLabel;
        private NumericUpDown _nudDrawdownPct;

        // ── Checkboxes ──────────────────────────────────────────────────────
        private CheckBox _chkDrawdown;
        private CheckBox _chkAutoStart;

        // ── Buttons ─────────────────────────────────────────────────────────
        private Button _btnSave;
        private Button _btnCancel;

        private void InitializeComponent()
        {
            _lblHeader        = new Label();
            _lblSubHeader     = new Label();
            _pnlSettings      = new Panel();
            _lblRiskLabel     = new Label();
            _nudRisk          = new NumericUpDown();
            _lblPollMsLabel   = new Label();
            _nudPollMs        = new NumericUpDown();
            _lblRetryLabel    = new Label();
            _nudRetry         = new NumericUpDown();
            _lblDrawdownLabel = new Label();
            _nudDrawdownPct   = new NumericUpDown();
            _chkDrawdown      = new CheckBox();
            _chkAutoStart     = new CheckBox();
            _btnSave          = new Button();
            _btnCancel        = new Button();

            SuspendLayout();

            // ── Form ─────────────────────────────────────────────────
            Text            = "Bot Trade Settings";
            ClientSize      = new Size(440, 528);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;

            // ── Header ───────────────────────────────────────────────
            _lblHeader.Text      = "Bot Trade Settings";
            _lblHeader.Location  = new Point(16, 16);
            _lblHeader.Size      = new Size(408, 26);
            _lblHeader.AutoSize  = false;

            _lblSubHeader.Text      = "Configure risk management and execution parameters.";
            _lblSubHeader.Location  = new Point(18, 44);
            _lblSubHeader.Size      = new Size(406, 18);
            _lblSubHeader.AutoSize  = false;

            // ── Settings panel ────────────────────────────────────────
            _pnlSettings.Location = new Point(12, 70);
            _pnlSettings.Size     = new Size(416, 394);

            const int FieldX = 210;
            const int FieldW = 110;

            PlaceRow(_lblRiskLabel,      "Max Risk %",           _nudRisk,      16,  FieldX, FieldW);
            PlaceRow(_lblPollMsLabel,    "Poll Interval (ms)",   _nudPollMs,    54, FieldX, FieldW);
            PlaceRow(_lblRetryLabel,     "Retry Count",          _nudRetry,     92, FieldX, FieldW);
            PlaceRow(_lblDrawdownLabel,  "Drawdown Stop %",      _nudDrawdownPct, 130, FieldX, FieldW);

            _nudRisk.DecimalPlaces  = 1; _nudRisk.Minimum  = 0.1m;  _nudRisk.Maximum  = 50m;    _nudRisk.Increment  = 0.1m;
            _nudPollMs.Minimum      = 500; _nudPollMs.Maximum = 60000; _nudPollMs.Increment = 500;
            _nudRetry.Minimum       = 1;   _nudRetry.Maximum  = 10;
            _nudDrawdownPct.DecimalPlaces = 1; _nudDrawdownPct.Minimum = 1m; _nudDrawdownPct.Maximum = 50m; _nudDrawdownPct.Increment = 0.5m;

            PlaceCheckBox(_chkDrawdown,  "Enable drawdown protection (emergency stop)", 180);
            PlaceCheckBox(_chkAutoStart, "Auto-start bot on app launch", 214);

            foreach (var c in new Control[] {
                _lblRiskLabel, _nudRisk,
                _lblPollMsLabel, _nudPollMs,
                _lblRetryLabel, _nudRetry, _lblDrawdownLabel, _nudDrawdownPct,
                _chkDrawdown, _chkAutoStart })
            {
                _pnlSettings.Controls.Add(c);
            }

            // ── Buttons ───────────────────────────────────────────────
            _btnSave.Text     = "Save Settings";
            _btnSave.Location = new Point(16, 474);
            _btnSave.Size     = new Size(182, 40);
            _btnSave.Cursor   = Cursors.Hand;
            _btnSave.Click   += BtnSave_Click;

            _btnCancel.Text     = "Cancel";
            _btnCancel.Location = new Point(210, 474);
            _btnCancel.Size     = new Size(110, 40);
            _btnCancel.Cursor   = Cursors.Hand;
            _btnCancel.Click   += BtnCancel_Click;

            Controls.Add(_lblHeader);
            Controls.Add(_lblSubHeader);
            Controls.Add(_pnlSettings);
            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);

            ResumeLayout(false);
        }

        private static void PlaceRow(Label lbl, string text, Control editor, int y, int editorX, int editorW)
        {
            lbl.AutoSize   = false;
            lbl.Text       = text;
            lbl.Location   = new Point(16, y + 4);
            lbl.Size       = new Size(editorX - 26, 20);
            lbl.TextAlign  = ContentAlignment.MiddleLeft;

            editor.Location = new Point(editorX, y);
            editor.Size     = new Size(editorW, 26);
        }

        private static void PlaceCheckBox(CheckBox chk, string text, int y)
        {
            chk.AutoSize  = false;
            chk.Text      = text;
            chk.Location  = new Point(16, y);
            chk.Size      = new Size(384, 26);
            chk.TextAlign = ContentAlignment.MiddleLeft;
        }
    }
}
