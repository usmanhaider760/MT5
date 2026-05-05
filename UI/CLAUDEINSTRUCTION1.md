# MT5TradingBotPro — Claude Code Master Instructions
# Save as CLAUDE.md in project root. Claude Code reads this automatically.

==================================================
WHO YOU ARE
==================================================

You are a senior C# and MQL5 developer working on MT5TradingBotPro.
Architecture has three signal pathways:

  Path 1: File-based signals → AutoBotService.ExecuteTradeWithValidationCoreAsync()
  Path 2: AI signals → ClaudeSignalService → same gate as Path 1
  Path 3: Auto-scalp → ScalpingSessionService.EvaluateSnapshot() → same gate

The bot received a DEMO ONLY audit verdict. Your task is to implement
pre-planned fixes in strict order. Do not refactor unrelated code.
Do not rename existing methods. Do not restructure files not mentioned.
One fix at a time. Build must compile before moving to next fix.

==================================================
ARCHITECTURE REFERENCE — READ BEFORE EVERY FIX
==================================================

Key files and their roles:

  AutoBotService.cs
    - Main execution gate: ExecuteTradeWithValidationCoreAsync() line ~758
    - Heartbeat loop: polls drawdown, trailing stop, breakeven
    - Daily trade counter, kill switch, rollout stage all live here

  ScalpingSessionService.cs
    - EvaluateSnapshot() line ~660: multi-factor confluence scorer
    - ReadAtrPips() line ~432: reads ATR from snapshot
    - Min decision score enforced at line ~316

  RiskManager.cs
    - ValidateAsync() line ~189: R:R, spread%, dollar risk, lot size
    - Live spread fetched here at execution moment

  StrategyEngine.cs
    - Line ~39: stopDistance = flat 15 pips — THIS IS A KNOWN BUG, fix in Fix 6

  TradingBotEA.mq5
    - SnapshotTrend(): EMA20/50/200, H4/H1/M15/M5 trend
    - SnapshotIndicatorsJson(): RSI(14), MACD(12/26/9), Stoch(5,3,3), ATR(14), ADX(14)
    - SnapshotCandleJson(): is_engulfing, is_pin_bar, is_inside_bar, is_doji
    - SnapshotStructureJson(): swing highs/lows, BOS, market structure

  FmpNewsCalendarService.cs
    - Live economic calendar, 15-min cache
    - news_currencies array has duplication bug (Fix 8)

  SignalDecisionService.cs
    - Line ~45: AI directly sets StopLoss and TakeProfit — known issue (Fix 12)

  settings.json
    - news_currencies: duplicated 50+ times — known bug
    - atr_multiplier_sl: 1.0 defined but never read — fix in Fix 6
    - atr_multiplier_tp: 1.2 defined but never read — fix in Fix 6

Snapshot JSON contract between EA and C#:
  indicators.m5.adx         — ADX value on M5
  indicators.h1.adx         — ADX value on H1
  structure.trend_h1        — BULLISH / BEARISH / RANGING
  structure.bos_detected    — bool, break of structure
  candles.m5.is_engulfing   — bool
  candles.m5.is_pin_bar     — bool
  indicators.m5.atr         — ATR(14) value on M5
  session.london_open       — bool
  session.newyork_open      — bool

==================================================
CODING RULES — FOLLOW ALWAYS
==================================================

1. Touch only the method or block mentioned in the fix. Nothing else.
2. Always add a structured log line after every new check using existing
   Serilog pattern in the file. Example:
     _logger.Warning("TRADE_BLOCKED | Reason: {Reason} | Value: {Value}", reason, value);
3. Every new hard block must return a TradeResult with a specific ErrorCode string.
   Follow the existing pattern in AutoBotService. Example:
     return TradeResult.Rejected("ADX_RANGING_BLOCK", "ADX below threshold");
4. Never use magic numbers. Use named constants or read from config.
5. After every fix: confirm the build compiles. Say "BUILD OK" or "BUILD FAILED".
6. If a fix requires changes to both TradingBotEA.mq5 AND a C# file,
   do the MQ5 change first, then the C# change. State this clearly.
7. Do not add using statements that are already present in the file.
8. If you are unsure which overload or pattern the codebase uses,
   search the file for an existing similar pattern and follow it exactly.

==================================================
MODEL CANNOT SWITCH ITSELF
==================================================

