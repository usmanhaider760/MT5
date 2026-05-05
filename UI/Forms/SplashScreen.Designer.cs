namespace MT5TradingBot.UI
{
    partial class SplashScreen
    {
        private System.ComponentModel.IContainer components = null;

        // ── Header ────────────────────────────────────────────────
        private Panel  _pnlHeader;
        private Label  _lblAppIcon;
        private Label  _lblAppName;
        private Label  _lblSubtitle;

        // ── Divider ───────────────────────────────────────────────
        private Panel _pnlDivider;

        // ── Check rows (dynamic content) ──────────────────────────
        private Panel _pnlCheckArea;

        // ── Footer ────────────────────────────────────────────────
        private Panel  _pnlFooter;
        private Panel  _pnlProgressTrack;
        private Panel  _pnlProgressFill;
        private TableLayoutPanel _footerLayout;
        private FlowLayoutPanel _buttonRow;
        private Label  _lblStatus;
        private Button _btnCancel;
        private Button _btnProceed;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            SuspendLayout();

            // ── Form ──────────────────────────────────────────────
            this.Text             = "MT5 Trading Bot Pro";
            this.Size             = new Size(720, 600);
            this.MinimumSize      = new Size(680, 560);
            this.FormBorderStyle  = FormBorderStyle.None;
            this.StartPosition    = FormStartPosition.CenterScreen;
            this.BackColor        = Color.FromArgb(13, 13, 19);
            this.ForeColor        = Color.FromArgb(218, 218, 230);
            this.Font             = new Font("Segoe UI", 9f);

            // ── Header ────────────────────────────────────────────
            _pnlHeader = new Panel
            {
                Dock = DockStyle.Top, Height = 116,
                BackColor = Color.FromArgb(22, 22, 32)
            };
            _lblAppIcon = new Label
            {
                Text = "⚡", Location = new Point(32, 28), AutoSize = true,
                Font = new Font("Segoe UI", 38f), ForeColor = Color.FromArgb(99, 179, 237)
            };
            _lblAppName = new Label
            {
                Text = "MT5 Trading Bot Pro", Location = new Point(104, 28), AutoSize = true,
                Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
                ForeColor = Color.FromArgb(218, 218, 230)
            };
            _lblSubtitle = new Label
            {
                Text = "Professional Algorithmic Trading Platform",
                Location = new Point(106, 74), AutoSize = true,
                Font = new Font("Segoe UI", 10f), ForeColor = Color.FromArgb(110, 110, 130)
            };
            _pnlHeader.Controls.AddRange(new Control[] { _lblAppIcon, _lblAppName, _lblSubtitle });

            // ── Divider ───────────────────────────────────────────
            _pnlDivider = new Panel
            {
                Dock = DockStyle.Top, Height = 2,
                BackColor = Color.FromArgb(45, 45, 65)
            };

            // ── Check area ────────────────────────────────────────
            _pnlCheckArea = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(13, 13, 19),
                Padding = new Padding(28, 18, 28, 14),
                AutoScroll = true
            };

            // ── Footer ────────────────────────────────────────────
            _pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom, Height = 108,
                BackColor = Color.FromArgb(18, 18, 26),
                Padding = new Padding(30, 16, 30, 16)
            };

            _footerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = _pnlFooter.BackColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _footerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 9));
            _footerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _footerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

            // Custom drawn progress bar (full color control)
            _pnlProgressTrack = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                BackColor = Color.FromArgb(40, 40, 55)
            };
            _pnlProgressFill = new Panel
            {
                Location = new Point(0, 0), Size = new Size(0, 9),
                BackColor = Color.FromArgb(99, 179, 237)
            };
            _pnlProgressTrack.Controls.Add(_pnlProgressFill);

            _lblStatus = new Label
            {
                Text = "Initializing...",
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(110, 110, 130),
                Margin = new Padding(0, 8, 0, 2)
            };
            _buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = _pnlFooter.BackColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _btnCancel = new Button
            {
                Text = "Cancel", Size = new Size(142, 32),
                BackColor = Color.FromArgb(28, 29, 42), ForeColor = Color.FromArgb(180, 80, 80),
                FlatStyle = FlatStyle.Flat, Enabled = true,
                Font = new Font("Segoe UI Semibold", 9f), Cursor = Cursors.Hand,
                Margin = new Padding(8, 2, 0, 0)
            };
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(100, 50, 50);
            _btnCancel.FlatAppearance.BorderSize  = 1;

            _btnProceed = new Button
            {
                Text = "Please wait...", Size = new Size(142, 32),
                BackColor = Color.FromArgb(28, 29, 42), ForeColor = Color.FromArgb(110, 110, 130),
                FlatStyle = FlatStyle.Flat, Enabled = false,
                Font = new Font("Segoe UI Semibold", 9f), Cursor = Cursors.Default,
                Margin = new Padding(8, 2, 0, 0)
            };
            _btnProceed.FlatAppearance.BorderColor = Color.FromArgb(45, 45, 65);
            _btnProceed.FlatAppearance.BorderSize  = 1;

            _buttonRow.Controls.Add(_btnProceed);
            _buttonRow.Controls.Add(_btnCancel);
            _footerLayout.Controls.Add(_pnlProgressTrack, 0, 0);
            _footerLayout.Controls.Add(_lblStatus, 0, 1);
            _footerLayout.Controls.Add(_buttonRow, 0, 2);
            _pnlFooter.Controls.Add(_footerLayout);

            // Add to form (Fill first, then Top panels, Bottom last)
            this.Controls.Add(_pnlCheckArea);
            this.Controls.Add(_pnlDivider);
            this.Controls.Add(_pnlFooter);
            this.Controls.Add(_pnlHeader);

            ResumeLayout(false);
        }
    }
}
