namespace MT5TradingBot.UI
{
    public sealed partial class MainForm
    {
        private static readonly Color C_BG      = Color.FromArgb(13, 13, 19);
        private static readonly Color C_SURFACE = Color.FromArgb(22, 22, 32);
        private static readonly Color C_CARD    = Color.FromArgb(28, 29, 42);
        private static readonly Color C_ACCENT  = Color.FromArgb(99, 179, 237);
        private static readonly Color C_GREEN   = Color.FromArgb(72, 199, 142);
        private static readonly Color C_RED     = Color.FromArgb(252, 95, 95);
        private static readonly Color C_YELLOW  = Color.FromArgb(250, 199, 117);
        private static readonly Color C_TEXT    = Color.FromArgb(218, 218, 230);
        private static readonly Color C_MUTED   = Color.FromArgb(110, 110, 130);
        private static readonly Color C_BORDER  = Color.FromArgb(45, 48, 64);
        private static readonly Font F_BASE     = new("Segoe UI", 9F);
        private static readonly Font F_LABEL    = new("Segoe UI", 9F);
        private static readonly Font F_HEADER   = new("Segoe UI Semibold", 10F, FontStyle.Bold);
        private static readonly Font F_BUTTON   = new("Segoe UI Semibold", 9F);
        private static readonly Font F_ACTION   = new("Segoe UI Semibold", 10.5F);
        private static readonly Font F_MONO     = new("Consolas", 9F);

        private void ApplyStableLayout()
        {
            SuspendLayout();

            ConfigureRootLayout();
            ConfigureHeaderLayout();
            ConfigureTradeTabLayout();
            ConfigureGridTabLayout(_tabPositions, "_positionsLayout", _gridPos, _btnClosePos, _btnCloseAllPos, _btnRefreshPos);
            ConfigureGridTabLayout(_tabHistory, "_historyLayout", _gridHistory, _btnImportHistory, _btnClearHistory);
            ConfigureBotTabLayout();
            ConfigureClaudeTabLayout();
            ConfigureLogTabLayout();
            ApplyVisualSystem();

            ResumeLayout(true);
        }

        private void ConfigureRootLayout()
        {
            _layoutRoot.Dock = DockStyle.Fill;
            _layoutRoot.Margin = Padding.Empty;
            _layoutRoot.Padding = Padding.Empty;
            _layoutRoot.ColumnStyles.Clear();
            _layoutRoot.RowStyles.Clear();
            _layoutRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            _layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            _layoutRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            _layoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _pnlHeader.Dock = DockStyle.Fill;
            _pnlHeader.Margin = Padding.Empty;
            _pnlConnBar.Dock = DockStyle.Fill;
            _pnlConnBar.Margin = Padding.Empty;
            _pnlAccountBar.Dock = DockStyle.Fill;
            _pnlAccountBar.Margin = Padding.Empty;
            _tabControl.Dock = DockStyle.Fill;
            _tabControl.Margin = Padding.Empty;
            _tabControl.SizeMode = TabSizeMode.Fixed;
            _tabControl.ItemSize = new Size(132, 34);
            _tabControl.Padding = new Point(12, 6);
        }

        private void ConfigureHeaderLayout()
        {
            _lblTitle.Text = "MT5 Trading Bot";
            _lblTitle.Location = new Point(14, 14);
            _pnlDot.Location = new Point(230, 21);
            _pnlDot.Size = new Size(10, 10);
            _lblConnStatus.Location = new Point(246, 18);
            _lblTime.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _lblTime.Location = new Point(Math.Max(760, ClientSize.Width - 360), 18);

            ConfigureConnectionBarLayout();
            ConfigureAccountBarLayout();
        }