Claude Code cannot change its own model. When a fix below is marked
[SWITCH TO OPUS] you must stop, print this message, and wait:

  *** STOP — TYPE /model opus-4-5 IN TERMINAL THEN SAY "ready" ***

When a fix is marked [SWITCH TO SONNET] print:

  *** STOP — TYPE /model claude-sonnet-4-5 IN TERMINAL THEN SAY "ready" ***

When a fix is marked [HAIKU OK] you are already on the right model
if using Haiku. If on Sonnet, proceed — Sonnet handles these fine too.

==================================================
FIX LIST — EXECUTE IN THIS EXACT ORDER
==================================================

----------------------------------------
FIX 1 — ADX RANGING BLOCK          [MODEL: claude-sonnet-4-5-20251001]
----------------------------------------

WHAT: Add a hard ADX block to two places:
  A) EvaluateSnapshot() in ScalpingSessionService.cs
  B) ExecuteTradeWithValidationCoreAsync() in AutoBotService.cs

WHY: Bot currently trades during ranging markets. ADX < 20 means all
trend indicators (EMA, MACD, RSI) are producing false signals.
This is the single highest-impact fix.

HOW — Part A (ScalpingSessionService.cs):
  Location: EvaluateSnapshot() — BEFORE the scoring loop begins
  Insert this block as the FIRST check in the method:

    var adxM5 = snapshot?.indicators?.m5?.adx ?? 0;
    if (adxM5 < 20)
    {
        _logger.Warning("SCALP_BLOCKED | Reason: ADX_RANGING | ADX: {Adx} | Symbol: {Symbol}",
            adxM5, snapshot.symbol);
        return new ScalpingDecision(false, "ADX_RANGING_BLOCK",
            $"ADX {adxM5:F1} below minimum 20 — ranging market, no trade");
    }

HOW — Part B (AutoBotService.cs):
  Location: ExecuteTradeWithValidationCoreAsync() — after the spread check,
  before RiskManager.ValidateAsync()
  Insert this block:

    var snapshotAdx = request.Snapshot?.indicators?.m5?.adx ?? 0;
    if (snapshotAdx > 0 && snapshotAdx < 20)
    {
        _logger.Warning("GATE_BLOCKED | Reason: ADX_RANGING | ADX: {Adx} | Symbol: {Symbol}",
            snapshotAdx, request.Symbol);
        return TradeResult.Rejected("ADX_RANGING_BLOCK",
            $"ADX {snapshotAdx:F1} below 20 — ranging market filter");
    }

VERIFY: Search codebase for all calls to EvaluateSnapshot() and
ExecuteTradeWithValidationCoreAsync(). Confirm the new block appears
before any OrderSend() call site. Confirm build compiles.

----------------------------------------
FIX 2 — SESSION TIME GATE          [MODEL: claude-sonnet-4-5-20251001]
----------------------------------------

WHAT: Hard block any trade outside 07:00–16:00 UTC in the main execution gate.

WHY: File-based signals have no session check. A signal at 03:00 UTC
(Asia session) executes without any block. XAUUSD has insufficient
liquidity in Asia for scalping — results in wide spreads and stop hunts.

HOW (AutoBotService.cs):
  Location: ExecuteTradeWithValidationCoreAsync() — immediately after
  Fix 1 ADX block, before RiskManager.ValidateAsync()

  Insert this block:

    var utcNow = DateTime.UtcNow;
    var utcHour = utcNow.Hour;
    var isAllowedSession = utcHour >= 7 && utcHour < 16;
    if (!isAllowedSession)
    {
        _logger.Warning("GATE_BLOCKED | Reason: SESSION_TIME | UTC: {Time} | Symbol: {Symbol}",
            utcNow.ToString("HH:mm"), request.Symbol);
        return TradeResult.Rejected("SESSION_TIME_BLOCK",
            $"Trade outside allowed session window (07:00–16:00 UTC). Current: {utcNow:HH:mm} UTC");
    }

NOTE: If broker server time is already available on the request object
(check for request.ServerTime or similar), use that instead of
DateTime.UtcNow. Search the file for existing ServerTime usage first.

VERIFY: Confirm no other code path reaches OrderSend() without passing
through this check. Confirm build compiles.

----------------------------------------
FIX 3 — H1 TREND AS HARD PREREQUISITE   [MODEL: claude-sonnet-4-5-20251001]
----------------------------------------

