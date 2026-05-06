using MT5TradingBot.Models;
using MT5TradingBot.Modules.TradeRules;

namespace MT5TradingBot.UI
{
    public sealed class LiveTradeRulesMonitorControlForm : Form
    {
        private readonly TradeRulesContext _context;
        private readonly TradeRulesRuntimeSnapshotService _snapshotService;
        private readonly TradeRulesRuntimeControlService _controlService;
        private readonly TradeRulesExportService _exportService = new();
        private readonly Action<string>? _auditLog;
        private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 1000 };
        private readonly Label _lblHeader = new();
        private readonly Label _lblSummary = new();
        private readonly TextBox _txtSearch = new();
        private readonly Button _btnEnableEditing = new();
        private readonly Button _btnApplyRuntime = new();
        private readonly Button _btnSavePair = new();
        private readonly Button _btnSaveStrategy = new();
        private readonly Button _btnReset = new();
        private readonly Button _btnExport = new();
        private readonly Button _btnClose = new();
        private readonly TabControl _tabs = new();
        private readonly Dictionary<string, DataGridView> _ruleGrids = new(StringComparer.OrdinalIgnoreCase);
        private readonly TextBox _txtAdvancedDetails = new();
        private readonly ComboBox _cmbGroupFilter = new();
        private readonly FlowLayoutPanel _auditFilters = new();
        private readonly Button _btnEnableGroup = new();
        private readonly Button _btnDisableGroup = new();
        private readonly ListBox _lstHistory = new();
        private readonly Button _btnClearHistory = new();
        private readonly List<string> _history = [];
        private readonly Dictionary<string, string> _lastRuleStates = new(StringComparer.OrdinalIgnoreCase);
        private string _statusFilter = "All";
        private string _groupFilter = "All";
        private TradeRulesRuntimeSnapshotResult? _latest;
        private bool _runtimeEditingEnabled;

        public LiveTradeRulesMonitorControlForm(
            TradeRulesContext context,
            TradeRulesRuntimeSnapshotService snapshotService,
            TradeRulesRuntimeControlService? controlService = null,
            Action<string>? auditLog = null)
        {
            _context = context;
            _snapshotService = snapshotService;
            _controlService = controlService ?? new TradeRulesRuntimeControlService(new AppSettings());
            _auditLog = auditLog;
            _runtimeEditingEnabled = !context.IsRunningTrade;

            Text = "Live Trade Rules Monitor & Control";
            MinimumSize = new Size(1060, 720);
            Size = new Size(1220, 820);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(12, 14, 22);
            ForeColor = Color.FromArgb(230, 235, 245);

            BuildLayout();
            BuildTabs();
            UpdateEditingState();

            _refreshTimer.Tick += async (_, _) => await RefreshSnapshotAsync().ConfigureAwait(true);
            Shown += async (_, _) =>
            {
                await RefreshSnapshotAsync().ConfigureAwait(true);
                _refreshTimer.Start();
            };
            FormClosed += (_, _) =>
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
            };
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(12),
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            _lblHeader.Dock = DockStyle.Fill;
            _lblHeader.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            _lblHeader.ForeColor = Color.FromArgb(220, 230, 245);
            _lblHeader.Padding = new Padding(8);

            _lblSummary.Dock = DockStyle.Fill;
            _lblSummary.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            _lblSummary.ForeColor = Color.FromArgb(144, 238, 170);
            _lblSummary.Padding = new Padding(8);
            _lblSummary.BackColor = Color.FromArgb(18, 24, 34);

            var searchRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            searchRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            searchRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            var searchLine = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            searchLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            searchLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            searchLine.Controls.Add(new Label
            {
                Text = "Search",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ForeColor
            }, 0, 0);
            _txtSearch.Dock = DockStyle.Fill;
            _txtSearch.BackColor = Color.FromArgb(20, 24, 34);
            _txtSearch.ForeColor = ForeColor;
            _txtSearch.BorderStyle = BorderStyle.FixedSingle;
            _txtSearch.TextChanged += (_, _) => BindRules();
            searchLine.Controls.Add(_txtSearch, 1, 0);
            _btnExport.Text = "Export";
            StyleButton(_btnExport, Color.FromArgb(99, 179, 237));
            _btnExport.Click += (_, _) => ExportSnapshot();
            searchLine.Controls.Add(_btnExport, 2, 0);

