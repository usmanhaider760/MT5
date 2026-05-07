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
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            MinimumSize = new Size(860, 640);
            ClientSize = new Size(860, 640);
            BackColor = C_BG;
            ForeColor = C_TEXT;
            Font = new Font("Segoe UI", 9F);

            var header = BuildHeader(warnings.Count);
            var list = BuildWarningList(warnings);
            var footer = BuildFooter();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = C_BG,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            header.Dock = DockStyle.Fill;
            list.Dock = DockStyle.Fill;
            footer.Dock = DockStyle.Fill;
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(list, 0, 1);
            root.Controls.Add(footer, 0, 2);
            Controls.Add(root);

            AcceptButton = footer.Controls.OfType<Button>().FirstOrDefault(b => b.Name == "_btnContinueWarning");
            CancelButton = footer.Controls.OfType<Button>().FirstOrDefault(b => b.Name == "_btnCancelWarning");
            Resize += (_, _) => ResizeWarningCards(list);
            Shown += (_, _) => ResizeWarningCards(list);
            CopyablePopupText.Enable(this);
        }

        public Task<DialogResult> ShowModelessAsync(IWin32Window? owner)
        {
            var completion = new TaskCompletionSource<DialogResult>();
            FormClosed += (_, _) =>
            {
                var result = DialogResult == DialogResult.None ? DialogResult.Cancel : DialogResult;
                completion.TrySetResult(result);
            };

            if (owner == null)
                Show();
            else
                Show(owner);

            return completion.Task;
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
                Padding = new Padding(18, 18, 18, 14)
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

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = C_HEADER,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 134));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var note = new Label
            {
                Text = "Continuing means you reviewed these warning details and still approve this trade.",
                Dock = DockStyle.Fill,
                ForeColor = C_MUTED,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0, 0, 12, 0)
            };

            var btnCancel = new Button
            {
                Name = "_btnCancelWarning",
                Text = "Cancel Trade",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 52, 68),
                ForeColor = C_TEXT,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 4, 12, 4)
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
                Dock = DockStyle.Fill,
                BackColor = C_GREEN,
                ForeColor = Color.FromArgb(10, 10, 20),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                Margin = new Padding(0, 4, 0, 4)
            };
            btnContinue.FlatAppearance.BorderSize = 0;
            btnContinue.Click += (_, _) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            layout.Controls.Add(note, 0, 0);
            layout.Controls.Add(btnCancel, 1, 0);
            layout.Controls.Add(btnContinue, 2, 0);
            footer.Controls.Add(layout);
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
            title.Name = "_title";
            var detail = SectionLabel("Detail", warning.Message, 62, 42, width - 86, 52);
            detail.Name = "_detail";
            var current = ValuePanel(ValueHeading("Current value", warning.CurrentValueSource), warning.CurrentValue, "Source", warning.CurrentValueSource, 62, 104, 342, 92);
            current.Name = "_current";
            var baseline = ValuePanel(ValueHeading("Base value", warning.BaseValueSource), warning.BaseValue, "Source", warning.BaseValueSource, 418, 104, 342, 92);
            baseline.Name = "_baseline";
            var compare = SectionLabel("Comparison", warning.Compare, 62, 206, width - 86, 42);
            compare.Name = "_compare";

            card.Controls.AddRange([icon, title, detail, current, baseline, compare]);
            return card;
        }

        private static void ResizeWarningCards(FlowLayoutPanel list)
        {
            int cardWidth = Math.Max(804, list.ClientSize.Width - list.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 8);
            foreach (var card in list.Controls.OfType<Panel>())
                ResizeWarningCard(card, cardWidth);
        }

        private static void ResizeWarningCard(Panel card, int width)
        {
            card.Width = width;

            int left = 62;
            int right = 44;
            int gap = 14;
            int twoColumnWidth = Math.Max(280, (width - left - right - gap) / 2);

            if (card.Controls.Find("_title", false).FirstOrDefault() is Label title)
                title.Width = width - 88;

            if (card.Controls.Find("_detail", false).FirstOrDefault() is Panel detail)
                ResizeSectionPanel(detail, width - 86);

            if (card.Controls.Find("_current", false).FirstOrDefault() is Panel current)
            {
                current.Width = twoColumnWidth;
                ResizeValuePanel(current);
            }

            if (card.Controls.Find("_baseline", false).FirstOrDefault() is Panel baseline)
            {
                baseline.Left = left + twoColumnWidth + gap;
                baseline.Width = twoColumnWidth;
                ResizeValuePanel(baseline);
            }

            if (card.Controls.Find("_compare", false).FirstOrDefault() is Panel compare)
                ResizeSectionPanel(compare, width - 86);
        }

        private static void ResizeValuePanel(Panel panel)
        {
            foreach (var label in panel.Controls.OfType<Label>())
                label.Width = panel.Width - 20;
        }

        private static void ResizeSectionPanel(Panel panel, int width)
        {
            panel.Width = width;
            var labels = panel.Controls.OfType<Label>().ToList();
            if (labels.Count > 1)
                labels[1].Width = width;
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

        private static string ValueHeading(string prefix, string source)
        {
            string context = ShortValueContext(source);
            return string.IsNullOrWhiteSpace(context)
                ? prefix
                : $"{prefix}: {context}";
        }

        private static string ShortValueContext(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "";

            string text = source.Trim();
            string[] prefixes =
            [
                "Review Trade current ",
                "Review Trade ",
                "Review snapshot: ",
                "Review Trade risk preview: ",
                "Live MT5 symbol snapshot: ",
                "Live rule: ",
                "Bot Configuration: ",
                "AI API Config tab: ",
                "Scalping trade page ",
                "Normal trade page ",
                "Scalping Trade Page: ",
                "Normal Trade Page: ",
                "Trade Page: "
            ];

            foreach (string prefix in prefixes)
            {
                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    text = text[prefix.Length..].Trim();
                    break;
                }
            }

            text = text
                .Replace("price.spread_pips", "live spread", StringComparison.OrdinalIgnoreCase)
                .Replace("Max Spread Pips", "max spread", StringComparison.OrdinalIgnoreCase)
                .Replace("Risk Reward Ratio", "required R:R", StringComparison.OrdinalIgnoreCase)
                .Replace("TP pips / SL pips after applying visible inputs", "TP/SL R:R", StringComparison.OrdinalIgnoreCase)
                .Replace("TP pips after applying visible inputs", "TP pips", StringComparison.OrdinalIgnoreCase)
                .Replace("selected lot size, entry, stop loss, live equity", "risk vs equity", StringComparison.OrdinalIgnoreCase)
                .Replace("Max Risk %", "max risk", StringComparison.OrdinalIgnoreCase);

            const int maxChars = 38;
            return text.Length <= maxChars ? text : text[..(maxChars - 1)].TrimEnd() + "...";
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