WHAT: Promote H1 trend alignment from a score deduction to a hard abort
in EvaluateSnapshot() in ScalpingSessionService.cs.

WHY: Currently a trade can fire against the H1 trend if other score
factors compensate. Counter-trend scalping on XAUUSD causes the
highest loss rate. The H1 trend must be a non-negotiable precondition.

HOW (ScalpingSessionService.cs):
  Location: EvaluateSnapshot() — after the ADX check from Fix 1,
  before the scoring loop

  Step 1: Determine the expected trend direction from the signal:
    var expectedTrend = request.Direction == TradeDirection.Buy ? "BULLISH" : "BEARISH";

  Step 2: Read H1 trend from snapshot:
    var h1Trend = snapshot?.structure?.trend_h1 ?? "RANGING";

  Step 3: Insert hard block:
    if (h1Trend != expectedTrend)
    {
        _logger.Warning("SCALP_BLOCKED | Reason: H1_TREND_MISMATCH | H1: {H1} | Expected: {Expected} | Symbol: {Symbol}",
            h1Trend, expectedTrend, snapshot.symbol);
        return new ScalpingDecision(false, "H1_TREND_MISMATCH",
            $"H1 trend is {h1Trend}, signal requires {expectedTrend}");
    }

  Step 4: Find the existing score deduction for H1 trend misalignment
  inside the scoring loop and REMOVE it. It is now a precondition,
  not a score point.

VERIFY: Confirm H1 trend check appears before the scoring loop.
Confirm the old score deduction line is removed. Confirm build compiles.

----------------------------------------
FIX 4 — SIGNAL EXPIRY DEFAULT      [MODEL: claude-haiku-4-5-20251001]
----------------------------------------

WHAT: Set default ExpiryMinutes to 0.17 (10 seconds) for all scalp signals.

WHY: File-based signals currently default to ExpiryMinutes = 0 (never expires).
A signal 60 seconds old on XAUUSD M5 can be 80+ pips away from current
price. The stale signal passes R:R checks and executes at the wrong price.

HOW:
  Search the codebase for where ExpiryMinutes is set or defaulted.
  Look in: signal file deserializer, AutoBotService signal intake,
  ScalpingSessionService signal builder.

  Wherever ExpiryMinutes is set to 0 or not set, change the default to:
    ExpiryMinutes = 0.17  // 10 seconds — required for M5 scalping

  If there is a constant or config key for this, update that instead.
  Do not hardcode 0.17 in multiple places — use a single named constant:
    private const double ScalpSignalExpiryMinutes = 0.17;

VERIFY: Confirm no file-based signal can enter the gate with
ExpiryMinutes = 0. Confirm build compiles.

----------------------------------------
FIX 5 — RSI THRESHOLD + CROSS CHECK   [MODEL: claude-sonnet-4-5-20251001]
----------------------------------------

WHAT: Two changes to RSI logic in ScalpingSessionService.cs:
  A) Lower hard block thresholds from 78/22 to 70/30
  B) Add RSI cross-above-50 check for BUY, cross-below-50 for SELL

WHY: Bot currently allows BUY entries at RSI 71–77 (overbought zone).
Institutional standard is 70 as the hard ceiling. Additionally, RSI
only checks zone membership — it does not verify directional momentum
cross of the midline.

HOW — Part A (ScalpingSessionService.cs):
  Find the lines:
    RSI > 78 → hard block BUY   (line ~693)
    RSI < 22 → hard block SELL  (line ~697)
  Change to:
    if (rsi > 70) → hard block BUY
    if (rsi < 30) → hard block SELL

HOW — Part B:
  After the hard block, add RSI cross check.
  You need current RSI and previous RSI. Check if snapshot provides
  indicators.m5.rsi_prev or similar. If not, use indicators.m5.rsi
  for current and add a note that prev-bar RSI should be added to EA.

  For now implement with available data:
    var rsiBuy  = rsi > 48 && rsi < 70;   // in momentum zone, not extreme
    var rsiSell = rsi < 52 && rsi > 30;   // in momentum zone, not extreme

  Add this as a SCORED check (not hard block), 1 point if aligned.
  Do not remove the existing RSI zone scoring — add the cross check
  as an additional point alongside it.

