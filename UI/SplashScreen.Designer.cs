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
        private Label  _lblStatus;
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
            this.Size             = new Size(640, 460);
            this.FormBorderStyle  = FormBorderStyle.None;
            this.StartPosition    = FormStartPosition.CenterScreen;
            this.BackColor        = Color.FromArgb(13, 13, 19);
            this.ForeColor        = Color.FromArgb(218, 218, 230);
            this.Font             = new Font("Segoe UI", 9f);

            // ── Header ────────────────────────────────────────────
            _pnlHeader = new Panel
            {
                Dock = DockStyle.Top, Height = 130,
                BackColor = Color.FromArgb(22, 22, 32)
            };
            _lblAppIcon = new Label
            {
                Text = "⚡", Location = new Point(32, 28), AutoSize = true,
                Font = new Font("Segoe UI", 38f), ForeColor = Color.FromArgb(99, 179, 237)
            };
            _lblAppName = new Label
            {
                Text = "MT5 Trading Bot Pro", Location = new Point(106, 32), AutoSize = true,
                Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold),
                ForeColor = Color.FromArgb(218, 218, 230)
            };
            _lblSubtitle = new Label
            {
                Text = "Professional Algorithmic Trading Platform",
                Location = new Point(108, 80), AutoSize = true,
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
                Padding = new Padding(30, 16, 30, 0)
            };

            // ── Footer ────────────────────────────────────────────
            _pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom, Height = 80,
                BackColor = Color.FromArgb(18, 18, 26)
            };

            // Custom drawn progress bar (full color control)
            _pnlProgressTrack = new Panel
            {
                Location = new Point(30, 16), Size = new Size(580, 6),
                BackColor = Color.FromArgb(40, 40, 55)
            };
            _pnlProgressFill = new Panel
            {
                Location = new Point(0, 0), Size = new Size(0, 6),
                BackColor = Color.FromArgb(99, 179, 237)
            };
            _pnlProgressTrack.Controls.Add(_pnlProgressFill);

            _lblStatus = new Label
            {
                Text = "Initializing...", Location = new Point(30, 30), AutoSize = true,
                Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(110, 110, 130)
            };
            _btnProceed = new Button
            {
                Text = "Please wait...", Location = new Point(464, 44), Size = new Size(146, 28),
                BackColor = Color.FromArgb(28, 29, 42), ForeColor = Color.FromArgb(110, 110, 130),
                FlatStyle = FlatStyle.Flat, Enabled = false,
                Font = new Font("Segoe UI Semibold", 9f), Cursor = Cursors.Default
            };
            _btnProceed.FlatAppearance.BorderColor = Color.FromArgb(45, 45, 65);
            _btnProceed.FlatAppearance.BorderSize  = 1;

            _pnlFooter.Controls.AddRange(new Control[] { _pnlProgressTrack, _lblStatus, _btnProceed });

            // Add to form (Fill first, then Top panels, Bottom last)
            this.Controls.Add(_pnlCheckArea);
            this.Controls.Add(_pnlDivider);
            this.Controls.Add(_pnlFooter);
            this.Controls.Add(_pnlHeader);

            ResumeLayout(false);
        }
    }
}
