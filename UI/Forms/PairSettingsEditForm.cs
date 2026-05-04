using MT5TradingBot.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace MT5TradingBot.UI
{
    public sealed class PairSettingsEditForm : Form
    {
        private static readonly string[] SessionOptions =
        [
            "Asian",
            "Asian_Low_Liquidity",
            "London",
            "NewYork",
            "London_NewYork_Overlap",
            "Rollover",
            "High_Impact_News"
        ];

        private static readonly Dictionary<string, AppHelpContent> PairSettingHelp = new(StringComparer.OrdinalIgnoreCase)
        {
            ["pair"] = new(
                "The broker symbol this rule belongs to, for example XAUUSD, XAUUSDm, EURUSD, or GBPJPY.",
                "The bot uses this name to load symbol-specific spread, stop-loss, take-profit, session, and slippage rules before it allows a trade.",
                "Use the exact symbol shown in MT5 Market Watch. Broker suffixes matter, so XAUUSD and XAUUSDm can be different symbols.",
                "For gold, start with the exact broker symbol and keep one clean setting per tradable symbol.",
                "If this does not match MT5, the bot may fall back to generic settings or fail to find broker rules."),

            ["pip_size"] = new(
                "The price movement counted as one pip for this symbol.",
                "All spread, SL, TP, ATR, slippage, and dollar-risk calculations depend on this value.",
                "Usually comes from broker digits/point size. For many XAUUSD symbols in this bot, 1 pip is 0.01 price movement.",
                "For XAUUSD, confirm by comparing MT5 price movement and bot logs. If price moves from 4565.00 to 4565.01 and the bot says 1 pip, pip size is 0.01.",
                "Wrong pip size makes every risk number wrong. It can make a trade look small when it is actually large."),

            ["max_spread_pips"] = new(
                "The largest spread the bot may accept for this pair.",
                "Higher spread means the trade starts with more cost. For scalping, spread can destroy the edge before price moves.",
                "Comes from pair settings, but live spread comes from MT5 and is checked before execution.",
                "For XAUUSD scalping, keep this tight and below about 20 percent of TP distance. A 150 pip TP should usually avoid spreads above 30 pips.",
                "If this is too high, the bot may trade during expensive or unstable market conditions."),

            ["good_spread_pips"] = new(
                "The spread level considered clean or attractive for this pair.",
                "The bot can use this as a reference when suggesting scalping values and judging market quality.",
                "Set from your broker's normal active-session spread, based on demo/live observation.",
                "Use a value that is common during good London/New York liquidity, not the worst spread of the day.",
                "If this is too loose, the bot may treat poor conditions as normal."),

            ["acceptable_spread_pips"] = new(
                "The spread level still acceptable, but not ideal.",
                "It helps separate normal cost from warning conditions. Near this level, trades need stronger confirmation.",
                "Set from live broker behavior after watching the pair across active sessions.",
                "For XAUUSD, this should usually be below max spread and close to what you actually see in tradable periods.",
                "If acceptable spread is close to max spread, the warning zone becomes less useful."),

            ["min_sl_pips"] = new(
                "The smallest stop-loss distance allowed for this pair.",
                "Stops that are too tight get hit by normal noise, spread, broker stop level, or slippage.",
                "Comes from pair settings, then the bot also checks live ATR and broker stop/freeze levels.",
                "For XAUUSD scalping, this should respect live volatility. If ATR is high, the bot may require a larger SL or wait.",
                "Too small means frequent stop-outs. Too large means the trade may no longer be a scalp."),

            ["max_sl_pips"] = new(
                "The largest stop-loss distance allowed for this pair.",
                "This prevents the bot from turning a scalp into a wide-risk trade during high volatility.",
                "Comes from pair settings and acts as a guardrail over dynamic bot suggestions.",
                "For XAUUSD, if live ATR needs more than this value, the professional action is usually no trade.",
                "Do not increase this just to force trades. Bigger stops increase account risk and can hide bad entries."),

            ["min_tp_pips"] = new(
                "The smallest take-profit distance allowed for this pair.",
                "TP must be large enough to pay for spread, slippage, commission, and still leave reward.",
                "Comes from pair settings and is also checked against minimum R:R.",
                "For scalping, TP should normally be at least 1.5 times the SL and far enough beyond spread cost.",
                "Too small means the bot can win often but still lose money after trading costs."),

            ["scalping_min_rr"] = new(
                "The minimum reward compared with risk. 1.5 means target profit must be at least 1.5 times the stop loss risk.",
                "Blocks trades where the reward is not worth the risk and cost.",
                "Comes from pair settings, with the risk engine enforcing a professional floor.",
                "Use 1.5 or higher for scalping unless you have strong evidence that lower R:R is profitable after costs.",
                "Low R:R can look safe because TP is close, but spread and slippage can make it unprofitable."),

            ["preferred_rr"] = new(
                "The target reward/risk ratio the bot prefers when suggesting TP.",
                "Higher preferred R:R improves payoff, but fewer trades may qualify.",
                "Comes from pair settings and is used by dynamic scalping value suggestions.",
                "Use this as the normal target, while minimum R:R is the hard floor.",
                "If preferred R:R is too high for the pair's normal movement, the bot may wait most of the time."),

            ["atr_multiplier_sl"] = new(
                "How much ATR volatility should influence stop-loss distance.",
                "Higher multiplier means wider stops during volatile conditions; lower means tighter but noisier stops.",
                "ATR comes from MT5 market snapshot. This multiplier comes from pair settings.",
                "For XAUUSD, use ATR carefully because gold volatility expands quickly around news and session opens.",
                "Too low ignores volatility. Too high can create stops too wide for scalping."),

            ["atr_multiplier_tp"] = new(
                "How much ATR volatility should influence take-profit distance.",
                "It helps keep targets realistic for current market movement.",
                "ATR comes from MT5 market snapshot. This multiplier comes from pair settings.",
                "Use it to avoid tiny targets in active markets and unrealistic targets in slow markets.",
                "A target beyond realistic movement may never fill before reversal."),

            ["min_atr_pips_m5"] = new(
                "The minimum M5 volatility needed to consider scalping.",
                "If ATR is too low, price may not move enough to cover spread and slippage.",
                "Comes from MT5 M5 ATR and this pair setting.",
                "Use this to avoid dead markets and slow periods.",
                "Too high can block normal calm trades. Too low can allow trades with no movement."),

            ["max_atr_pips_m5"] = new(
                "The maximum M5 volatility allowed for scalping.",
                "If ATR is too high, stops need to be too wide and fills can become unstable.",
                "Comes from MT5 M5 ATR and this pair setting.",
                "For XAUUSD, high ATR often means news, panic movement, or sharp liquidity changes. Waiting is usually safer.",
                "Raising this can force the bot into high-risk conditions."),

            ["min_atr_pips_m15"] = new(
                "The minimum M15 volatility needed for a broader active-market context.",
                "It helps avoid scalping when the larger short-term market is too quiet.",
                "Comes from MT5 M15 ATR and this pair setting.",
                "Use together with M5 ATR: M5 checks immediate movement, M15 checks broader session energy.",
                "If M15 is too low, trades may stall even if a single M5 candle moves."),

            ["max_atr_pips_m15"] = new(
                "The maximum M15 volatility allowed before scalping is considered unsafe.",
                "Blocks trades when the short-term market is moving too violently for a small scalp plan.",
                "Comes from MT5 M15 ATR and this pair setting.",
                "For gold, this helps avoid news spikes and fast one-way sweeps.",
                "If this is too high, the bot may use huge stops or enter during unstable spreads."),

            ["avoid_trade_if_spread_above_percent_of_tp"] = new(
                "The maximum allowed spread as a percentage of the take-profit distance.",
                "This protects scalping edge. If spread is 20 percent of TP, the trade starts with a large cost.",
                "Spread comes from MT5. TP comes from the trade plan. This percentage comes from pair settings.",
                "Professional scalping usually keeps this at or below 15 to 20 percent.",
                "If this is too high, the bot may take trades where trading cost consumes most expected profit."),

            ["minimum_distance_from_key_level_pips"] = new(
                "How far price should be from nearby support or resistance before entering.",
                "Avoids buying directly into resistance or selling directly into support.",
                "Key levels come from the MT5 market snapshot and bot structure analysis. This distance comes from pair settings.",
                "For XAUUSD, leave enough room for TP before the next obvious barrier.",
                "If this is too small, trades may hit a wall quickly and reverse."),

            ["break_even_after_profit_pips"] = new(
                "Profit distance after which the bot may move stop loss to entry price.",
                "Reduces risk after the trade moves in your favor.",
                "Comes from pair settings and is used by trade management logic when enabled.",
                "Set it far enough that normal pullback does not close the trade too early.",
                "Too early can turn good trades into zero-profit exits. Too late leaves risk open longer."),

            ["trailing_start_pips"] = new(
                "Profit distance where trailing stop can begin.",
                "Locks in profit while allowing the trade to continue.",
                "Comes from pair settings and applies to trade management when enabled.",
                "Start trailing only after the trade has moved enough to absorb normal gold pullbacks.",
                "Starting too early can close trades before TP. Starting too late may give back profit."),

            ["trailing_step_pips"] = new(
                "How far the trailing stop moves each time it updates.",
                "Controls how tightly the bot follows price after trailing starts.",
                "Comes from pair settings and applies to trade management when enabled.",
                "Use a step that matches normal pullback size for the pair.",
                "Too tight causes early exits. Too loose may not protect enough profit."),

            ["max_slippage_pips"] = new(
                "The largest allowed difference between expected price and filled price.",
                "Slippage is a hidden cost. In scalping, repeated small slippage can turn a good strategy into a losing one.",
                "Expected and fill prices come from MT5 execution results. This limit comes from pair settings.",
                "For XAUUSD, keep this strict and review broker performance if slippage is frequent.",
                "If this is too high, the bot may accept poor fills silently."),

            ["recommended_sessions"] = new(
                "The market sessions where this pair is usually best to trade.",
                "Trading during liquid sessions usually improves spread, fill quality, and movement.",
                "Selected by the user based on pair behavior, broker spreads, and strategy testing.",
                "For XAUUSD, London, New York, and their overlap are usually more active.",
                "A good session is not always safe. News and spread spikes still override this."),

            ["avoid_sessions"] = new(
                "The market sessions where this pair should normally be avoided.",
                "Avoiding weak or dangerous periods reduces bad fills, spread spikes, and false signals.",
                "Selected by the user from broker observation and strategy rules.",
                "For XAUUSD, rollover and high-impact news periods are usually dangerous.",
                "Do not remove avoid sessions just to get more trades. More trades is not the same as better trades.")
        };

        private readonly TextBox _txtPair = new();
        private readonly CheckedListBox _lstRecommendedSessions = new();
        private readonly CheckedListBox _lstAvoidSessions = new();
        private readonly Dictionary<string, NumericUpDown> _inputs = new(StringComparer.OrdinalIgnoreCase);
        private readonly ToolTip _quickHelp = new() { InitialDelay = 350, ShowAlways = true };

        public PairTradingSettings Settings { get; private set; }

        public PairSettingsEditForm(PairTradingSettings? settings = null)
        {
            Settings = settings == null ? new PairTradingSettings() : Clone(settings);

            Text = string.IsNullOrWhiteSpace(Settings.Pair) ? "Add Pair Settings" : $"Edit {Settings.Pair}";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new Size(560, 520);
            ClientSize = new Size(660, 720);
            BackColor = Color.FromArgb(22, 22, 32);
            ForeColor = Color.FromArgb(218, 218, 230);
            Font = new Font("Segoe UI", 9F);

            BuildUi();
            LoadSettings(Settings);
        }

        private void BuildUi()
        {
            var content = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Padding = new Padding(14),
                Dock = DockStyle.Top
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;
            AddTextRow(content, row++, "Trading pair", _txtPair, upper: true);
            AddNumberRow(content, row++, "Pip size", "pip_size", 0.00001M, 1000M, 5);
            AddNumberRow(content, row++, "Maximum spread (pips)", "max_spread_pips", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Good spread target (pips)", "good_spread_pips", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Acceptable spread (pips)", "acceptable_spread_pips", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Minimum stop loss (pips)", "min_sl_pips", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Maximum stop loss (pips)", "max_sl_pips", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Minimum take profit (pips)", "min_tp_pips", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Minimum risk/reward", "scalping_min_rr", 0M, 100M, 2);
            AddNumberRow(content, row++, "Preferred risk/reward", "preferred_rr", 0M, 100M, 2);
            AddNumberRow(content, row++, "ATR stop-loss multiplier", "atr_multiplier_sl", 0M, 100M, 2);
            AddNumberRow(content, row++, "ATR take-profit multiplier", "atr_multiplier_tp", 0M, 100M, 2);
            AddNumberRow(content, row++, "Minimum M5 ATR (pips)", "min_atr_pips_m5", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Maximum M5 ATR (pips)", "max_atr_pips_m5", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Minimum M15 ATR (pips)", "min_atr_pips_m15", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Maximum M15 ATR (pips)", "max_atr_pips_m15", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Block if spread exceeds TP (%)", "avoid_trade_if_spread_above_percent_of_tp", 0M, 100M, 2);
            AddNumberRow(content, row++, "Minimum distance from key level (pips)", "minimum_distance_from_key_level_pips", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Move SL to break-even after profit (pips)", "break_even_after_profit_pips", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Start trailing after profit (pips)", "trailing_start_pips", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Trailing step (pips)", "trailing_step_pips", 0M, 10000M, 2);
            AddNumberRow(content, row++, "Maximum slippage (pips)", "max_slippage_pips", 0M, 10000M, 2);
            AddSessionRow(content, row++, "Best trading sessions", _lstRecommendedSessions);
            AddSessionRow(content, row++, "Sessions to avoid", _lstAvoidSessions);

            var scroller = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BackColor
            };
            scroller.Controls.Add(content);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            var btnSave = ActionButton("Save", Color.FromArgb(72, 199, 142));
            var btnCancel = ActionButton("Cancel", Color.FromArgb(110, 110, 130));
            var btnJson = ActionButton("Edit JSON", Color.FromArgb(72, 150, 220));
            btnSave.Click += BtnSave_Click;
            btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
            btnJson.Click += BtnEditJson_Click;
            buttons.Controls.Add(btnSave);
            buttons.Controls.Add(btnCancel);
            buttons.Controls.Add(btnJson);

            Controls.Add(scroller);
            Controls.Add(buttons);
        }

        private static Button ActionButton(string text, Color color) => new()
        {
            Text = text,
            Width = 96,
            Height = 32,
            BackColor = color,
            ForeColor = Color.FromArgb(10, 10, 20),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0)
        };

        private Control RowLabel(string text, string helpKey)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };

            var label = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(218, 218, 230),
                Cursor = Cursors.Hand
            };

            var help = new Button
            {
                Text = "?",
                Dock = DockStyle.Right,
                Width = 26,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                BackColor = Color.FromArgb(38, 40, 56),
                ForeColor = Color.FromArgb(99, 179, 237),
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                TabStop = false
            };
            help.FlatAppearance.BorderColor = Color.FromArgb(45, 48, 64);
            help.FlatAppearance.BorderSize = 1;

            string quick = BuildQuickHelp(helpKey);
            _quickHelp.SetToolTip(label, quick);
            _quickHelp.SetToolTip(help, "Open detailed help for " + text + ".");

            void ShowHelp()
            {
                if (PairSettingHelp.TryGetValue(helpKey, out var content))
                    AppHelpBox.Show(this, text, content);
            }

            label.Click += (_, _) => ShowHelp();
            help.Click += (_, _) => ShowHelp();
            panel.Controls.Add(label);
            panel.Controls.Add(help);
            return panel;
        }

        private static string BuildQuickHelp(string helpKey)
        {
            if (!PairSettingHelp.TryGetValue(helpKey, out var content))
                return "Open field help.";

            return content.Meaning;
        }

        private void AddTextRow(TableLayoutPanel layout, int row, string label, TextBox textBox, bool upper)
        {
            textBox.Dock = DockStyle.Fill;
            textBox.CharacterCasing = upper ? CharacterCasing.Upper : CharacterCasing.Normal;
            textBox.BackColor = Color.FromArgb(13, 13, 19);
            textBox.ForeColor = Color.White;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.Controls.Add(RowLabel(label, "pair"), 0, row);
            layout.Controls.Add(textBox, 1, row);
        }

        private void AddNumberRow(TableLayoutPanel layout, int row, string label, string key, decimal min, decimal max, int decimals)
        {
            var input = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimals,
                Increment = decimals >= 4 ? 0.0001M : 0.1M,
                BackColor = Color.FromArgb(13, 13, 19),
                ForeColor = Color.White,
                ThousandsSeparator = false
            };
            _inputs[key] = input;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.Controls.Add(RowLabel(label, key), 0, row);
            layout.Controls.Add(input, 1, row);
        }

        private void AddSessionRow(TableLayoutPanel layout, int row, string label, CheckedListBox list)
        {
            list.Dock = DockStyle.Fill;
            list.CheckOnClick = true;
            list.BackColor = Color.FromArgb(13, 13, 19);
            list.ForeColor = Color.White;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.IntegralHeight = false;
            list.HorizontalScrollbar = true;
            list.Items.AddRange(SessionOptions);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
            layout.Controls.Add(RowLabel(label, list == _lstRecommendedSessions ? "recommended_sessions" : "avoid_sessions"), 0, row);
            layout.Controls.Add(list, 1, row);
        }

        private void LoadSettings(PairTradingSettings settings)
        {
            _txtPair.Text = settings.Pair;
            Set("pip_size", settings.PipSize);
            Set("max_spread_pips", settings.MaxSpreadPips);
            Set("good_spread_pips", settings.GoodSpreadPips);
            Set("acceptable_spread_pips", settings.AcceptableSpreadPips);
            Set("min_sl_pips", settings.MinSlPips);
            Set("max_sl_pips", settings.MaxSlPips);
            Set("min_tp_pips", settings.MinTpPips);
            Set("scalping_min_rr", settings.ScalpingMinRR);
            Set("preferred_rr", settings.PreferredRR);
            Set("atr_multiplier_sl", settings.AtrMultiplierSl);
            Set("atr_multiplier_tp", settings.AtrMultiplierTp);
            Set("min_atr_pips_m5", settings.MinAtrPipsM5);
            Set("max_atr_pips_m5", settings.MaxAtrPipsM5);
            Set("min_atr_pips_m15", settings.MinAtrPipsM15);
            Set("max_atr_pips_m15", settings.MaxAtrPipsM15);
            Set("avoid_trade_if_spread_above_percent_of_tp", settings.AvoidTradeIfSpreadAbovePercentOfTp);
            Set("minimum_distance_from_key_level_pips", settings.MinimumDistanceFromKeyLevelPips);
            Set("break_even_after_profit_pips", settings.BreakEvenAfterProfitPips);
            Set("trailing_start_pips", settings.TrailingStartPips);
            Set("trailing_step_pips", settings.TrailingStepPips);
            Set("max_slippage_pips", settings.MaxSlippagePips);
            SetCheckedSessions(_lstRecommendedSessions, settings.RecommendedSessions);
            SetCheckedSessions(_lstAvoidSessions, settings.AvoidSessions);
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtPair.Text))
            {
                AppMessageBox.Warning(this, "Pair name is required.", "Pair Settings");
                return;
            }

            Settings = CollectCurrentSettings();
            DialogResult = DialogResult.OK;
        }

        private void BtnEditJson_Click(object? sender, EventArgs e)
        {
            string json = BuildSinglePairJson(CollectCurrentSettings());
            using var form = new PairSettingsJsonForm(
                json,
                title: "Edit Pair JSON",
                acceptButtonText: "Save JSON",
                acceptValidator: ValidatePairJson);
            if (form.ShowDialog(this) != DialogResult.OK)
                return;

            ApplyJsonToForm(form.JsonText);
            Settings = CollectCurrentSettings();
            DialogResult = DialogResult.OK;
        }

        private PairTradingSettings CollectCurrentSettings() => new()
        {
            Pair = _txtPair.Text.Trim().ToUpperInvariant(),
            PipSize = Get("pip_size"),
            MaxSpreadPips = Get("max_spread_pips"),
            GoodSpreadPips = Get("good_spread_pips"),
            AcceptableSpreadPips = Get("acceptable_spread_pips"),
            MinSlPips = Get("min_sl_pips"),
            MaxSlPips = Get("max_sl_pips"),
            MinTpPips = Get("min_tp_pips"),
            ScalpingMinRR = Get("scalping_min_rr"),
            PreferredRR = Get("preferred_rr"),
            AtrMultiplierSl = Get("atr_multiplier_sl"),
            AtrMultiplierTp = Get("atr_multiplier_tp"),
            MinAtrPipsM5 = Get("min_atr_pips_m5"),
            MaxAtrPipsM5 = Get("max_atr_pips_m5"),
            MinAtrPipsM15 = Get("min_atr_pips_m15"),
            MaxAtrPipsM15 = Get("max_atr_pips_m15"),
            AvoidTradeIfSpreadAbovePercentOfTp = Get("avoid_trade_if_spread_above_percent_of_tp"),
            MinimumDistanceFromKeyLevelPips = Get("minimum_distance_from_key_level_pips"),
            BreakEvenAfterProfitPips = Get("break_even_after_profit_pips"),
            TrailingStartPips = Get("trailing_start_pips"),
            TrailingStepPips = Get("trailing_step_pips"),
            MaxSlippagePips = Get("max_slippage_pips"),
            RecommendedSessions = GetCheckedSessions(_lstRecommendedSessions),
            AvoidSessions = GetCheckedSessions(_lstAvoidSessions)
        };

        private static string BuildSinglePairJson(PairTradingSettings s)
        {
            var jo = JObject.FromObject(s);
            jo.AddFirst(new JProperty("pair", string.IsNullOrWhiteSpace(s.Pair) ? "PAIR_NAME" : s.Pair));
            return jo.ToString(Formatting.Indented);
        }

        private void ApplyJsonToForm(string json)
        {
            var jo = JObject.Parse(json);
            var settings = jo.ToObject<PairTradingSettings>()
                ?? throw new InvalidOperationException("Failed to parse pair settings JSON.");
            if (jo["pair"]?.ToString() is string pair && !string.IsNullOrWhiteSpace(pair))
                settings.Pair = pair.ToUpperInvariant();
            LoadSettings(settings);
        }

        private bool ValidatePairJson(string json)
        {
            try
            {
                var jo = JObject.Parse(json);
                var settings = jo.ToObject<PairTradingSettings>()
                    ?? throw new InvalidOperationException("Failed to parse pair settings JSON.");

                if (jo["pair"]?.ToString() is not string pair || string.IsNullOrWhiteSpace(pair))
                    throw new InvalidOperationException("pair is required.");
                settings.Pair = pair.ToUpperInvariant();
                if (settings.PipSize <= 0)
                    throw new InvalidOperationException("pip_size must be greater than 0.");
                if (settings.MaxSpreadPips < 0)
                    throw new InvalidOperationException("max_spread_pips cannot be negative.");
                if (settings.GoodSpreadPips < 0)
                    throw new InvalidOperationException("good_spread_pips cannot be negative.");
                if (settings.AcceptableSpreadPips < 0)
                    throw new InvalidOperationException("acceptable_spread_pips cannot be negative.");
                if (settings.MinSlPips < 0)
                    throw new InvalidOperationException("min_sl_pips cannot be negative.");
                if (settings.MaxSlPips > 0 && settings.MaxSlPips < settings.MinSlPips)
                    throw new InvalidOperationException("max_sl_pips must be greater than or equal to min_sl_pips.");
                if (settings.MinTpPips < 0)
                    throw new InvalidOperationException("min_tp_pips cannot be negative.");

                return true;
            }
            catch (Exception ex)
            {
                AppMessageBox.Warning(this, ex.Message, "Pair Settings JSON");
                return false;
            }
        }

        private void Set(string key, double value) =>
            _inputs[key].Value = Math.Min(_inputs[key].Maximum, Math.Max(_inputs[key].Minimum, decimal.Parse(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)));

        private double Get(string key) => (double)_inputs[key].Value;

        private static List<string> GetCheckedSessions(CheckedListBox list) =>
            [.. list.CheckedItems.Cast<string>()];

        private static void SetCheckedSessions(CheckedListBox list, IEnumerable<string> values)
        {
            for (int i = 0; i < list.Items.Count; i++)
                list.SetItemChecked(i, false);

            foreach (var raw in values.Where(v => !string.IsNullOrWhiteSpace(v)))
            {
                string value = raw.Trim();
                int index = IndexOfSession(list, value);
                if (index < 0)
                {
                    list.Items.Add(value);
                    index = list.Items.Count - 1;
                }

                list.SetItemChecked(index, true);
            }
        }

        private static int IndexOfSession(CheckedListBox list, string value)
        {
            for (int i = 0; i < list.Items.Count; i++)
            {
                if (string.Equals(list.Items[i]?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static PairTradingSettings Clone(PairTradingSettings settings) => new()
        {
            Pair = settings.Pair,
            PipSize = settings.PipSize,
            MaxSpreadPips = settings.MaxSpreadPips,
            GoodSpreadPips = settings.GoodSpreadPips,
            AcceptableSpreadPips = settings.AcceptableSpreadPips,
            MinSlPips = settings.MinSlPips,
            MaxSlPips = settings.MaxSlPips,
            MinTpPips = settings.MinTpPips,
            ScalpingMinRR = settings.ScalpingMinRR,
            PreferredRR = settings.PreferredRR,
            AtrMultiplierSl = settings.AtrMultiplierSl,
            AtrMultiplierTp = settings.AtrMultiplierTp,
            MinAtrPipsM5 = settings.MinAtrPipsM5,
            MaxAtrPipsM5 = settings.MaxAtrPipsM5,
            MinAtrPipsM15 = settings.MinAtrPipsM15,
            MaxAtrPipsM15 = settings.MaxAtrPipsM15,
            AvoidTradeIfSpreadAbovePercentOfTp = settings.AvoidTradeIfSpreadAbovePercentOfTp,
            MinimumDistanceFromKeyLevelPips = settings.MinimumDistanceFromKeyLevelPips,
            BreakEvenAfterProfitPips = settings.BreakEvenAfterProfitPips,
            TrailingStartPips = settings.TrailingStartPips,
            TrailingStepPips = settings.TrailingStepPips,
            MaxSlippagePips = settings.MaxSlippagePips,
            RecommendedSessions = [.. settings.RecommendedSessions],
            AvoidSessions = [.. settings.AvoidSessions]
        };
    }
}