VERIFY: Confirm hard block is at 70/30 not 78/22.
Confirm RSI cross check adds to score correctly. Build compiles.

----------------------------------------
FIX 6 — ATR SPIKE FILTER           [MODEL: claude-sonnet-4-5-20251001]
----------------------------------------

WHAT: Implement a 20-bar rolling ATR average and suspend trading
when current ATR exceeds 2× the average.

WHY: No abnormal volatility detection exists. Bot will enter trades
during a sudden volatility spike (e.g. unexpected news) with an
undersized SL, causing immediate stop-out. This is the most dangerous
missing filter for a live account.

Also fix StrategyEngine.cs line ~39 where SL = flat 15 pips.
Replace with ATR-based SL using the already-defined config values:
  atr_multiplier_sl = 1.0  (in settings.json — read this value)
  atr_multiplier_tp = 1.2  (in settings.json — read this value)

HOW — ATR spike filter (ScalpingSessionService.cs):
  Add a private rolling list to store recent ATR values:
    private readonly Queue<double> _atrHistory = new(20);

  In EvaluateSnapshot(), after Fix 1 ADX check:
    var currentAtr = snapshot?.indicators?.m5?.atr ?? 0;
    if (currentAtr > 0)
    {
        if (_atrHistory.Count >= 20) _atrHistory.Dequeue();
        _atrHistory.Enqueue(currentAtr);
    }
    if (_atrHistory.Count >= 10)
    {
        var avgAtr = _atrHistory.Average();
        if (currentAtr > avgAtr * 2.0)
        {
            _logger.Warning("SCALP_BLOCKED | Reason: ATR_SPIKE | CurrentATR: {Current} | AvgATR: {Avg} | Symbol: {Symbol}",
                currentAtr, avgAtr, snapshot.symbol);
            return new ScalpingDecision(false, "ATR_SPIKE_BLOCK",
                $"ATR spike detected: {currentAtr:F1} > 2× average {avgAtr:F1}");
        }
    }

HOW — Fix StrategyEngine.cs flat SL:
  Location: line ~39 where stopDistance is set
  Replace flat 15 pips with:
    var atrPips  = request.Snapshot?.indicators?.m5?.atr ?? 15;
    var slMult   = _config.GetValue<double>("Trading:atr_multiplier_sl", 1.5);
    var tpMult   = _config.GetValue<double>("Trading:atr_multiplier_tp", 2.5);
    var stopDistance = Math.Max(atrPips * slMult, info.SpreadPips * 3) * pipSize;
    var tpDistance   = atrPips * tpMult * pipSize;

  If _config is not injected in StrategyEngine, read multipliers from
  the pair settings object that is already passed to the method.

VERIFY: Confirm _atrHistory queue fills before spike filter activates
(count >= 10 guard). Confirm StrategyEngine no longer uses flat 15 pips.
Confirm build compiles.

----------------------------------------
FIX 7 — ENGULFING / BOS SCORED CHECK   [MODEL: claude-sonnet-4-5-20251001]
----------------------------------------

WHAT: Add is_engulfing and bos_detected as scored checks in
EvaluateSnapshot() in ScalpingSessionService.cs.

WHY: EA already detects engulfing candles and break-of-structure.
The snapshot fields exist. The C# scorer simply never reads them.
Entries currently fire on M5 candle direction alone without any
pattern confirmation at a key level.

HOW (ScalpingSessionService.cs):
  Location: inside EvaluateSnapshot() scoring loop, after existing
  candle direction score check

  Add:
    // Engulfing candle confirmation
    var isEngulfing = snapshot?.candles?.m5?.is_engulfing ?? false;
    if (isEngulfing)
    {
        score += 1;
        scoreReasons.Add("Engulfing candle confirmed on M5");
    }

    // Break of structure confirmation
    var bosDetected = snapshot?.structure?.bos_detected ?? false;
    if (bosDetected)
    {
        score += 1;
        scoreReasons.Add("Break of structure detected on M5");
    }

NOTE: Do not make these hard blocks — they are bonus score points.
The minimum score threshold (currently 6) will naturally require
more confluence when these are absent.

VERIFY: Confirm snapshot field names match exactly what the EA
sends in the JSON. Search TradingBotEA.mq5 for "is_engulfing" and
"bos_detected" to confirm the exact key names. Fix key names if
there is a mismatch. Confirm build compiles.

