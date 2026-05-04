using System.Text;
using System.Text.RegularExpressions;

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
                string logTimestamp = ExtractLogTimestamp(detail.OriginalMessage);
                string auditText = BuildAuditText(detail, logTimestamp);

                Text = "Log Decision Audit";
                FormBorderStyle = FormBorderStyle.Sizable;
                MaximizeBox = true;
                MinimizeBox = false;
                ClientSize = new Size(860, 720);
                MinimumSize = new Size(760, 560);
                BackColor = C_BG;
                ForeColor = C_TEXT;
                Font = new Font("Segoe UI", 9F);
                ShowInTaskbar = false;

                var header = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 116,
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
                    Text = "Log Decision Audit",
                    Location = new Point(82, 18),
                    Size = new Size(670, 26),
                    ForeColor = C_TEXT,
                    Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                    AutoEllipsis = true,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                var subtitle = new Label
                {
                    Text = "Copyable decision evidence: reason, checked values, formulas, outcome, and projected P/L.",
                    Location = new Point(84, 48),
                    Size = new Size(690, 22),
                    ForeColor = C_MUTED,
                    AutoEllipsis = true,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                var timestamp = new TextBox
                {
                    Text = $"Log time: {logTimestamp}",
                    Location = new Point(84, 76),
                    Size = new Size(690, 24),
                    ReadOnly = true,
                    BorderStyle = BorderStyle.None,
                    BackColor = C_HEADER,
                    ForeColor = C_WARN,
                    Font = new Font("Segoe UI Semibold", 9.3F, FontStyle.Bold),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                header.Controls.AddRange([iconBox, title, subtitle, timestamp]);

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

                AddSection(layout, "Audit text", "Copy this whole block and send it for decision review", auditText, C_WARN);
                AddSection(layout, "Original log", "The full log message behind the selected row", detail.OriginalMessage, C_MUTED);
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

                var copyAll = new Button
                {
                    Text = "Copy Audit Text",
                    Size = new Size(150, 36),
                    Location = new Point(18, 14),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                    BackColor = C_WARN,
                    ForeColor = Color.FromArgb(10, 10, 20)
                };
                copyAll.FlatAppearance.BorderSize = 0;
                copyAll.Click += (_, _) =>
                {
                    try
                    {
                        Clipboard.SetText(auditText);
                        copyAll.Text = "Copied";
                    }
                    catch
                    {
                        copyAll.Text = "Copy failed";
                    }
                };

                var ok = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Size = new Size(96, 36),
                    Location = new Point(ClientSize.Width - 114, 14),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                    BackColor = C_ACCENT,
                    ForeColor = Color.FromArgb(10, 10, 20)
                };
                ok.FlatAppearance.BorderSize = 0;
                footer.Controls.Add(copyAll);
                footer.Controls.Add(ok);
                AcceptButton = ok;
                CancelButton = ok;

                Controls.Add(body);
                Controls.Add(footer);
                Controls.Add(header);

                void ResizeSections()
                {
                    int cardWidth = Math.Max(690, scroller.ClientSize.Width - 32);
                    layout.Width = cardWidth + 8;
                    foreach (Panel card in layout.Controls.OfType<Panel>())
                    {
                        ResizeSection(card, cardWidth);
                    }
                }

                scroller.Resize += (_, _) => ResizeSections();
                ResizeSections();
            }

            private static void AddSection(FlowLayoutPanel layout, string heading, string subheading, string text, Color accent)
            {
                string bodyText = string.IsNullOrWhiteSpace(text) ? "Not available from this single log line." : text;
                var card = new Panel
                {
                    Width = 744,
                    Height = EstimateSectionHeight(bodyText, 696),
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
                    Name = "_sectionTitle",
                    Text = heading,
                    Location = new Point(18, 10),
                    Size = new Size(700, 21),
                    ForeColor = accent,
                    Font = new Font("Segoe UI Semibold", 9.8F, FontStyle.Bold),
                    AutoEllipsis = true
                };

                var caption = new Label
                {
                    Name = "_sectionCaption",
                    Text = subheading,
                    Location = new Point(18, 32),
                    Size = new Size(700, 19),
                    ForeColor = C_MUTED,
                    Font = new Font("Segoe UI", 8.8F),
                    AutoEllipsis = true
                };

                var detail = new TextBox
                {
                    Name = "_sectionDetail",
                    Text = bodyText,
                    Location = new Point(18, 58),
                    Size = new Size(696, Math.Max(42, card.Height - 66)),
                    Multiline = true,
                    ReadOnly = true,
                    BorderStyle = BorderStyle.None,
                    ScrollBars = ScrollBars.Vertical,
                    BackColor = C_CARD,
                    ForeColor = C_TEXT,
                    Font = new Font("Segoe UI", 9.3F)
                };

                card.Controls.Add(title);
                card.Controls.Add(caption);
                card.Controls.Add(detail);
                layout.Controls.Add(card);
            }

            private static void ResizeSection(Panel card, int cardWidth)
            {
                int textWidth = Math.Max(620, cardWidth - 48);
                var title = card.Controls.Find("_sectionTitle", false).OfType<Label>().FirstOrDefault();
                var caption = card.Controls.Find("_sectionCaption", false).OfType<Label>().FirstOrDefault();
                var detail = card.Controls.Find("_sectionDetail", false).OfType<TextBox>().FirstOrDefault();

                card.Width = cardWidth;
                if (title != null) title.Width = textWidth;
                if (caption != null) caption.Width = textWidth;
                if (detail != null)
                {
                    detail.Width = textWidth;
                    card.Height = EstimateSectionHeight(detail.Text, textWidth);
                    detail.Height = Math.Max(42, card.Height - 66);
                }
            }

            private static int EstimateSectionHeight(string text, int width)
            {
                using var bmp = new Bitmap(1, 1);
                using var g = Graphics.FromImage(bmp);
                using var font = new Font("Segoe UI", 9.3F);
                var size = g.MeasureString(string.IsNullOrWhiteSpace(text) ? "Not available from this single log line." : text, font, width);
                return Math.Min(360, Math.Max(112, (int)Math.Ceiling(size.Height) + 84));
            }

            private static string ExtractLogTimestamp(string original)
            {
                var match = Regex.Match(original, @"^\[(?<time>[^\]]+)\]");
                return match.Success ? match.Groups["time"].Value.Trim() : "not available";
            }

            private static string BuildAuditText(AppLogDetail detail, string logTimestamp)
            {
                var audit = new StringBuilder();
                audit.AppendLine("MT5 BOT LOG DECISION AUDIT");
                audit.AppendLine($"Log time: {logTimestamp}");
                audit.AppendLine($"Audit copied at local time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                audit.AppendLine();
                audit.AppendLine("ORIGINAL LOG");
                audit.AppendLine(detail.OriginalMessage);
                audit.AppendLine();
                audit.AppendLine("MEANING");
                audit.AppendLine(detail.Meaning);
                audit.AppendLine();
                audit.AppendLine("VALUES CHECKED / DECISION INPUTS");
                audit.AppendLine(detail.ValuesChecked);
                audit.AppendLine();
                audit.AppendLine("FORMULA / RULE");
                audit.AppendLine(detail.Formula);
                audit.AppendLine();
                audit.AppendLine("OUTCOME");
                audit.AppendLine(detail.Outcome);
                audit.AppendLine();
                audit.AppendLine("EXPECTED P/L");
                audit.AppendLine(detail.ExpectedPl);
                audit.AppendLine();
                audit.AppendLine("NEXT ACTION");
                audit.AppendLine(detail.NextAction);
                audit.AppendLine();
                audit.AppendLine("AUDIT NOTE");
                audit.AppendLine("This audit text is generated from the selected log line and the full hidden log message kept by the UI. If the selected row is from an older shortened on-screen log, some fields may be unavailable unless the original log file line is opened.");
                return audit.ToString();
            }
        }
    }
}
