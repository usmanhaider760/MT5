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

        private readonly TextBox _txtPair = new();
        private readonly CheckedListBox _lstRecommendedSessions = new();
        private readonly CheckedListBox _lstAvoidSessions = new();
        private readonly Dictionary<string, NumericUpDown> _inputs = new(StringComparer.OrdinalIgnoreCase);

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

        private static Label RowLabel(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(218, 218, 230)
        };

        private static void AddTextRow(TableLayoutPanel layout, int row, string label, TextBox textBox, bool upper)
        {
            textBox.Dock = DockStyle.Fill;
            textBox.CharacterCasing = upper ? CharacterCasing.Upper : CharacterCasing.Normal;
            textBox.BackColor = Color.FromArgb(13, 13, 19);
            textBox.ForeColor = Color.White;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.Controls.Add(RowLabel(label), 0, row);
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
            layout.Controls.Add(RowLabel(label), 0, row);
            layout.Controls.Add(input, 1, row);
        }

        private static void AddSessionRow(TableLayoutPanel layout, int row, string label, CheckedListBox list)
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
            layout.Controls.Add(RowLabel(label), 0, row);
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
