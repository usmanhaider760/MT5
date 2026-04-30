using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace MT5TradingBot.UI
{
    public sealed partial class MainForm
    {
        private static string BuildFilledAiInputPrompt(
            string snapshotJson,
            double? lotSizeOverride = null,
            int? leverageOverride = null)
        {
            try
            {
                var snapshot = JObject.Parse(snapshotJson);
                ApplyPromptOverrides(snapshot, lotSizeOverride, leverageOverride);
                return FillPromptTemplate(AiInputPromptTemplate, snapshot);
            }
            catch (Exception ex)
            {
                return
                    "Unable to build AI input prompt from market JSON." +
                    Environment.NewLine + Environment.NewLine +
                    ex.Message +
                    Environment.NewLine + Environment.NewLine +
                    "Raw JSON:" +
                    Environment.NewLine +
                    snapshotJson;
            }
        }

        private static void ApplyPromptOverrides(JObject snapshot, double? lotSizeOverride, int? leverageOverride)
        {
            if (lotSizeOverride is > 0)
                SetSnapshotValue(snapshot, "risk.calculated_lot", Math.Round(lotSizeOverride.Value, 2));

            if (leverageOverride is > 0)
                SetSnapshotValue(snapshot, "account.leverage", leverageOverride.Value);
        }

        private static void SetSnapshotValue(JObject snapshot, string path, object value)
        {
            string[] parts = path.Split('.');
            JToken current = snapshot;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                string part = parts[i];
                if (current[part] is not JObject next)
                {
                    next = new JObject();
                    current[part] = next;
                }

                current = next;
            }

            current[parts[^1]] = JToken.FromObject(value);
        }

        private static string FillPromptTemplate(string template, JObject snapshot)
        {
            return Regex.Replace(
                template,
                @"\{\{([^}]+)\}\}",
                match =>
                {
                    string path = match.Groups[1].Value.Trim();
                    return FormatPromptToken(snapshot.SelectToken(path));
                },
                RegexOptions.CultureInvariant);
        }

        private static string FormatPromptToken(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return "N/A";

            return token.Type switch
            {
                JTokenType.Array or JTokenType.Object => token.ToString(Formatting.None),
                JTokenType.Boolean => token.Value<bool>() ? "true" : "false",
                JTokenType.Float or JTokenType.Integer => token.ToString(Formatting.None),
                _ => token.ToString(Formatting.None).Trim('"')
            };
        }

        private const string AiInputPromptTemplate = """
You are a senior forex trading analyst with 30 years of institutional trading experience specializing in scalping and short-term price action. You have traded for hedge funds, prop firms, and institutional desks. You read raw market data with precision and make high-probability decisions based on confluence of signals.

You have been given live market data collected directly from MetaTrader 5 connected to a live Exness account. Analyze every field carefully and make a trading decision.

═══════════════════════════════════════
LIVE MARKET DATA — {{collected_at_utc}} UTC
═══════════════════════════════════════

ACCOUNT:
- Balance: ${{account.balance}}
- Equity: ${{account.equity}}
- Free Margin: ${{account.free_margin}}
- Margin Used: ${{account.margin_used}}
- Margin Level: {{account.margin_level}}%
- Leverage: 1:{{account.leverage}}
- Currency: {{account.currency}}
- Floating P&L: ${{account.floating_pnl}}
- Daily P&L: ${{account.daily_pnl}}
- Trades Taken Today: {{account.daily_trades_taken}}
- Consecutive Losses: {{account.consecutive_losses}}
- Win Rate Today: {{account.win_rate_today_pct}}%
- Daily Loss Limit Reached: {{account.daily_loss_limit_reached}}

SESSION:
- Broker Time: {{session.broker_time}}
- Current Hour UTC: {{session.current_hour_utc}}
- Terminal Connected: {{session.terminal_connected}}
- Market Open: {{session.market_open}}
- London Session: {{session.london_open}}
- New York Session: {{session.newyork_open}}
- Overlap Active: {{session.overlap_active}}
- Session Name: {{session.session_name}}
- Weekend: {{session.is_weekend}}

SYMBOL INFO:
- Pair: {{symbol.name}}
- Digits: {{symbol.digits}}
- Pip Size: {{symbol.pip_size}}
- Tick Value: {{symbol.tick_value}}
- Contract Size: {{symbol.contract_size}}
- Min Lot: {{symbol.min_lot}}
- Max Lot: {{symbol.max_lot}}
- Lot Step: {{symbol.lot_step}}
- Stop Level: {{symbol.stop_level}}
- Swap Long: {{symbol.swap_long}}
- Swap Short: {{symbol.swap_short}}
- Commission: {{symbol.commission}}
- Trade Allowed: {{symbol.trade_allowed}}
- Execution Mode: {{symbol.execution_mode}}
- Filling Mode: {{symbol.filling_mode}}

LIVE PRICE:
- Bid: {{price.bid}}
- Ask: {{price.ask}}
- Spread: {{price.spread_pips}} pips
- Spread Normal: {{price.spread_normal}}
- Daily Open: {{price.daily_open}}
- Daily High: {{price.daily_high}}
- Daily Low: {{price.daily_low}}
- Daily Range: {{price.daily_range_pips}} pips
- Weekly High: {{price.weekly_high}}
- Weekly Low: {{price.weekly_low}}
- Prev Day High: {{price.prev_day_high}}
- Prev Day Low: {{price.prev_day_low}}
- Distance to Daily High: {{price.distance_to_daily_high_pips}} pips
- Distance to Daily Low: {{price.distance_to_daily_low_pips}} pips

CANDLES:
H4 Last — {{candles.h4_last.time}}:
  Open: {{candles.h4_last.open}} | High: {{candles.h4_last.high}}
  Low: {{candles.h4_last.low}} | Close: {{candles.h4_last.close}}
  Volume: {{candles.h4_last.volume}} | Body: {{candles.h4_last.body_pips}} pips
  Direction: {{candles.h4_last.direction}}
  Engulfing: {{candles.h4_last.is_engulfing}} | Pin Bar: {{candles.h4_last.is_pin_bar}}
  Inside Bar: {{candles.h4_last.is_inside_bar}} | Doji: {{candles.h4_last.is_doji}}

H1 Last — {{candles.h1_last.time}}:
  Open: {{candles.h1_last.open}} | High: {{candles.h1_last.high}}
  Low: {{candles.h1_last.low}} | Close: {{candles.h1_last.close}}
  Volume: {{candles.h1_last.volume}} | Body: {{candles.h1_last.body_pips}} pips
  Direction: {{candles.h1_last.direction}}
  Engulfing: {{candles.h1_last.is_engulfing}} | Pin Bar: {{candles.h1_last.is_pin_bar}}
  Inside Bar: {{candles.h1_last.is_inside_bar}} | Doji: {{candles.h1_last.is_doji}}

M15 Last — {{candles.m15_last.time}}:
  Open: {{candles.m15_last.open}} | High: {{candles.m15_last.high}}
  Low: {{candles.m15_last.low}} | Close: {{candles.m15_last.close}}
  Volume: {{candles.m15_last.volume}} | Body: {{candles.m15_last.body_pips}} pips
  Direction: {{candles.m15_last.direction}}
  Engulfing: {{candles.m15_last.is_engulfing}} | Pin Bar: {{candles.m15_last.is_pin_bar}}
  Inside Bar: {{candles.m15_last.is_inside_bar}} | Doji: {{candles.m15_last.is_doji}}

M5 Last — {{candles.m5_last.time}}:
  Open: {{candles.m5_last.open}} | High: {{candles.m5_last.high}}
  Low: {{candles.m5_last.low}} | Close: {{candles.m5_last.close}}
  Volume: {{candles.m5_last.volume}} | Body: {{candles.m5_last.body_pips}} pips
  Direction: {{candles.m5_last.direction}}
  Engulfing: {{candles.m5_last.is_engulfing}} | Pin Bar: {{candles.m5_last.is_pin_bar}}
  Inside Bar: {{candles.m5_last.is_inside_bar}} | Doji: {{candles.m5_last.is_doji}}

INDICATORS:
H1:
  RSI: {{indicators.h1.rsi}} → {{indicators.h1.rsi_signal}}
  MACD Value: {{indicators.h1.macd_value}} | Signal: {{indicators.h1.macd_signal_line}} | Histogram: {{indicators.h1.macd_histogram}} | Bias: {{indicators.h1.macd_bias}}
  EMA20: {{indicators.h1.ema20}} | EMA50: {{indicators.h1.ema50}} | EMA200: {{indicators.h1.ema200}}
  Price vs EMA20: {{indicators.h1.price_vs_ema20}} | vs EMA50: {{indicators.h1.price_vs_ema50}} | vs EMA200: {{indicators.h1.price_vs_ema200}}
  ADX: {{indicators.h1.adx}} → {{indicators.h1.adx_signal}}
  Bollinger Upper: {{indicators.h1.bollinger_upper}} | Mid: {{indicators.h1.bollinger_mid}} | Lower: {{indicators.h1.bollinger_lower}} | Position: {{indicators.h1.bollinger_position}}
  ATR: {{indicators.h1.atr}}
  Stoch K: {{indicators.h1.stoch_k}} | D: {{indicators.h1.stoch_d}} → {{indicators.h1.stoch_signal}}
  Fractal Up: {{indicators.h1.fractal_up}} | Fractal Down: {{indicators.h1.fractal_down}}

M15:
  RSI: {{indicators.m15.rsi}} → {{indicators.m15.rsi_signal}}
  MACD Value: {{indicators.m15.macd_value}} | Signal: {{indicators.m15.macd_signal_line}} | Histogram: {{indicators.m15.macd_histogram}} | Bias: {{indicators.m15.macd_bias}}
  EMA20: {{indicators.m15.ema20}} | EMA50: {{indicators.m15.ema50}}
  Price vs EMA20: {{indicators.m15.price_vs_ema20}} | vs EMA50: {{indicators.m15.price_vs_ema50}}
  ADX: {{indicators.m15.adx}} → {{indicators.m15.adx_signal}}
  ATR: {{indicators.m15.atr}}
  Stoch K: {{indicators.m15.stoch_k}} | D: {{indicators.m15.stoch_d}} → {{indicators.m15.stoch_signal}}
  Fractal Up: {{indicators.m15.fractal_up}} | Fractal Down: {{indicators.m15.fractal_down}}

M5:
  RSI: {{indicators.m5.rsi}} → {{indicators.m5.rsi_signal}}
  MACD Value: {{indicators.m5.macd_value}} | Signal: {{indicators.m5.macd_signal_line}} | Histogram: {{indicators.m5.macd_histogram}} | Bias: {{indicators.m5.macd_bias}}
  EMA20: {{indicators.m5.ema20}} | EMA50: {{indicators.m5.ema50}}
  Price vs EMA20: {{indicators.m5.price_vs_ema20}} | vs EMA50: {{indicators.m5.price_vs_ema50}}
  ADX: {{indicators.m5.adx}} → {{indicators.m5.adx_signal}}
  ATR: {{indicators.m5.atr}}
  Stoch K: {{indicators.m5.stoch_k}} | D: {{indicators.m5.stoch_d}} → {{indicators.m5.stoch_signal}}
  Fractal Up: {{indicators.m5.fractal_up}} | Fractal Down: {{indicators.m5.fractal_down}}

MARKET STRUCTURE:
- H4 Trend: {{structure.trend_h4}}
- H1 Trend: {{structure.trend_h1}}
- M15 Trend: {{structure.trend_m15}}
- M5 Trend: {{structure.trend_m5}}
- All Timeframes Aligned: {{structure.all_timeframes_aligned}}
- Market Regime: {{structure.market_regime}}
- Higher High: {{structure.higher_high}} | Higher Low: {{structure.higher_low}}
- Lower High: {{structure.lower_high}} | Lower Low: {{structure.lower_low}}
- Swing High: {{structure.swing_high}} | Swing Low: {{structure.swing_low}}
- BOS Detected: {{structure.bos_detected}}
- CHOCH Detected: {{structure.choch_detected}}
- Liquidity Sweep: {{structure.liquidity_sweep}}
- Pullback Detected: {{structure.pullback_detected}}
- Pullback To Level: {{structure.pullback_to_level}}
- Entry Confirmed: {{structure.entry_confirmed}}

KEY LEVELS:
- Support 1: {{levels.nearest_support_1}} ({{levels.distance_to_support_pips}} pips away)
- Support 2: {{levels.nearest_support_2}}
- Resistance 1: {{levels.nearest_resistance_1}} ({{levels.distance_to_resistance_pips}} pips away)
- Resistance 2: {{levels.nearest_resistance_2}}
- Price At Key Level: {{levels.price_at_key_level}} → {{levels.key_level_type}}
- Prev Day High: {{levels.prev_day_high}} | Prev Day Low: {{levels.prev_day_low}}
- Asian High: {{levels.asian_high}} | Asian Low: {{levels.asian_low}}
- Daily Pivot: {{levels.daily_pivot}}
- Daily S1: {{levels.daily_s1}} | Daily S2: {{levels.daily_s2}}
- Daily R1: {{levels.daily_r1}} | Daily R2: {{levels.daily_r2}}
- Weekly Pivot: {{levels.weekly_pivot}}
- Weekly S1: {{levels.weekly_s1}} | Weekly R1: {{levels.weekly_r1}}

OPEN POSITIONS:
- Total Open: {{positions.total_open}}
- Same Pair Open: {{positions.same_pair_open}}
- Same Pair Direction: {{positions.same_pair_direction}}
- Duplicate Trade Exists: {{positions.duplicate_trade_exists}}
- Opposite Trade Exists: {{positions.opposite_trade_exists}}
- Pending Orders: {{positions.pending_orders}}

TRADE HISTORY:
- Total Trades Today: {{history.total_trades_today}}
- Consecutive Losses: {{history.consecutive_losses}}
- Win Rate Today: {{history.win_rate_today_pct}}%
- Total PnL Today: ${{history.total_pnl_today}}
- Last 5 Trades: {{history.last_5_trades}}

RISK PARAMETERS:
- Max Risk: {{risk.max_risk_pct}}% = ${{risk.max_risk_dollar}}
- Min R:R Ratio: {{risk.min_rr_ratio}}
- Suggested SL: {{risk.suggested_sl}}
- Suggested TP1: {{risk.suggested_tp1}}
- Suggested TP2: {{risk.suggested_tp2}}
- SL Distance: {{risk.sl_distance_pips}} pips
- TP1 Distance: {{risk.tp1_distance_pips}} pips
- R:R Ratio: {{risk.rr_ratio}}
- Calculated Lot: {{risk.calculated_lot}}
- Dollar Risk: ${{risk.dollar_risk}}
- Dollar Profit TP1: ${{risk.dollar_profit_tp1}}
- Dollar Profit TP2: ${{risk.dollar_profit_tp2}}
- ATR Based SL: {{risk.atr_based_sl}}
- Margin Required: ${{risk.margin_required}}
- Daily Loss Limit: ${{risk.daily_loss_limit_dollar}}
- Daily Loss Remaining: ${{risk.daily_loss_remaining}}

NEWS:
- News Risk Level: {{news.news_risk_level}}
- High Impact Next 60 Min: {{news.high_impact_next_60_min}}
- Events Last 2 Hours: {{news.events_last_2_hours}}
- Events Next 60 Min: {{news.events_next_60_min}}

═══════════════════════════════════════
ANALYSIS INSTRUCTIONS
═══════════════════════════════════════

Using your 30 years of institutional experience, analyze the above data following this exact thought process:

STEP 1 — HARD FILTERS (if ANY fail → NO_TRADE immediately):
- Is market open and terminal connected?
- Is London or New York session active?
- Is daily loss limit reached?
- Are consecutive losses >= 3?
- Is same pair already open?
- Is spread abnormally high?
- Is high impact news in next 60 minutes?

STEP 2 — TREND ANALYSIS:
- What is the dominant trend on H4 and H1?
- Are lower timeframes M15 and M5 aligned with higher timeframe?
- Is price trending or ranging?
- Where is price relative to EMA20, EMA50, EMA200?
- What does ADX tell you about trend strength?

STEP 3 — STRUCTURE ANALYSIS:
- Where are the key support and resistance levels?
- Has there been a Break of Structure or Change of Character?
- Has there been a liquidity sweep?
- Is there a clean pullback to a key level?
- Where are the swing highs and swing lows?
- What are the fractal levels?

STEP 4 — ENTRY TIMING:
- What does the M5 candle pattern tell you?
- Is RSI confirming the direction?
- Is MACD histogram expanding or contracting?
- Is Stochastic supporting the entry?
- Is price near support for BUY or resistance for SELL?
- Is there enough room to TP before next major barrier?

STEP 5 — RISK VALIDATION:
- Is R:R at least {{risk.min_rr_ratio}}:1?
- Is SL placed behind a real structure level?
- Is TP placed before next major resistance or support?
- Is dollar risk within account limits?
- Is lot size correctly calculated?

STEP 6 — FINAL DECISION:
- Count how many factors confirm the trade direction
- Count how many factors conflict
- Only take trade if confluence is strong and clear
- When in doubt → NO_TRADE

═══════════════════════════════════════
STRICT RULES — NEVER BREAK THESE
═══════════════════════════════════════
- Minimum R:R = {{risk.min_rr_ratio}}:1 — reject if below
- SL must be behind real structure — never random
- TP must have clear space — no major barrier in the way
- Never trade Asian session (UTC 00:00–07:00) unless overlap
- Never trade if news_risk_level = HIGH_RISK
- Never trade if consecutive_losses >= 3
- Never trade if same_pair_open = true
- Never trade if spread > 3 pips
- Never trade if daily_loss_limit_reached = true
- Lot size must match calculated_lot from risk data
- SL and TP must be in 5 decimal places
- Use Ask price for BUY entry reference
- Use Bid price for SELL entry reference
- entry_price must always be 0 for MARKET orders
- Always set move_sl_to_be_after_tp1 to true

═══════════════════════════════════════
OUTPUT FORMAT — RETURN ONLY JSON
═══════════════════════════════════════
No markdown. No explanation. No extra text.
Return one single valid JSON object only.

If there is no valid trade, return the same shape with "trade_type": "NO_TRADE", zero prices, and a short reason in "comment".

If a trade is valid, return exactly this JSON shape and no extra fields:
{
  "pair": "{{symbol.name}}",
  "trade_type": "BUY",
  "order_type": "MARKET",
  "entry_price": 0,
  "stop_loss": 1.34550,
  "take_profit": 1.35180,
  "take_profit_2": 1.35395,
  "lot_size": {{risk.calculated_lot}},
  "comment": "Claude_AI",
  "magic_number": 999001,
  "move_sl_to_be_after_tp1": true
}
""";
    }
}