        private void ConfigureConnectionBarLayout()
        {
            _cmbMode.Location = new Point(14, 8);
            _cmbMode.Size = new Size(120, 26);
            _lblPipeLabel.Location = new Point(152, 11);
            _lblPipeLabel.Size = new Size(70, 20);
            _txtPipeName.Location = new Point(226, 8);
            _txtPipeName.Size = new Size(260, 25);
            _btnConnect.Location = new Point(502, 6);
            _btnConnect.Size = new Size(112, 30);
            _btnDisconnect.Location = new Point(624, 6);
            _btnDisconnect.Size = new Size(112, 30);
            _chkAutoConn.Location = new Point(754, 9);
            _chkAutoConn.Size = new Size(180, 24);
        }

        private void ConfigureAccountBarLayout()
        {
            var labels = new[] { _lblAccNum, _lblBalance, _lblEquity, _lblFreeMargin, _lblPnl, _lblMarginLvl };
            var widths = new[] { 300, 145, 145, 165, 130, 150 };
            var x = 14;

            for (var i = 0; i < labels.Length; i++)
            {
                labels[i].AutoSize = false;
                labels[i].AutoEllipsis = true;
                labels[i].Location = new Point(x, 10);
                labels[i].Size = new Size(widths[i], 20);
                labels[i].TextAlign = ContentAlignment.MiddleLeft;
                x += widths[i] + 14;
            }
        }

        private void ConfigureTradeTabLayout()
        {
            var layout = ResetTabLayout(_tabTrade, "_tradeTabLayout", 2, 1);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 382F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _pnlTradeLeft.Dock = DockStyle.Fill;
            _pnlTradeLeft.Margin = new Padding(0, 0, 10, 0);
            _pnlTradeLeft.MinimumSize = new Size(360, 620);
            _pnlTradeLeft.AutoScroll = true;

            _pnlTradeRight.Dock = DockStyle.Fill;
            _pnlTradeRight.Margin = Padding.Empty;
            _pnlTradeRight.MinimumSize = new Size(520, 620);

            layout.Controls.Add(_pnlTradeLeft, 0, 0);
            layout.Controls.Add(_pnlTradeRight, 1, 0);

            ConfigureTradeTicketLayout();
            ConfigureJsonPanelLayout();
        }

        private void ConfigureTradeTicketLayout()
        {
            _lblTradeHeader.Location = new Point(18, 16);
            _lblTradeHeader.Size = new Size(300, 24);

            PlaceField(_lblPairLabel, _cmbPair, 56);
            PlaceField(_lblDirLabel, _cmbDir, 92);
            PlaceField(_lblOrderTypeLabel, _cmbOrderType, 128);
            PlaceField(_lblEntryLabel, _txtEntry, 176);
            PlaceField(_lblSLLabel, _txtSL, 212);
            PlaceField(_lblTPLabel, _txtTP, 248);
            PlaceField(_lblTP2Label, _txtTP2, 284);

            _chkAutoLot.Location = new Point(18, 326);
            _chkAutoLot.Size = new Size(180, 24);
            PlaceField(_lblLotLabel, _txtLot, 364, 120);
            _chkMoveSLBE.Location = new Point(18, 402);
            _chkMoveSLBE.Size = new Size(260, 24);

            _pnlRR.Location = new Point(18, 444);
            _pnlRR.Size = new Size(322, 84);
            _lblRR.Location = new Point(14, 12);
            _lblRR.Size = new Size(290, 20);
            _lblDollarRisk.Location = new Point(14, 34);
            _lblDollarRisk.Size = new Size(290, 20);
            _lblDollarProfit.Location = new Point(14, 56);
            _lblDollarProfit.Size = new Size(290, 20);

            _btnBuy.Location = new Point(18, 552);
            _btnBuy.Size = new Size(154, 46);
            _btnSell.Location = new Point(186, 552);
            _btnSell.Size = new Size(154, 46);
        }

        private void ConfigureJsonPanelLayout()
        {
            var layout = ResetPanelLayout(_pnlTradeRight, "_jsonPanelLayout", 1, 3);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));

            _lblJsonHeader.Dock = DockStyle.Fill;
            _lblJsonHeader.Margin = Padding.Empty;
            _lblJsonHeader.TextAlign = ContentAlignment.MiddleLeft;

