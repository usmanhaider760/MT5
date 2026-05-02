namespace MT5TradingBot.UI
{
    internal sealed class TradeWarningForm : Form
    {
        private static readonly Color C_BG = Color.FromArgb(13, 13, 19);
        private static readonly Color C_HEADER = Color.FromArgb(24, 25, 38);
        private static readonly Color C_CARD = Color.FromArgb(28, 29, 42);
        private static readonly Color C_PANEL = Color.FromArgb(20, 22, 34);
        private static readonly Color C_TEXT = Color.FromArgb(218, 218, 230);
        private static readonly Color C_MUTED = Color.FromArgb(142, 148, 170);
        private static readonly Color C_WARN = Color.FromArgb(250, 199, 117);
        private static readonly Color C_GREEN = Color.FromArgb(72, 199, 142);
        private static readonly Color C_BORDER = Color.FromArgb(45, 48, 64);

        public TradeWarningForm(IReadOnlyList<TradeWarningItem> warnings)
        {
            Text = "Trade Warning Review";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(860, 640);
            BackColor = C_BG;
            ForeColor = C_TEXT;
            Font = new Font("Segoe UI", 9F);

            var header = BuildHeader(warnings.Count);
            var list = BuildWarningList(warnings);
            var footer = BuildFooter();

            Controls.Add(header);
            Controls.Add(list);
            Controls.Add(footer);
            AcceptButton = footer.Controls.OfType<Button>().FirstOrDefault(b => b.Name == "_btnContinueWarning");
            CancelButton = footer.Controls.OfType<Button>().FirstOrDefault(b => b.Name == "_btnCancelWarning");
        }

        private static Panel BuildHeader(int warningCount)
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 104,
                BackColor = C_HEADER,
                Padding = new Padding(18, 14, 18, 12)
            };

            var icon = new Label
            {
                Text = "!",
                Location = new Point(18, 18),
                Size = new Size(44, 44),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(72, 54, 12),
                ForeColor = C_WARN,
                Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold)
            };

            var title = new Label
            {
                Text = "Review warnings before starting trade",
                Location = new Point(78, 16),
                Size = new Size(530, 28),
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold)
            };

            var subtitle = new Label
            {
                Text = "These checks are not hard blocks, but each one can change trade quality, cost, margin, or risk.",
                Location = new Point(80, 48),
                Size = new Size(610, 34),
                ForeColor = C_MUTED
            };

            var countBadge = new Label
            {
                Text = $"{warningCount} warning{(warningCount == 1 ? "" : "s")}",
                Location = new Point(704, 30),
                Size = new Size(128, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(66, 50, 10),
                ForeColor = C_WARN,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold)
            };

            header.Controls.AddRange([icon, title, subtitle, countBadge]);
            return header;
        }

        private static FlowLayoutPanel BuildWarningList(IReadOnlyList<TradeWarningItem> warnings)
        {
            var list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = C_BG,
                Padding = new Padding(18, 14, 18, 10)
            };

            foreach (var warning in warnings)
                list.Controls.Add(CreateWarningCard(warning, 804));

            return list;
        }

        private Panel BuildFooter()
        {
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 74,
                BackColor = C_HEADER,
                Padding = new Padding(18, 14, 18, 14)
            };

            var note = new Label
            {
                Text = "Continuing means you reviewed these warning details and still approve this trade.",
                Location = new Point(18, 20),
                Size = new Size(430, 24),
                ForeColor = C_MUTED
            };

            var btnCancel = new Button
            {
                Name = "_btnCancelWarning",
                Text = "Cancel Trade",
                Location = new Point(574, 18),
                Size = new Size(122, 36),
                BackColor = Color.FromArgb(50, 52, 68),
                ForeColor = C_TEXT,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = C_BORDER;
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            var btnContinue = new Button
            {
                Name = "_btnContinueWarning",
                Text = "Continue Trade",
                Location = new Point(708, 18),
                Size = new Size(134, 36),
                BackColor = C_GREEN,
                ForeColor = Color.FromArgb(10, 10, 20),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold)
            };
            btnContinue.FlatAppearance.BorderSize = 0;
            btnContinue.Click += (_, _) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            footer.Controls.AddRange([note, btnCancel, btnContinue]);
            return footer;
        }

        private static Panel CreateWarningCard(TradeWarningItem warning, int width)
        {
            var card = new Panel
            {
                Width = width,
                Height = 264,
                BackColor = C_CARD,
                Margin = new Padding(0, 0, 0, 14)
            };

            card.Paint += (_, e) =>
            {
                using var border = new Pen(C_BORDER);
                using var stripe = new SolidBrush(C_WARN);
                e.Graphics.FillRectangle(stripe, 0, 0, 5, card.Height);
                e.Graphics.DrawRectangle(border, 0, 0, card.Width - 1, card.Height - 1);
            };

            var icon = new Label
            {
                Text = "!",
                Location = new Point(18, 16),
                Size = new Size(32, 32),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(72, 54, 12),
                ForeColor = C_WARN,
                Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold)
            };

            var title = Label(warning.Title, 62, 14, width - 88, 24, C_WARN, bold: true, size: 10F);
            var detail = SectionLabel("Detail", warning.Message, 62, 42, width - 86, 52);
            var current = ValuePanel("Current value", warning.CurrentValue, "Source", warning.CurrentValueSource, 62, 104, 342, 92);
            var baseline = ValuePanel("Base / compare value", warning.BaseValue, "Source", warning.BaseValueSource, 418, 104, 342, 92);
            var compare = SectionLabel("Comparison", warning.Compare, 62, 206, width - 86, 42);

            card.Controls.AddRange([icon, title, detail, current, baseline, compare]);
            return card;
        }

        private static Panel ValuePanel(string label, string value, string sourceLabel, string source, int x, int y, int width, int height)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = C_PANEL
            };
            panel.Paint += (_, e) =>
            {
                using var border = new Pen(C_BORDER);
                e.Graphics.DrawRectangle(border, 0, 0, panel.Width - 1, panel.Height - 1);
            };

            panel.Controls.Add(Label(label, 10, 8, width - 20, 18, C_MUTED, bold: true, size: 8.5F));
            panel.Controls.Add(Label(value, 10, 30, width - 20, 22, C_TEXT, bold: true, size: 10F));
            panel.Controls.Add(Label($"{sourceLabel}: {source}", 10, 56, width - 20, 28, C_MUTED, size: 8.5F));
            return panel;
        }

        private static Panel SectionLabel(string heading, string text, int x, int y, int width, int height)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = C_CARD
            };

            panel.Controls.Add(Label($"{heading}:", 0, 0, 86, 18, C_MUTED, bold: true, size: 8.5F));
            panel.Controls.Add(Label(text, 0, 20, width, height - 20, C_TEXT, size: 9F));
            return panel;
        }

        private static Label Label(
            string text,
            int x,
            int y,
            int width,
            int height,
            Color color,
            bool bold = false,
            float size = 9F) => new()
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            AutoSize = false,
            AutoEllipsis = false,
            ForeColor = color,
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular)
        };
    }

    internal sealed record TradeWarningItem(
        string Title,
        string Message,
        string CurrentValue,
        string CurrentValueSource,
        string BaseValue,
        string BaseValueSource,
        string Compare);
}