----------------------------------------
FIX 8 — NEWS CURRENCIES BUG + CHF   [MODEL: claude-haiku-4-5-20251001]
----------------------------------------

WHAT: Fix duplicated news_currencies array in settings.json and add CHF.

WHY: The news_currencies array is duplicated 50+ times in settings.json.
This is a configuration bug. CHF (Swiss Franc) is also missing despite
XAUUSD having strong safe-haven correlation with CHF news events.

HOW (settings.json):
  Find all occurrences of news_currencies in settings.json.
  There should be exactly ONE definition. Remove all duplicates.
  The single correct array should be:

    "news_currencies": ["USD", "GBP", "EUR", "JPY", "CHF"]

  If news_currencies appears inside pair-specific config blocks,
  check whether it should be there or only at the root level.
  Follow the existing config structure — do not move it if it is
  intentionally per-pair.

VERIFY: Confirm only one news_currencies definition exists per
config scope. Confirm CHF is present. Confirm JSON is valid
(no trailing commas, no syntax errors). Confirm build compiles.

----------------------------------------
FIX 9 — ROUND NUMBER AWARENESS     [MODEL: claude-sonnet-4-5-20251001]
----------------------------------------

WHAT: Add a round number detector for XAUUSD that warns or adjusts
when SL or TP falls within 20 pips of a $50 or $100 price level.

WHY: XAUUSD reacts strongly at round number levels ($2300, $2350, $2400).
TP placed inside a round-number cluster will rarely fill cleanly.
SL placed just beyond a round number is a stop-hunt magnet.

HOW (RiskManager.cs or ScalpingSessionService.cs — wherever final
SL/TP values are confirmed before execution):

  Add a helper method:
    private bool IsNearRoundNumber(double price, double pipSize, int warningPips = 20)
    {
        var roundedTo50  = Math.Round(price / 50.0) * 50.0;
        var roundedTo100 = Math.Round(price / 100.0) * 100.0;
        var distTo50  = Math.Abs(price - roundedTo50)  / pipSize;
        var distTo100 = Math.Abs(price - roundedTo100) / pipSize;
        return distTo50 < warningPips || distTo100 < warningPips;
    }

  Call it before finalising SL and TP:
    if (IsNearRoundNumber(proposedTp, pipSize))
        _logger.Warning("ROUND_NUMBER_WARNING | TP {Tp} is within 20 pips of round level | Symbol: {Symbol}",
            proposedTp, symbol);
    if (IsNearRoundNumber(proposedSl, pipSize))
        _logger.Warning("ROUND_NUMBER_WARNING | SL {Sl} is within 20 pips of round level | Symbol: {Symbol}",
            proposedSl, symbol);

NOTE: This is a WARNING only — do not hard block. Log it so you can
review post-session whether round numbers are causing TP misses.
Upgrade to a hard block after 2 weeks of demo data confirms the pattern.

VERIFY: Confirm method is called for both SL and TP. Confirm it only
runs for XAUUSD (check symbol name before calling). Confirm build compiles.

----------------------------------------
FIX 10 — AI SL VALIDATION VS SWING  [MODEL: claude-sonnet-4-5-20251001]
----------------------------------------

WHAT: Validate AI-proposed SL against the nearest swing high/low from
the snapshot before accepting it in SignalDecisionService.cs.

WHY: SignalDecisionService line ~45 assigns aiAnalysis.StopLoss directly.
If Claude miscomputes SL, the gate accepts it as long as R:R is valid.
There is no check that the SL is actually behind a swing level.

HOW (SignalDecisionService.cs):
  Location: after AI StopLoss is read, before it is assigned to the request

  Read swing levels from snapshot:
    var swingLow  = request.Snapshot?.structure?.swing_low  ?? 0;
    var swingHigh = request.Snapshot?.structure?.swing_high ?? 0;
    var pipSize   = request.PipSize;

  For BUY signals — SL should be near or below swing_low:
    if (request.Direction == TradeDirection.Buy && swingLow > 0)
    {
        var distFromSwing = Math.Abs(aiAnalysis.StopLoss - swingLow) / pipSize;
        if (distFromSwing > 50)
            _logger.Warning("AI_SL_WARNING | AI SL {AiSl} is {Dist} pips from swing low {SwingLow}",
                aiAnalysis.StopLoss, distFromSwing, swingLow);
    }

  For SELL signals — SL should be near or above swing_high:
    if (request.Direction == TradeDirection.Sell && swingHigh > 0)
    {
        var distFromSwing = Math.Abs(aiAnalysis.StopLoss - swingHigh) / pipSize;
        if (distFromSwing > 50)
            _logger.Warning("AI_SL_WARNING | AI SL {AiSl} is {Dist} pips from swing high {SwingHigh}",
                aiAnalysis.StopLoss, distFromSwing, swingHigh);
    }

