namespace MT5TradingBot.UI
{
    internal sealed record AppLogDetail(
        string OriginalMessage,
        string Meaning,
        string ValuesChecked,
        string Formula,
        string Outcome,
        string ExpectedPl,
        string NextAction);

    internal static class AppLogDetailBox
    {
        private static readonly Color C_BG = Color.FromArgb(13, 13, 19);
        private static readonly Color C_HEADER = Color.FromArgb(24, 25, 38);
        private static readonly Color C_PANEL = Color.FromArgb(28, 29, 42);
        private static readonly Color C_CARD = Color.FromArgb(34, 36, 51);
        private static readonly Color C_TEXT = Color.FromArgb(218, 218, 230);
        private static readonly Color C_MUTED = Color.FromArgb(142, 148, 170);
        private static readonly Color C_ACCENT = Color.FromArgb(99, 179, 237);
        private static readonly Color C_WARN = Color.FromArgb(250, 199, 117);
        private static readonly Color C_GREEN = Color.FromArgb(72, 199, 142);
        private static readonly Color C_BLUE = Color.FromArgb(117, 167, 255);
        private static readonly Color C_PURPLE = Color.FromArgb(180, 138, 255);
        private static readonly Color C_BORDER = Color.FromArgb(45, 48, 64);

        public static void Show(IWin32Window? owner, AppLogDetail detail)
        {
            using var form = new AppLogDetailForm(detail);
            form.StartPosition = owner == null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
            if (owner == null)
                form.ShowDialog();
            else
                form.ShowDialog(owner);
        }

        private sealed class AppLogDetailForm : Form
        {
            public AppLogDetailForm(AppLogDetail detail)
            {
                Text = "Log Details";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ClientSize = new Size(820, 660);
                BackColor = C_BG;
                ForeColor = C_TEXT;
                Font = new Font("Segoe UI", 9F);
                ShowInTaskbar = false;

                var header = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 88,
                    BackColor = C_HEADER
                };

                var iconBox = new Label
                {
                    Text = "i",
                    Location = new Point(20, 21),
                    Size = new Size(44, 44),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.FromArgb(38, 40, 56),
                    ForeColor = C_ACCENT,
                    Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold)
                };

                var title = new Label
                {
                    Text = "Log Details",
                    Location = new Point(82, 18),
                    Size = new Size(670, 26),
                    ForeColor = C_TEXT,
                    Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                    AutoEllipsis = true
                };

                var subtitle = new Label
                {
                    Text = "Plain-language reason, checked values, formulas, outcome, and projected P/L when available.",
                    Location = new Point(84, 48),
                    Size = new Size(690, 22),
                    ForeColor = C_MUTED,
                    AutoEllipsis = true
                };

                header.Controls.AddRange([iconBox, title, subtitle]);

                var body = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = C_BG,
                    Padding = new Padding(18, 16, 18, 12)
                };

                var scroller = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    BackColor = C_PANEL,
                    Padding = new Padding(16, 12, 16, 16)
                };
                scroller.Paint += (_, e) =>
                {
                    using var border = new Pen(C_BORDER);
                    using var stripe = new SolidBrush(C_ACCENT);
                    e.Graphics.FillRectangle(stripe, 0, 0, 5, scroller.Height);
                    e.Graphics.DrawRectangle(border, 0, 0, scroller.Width - 1, scroller.Height - 1);
                };

                var layout = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    BackColor = C_PANEL,
                    Padding = new Padding(0)
                };

                AddSection(layout, "Original log", "The exact selected line from the log window", detail.OriginalMessage, C_MUTED);
                AddSection(layout, "Meaning", "What this message means in plain language", detail.Meaning, C_ACCENT);
                AddSection(layout, "Values checked", "Numbers, limits, and conditions mentioned in the log", detail.ValuesChecked, C_BLUE);
                AddSection(layout, "Formula", "How the bot calculated or compared the values", detail.Formula, C_PURPLE);
                AddSection(layout, "Outcome", "Whether the bot traded, waited, or blocked execution", detail.Outcome, C_GREEN);
                AddSection(layout, "Expected P/L", "Approximate loss at SL and profit at TP when available", detail.ExpectedPl, C_WARN);
                AddSection(layout, "Next action", "What the operator should check next", detail.NextAction, C_ACCENT);

                scroller.Controls.Add(layout);
                body.Controls.Add(scroller);

                var footer = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 64,
                    BackColor = C_HEADER
                };
                var ok = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Size = new Size(96, 36),
                    Location = new Point(ClientSize.Width - 114, 14),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                    BackColor = C_ACCENT,
                    ForeColor = Color.FromArgb(10, 10, 20)
                };
                ok.FlatAppearance.BorderSize = 0;
                footer.Controls.Add(ok);
                AcceptButton = ok;
                CancelButton = ok;

                Controls.Add(body);
                Controls.Add(footer);
                Controls.Add(header);
            }

            private static void AddSection(FlowLayoutPanel layout, string heading, string subheading, string text, Color accent)
            {
                var card = new Panel
                {
                    Width = 744,
                    Height = EstimateSectionHeight(text),
                    BackColor = C_CARD,
                    Margin = new Padding(4, 0, 4, 10),
                    Padding = new Padding(18, 12, 14, 10)
                };
                card.Paint += (_, e) =>
                {
                    using var border = new Pen(C_BORDER);
                    using var stripe = new SolidBrush(accent);
                    e.Graphics.FillRectangle(stripe, 0, 0, 5, card.Height);
                    e.Graphics.DrawRectangle(border, 0, 0, card.Width - 1, card.Height - 1);
                };

                var title = new Label
                {
                    Text = heading,
                    Location = new Point(18, 10),
                    Size = new Size(700, 21),
                    ForeColor = accent,
                    Font = new Font("Segoe UI Semibold", 9.8F, FontStyle.Bold),
                    AutoEllipsis = true
                };

                var caption = new Label
                {
                    Text = subheading,
                    Location = new Point(18, 32),
                    Size = new Size(700, 19),
                    ForeColor = C_MUTED,
                    Font = new Font("Segoe UI", 8.8F),
                    AutoEllipsis = true
                };

                var detail = new Label
                {
                    Text = string.IsNullOrWhiteSpace(text) ? "Not available from this single log line." : text,
                    Location = new Point(18, 58),
                    Size = new Size(696, Math.Max(42, card.Height - 66)),
                    ForeColor = C_TEXT,
                    Font = new Font("Segoe UI", 9.3F),
                    AutoEllipsis = false
                };

                card.Controls.Add(title);
                card.Controls.Add(caption);
                card.Controls.Add(detail);
                layout.Controls.Add(card);
            }

            private static int EstimateSectionHeight(string text)
            {
                using var bmp = new Bitmap(1, 1);
                using var g = Graphics.FromImage(bmp);
                using var font = new Font("Segoe UI", 9.3F);
                var size = g.MeasureString(string.IsNullOrWhiteSpace(text) ? "Not available from this single log line." : text, font, 696);
                return Math.Min(190, Math.Max(112, (int)Math.Ceiling(size.Height) + 78));
            }
        }
    }
}