            var filterLine = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5 };
            filterLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            filterLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            filterLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            filterLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            filterLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _cmbGroupFilter.Dock = DockStyle.Fill;
            _cmbGroupFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbGroupFilter.BackColor = Color.FromArgb(20, 24, 34);
            _cmbGroupFilter.ForeColor = ForeColor;
            _cmbGroupFilter.SelectedIndexChanged += (_, _) =>
            {
                _groupFilter = _cmbGroupFilter.SelectedItem?.ToString() ?? "All";
                BindRules();
            };
            filterLine.Controls.Add(new Label
            {
                Text = "Group",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ForeColor
            }, 0, 0);
            filterLine.Controls.Add(_cmbGroupFilter, 1, 0);
            _btnEnableGroup.Text = "Enable Group";
            _btnDisableGroup.Text = "Disable Group";
            _btnEnableGroup.Click += (_, _) => ToggleVisibleGroup(true);
            _btnDisableGroup.Click += (_, _) => ToggleVisibleGroup(false);
            StyleButton(_btnEnableGroup, Color.FromArgb(46, 160, 94));
            StyleButton(_btnDisableGroup, Color.FromArgb(165, 80, 60));
            filterLine.Controls.Add(_btnEnableGroup, 2, 0);
            filterLine.Controls.Add(_btnDisableGroup, 3, 0);

            _auditFilters.Dock = DockStyle.Fill;
            _auditFilters.FlowDirection = FlowDirection.LeftToRight;
            _auditFilters.WrapContents = false;
            foreach (string status in new[] { "All", "Blocked", "Warning", "Disabled", "Passed" })
            {
                var button = new Button { Text = status, Width = 82, Height = 28 };
                StyleButton(button, Color.FromArgb(55, 65, 85));
                button.Click += (_, _) =>
                {
                    _statusFilter = status;
                    BindRules();
                };
                _auditFilters.Controls.Add(button);
            }
            filterLine.Controls.Add(_auditFilters, 4, 0);

            searchRow.Controls.Add(searchLine, 0, 0);
            searchRow.Controls.Add(filterLine, 0, 1);

            var body = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 430,
                Panel1MinSize = 260,
                Panel2MinSize = 90,
                BackColor = BackColor
            };

            _tabs.Dock = DockStyle.Fill;
            body.Panel1.Controls.Add(_tabs);

            var lower = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 620,
                Panel1MinSize = 280,
                Panel2MinSize = 220,
                BackColor = BackColor
            };

            _txtAdvancedDetails.Dock = DockStyle.Fill;
            _txtAdvancedDetails.Multiline = true;
            _txtAdvancedDetails.ReadOnly = true;
            _txtAdvancedDetails.ScrollBars = ScrollBars.Vertical;
            _txtAdvancedDetails.BackColor = Color.FromArgb(18, 24, 34);
            _txtAdvancedDetails.ForeColor = Color.FromArgb(210, 220, 235);
            _txtAdvancedDetails.BorderStyle = BorderStyle.FixedSingle;
            _txtAdvancedDetails.Font = new Font("Consolas", 9F);
            _txtAdvancedDetails.Text = "Advanced Details";
            lower.Panel1.Controls.Add(_txtAdvancedDetails);

            var historyPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            historyPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            historyPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _btnClearHistory.Text = "Clear History";
            _btnClearHistory.Dock = DockStyle.Right;
            _btnClearHistory.Width = 120;
            StyleButton(_btnClearHistory, Color.FromArgb(55, 65, 85));
            _btnClearHistory.Click += (_, _) =>
            {
                _history.Clear();
                _lstHistory.Items.Clear();
            };
            historyPanel.Controls.Add(_btnClearHistory, 0, 0);
            _lstHistory.Dock = DockStyle.Fill;
            _lstHistory.BackColor = Color.FromArgb(18, 24, 34);
            _lstHistory.ForeColor = Color.FromArgb(220, 228, 240);
            _lstHistory.BorderStyle = BorderStyle.FixedSingle;
            _lstHistory.Font = new Font("Consolas", 8.5F);
            historyPanel.Controls.Add(_lstHistory, 0, 1);
            lower.Panel2.Controls.Add(historyPanel);
            body.Panel2.Controls.Add(lower);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            _btnClose.Text = "Close";
            _btnClose.Click += (_, _) => Close();
            _btnReset.Text = "Reset";
            _btnSaveStrategy.Text = "Save Strategy Defaults";
            _btnSavePair.Text = "Save Pair Defaults";
            _btnApplyRuntime.Text = "Apply Runtime";
            _btnApplyRuntime.Click += async (_, _) => await ApplyRuntimeAsync().ConfigureAwait(true);
            _btnSavePair.Click += async (_, _) => await SavePairDefaultsAsync().ConfigureAwait(true);
            _btnSaveStrategy.Click += async (_, _) => await SaveStrategyDefaultsAsync().ConfigureAwait(true);
            _btnReset.Click += (_, _) => ResetVisibleRules();
            _btnEnableEditing.Text = "Enable Runtime Editing";
            _btnEnableEditing.Click += (_, _) =>
            {
                _runtimeEditingEnabled = true;
                UpdateEditingState();
            };

            foreach (var button in new[] { _btnClose, _btnReset, _btnSaveStrategy, _btnSavePair, _btnApplyRuntime, _btnEnableEditing })
            {
                StyleButton(button, Color.FromArgb(55, 65, 85));
                buttons.Controls.Add(button);
            }

            root.Controls.Add(_lblHeader, 0, 0);
            root.Controls.Add(_lblSummary, 0, 1);
            root.Controls.Add(searchRow, 0, 2);
            root.Controls.Add(body, 0, 3);
            root.Controls.Add(buttons, 0, 4);
            Controls.Add(root);
        }

        private void BuildTabs()
        {
            string[] tabNames = _context.Strategy switch
            {
                TradeRulesStrategy.Scalping =>
                [
                    "Live Overview", "Scalping Rules", "Common Rules", "Pair Rules", "Broker Rules",
                    "Account Protection", "Safety / News / Session", "Decision Audit"
                ],
                TradeRulesStrategy.Normal =>
                [
                    "Live Overview", "Normal Rules", "Common Rules", "Pair Rules", "Broker Rules",
                    "Account Protection", "Safety / News / Session", "Decision Audit"
                ],
                _ =>
                [
                    "Live Overview", "Common Rules", "Pair Rules", "Broker Rules",
                    "Account Protection", "Safety / News / Session", "Decision Audit"
                ]
            };

            foreach (string tabName in tabNames)
            {
                var page = new TabPage(tabName) { BackColor = BackColor, ForeColor = ForeColor };
                var grid = CreateRulesGrid();
                grid.SelectionChanged += (_, _) => ShowAdvancedDetails(grid);
                grid.CellEndEdit += Grid_CellEndEdit;
                grid.CellClick += Grid_CellClick;
                page.Controls.Add(grid);
                _ruleGrids[tabName] = grid;
                _tabs.TabPages.Add(page);
            }
        }

        private static DataGridView CreateRulesGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(12, 14, 22),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                EnableHeadersVisualStyles = false
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 38, 54);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.BackColor = Color.FromArgb(18, 24, 34);
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(230, 235, 245);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(38, 58, 82);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.Columns.Add("Enabled", "Enabled");
            grid.Columns.Add("Rule", "Rule");
            grid.Columns.Add("Source", "Source");
            grid.Columns.Add("Standard", "Standard");
            grid.Columns.Add("Configured", "Configured");
            grid.Columns.Add("Preview", "Preview");
            grid.Columns.Add("Live", "Live");
            grid.Columns.Add("Range", "Min / Max");
            grid.Columns.Add("Feedback", "Feedback");
            grid.Columns.Add("Status", "Status");
            grid.Columns.Add("WouldHave", "Would Have");
            grid.Columns.Add("Reason", "Reason");
            grid.Columns.Add("Reset", "Reset");
            return grid;
        }

        private async Task RefreshSnapshotAsync()
        {
            try
            {
                _latest = await _snapshotService.BuildAsync(_context).ConfigureAwait(true);
                UpdateHeader();
                UpdateGroupFilterItems();
                TrackSnapshotHistory(_latest.Rules);
                BindRules();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                _lblSummary.Text = $"Snapshot unavailable: {ex.Message}";
                _lblSummary.ForeColor = Color.FromArgb(252, 95, 95);
            }
        }

        private void ExportSnapshot()
        {
            if (_latest == null)
            {
                MessageBox.Show(this, "No snapshot is available yet.", "Rules Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var menu = new ContextMenuStrip();
            menu.Items.Add("Export JSON", null, (_, _) => SaveExport(json: true));
            menu.Items.Add("Export readable TXT", null, (_, _) => SaveExport(json: false));
            menu.Show(_btnExport, new Point(0, _btnExport.Height));
        }

        private void SaveExport(bool json)
        {
            if (_latest == null) return;

            using var dialog = new SaveFileDialog
            {
                Filter = json ? "JSON|*.json" : "Text|*.txt",
                FileName = $"rules-monitor-{_context.Pair.DefaultIfBlank("context")}-{DateTime.Now:yyyyMMdd-HHmmss}.{(json ? "json" : "txt")}"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            if (json)
                _exportService.WriteJson(dialog.FileName, _latest, _history);
            else
                _exportService.WriteText(dialog.FileName, _latest, _history);

            AddHistory($"User exported {(json ? "JSON" : "TXT")} {Path.GetFileName(dialog.FileName)}");
            _auditLog?.Invoke($"[RULES_MONITOR] Export | Type={(json ? "JSON" : "TXT")} | Pair={_context.Pair} | Strategy={_context.Strategy}");
            MessageBox.Show(this, "Export complete.", "Rules Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateHeader()
        {
            if (_latest == null) return;
            AccountInfo? account = _latest.Account;
            LivePosition? position = _latest.Position;
            SymbolInfo? symbol = _latest.Symbol;
            string accountText = account == null
                ? "Account: unavailable"
                : $"Account {account.AccountNumber} | Server {account.Server} | Balance {account.Balance:F2} | Equity {account.Equity:F2} | Free Margin {account.FreeMargin:F2} | Margin {account.MarginLevel:F1}% | Floating P/L {account.Profit:F2}";
            string tradeText =
                $"Pair {_context.Pair.DefaultIfBlank("-")} | Strategy {_context.Strategy} | Ticket {_context.Ticket?.ToString() ?? "-"} | Type {_context.TradeType?.ToString() ?? "-"} | Source {_context.OpenedFrom.DefaultIfBlank("-")}";
            string positionText = position == null
                ? $"Price {(symbol == null ? "-" : $"{symbol.Bid:F5}/{symbol.Ask:F5}")}"
                : $"Entry {position.OpenPrice:F5} | Current {position.CurrentPrice:F5} | SL {position.StopLoss:F5} | TP {position.TakeProfit:F5} | Lot {position.Lots:F2} | P/L {position.Profit:F2} | Opened {position.OpenTime:g}";

            _lblHeader.Text = $"{accountText}{Environment.NewLine}{tradeText}{Environment.NewLine}{positionText}";

            var summary = _latest.Summary;
            _lblSummary.Text =
                $"Current Decision: {summary.CurrentDecision} | Main Blocking Rule: {summary.MainBlockingRule.DefaultIfBlank("-")} | Risk Level: {summary.RiskLevel} | " +
                $"Passed: {summary.Passed} | Warning: {summary.Warning} | Blocked: {summary.Blocked} | Disabled: {summary.Disabled} | Disabled But Would Block: {summary.DisabledButWouldBlock}";
            _lblSummary.ForeColor = summary.RiskLevel == "High"
                ? Color.FromArgb(252, 95, 95)
                : summary.RiskLevel == "Medium"
                    ? Color.FromArgb(250, 199, 117)
                    : Color.FromArgb(144, 238, 170);
        }

        private void BindRules()
        {
            if (_latest == null) return;
            string search = _txtSearch.Text.Trim();

            foreach (var (tabName, grid) in _ruleGrids)
            {
                grid.Rows.Clear();
                foreach (var rule in FilterRulesForTab(tabName, _latest.Rules, search))
                {
                    int row = grid.Rows.Add(
                        rule.IsEnabled ? "Yes" : "No",
                        $"{rule.RuleCode} - {rule.RuleName}",
                        rule.SourceName,
                        FormatValue(rule.StandardValue),
                        FormatValue(rule.ConfiguredValue),
                        FormatValue(rule.PreviewValue ?? rule.ConfiguredValue),
                        FormatValue(rule.LiveValue),
                        FormatRange(rule),
                        FeedbackText(rule),
                        rule.Result,
                        rule.WouldHaveResult ?? "-",
                        rule.Reason,
                        "Reset");
                    grid.Rows[row].Tag = rule;
                    grid.Rows[row].DefaultCellStyle.ForeColor = StatusColor(rule);
                    grid.Rows[row].Cells["Feedback"].Style.BackColor = FeedbackColor(rule);
                    grid.Rows[row].Cells["Feedback"].Style.ForeColor = Color.White;
                    grid.Rows[row].Cells["Status"].Style.BackColor = StatusBackColor(rule);
                }
            }
        }

        private void UpdateGroupFilterItems()
        {
            if (_latest == null) return;
            var groups = _latest.Rules
                .Select(r => r.GroupName)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g)
                .Prepend("All")
                .ToList();

            string current = _cmbGroupFilter.SelectedItem?.ToString() ?? _groupFilter;
            _cmbGroupFilter.Items.Clear();
            foreach (string group in groups)
                _cmbGroupFilter.Items.Add(group);
            _cmbGroupFilter.SelectedItem = groups.Contains(current, StringComparer.OrdinalIgnoreCase) ? current : "All";
        }

        private void ShowAdvancedDetails(DataGridView grid)
        {
            if (grid.SelectedRows.Count == 0 || grid.SelectedRows[0].Tag is not TradeRuleRuntimeSnapshot rule)
                return;

            _txtAdvancedDetails.Text =
                $"Rule Code: {rule.RuleCode}{Environment.NewLine}" +
                $"Rule Name: {rule.RuleName}{Environment.NewLine}" +
                $"Category: {rule.Category}{Environment.NewLine}" +
                $"Group: {rule.GroupName}{Environment.NewLine}" +
                $"Function: {rule.FunctionName}{Environment.NewLine}" +
                $"Variable: {rule.VariableName}{Environment.NewLine}" +
                $"Source File: {rule.SourceFile}{Environment.NewLine}" +
                $"Source Name: {rule.SourceName}{Environment.NewLine}" +
                $"Enabled: {rule.IsEnabled}{Environment.NewLine}" +
                $"Critical: {rule.IsCritical}{Environment.NewLine}" +
                $"Standard: {FormatValue(rule.StandardValue)}{Environment.NewLine}" +
                $"Configured: {FormatValue(rule.ConfiguredValue)}{Environment.NewLine}" +
                $"Preview: {FormatValue(rule.PreviewValue)}{Environment.NewLine}" +
                $"Live: {FormatValue(rule.LiveValue)}{Environment.NewLine}" +
                $"Range: {FormatRange(rule)}{Environment.NewLine}" +
                $"Result: {rule.Result}{Environment.NewLine}" +
                $"Would Have Result: {rule.WouldHaveResult ?? "-"}{Environment.NewLine}" +
                $"Actual Effect: {rule.ActualEffect}{Environment.NewLine}" +
                $"Reason: {rule.Reason}{Environment.NewLine}" +
                $"Last Checked UTC: {rule.LastCheckedAtUtc:O}";
        }

        private IEnumerable<TradeRuleRuntimeSnapshot> FilterRulesForTab(
            string tabName,
            IReadOnlyList<TradeRuleRuntimeSnapshot> rules,
            string search)
        {
            IEnumerable<TradeRuleRuntimeSnapshot> query = tabName switch
            {
                "Live Overview" => rules.Where(r => r.RuleCode is
                    "EXEC-FINAL-GATE" or "SCALP-SPREAD-LIMIT" or "SCALP-BUY-SCORE" or "SCALP-SELL-SCORE" or
                    "SAFETY-ADX-RANGING" or "SCALP-COOLDOWN" or "SAFETY-NEWS-BLACKOUT" or "ACCOUNT-MARGIN" or
                    "ACCOUNT-DAILY-LOSS" or "ACCOUNT-WEEKLY-LOSS" or "ACCOUNT-MAX-CONCURRENT"),
                "Decision Audit" => rules.Where(r => r.Category == "Decision Audit"),
                _ => rules.Where(r => r.Category == tabName)
            };

            if (tabName == "Decision Audit")
            {
                query = _statusFilter switch
                {
                    "Blocked" => query.Where(r => r.Result == TradeRuleResults.Block),
                    "Warning" => query.Where(r => r.Result == TradeRuleResults.Warning),
                    "Disabled" => query.Where(r => r.Result == TradeRuleResults.Disabled),
                    "Passed" => query.Where(r => r.Result == TradeRuleResults.Pass),
                    _ => query
                };
            }

            if (!string.IsNullOrWhiteSpace(_groupFilter) && _groupFilter != "All")
                query = query.Where(r => string.Equals(r.GroupName, _groupFilter, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(search))
                return query;

            return query.Where(r =>
                Contains(r.RuleCode, search) ||
                Contains(r.RuleName, search) ||
                Contains(r.FunctionName, search) ||
                Contains(r.VariableName, search) ||
                Contains(r.Result, search) ||
                Contains(r.SourceName, search) ||
                Contains(r.Category, search));
        }

        private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (sender is not DataGridView grid || e.RowIndex < 0 || grid.Rows[e.RowIndex].Tag is not TradeRuleRuntimeSnapshot rule)
                return;

            string columnName = grid.Columns[e.ColumnIndex].Name;
            if (columnName == "Preview")
            {
                rule.PreviewValue = ParsePreviewValue(rule, grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString());
                return;
            }

            if (columnName != "Enabled")
                return;

            string text = grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "";
            bool enabled = text.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                           text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                           text.Equals("enabled", StringComparison.OrdinalIgnoreCase);
            if (!enabled && rule.IsCritical && MessageBox.Show(
                    this,
                    $"You are disabling critical rule {rule.RuleCode} {rule.RuleName}. Continue?",
                    "Critical Rule Disable",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                    grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = "Yes";
                    return;
            }

            rule.IsEnabled = enabled;
            TradeRuleStatusEvaluator.ApplyEnabledState(rule, rule.WouldHaveResult ?? rule.Result);
            AddHistory($"User {(enabled ? "enabled" : "disabled")} {rule.RuleCode} {rule.RuleName}");
            _auditLog?.Invoke($"[RULES_MONITOR] BypassChanged | Rule={rule.RuleCode} {rule.RuleName} | Enabled={enabled} | Pair={_context.Pair} | Strategy={_context.Strategy}");
            if (!enabled && rule.IsCritical)
                _auditLog?.Invoke($"[RULES_MONITOR] CriticalRuleDisabled | Rule={rule.RuleCode} {rule.RuleName} | Pair={_context.Pair} | Strategy={_context.Strategy}");
            BindRules();
        }

        private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (sender is not DataGridView grid || e.RowIndex < 0 || grid.Rows[e.RowIndex].Tag is not TradeRuleRuntimeSnapshot rule)
                return;

            if (grid.Columns[e.ColumnIndex].Name != "Reset")
                return;

            _controlService.ResetRule(rule);
            AddHistory($"User reset {rule.RuleCode} {rule.RuleName}");
            _auditLog?.Invoke($"[RULES_MONITOR] ValueChanged | Rule={rule.RuleCode} {rule.RuleName} | Old={FormatValue(rule.ConfiguredValue)} | New={FormatValue(rule.PreviewValue)} | Pair={_context.Pair} | Strategy={_context.Strategy}");
            BindRules();
        }

        private void ToggleVisibleGroup(bool enabled)
        {
            if (_latest == null || _cmbGroupFilter.SelectedItem?.ToString() is not { } group || group == "All")
            {
                MessageBox.Show(this, "Select a specific group first.", "Rules Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var groupRules = _latest.Rules
                .Where(r => string.Equals(r.GroupName, group, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!enabled && MessageBox.Show(
                    this,
                    $"You are disabling {groupRules.Count} {group} rules. This may increase trading risk. Continue?",
                    "Disable Rule Group",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            foreach (var rule in groupRules)
                rule.IsEnabled = enabled;

            AddHistory($"User {(enabled ? "enabled" : "disabled")} group {group}");
            BindRules();
        }

        private async Task ApplyRuntimeAsync()
        {
            if (_latest == null) return;
            CaptureGridPreviewValues();
            await _controlService.ApplyRuntimeAsync(_context, _latest.Rules).ConfigureAwait(true);
            AddHistory("User applied runtime edits");
            _auditLog?.Invoke($"[RULES_MONITOR] ApplyRuntime | Pair={_context.Pair} | Strategy={_context.Strategy} | Ticket={_context.Ticket?.ToString() ?? "-"}");
            MessageBox.Show(this, "Runtime edits applied for the current context. Existing MT5 position SL/TP was not modified.", "Rules Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task SavePairDefaultsAsync()
        {
            if (_latest == null) return;
            CaptureGridPreviewValues();
            await _controlService.SavePairDefaultsAsync(_context, _latest.Rules).ConfigureAwait(true);
            AddHistory("User saved pair defaults");
            _auditLog?.Invoke($"[RULES_MONITOR] SavePairDefaults | Pair={_context.Pair} | Strategy={_context.Strategy}");
            MessageBox.Show(this, "Pair defaults saved.", "Rules Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task SaveStrategyDefaultsAsync()
        {
            if (_latest == null) return;
            CaptureGridPreviewValues();
            await _controlService.SaveStrategyDefaultsAsync(_context, _latest.Rules).ConfigureAwait(true);
            AddHistory("User saved strategy defaults");
            _auditLog?.Invoke($"[RULES_MONITOR] SaveStrategyDefaults | Pair={_context.Pair} | Strategy={_context.Strategy}");
            MessageBox.Show(this, "Strategy defaults saved.", "Rules Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ResetVisibleRules()
        {
            if (_latest == null || _tabs.SelectedTab == null) return;
            var visible = FilterRulesForTab(_tabs.SelectedTab.Text, _latest.Rules, _txtSearch.Text.Trim()).ToList();
            _controlService.ResetRules(visible);
            AddHistory($"User reset {visible.Count} visible rules");
            _auditLog?.Invoke($"[RULES_MONITOR] Reset | Pair={_context.Pair} | Strategy={_context.Strategy} | Count={visible.Count}");
            BindRules();
        }

        private void CaptureGridPreviewValues()
        {
            foreach (var grid in _ruleGrids.Values)
            {
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.Tag is not TradeRuleRuntimeSnapshot rule) continue;
                    rule.IsEnabled = IsYes(row.Cells["Enabled"].Value?.ToString());
                    object? oldValue = rule.PreviewValue ?? rule.ConfiguredValue;
                    rule.PreviewValue = ParsePreviewValue(rule, row.Cells["Preview"].Value?.ToString());
                    if (!Equals(oldValue, rule.PreviewValue))
                        AddHistory($"User changed {rule.RuleCode} {rule.RuleName}: {FormatValue(oldValue)} -> {FormatValue(rule.PreviewValue)}");
                }
            }
        }

        private void TrackSnapshotHistory(IReadOnlyList<TradeRuleRuntimeSnapshot> rules)
        {
            foreach (var rule in rules)
            {
                string state = $"{rule.Result}|{rule.WouldHaveResult}|{FormatValue(rule.LiveValue)}|{rule.IsEnabled}";
                if (_lastRuleStates.TryGetValue(rule.RuleCode, out string? previous))
                {
                    if (!string.Equals(previous, state, StringComparison.Ordinal))
                        AddHistory($"{rule.RuleCode} {rule.RuleName} {rule.Result} Live={FormatValue(rule.LiveValue)}");
                }
                _lastRuleStates[rule.RuleCode] = state;
            }
        }

        private void AddHistory(string message)
        {
            string row = $"{DateTime.Now:HH:mm:ss} {message}";
            _history.Add(row);
            while (_history.Count > 200)
                _history.RemoveAt(0);

            _lstHistory.Items.Clear();
            foreach (string item in _history)
                _lstHistory.Items.Add(item);
            if (_lstHistory.Items.Count > 0)
                _lstHistory.TopIndex = _lstHistory.Items.Count - 1;
        }

        private static object? ParsePreviewValue(TradeRuleRuntimeSnapshot rule, string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || text == "-")
                return rule.PreviewValue ?? rule.ConfiguredValue;

            if (rule.ConfiguredValue is bool && bool.TryParse(text, out bool b))
                return b;

            if (rule.ConfiguredValue is int && int.TryParse(text, out int i))
                return i;

            if (double.TryParse(text, out double d))
                return d;

            return text;
        }

        private static bool IsYes(string? text) =>
            text?.Equals("yes", StringComparison.OrdinalIgnoreCase) == true ||
            text?.Equals("true", StringComparison.OrdinalIgnoreCase) == true ||
            text?.Equals("enabled", StringComparison.OrdinalIgnoreCase) == true;

        private void UpdateEditingState()
        {
            bool allow = _runtimeEditingEnabled;
            _btnEnableEditing.Visible = _context.IsRunningTrade && !allow;
            _btnApplyRuntime.Enabled = allow;
            _btnSavePair.Enabled = allow;
            _btnSaveStrategy.Enabled = allow;
            _btnReset.Enabled = allow;
            foreach (var grid in _ruleGrids.Values)
                grid.ReadOnly = !allow;
        }

        private static void StyleButton(Button button, Color color)
        {
            button.Height = 34;
            button.Width = Math.Max(120, button.Width);
            button.Margin = new Padding(6, 6, 0, 6);
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Cursor = Cursors.Hand;
        }

        private static Color StatusColor(TradeRuleRuntimeSnapshot rule) =>
            rule.Result switch
            {
                TradeRuleResults.Pass => Color.FromArgb(144, 238, 170),
                TradeRuleResults.Warning => Color.FromArgb(250, 199, 117),
                TradeRuleResults.Block => Color.FromArgb(252, 95, 95),
                TradeRuleResults.Disabled => Color.FromArgb(170, 180, 200),
                _ => Color.FromArgb(180, 190, 210)
            };

        private static Color StatusBackColor(TradeRuleRuntimeSnapshot rule) =>
            rule.Result switch
            {
                TradeRuleResults.Pass => Color.FromArgb(20, 72, 44),
                TradeRuleResults.Warning => Color.FromArgb(92, 70, 26),
                TradeRuleResults.Block => Color.FromArgb(88, 30, 34),
                TradeRuleResults.Disabled => Color.FromArgb(52, 58, 70),
                _ => Color.FromArgb(38, 44, 58)
            };

        private static Color FeedbackColor(TradeRuleRuntimeSnapshot rule) =>
            rule.Result switch
            {
                TradeRuleResults.Pass => Color.FromArgb(46, 160, 94),
                TradeRuleResults.Warning => Color.FromArgb(212, 150, 40),
                TradeRuleResults.Block => Color.FromArgb(208, 64, 72),
                TradeRuleResults.Disabled when rule.WouldHaveResult == TradeRuleResults.Block => Color.FromArgb(208, 104, 48),
                TradeRuleResults.Disabled => Color.FromArgb(95, 105, 120),
                _ => Color.FromArgb(80, 90, 110)
            };

        private static string FeedbackText(TradeRuleRuntimeSnapshot rule) =>
            rule.Result switch
            {
                TradeRuleResults.Pass => "Green",
                TradeRuleResults.Warning => "Yellow",
                TradeRuleResults.Block => "Red",
                TradeRuleResults.Disabled when rule.WouldHaveResult == TradeRuleResults.Block => "Orange",
                TradeRuleResults.Disabled => "Gray",
                _ => "Neutral"
            };

        private static string FormatRange(TradeRuleRuntimeSnapshot rule)
        {
            string min = rule.MinValue.HasValue ? rule.MinValue.Value.ToString("0.#####") : "-";
            string max = rule.MaxValue.HasValue ? rule.MaxValue.Value.ToString("0.#####") : "-";
            return $"{min} / {max} {rule.Unit}".TrimEnd();
        }

        private static string FormatValue(object? value) =>
            value switch
            {
                null => "-",
                double d => d.ToString("0.#####"),
                float f => f.ToString("0.#####"),
                decimal d => d.ToString("0.#####"),
                IEnumerable<string> strings => string.Join(", ", strings),
                _ => value.ToString() ?? "-"
            };

        private static bool Contains(string? value, string search) =>
            value?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static class TradeRulesMonitorStringExtensions
    {
        public static string DefaultIfBlank(this string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