NOTE: Warning only for now. After 2 weeks demo data, upgrade to
hard reject if distFromSwing > 80 pips.

VERIFY: Confirm snapshot field names for swing_low and swing_high
match EA output. Search TradingBotEA.mq5 for the exact keys.
Confirm build compiles.

----------------------------------------
FIX 11 — ASIAN SESSION LEVELS      [MODEL: claude-sonnet-4-5-20251001]
----------------------------------------

WHAT: Track Asian session high/low in TradingBotEA.mq5 and expose
in the snapshot JSON for C# consumption.

WHY: asian_high and asian_low are currently hardcoded to 0.00000.
This is a critical missing reference for London open strategy —
Asia session high/low are the first major S/R levels of the London day.

HOW — Part 1 (TradingBotEA.mq5):
  Add two global variables:
    double g_AsianHigh = 0;
    double g_AsianLow  = 0;

  In OnTick() or a session tracking function, track the range
  during Asia session hours (00:00–07:00 UTC server time):
    datetime serverTime = TimeGMT();
    int hour = TimeHour(serverTime);
    double bid = SymbolInfoDouble(_Symbol, SYMBOL_BID);

    if (hour >= 0 && hour < 7)
    {
        if (g_AsianHigh == 0 || bid > g_AsianHigh) g_AsianHigh = bid;
        if (g_AsianLow  == 0 || bid < g_AsianLow)  g_AsianLow  = bid;
    }
    if (hour == 7 && minute == 0) // Reset at London open start
    {
        // Keep values — they are now the reference for the day
        // Reset tomorrow at midnight
    }
    if (hour == 0 && minute == 0) // Midnight reset
    {
        g_AsianHigh = 0;
        g_AsianLow  = 0;
    }

HOW — Part 2 (TradingBotEA.mq5 snapshot JSON):
  Find the section that builds the session JSON object.
  Replace the hardcoded 0.00000 values:
    "asian_high": 0.00000  →  "asian_high": g_AsianHigh
    "asian_low":  0.00000  →  "asian_low":  g_AsianLow

HOW — Part 3 (ScalpingSessionService.cs):
  After Asian session levels are in the snapshot, add a scored check:
    var asianHigh = snapshot?.session?.asian_high ?? 0;
    var asianLow  = snapshot?.session?.asian_low  ?? 0;
    var currentPrice = snapshot?.price?.bid ?? 0;

    if (asianHigh > 0 && asianLow > 0)
    {
        // BUY above Asian high = breakout confirmation (+1 point)
        if (request.Direction == TradeDirection.Buy && currentPrice > asianHigh)
        {
            score += 1;
            scoreReasons.Add("Price above Asian session high — London breakout");
        }
        // SELL below Asian low = breakout confirmation (+1 point)
        if (request.Direction == TradeDirection.Sell && currentPrice < asianLow)
        {
            score += 1;
            scoreReasons.Add("Price below Asian session low — London breakout");
        }
    }

VERIFY: Confirm EA compiles in MetaEditor after MQ5 changes.
Confirm C# snapshot deserialization reads asian_high and asian_low.
Confirm build compiles.

----------------------------------------
FIX 12 — LAYER AUDIT LOG PER TRADE  [MODEL: claude-sonnet-4-5-20251001]
----------------------------------------

WHAT: Add a structured layer-by-layer audit log for every trade attempt
showing which checks passed and which failed.

WHY: Current logging captures only the first failing check.
Post-session debugging is blind — you cannot see whether a rejected
trade would have passed Layers 1–5 or failed all of them.

