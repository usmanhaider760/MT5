using System.Text.Json;

namespace MT5TradingBot.UI
{
    public sealed class JsonViewForm : Form
    {
        private static readonly Color C_BG      = Color.FromArgb(13,  13,  19);
        private static readonly Color C_SURFACE = Color.FromArgb(22,  22,  32);
        private static readonly Color C_TEXT    = Color.FromArgb(218, 218, 230);
        private static readonly Color C_MUTED   = Color.FromArgb(110, 110, 130);
        private static readonly Color C_ACCENT  = Color.FromArgb(130, 170, 255);
        private static readonly Color C_BORDER  = Color.FromArgb(45,  48,  64);

        private readonly RichTextBox _rtb;
        private readonly Label       _lblTitle;
        private readonly Label       _lblPath;
        private readonly Button      _btnClose;
        private readonly Button      _btnCopy;

        public JsonViewForm(string filePath, string rawJson)
        {
            Text            = "Signal JSON";
            ClientSize      = new Size(640, 560);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize     = new Size(420, 320);
            MaximizeBox     = true;
            MinimizeBox     = false;
            BackColor       = C_BG;
            ForeColor       = C_TEXT;
            Font            = new Font("Segoe UI", 9F);

            _lblTitle = new Label
            {
                Text      = "Signal JSON",
                Location  = new Point(16, 14),
                Size      = new Size(600, 24),
                AutoSize  = false,
                Font      = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 210, 240),
                BackColor = Color.Transparent
            };

            _lblPath = new Label
            {
                Text      = string.IsNullOrWhiteSpace(filePath) ? "(no file path)" : filePath,
                Location  = new Point(18, 40),
                Size      = new Size(600, 16),
                AutoSize  = false,
                Font      = new Font("Segoe UI", 8F),
                ForeColor = C_MUTED,
                BackColor = Color.Transparent
            };

            _rtb = new RichTextBox
            {
                Location    = new Point(12, 66),
                Anchor      = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor   = C_SURFACE,
                ForeColor   = C_TEXT,
                Font        = new Font("Consolas", 10F),
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly    = true,
                WordWrap    = false,
                ScrollBars  = RichTextBoxScrollBars.Both,
                Text        = FormatJson(rawJson)
            };

            _btnCopy = new Button
            {
                Text      = "Copy",
                Size      = new Size(88, 36),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left,
                Cursor    = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(38, 48, 72),
                ForeColor = C_ACCENT,
                Font      = new Font("Segoe UI Semibold", 9F)
            };
            _btnCopy.FlatAppearance.BorderColor = C_BORDER;
            _btnCopy.FlatAppearance.BorderSize  = 1;
            _btnCopy.Click += (_, _) =>
            {
                Clipboard.SetText(_rtb.Text);
                _btnCopy.Text = "Copied!";
                var t = new System.Windows.Forms.Timer { Interval = 1400 };
                t.Tick += (_, _) => { _btnCopy.Text = "Copy"; t.Stop(); t.Dispose(); };
                t.Start();
            };

            _btnClose = new Button
            {
                Text      = "Close",
                Size      = new Size(88, 36),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Right,
                Cursor    = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 52, 68),
                ForeColor = C_TEXT,
                Font      = new Font("Segoe UI Semibold", 9F)
            };
            _btnClose.FlatAppearance.BorderColor = C_BORDER;
            _btnClose.FlatAppearance.BorderSize  = 1;
            _btnClose.Click += (_, _) => Close();

            Controls.AddRange(new Control[] { _lblTitle, _lblPath, _rtb, _btnCopy, _btnClose });

            // Divider line under header
            Paint += (_, e) =>
            {
                using var pen = new Pen(C_BORDER);
                e.Graphics.DrawLine(pen, 0, 60, Width, 60);
            };

            ResizeControls();
            SizeChanged += (_, _) => ResizeControls();
        }

        private void ResizeControls()
        {
            int pad   = 12;
            int btnH  = 36;
            int btnY  = ClientSize.Height - pad - btnH;

            _rtb.Size     = new Size(ClientSize.Width - pad * 2, btnY - 66 - pad);
            _lblTitle.Width = ClientSize.Width - 32;
            _lblPath.Width  = ClientSize.Width - 32;

            _btnCopy.Location  = new Point(pad, btnY);
            _btnClose.Location = new Point(ClientSize.Width - pad - _btnClose.Width, btnY);
        }

        private static string FormatJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw ?? "";
            try
            {
                using var doc = JsonDocument.Parse(raw);
                return JsonSerializer.Serialize(doc.RootElement,
                    new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return raw;
            }
        }
    }
}
