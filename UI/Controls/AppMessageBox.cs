namespace MT5TradingBot.UI
{
    internal static class AppMessageBox
    {
        public static DialogResult Show(
            IWin32Window? owner,
            string message,
            string title = "MT5 Trading Bot",
            MessageBoxIcon icon = MessageBoxIcon.Information,
            MessageBoxButtons buttons = MessageBoxButtons.OK)
        {
            using var form = new AppMessageBoxForm(title, message, icon, buttons);
            form.StartPosition = owner == null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
            return owner == null ? form.ShowDialog() : form.ShowDialog(owner);
        }

        public static bool Confirm(IWin32Window? owner, string message, string title = "Confirm") =>
            Show(owner, message, title, MessageBoxIcon.Warning, MessageBoxButtons.YesNo) == DialogResult.Yes;

        public static void Warning(IWin32Window? owner, string message, string title = "Warning") =>
            Show(owner, message, title, MessageBoxIcon.Warning, MessageBoxButtons.OK);

        public static void Error(IWin32Window? owner, string message, string title = "Error") =>
            Show(owner, message, title, MessageBoxIcon.Error, MessageBoxButtons.OK);

        public static void Error(
            IWin32Window? owner,
            string message,
            string title,
            MessageBoxButtons buttons,
            MessageBoxIcon icon) =>
            Show(owner, message, title, icon, buttons);

        public static void Info(IWin32Window? owner, string message, string title = "Information") =>
            Show(owner, message, title, MessageBoxIcon.Information, MessageBoxButtons.OK);

        private sealed class AppMessageBoxForm : Form
        {
            private static readonly Color C_BG = Color.FromArgb(13, 13, 19);
            private static readonly Color C_HEADER = Color.FromArgb(24, 25, 38);
            private static readonly Color C_PANEL = Color.FromArgb(28, 29, 42);
            private static readonly Color C_TEXT = Color.FromArgb(218, 218, 230);
            private static readonly Color C_MUTED = Color.FromArgb(142, 148, 170);
            private static readonly Color C_ACCENT = Color.FromArgb(99, 179, 237);
            private static readonly Color C_WARN = Color.FromArgb(250, 199, 117);
            private static readonly Color C_ERROR = Color.FromArgb(252, 95, 95);
            private static readonly Color C_GREEN = Color.FromArgb(72, 199, 142);
            private static readonly Color C_BORDER = Color.FromArgb(45, 48, 64);

            public AppMessageBoxForm(string titleText, string message, MessageBoxIcon icon, MessageBoxButtons buttons)
            {
                string cleanMessage = string.IsNullOrWhiteSpace(message) ? "No message details were provided." : message.Trim();
                var accent = AccentFor(icon);
                var (symbol, caption) = IconInfo(icon);
                int messageHeight = Math.Min(280, Math.Max(74, EstimateTextHeight(cleanMessage, 560)));
                int height = Math.Min(460, Math.Max(238, 154 + messageHeight));

                Text = titleText;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ClientSize = new Size(680, height);
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
                    Text = symbol,
                    Location = new Point(20, 20),
                    Size = new Size(44, 44),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.FromArgb(38, 40, 56),
                    ForeColor = accent,
                    Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold)
                };

                var title = new Label
                {
                    Text = titleText,
                    Location = new Point(82, 18),
                    Size = new Size(560, 26),
                    ForeColor = C_TEXT,
                    Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                    AutoEllipsis = true
                };

                var subTitle = new Label
                {
                    Text = caption,
                    Location = new Point(84, 48),
                    Size = new Size(540, 22),
                    ForeColor = C_MUTED,
                    AutoEllipsis = true
                };

                header.Controls.AddRange([iconBox, title, subTitle]);

                var body = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = C_BG,
                    Padding = new Padding(18, 16, 18, 12)
                };

                var messagePanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = C_PANEL,
                    Padding = new Padding(14, 12, 14, 12)
                };
                messagePanel.Paint += (_, e) =>
                {
                    using var border = new Pen(C_BORDER);
                    using var stripe = new SolidBrush(accent);
                    e.Graphics.FillRectangle(stripe, 0, 0, 5, messagePanel.Height);
                    e.Graphics.DrawRectangle(border, 0, 0, messagePanel.Width - 1, messagePanel.Height - 1);
                };

                var messageLabel = new Label
                {
                    Text = cleanMessage,
                    Dock = DockStyle.Fill,
                    ForeColor = C_TEXT,
                    Font = new Font("Segoe UI", 9.5F),
                    AutoEllipsis = false
                };
                messagePanel.Controls.Add(messageLabel);
                body.Controls.Add(messagePanel);

                var footer = BuildFooter(buttons, accent);

                Controls.Add(body);
                Controls.Add(footer);
                Controls.Add(header);
            }

            private Panel BuildFooter(MessageBoxButtons buttons, Color accent)
            {
                var footer = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 64,
                    BackColor = C_HEADER
                };

                var buttonList = BuildButtons(buttons, accent);
                int x = ClientSize.Width - 18 - buttonList.Sum(b => b.Width) - Math.Max(0, buttonList.Count - 1) * 10;
                foreach (var button in buttonList)
                {
                    button.Location = new Point(x, 14);
                    x += button.Width + 10;
                    footer.Controls.Add(button);

                    if (button.DialogResult is DialogResult.OK or DialogResult.Yes)
                        AcceptButton = button;
                    if (button.DialogResult is DialogResult.Cancel or DialogResult.No)
                        CancelButton = button;
                }

                return footer;
            }

            private List<Button> BuildButtons(MessageBoxButtons buttons, Color accent) =>
                buttons switch
                {
                    MessageBoxButtons.YesNo => [Button("No", DialogResult.No, secondary: true), Button("Yes", DialogResult.Yes, accent)],
                    MessageBoxButtons.OKCancel => [Button("Cancel", DialogResult.Cancel, secondary: true), Button("OK", DialogResult.OK, accent)],
                    _ => [Button("OK", DialogResult.OK, accent)]
                };

            private static Button Button(string text, DialogResult result, Color? accent = null, bool secondary = false)
            {
                var button = new Button
                {
                    Text = text,
                    DialogResult = result,
                    Size = new Size(text.Length > 6 ? 118 : 96, 36),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                    BackColor = secondary ? Color.FromArgb(50, 52, 68) : accent ?? C_GREEN,
                    ForeColor = secondary ? C_TEXT : Color.FromArgb(10, 10, 20)
                };
                button.FlatAppearance.BorderColor = secondary ? C_BORDER : button.BackColor;
                button.FlatAppearance.BorderSize = secondary ? 1 : 0;
                return button;
            }

            private static (string Symbol, string Caption) IconInfo(MessageBoxIcon icon) =>
                icon switch
                {
                    MessageBoxIcon.Error => ("X", "Action could not be completed. Review the detail below."),
                    MessageBoxIcon.Warning => ("!", "Please review this warning before continuing."),
                    MessageBoxIcon.Question => ("?", "Confirm the action before continuing."),
                    _ => ("i", "Information from MT5 Trading Bot.")
                };

            private static Color AccentFor(MessageBoxIcon icon) =>
                icon switch
                {
                    MessageBoxIcon.Error => C_ERROR,
                    MessageBoxIcon.Warning => C_WARN,
                    MessageBoxIcon.Question => C_ACCENT,
                    _ => C_ACCENT
                };

            private static int EstimateTextHeight(string text, int width)
            {
                using var bmp = new Bitmap(1, 1);
                using var g = Graphics.FromImage(bmp);
                var size = g.MeasureString(text, new Font("Segoe UI", 9.5F), width);
                return (int)Math.Ceiling(size.Height) + 32;
            }
        }
    }
}