            _txtJson.Dock = DockStyle.Fill;
            _txtJson.Margin = new Padding(0, 8, 0, 8);

            var buttons = CreateButtonRow("_jsonButtons", _btnJsonLoad, _btnJsonExec, _btnJsonFmt, _btnJsonSample);

            layout.Controls.Add(_lblJsonHeader, 0, 0);
            layout.Controls.Add(_txtJson, 0, 1);
            layout.Controls.Add(buttons, 0, 2);
        }

        private void ConfigureGridTabLayout(TabPage tab, string name, DataGridView grid, params Button[] buttons)
        {
            var layout = ResetTabLayout(tab, name, 1, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            grid.Dock = DockStyle.Fill;
            grid.Margin = Padding.Empty;

            layout.Controls.Add(grid, 0, 0);
            layout.Controls.Add(CreateButtonRow($"{name}Buttons", buttons), 0, 1);
        }

        private void ConfigureBotTabLayout()
        {
            var layout = ResetTabLayout(_tabBot, "_botTabLayout", 2, 2);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 570F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _lblBotBadge.Text = "BOT STOPPED";
            _lblBotBadge.Dock = DockStyle.Fill;
            _lblBotBadge.Margin = Padding.Empty;
            _lblBotBadge.TextAlign = ContentAlignment.MiddleLeft;

            _pnlBotCard.Dock = DockStyle.Fill;
            _pnlBotCard.Margin = new Padding(0, 8, 10, 0);
            _pnlBotCard.AutoScroll = true;

            _pnlBotInfo.Dock = DockStyle.Fill;
            _pnlBotInfo.Margin = new Padding(0, 8, 0, 0);

            layout.Controls.Add(_lblBotBadge, 0, 0);
            layout.SetColumnSpan(_lblBotBadge, 2);
            layout.Controls.Add(_pnlBotCard, 0, 1);
            layout.Controls.Add(_pnlBotInfo, 1, 1);

            ConfigureBotSettingsLayout();

            var infoLayout = ResetPanelLayout(_pnlBotInfo, "_botInfoLayout", 1, 3);
            infoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            infoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            infoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));

            _lblBotInfoHeader.Dock = DockStyle.Fill;
            _lblBotInfoHeader.TextAlign = ContentAlignment.MiddleLeft;
            _rtbBotHelp.Dock = DockStyle.Fill;
            _rtbBotHelp.Margin = new Padding(0, 8, 0, 8);

            infoLayout.Controls.Add(_lblBotInfoHeader, 0, 0);
            infoLayout.Controls.Add(_rtbBotHelp, 0, 1);
            infoLayout.Controls.Add(CreateButtonRow("_botInfoButtons", _btnOpenFolder), 0, 2);
        }

        private void ConfigureBotSettingsLayout()
        {
            _lblBotCardHeader.Location = new Point(18, 16);
            _lblBotCardHeader.Size = new Size(300, 24);

            PlaceField(_lblWatchFolderLabel, _txtWatchFolder, 58, 330, 176);
            PlaceField(_lblRiskLabel, _nudRisk, 96, 110, 176);
            PlaceField(_lblMinRRLabel, _nudMinRR, 134, 110, 176);
            PlaceField(_lblMaxTradesLabel, _nudMaxTrades, 172, 110, 176);
            PlaceField(_lblPollMsLabel, _nudPollMs, 210, 130, 176);
            PlaceField(_lblRetryLabel, _nudRetry, 248, 110, 176);
            PlaceField(_lblAllowedPairsLabel, _txtAllowedPairs, 286, 330, 176);
            PlaceField(_lblDrawdownLabel, _nudDrawdownPct, 324, 110, 176);

            _chkAutoLotBot.Location = new Point(18, 374);
            _chkEnforceRR.Location = new Point(18, 408);
            _chkDrawdown.Location = new Point(18, 442);
            _chkAutoStart.Location = new Point(18, 476);
            _chkAutoLotBot.Size = _chkEnforceRR.Size = _chkDrawdown.Size = _chkAutoStart.Size = new Size(360, 24);

            _btnStartBot.Location = new Point(18, 532);
            _btnStartBot.Size = new Size(158, 42);
            _btnStopBot.Location = new Point(188, 532);
            _btnStopBot.Size = new Size(158, 42);
        }

        private void ConfigureClaudeTabLayout()
        {
            var layout = ResetTabLayout(_tabClaude, "_claudeTabLayout", 2, 2);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 570F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _lblClaudeBadge.Text = "CLAUDE STOPPED";
            _lblClaudeBadge.Dock = DockStyle.Fill;
            _lblClaudeBadge.Margin = Padding.Empty;
            _lblClaudeBadge.TextAlign = ContentAlignment.MiddleLeft;

            _pnlClaudeCard.Dock = DockStyle.Fill;
            _pnlClaudeCard.Margin = new Padding(0, 8, 10, 0);
            _pnlClaudeCard.AutoScroll = true;

            _pnlClaudePromptCard.Dock = DockStyle.Fill;
            _pnlClaudePromptCard.Margin = new Padding(0, 8, 0, 0);

            layout.Controls.Add(_lblClaudeBadge, 0, 0);
            layout.SetColumnSpan(_lblClaudeBadge, 2);
            layout.Controls.Add(_pnlClaudeCard, 0, 1);
            layout.Controls.Add(_pnlClaudePromptCard, 1, 1);

            ConfigureClaudeSettingsLayout();

            var promptLayout = ResetPanelLayout(_pnlClaudePromptCard, "_claudePromptLayout", 1, 3);
            promptLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            promptLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            promptLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));

            _lblPromptHeader.Dock = DockStyle.Fill;
            _lblPromptHeader.TextAlign = ContentAlignment.MiddleLeft;
            _txtClaudePrompt.Dock = DockStyle.Fill;
            _txtClaudePrompt.Margin = new Padding(0, 8, 0, 8);

            promptLayout.Controls.Add(_lblPromptHeader, 0, 0);
            promptLayout.Controls.Add(_txtClaudePrompt, 0, 1);
            promptLayout.Controls.Add(CreateButtonRow("_claudePromptButtons", _btnResetPrompt), 0, 2);
        }

        private void ConfigureClaudeSettingsLayout()
        {
            _lblClaudeCardHeader.Location = new Point(18, 16);
            _lblClaudeCardHeader.Size = new Size(330, 24);

            PlaceField(_lblApiKeyLabel, _txtClaudeApiKey, 58, 320, 186);
            PlaceField(_lblModelLabel, _lblModelValue, 96, 320, 186);
            PlaceField(_lblSymbolsLabel, _txtClaudeSymbols, 134, 320, 186);
            PlaceField(_lblPollSecLabel, _nudClaudePollSec, 172, 130, 186);

            _lblClaudeNote1.Location = new Point(18, 224);
            _lblClaudeNote1.Size = new Size(500, 22);
            _lblClaudeNote2.Location = new Point(18, 252);
            _lblClaudeNote2.Size = new Size(500, 22);

            _btnStartClaude.Location = new Point(18, 314);
            _btnStartClaude.Size = new Size(178, 42);
            _btnStopClaude.Location = new Point(208, 314);
            _btnStopClaude.Size = new Size(178, 42);
        }

        private void ConfigureLogTabLayout()
        {
            var layout = ResetTabLayout(_tabLog, "_logTabLayout", 1, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            _txtLog.Dock = DockStyle.Fill;
            _txtLog.Margin = Padding.Empty;

            layout.Controls.Add(_txtLog, 0, 0);
            layout.Controls.Add(CreateButtonRow("_logButtons", _btnClearLog, _btnSaveLog), 0, 1);
        }

        private static TableLayoutPanel ResetTabLayout(TabPage tab, string name, int columns, int rows)
        {
            var layout = tab.Controls.OfType<TableLayoutPanel>().FirstOrDefault(c => c.Name == name)
                ?? new TableLayoutPanel { Name = name };

            tab.SuspendLayout();
            tab.Controls.Clear();
            tab.AutoScroll = true;
            tab.Padding = new Padding(12, 8, 12, 12);
            tab.Controls.Add(layout);

            ResetTable(layout, columns, rows);
            tab.ResumeLayout(false);
            return layout;
        }

        private static TableLayoutPanel ResetPanelLayout(Panel panel, string name, int columns, int rows)
        {
            var layout = panel.Controls.OfType<TableLayoutPanel>().FirstOrDefault(c => c.Name == name)
                ?? new TableLayoutPanel { Name = name };

            panel.SuspendLayout();
            panel.Controls.Clear();
            panel.Padding = new Padding(14, 12, 14, 12);
            panel.Controls.Add(layout);

            ResetTable(layout, columns, rows);
            panel.ResumeLayout(false);
            return layout;
        }

        private static void ResetTable(TableLayoutPanel layout, int columns, int rows)
        {
            layout.SuspendLayout();
            layout.Controls.Clear();
            layout.ColumnStyles.Clear();
            layout.RowStyles.Clear();
            layout.ColumnCount = columns;
            layout.RowCount = rows;
            layout.Dock = DockStyle.Fill;
            layout.Margin = Padding.Empty;
            layout.Padding = Padding.Empty;
            layout.ResumeLayout(false);
        }

        private static FlowLayoutPanel CreateButtonRow(string name, params Button[] buttons)
        {
            var row = new FlowLayoutPanel
            {
                Name = name,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                WrapContents = true
            };

            foreach (var button in buttons)
            {
                button.Anchor = AnchorStyles.None;
                button.Margin = new Padding(0, 6, 10, 0);
                row.Controls.Add(button);
            }

            return row;
        }

        private static void PlaceField(Label label, Control editor, int y, int editorWidth = 210, int editorX = 130)
        {
            label.AutoSize = false;
            label.Location = new Point(18, y + 3);
            label.Size = new Size(editorX - 30, 22);
            label.TextAlign = ContentAlignment.MiddleLeft;

            editor.Location = new Point(editorX, y);
            editor.Size = new Size(editorWidth, 26);
        }

        private void ApplyVisualSystem()
        {
            ApplyCommonStyle(this);
            StyleHeaders();
            StyleDataGrid(_gridPos);
            StyleDataGrid(_gridHistory);
            StyleTextSurface(_txtJson);
            StyleTextSurface(_rtbBotHelp);
            StyleTextSurface(_txtClaudePrompt);
            StyleTextSurface(_txtLog);
            StylePrimaryButton(_btnBuy, C_GREEN);
            StylePrimaryButton(_btnSell, C_RED);
            StylePrimaryButton(_btnStartBot, C_GREEN);
            StylePrimaryButton(_btnStopBot, C_RED);
            StylePrimaryButton(_btnStartClaude, C_GREEN);
            StylePrimaryButton(_btnStopClaude, C_RED);
            _pnlRR.BackColor = Color.FromArgb(24, 25, 38);
        }

        private void ApplyCommonStyle(Control root)
        {
            foreach (Control control in root.Controls)
            {
                control.Font = F_BASE;

                if (control is Panel panel)
                {
                    panel.BackColor = panel == _pnlHeader || panel == _pnlConnBar || panel == _pnlAccountBar
                        ? C_SURFACE
                        : C_CARD;
                }
                else if (control is TabPage tab)
                {
                    tab.BackColor = C_BG;
                    tab.ForeColor = C_TEXT;
                }
                else if (control is Label label)
                {
                    label.Font = F_LABEL;
                    label.ForeColor = C_MUTED;
                    label.AutoEllipsis = true;
                }
                else if (control is TextBox textBox)
                {
                    textBox.Font = F_MONO;
                    textBox.BackColor = C_SURFACE;
                    textBox.ForeColor = C_TEXT;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (control is ComboBox comboBox)
                {
                    comboBox.Font = F_BASE;
                    comboBox.BackColor = C_SURFACE;
                    comboBox.ForeColor = C_TEXT;
                    comboBox.FlatStyle = FlatStyle.Flat;
                    comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
                }
                else if (control is NumericUpDown numeric)
                {
                    numeric.Font = F_BASE;
                    numeric.BackColor = C_SURFACE;
                    numeric.ForeColor = C_TEXT;
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (control is CheckBox checkBox)
                {
                    checkBox.Font = F_BASE;
                    checkBox.ForeColor = C_TEXT;
                    checkBox.BackColor = Color.Transparent;
                    checkBox.AutoSize = false;
                    checkBox.TextAlign = ContentAlignment.MiddleLeft;
                }
                else if (control is Button button)
                {
                    button.Font = F_BUTTON;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 0;
                    button.ForeColor = Color.FromArgb(10, 10, 20);
                    button.MinimumSize = new Size(90, 30);
                    button.TextAlign = ContentAlignment.MiddleCenter;
                    button.UseVisualStyleBackColor = false;
                }

                if (control.HasChildren)
                    ApplyCommonStyle(control);
            }
        }

        private void StyleHeaders()
        {
            foreach (var label in new[]
            {
                _lblTradeHeader,
                _lblJsonHeader,
                _lblBotCardHeader,
                _lblBotInfoHeader,
                _lblClaudeCardHeader,
                _lblPromptHeader
            })
            {
                label.Font = F_HEADER;
                label.ForeColor = Color.FromArgb(200, 210, 240);
                label.AutoSize = false;
                label.TextAlign = ContentAlignment.MiddleLeft;
            }

            _lblTitle.Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold);
            _lblTitle.ForeColor = C_TEXT;
            _lblConnStatus.ForeColor = C_MUTED;
            _lblTime.ForeColor = C_MUTED;
            _lblBotBadge.ForeColor = C_RED;
            _lblClaudeBadge.ForeColor = C_RED;
            _lblSLLabel.ForeColor = C_RED;
            _lblTPLabel.ForeColor = C_GREEN;
            _lblTP2Label.ForeColor = C_ACCENT;
            _lblRR.ForeColor = C_ACCENT;
            _lblDollarRisk.ForeColor = C_RED;
            _lblDollarProfit.ForeColor = C_GREEN;
            _lblModelValue.ForeColor = C_TEXT;
            _lblClaudeNote1.ForeColor = C_MUTED;
            _lblClaudeNote2.ForeColor = C_MUTED;
        }

        private static void StyleTextSurface(RichTextBox textBox)
        {
            textBox.Font = F_MONO;
            textBox.BackColor = C_SURFACE;
            textBox.ForeColor = C_TEXT;
            textBox.BorderStyle = BorderStyle.FixedSingle;
        }

        private static void StyleDataGrid(DataGridView grid)
        {
            grid.BackgroundColor = C_CARD;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersHeight = 34;
            grid.RowTemplate.Height = 30;
            grid.EnableHeadersVisualStyles = false;
            grid.GridColor = C_BORDER;
            grid.DefaultCellStyle.Font = new Font("Consolas", 9F);
            grid.DefaultCellStyle.BackColor = Color.FromArgb(24, 25, 38);
            grid.DefaultCellStyle.ForeColor = C_TEXT;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 80, 120);
            grid.DefaultCellStyle.SelectionForeColor = C_TEXT;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(18, 18, 28);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = C_MUTED;
        }

        private static void StylePrimaryButton(Button button, Color color)
        {
            button.BackColor = color;
            button.Font = F_ACTION;
            button.Height = Math.Max(button.Height, 40);
        }

        private void DrawTabItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tc) return;
            var tab = tc.TabPages[e.Index];
            bool sel = e.Index == tc.SelectedIndex;
            using var bg = new SolidBrush(sel ? C_CARD : C_SURFACE);
            e.Graphics.FillRectangle(bg, e.Bounds);

            var textBounds = Rectangle.Inflate(e.Bounds, -8, -2);
            TextRenderer.DrawText(
                e.Graphics,
                tab.Text,
                tc.Font,
                textBounds,
                sel ? C_ACCENT : C_MUTED,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