HOW (AutoBotService.cs):
  Create a simple record for collecting layer results:
    private record LayerResult(string Layer, bool Passed, string Reason);

  In ExecuteTradeWithValidationCoreAsync(), create a list at the start:
    var layerLog = new List<LayerResult>();

  After each check in the gate, append to the list. Examples:
    layerLog.Add(new LayerResult("ADX_RANGING", adxM5 >= 20,
        $"ADX={adxM5:F1}"));
    layerLog.Add(new LayerResult("SESSION_GATE", isAllowedSession,
        $"UTC={utcNow:HH:mm}"));
    layerLog.Add(new LayerResult("H1_TREND", h1TrendAligned,
        $"H1={h1Trend}"));
    layerLog.Add(new LayerResult("NEWS", !newsBlocked,
        newsBlocked ? "News blackout active" : "Clear"));
    layerLog.Add(new LayerResult("SPREAD", spreadOk,
        $"Spread={currentSpread}pips"));
    layerLog.Add(new LayerResult("RISK", riskPassed,
        $"RR={rrRatio:F2}"));

  At the end of the method — whether trade executes or is rejected —
  log the full layer summary:
    var passed = layerLog.Count(l => l.Passed);
    var failed = layerLog.Count(l => !l.Passed);
    _logger.Information(
        "TRADE_AUDIT | Symbol: {Symbol} | Direction: {Dir} | Layers: {Passed}/{Total} passed | " +
        "Failed: [{Failed}] | Executed: {Executed}",
        request.Symbol,
        request.Direction,
        passed,
        layerLog.Count,
        string.Join(", ", layerLog.Where(l => !l.Passed).Select(l => l.Layer)),
        tradeExecuted);

VERIFY: Confirm log line appears in output for both executed and
rejected trades. Confirm no existing logging is removed.
Confirm build compiles.

----------------------------------------
FIX 13 — VWAP CALCULATION          [MODEL: opus-4-5]  ← SWITCH REQUIRED
----------------------------------------

  *** STOP — TYPE /model opus-4-5 IN TERMINAL THEN SAY "ready" ***

WHAT: Implement session VWAP calculation from the current trading day's
price data and expose it in the snapshot.

WHY: VWAP is the institutional intraday mean. Price above VWAP = buyers
in control. Price below VWAP = sellers in control. It is a primary
reference for institutional order flow — currently absent from the bot.

HOW — Part 1 (TradingBotEA.mq5):
  Add VWAP tracking variables:
    double g_VwapNumerator   = 0;  // cumulative (price × volume)
    double g_VwapDenominator = 0;  // cumulative volume
    double g_Vwap            = 0;

  Reset at session start (00:00 UTC):
    if (hour == 0 && minute == 0)
    {
        g_VwapNumerator   = 0;
        g_VwapDenominator = 0;
        g_Vwap            = 0;
    }

  On every tick, update VWAP:
    double typicalPrice = (SymbolInfoDouble(_Symbol, SYMBOL_BID) +
                           SymbolInfoDouble(_Symbol, SYMBOL_ASK) +
                           iClose(_Symbol, PERIOD_M1, 0)) / 3.0;
    double tickVol = (double)iVolume(_Symbol, PERIOD_M1, 0);
    if (tickVol > 0)
    {
        g_VwapNumerator   += typicalPrice * tickVol;
        g_VwapDenominator += tickVol;
        g_Vwap = g_VwapDenominator > 0
            ? g_VwapNumerator / g_VwapDenominator
            : typicalPrice;
    }

  Expose in session JSON:
    "vwap": g_Vwap

HOW — Part 2 (ScalpingSessionService.cs):
  Add VWAP bias as a scored check:
    var vwap = snapshot?.session?.vwap ?? 0;
    var bid  = snapshot?.price?.bid ?? 0;
    if (vwap > 0 && bid > 0)
    {
        var aboveVwap = bid > vwap;
        var vwapAligned = (request.Direction == TradeDirection.Buy  && aboveVwap) ||
                          (request.Direction == TradeDirection.Sell && !aboveVwap);
        if (vwapAligned)
        {
            score += 1;
            scoreReasons.Add($"Price {(aboveVwap ? "above" : "below")} VWAP — institutional bias aligned");
        }
        else
        {
            _logger.Debug("VWAP_COUNTER | Price vs VWAP misaligned | VWAP: {Vwap} | Bid: {Bid}",
                vwap, bid);
        }
    }

VERIFY: Confirm VWAP resets correctly at midnight. Confirm it does not
reset at London open (it is a daily VWAP, not session VWAP).
Confirm EA compiles. Confirm C# build compiles.

  *** SWITCH BACK — TYPE /model claude-sonnet-4-5-20251001 IN TERMINAL ***

----------------------------------------
FIX 14 — H1 TREND REVERSAL MANAGEMENT  [MODEL: opus-4-5]  ← SWITCH REQUIRED
----------------------------------------

  *** STOP — TYPE /model opus-4-5 IN TERMINAL THEN SAY "ready" ***

WHAT: When H1 trend reverses while a position is open, move SL to
breakeven immediately and log a trend reversal warning.

WHY: Currently a position entered on a bullish H1 is held to original
TP even if H1 reverses to bearish mid-trade. The bot has no awareness
of changing conditions after entry.

HOW (AutoBotService.cs — heartbeat monitoring loop):
  Location: the heartbeat method that polls open positions
  (line ~481 area — the method polling drawdown, BE, trailing)

  For each open position, fetch the latest snapshot:
    var latestSnapshot = await _mt5Bridge.GetSnapshotAsync(position.Symbol);
    var currentH1Trend = latestSnapshot?.structure?.trend_h1 ?? "UNKNOWN";

  Compare against the trend at entry time. The entry trend must be
  stored when the trade is opened:
    // When trade opens — store entry trend on the position record
    position.EntryH1Trend = snapshot?.structure?.trend_h1;

  In the heartbeat loop check:
    if (position.EntryH1Trend != null &&
        currentH1Trend != "UNKNOWN" &&
        currentH1Trend != position.EntryH1Trend)
    {
        _logger.Warning(
            "TREND_REVERSAL | Symbol: {Symbol} | Ticket: {Ticket} | " +
            "EntryTrend: {Entry} | CurrentTrend: {Current} — moving SL to breakeven",
            position.Symbol, position.Ticket,
            position.EntryH1Trend, currentH1Trend);

        // Move SL to breakeven (entry price)
        await _mt5Bridge.ModifyPositionAsync(position.Ticket,
            newSl: position.EntryPrice,
            newTp: position.TakeProfit);
    }

NOTE: This requires adding EntryH1Trend to the position tracking model.
Find the position model class and add:
    public string? EntryH1Trend { get; set; }

Check what position model the heartbeat currently uses and add this
field there. Do not create a new class.

VERIFY: Confirm position model has EntryH1Trend field.
Confirm heartbeat loop calls GetSnapshotAsync correctly.
Confirm ModifyPositionAsync signature matches existing usage in codebase.
Confirm build compiles.

  *** SWITCH BACK — TYPE /model claude-sonnet-4-5-20251001 IN TERMINAL ***

==================================================
AFTER ALL FIXES — FINAL CHECKLIST
==================================================

When all 14 fixes are complete, run this checklist:

[ ] Build compiles with zero errors
[ ] All new checks appear BEFORE OrderSend() in every code path
[ ] Search codebase: confirm OrderSend() has no reachable path
    that skips ADX check, session check, and H1 trend check
[ ] Run bot in demo for 48 hours and review:
    - TRADE_AUDIT log lines — confirm layers are logging
    - SCALP_BLOCKED lines — confirm ADX filter is firing
    - SESSION_TIME_BLOCK lines — confirm Asia-hour blocks work
    - ROUND_NUMBER_WARNING lines — review TP/SL placement
[ ] Confirm _atrHistory queue in ScalpingSessionService has
    data after 10+ signals (check via debug log if needed)
[ ] Confirm ExpiryMinutes is never 0 for any scalp signal
[ ] Confirm news_currencies has exactly one definition in settings.json
[ ] Re-run the full audit prompt against the updated codebase
    and confirm verdict improves from DEMO ONLY to SMALL LIVE READY

==================================================
MODEL REFERENCE CARD
==================================================

  Haiku  (claude-haiku-4-5-20251001)  → Fix 4, Fix 8 only
  Sonnet (claude-sonnet-4-5-20251001) → Fix 1,2,3,5,6,7,9,10,11,12
  Opus   (opus-4-5)                   → Fix 13, Fix 14 only

  To switch in VS Code terminal:
    /model claude-haiku-4-5-20251001
    /model claude-sonnet-4-5-20251001
    /model opus-4-5

  Claude cannot switch models itself. When you see:
    *** STOP — TYPE /model ... IN TERMINAL THEN SAY "ready" ***
  You must type the command manually before Claude continues.