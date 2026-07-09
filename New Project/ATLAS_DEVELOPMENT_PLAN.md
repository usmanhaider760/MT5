# ATLAS Development Task Tracker
# HOW TO USE: Type "next" → I read this file → implement the first [ ] task → mark it [x] → commit

---

## CURRENT STATUS
**Last completed:** P4-4 — Add System_Log_Service with log levels (Phase 4 complete — ALL 26 TASKS DONE)
**Up next:** — nothing left on this list; see COMPLETION STATUS below

---

## TASK LIST (work top-to-bottom, never skip)

### PHASE 0 — CRITICAL FIXES (must complete before any live/forward testing)

- [x] **P0-1** — Wire live account state into Risk_Manager
- [x] **P0-2** — Connect news filter to economic calendar service
- [x] **P0-3** — Fix lot size: round DOWN not nearest
- [x] **P0-4** — Enforce broker lot step in lot calculation
- [x] **P0-5** — Fix Trade_Result_BO R_Multiple wrong for Gold and JPY
- [x] **P0-6** — Fix Emergency Stop mode never recovers from caution/recovery

### PHASE 1 — HIGH PRIORITY (complete before live trading)

- [x] **P1-1** — Create Strategy_Performance_Repository and persist to DB
- [x] **P1-2** — Add all risk parameters to appsettings.json + Atlas_Config
- [x] **P1-3** — Fix dashboard session label to use Trading_Constants (not hardcoded hours)
- [x] **P1-4** — Add max lot cap to Risk_Manager and appsettings
- [x] **P1-5** — Load trade history from DB into Performance_Monitor on startup

### PHASE 2 — MEDIUM PRIORITY (complete before extended live use)

- [x] **P2-1** — Fix Signal_Reject_Reason: assign correct enum per risk check
- [x] **P2-2** — Add request correlation IDs to MT5 bridge
- [x] **P2-3** — Add Modify_Take_Profit support to execution layer
- [x] **P2-4** — Secure credentials: env-var override for Telegram token and email password
- [x] **P2-5** — Fix dual MT5_Bridge_Client competition (ping monitor vs pipeline)
- [x] **P2-6** — Add Ai_Signal_Filter stub wired into pipeline

### PHASE 3 — TEST COVERAGE (required before live trading)

- [x] **P3-1** — Strategy signal tests: all 5 strategies (A–E)
- [x] **P3-2** — Market_Regime_Detector tests
- [x] **P3-3** — News_Filter_Service tests
- [x] **P3-4** — Emergency_Stop_Service tests
- [x] **P3-5** — MT5 bridge mock integration tests

### PHASE 4 — CLEANUP (nice-to-have)

- [x] **P4-1** — Remove legacy AIForexBot project from solution
- [x] **P4-2** — Extract Market_Context_Builder as discrete service
- [x] **P4-3** — Add Exit_Reason_Enum to replace Close_Reason string
- [x] **P4-4** — Add System_Log_Service with log levels

---

## TASK DETAILS
# (full spec for each task — I read these when implementing)

---

### P0-1 — Wire live account state into Risk_Manager
**Problem:** Risk_Manager._account starts as empty new Account_State_BO(). All daily/weekly/drawdown checks use zero-balance data. Risk limits never trigger in production.
**Root cause:** MT5_Account_Service fetches live data for the UI only. Pipeline Risk_Manager never receives it.

**Changes:**
1. `src/Atlas_Domain/Interfaces/I_Risk_Manager.cs`
   - Add to interface: `void Update_Account_State(Account_State_BO state);`

2. `src/Atlas_Execution/MT5/MT5_Account_Service.cs`
   - Verify Get_Account_State_Async() returns Day_Open_Balance, Week_Open_Balance, Peak_Equity, Consecutive_Losses
   - If missing: track Day_Open_Balance as balance on first call of the UTC day (store last-seen date + balance)

3. `src/Atlas_Application/Services/Trade_Pipeline_Service.cs`
   - Add field: `private readonly MT5_Account_Service? _account_service;`
   - Add to constructor parameters: `MT5_Account_Service? account_service = null`
   - Assign in constructor body: `_account_service = account_service;`
   - In Run_Cycle_Async(), BEFORE `var account = await _risk_manager.Get_Account_State_Async();` add:
     ```csharp
     if (_account_service != null)
     {
         int open_count = open_positions?.Count ?? 0;
         var live = await _account_service.Get_Account_State_Async(open_trade_count: open_count);
         _risk_manager.Update_Account_State(live);
     }
     ```
   - NOTE: open_positions is fetched after account in current code — reorder: fetch positions first, then account with open_count

4. `src/Atlas_Application/Services/Atlas_Bot_Bootstrap.cs`
   - Pass `account_svc` to Trade_Pipeline_Service constructor

**Tests to add in `src/Atlas_Tests/Risk_Manager_Tests.cs`:**
- Test: Validate_Trade_Risk with account showing 1% daily loss and max 0.75% limit → rejected
- Test: Validate_Trade_Risk with account showing 5% drawdown and 5% circuit breaker → rejected

---

### P0-2 — Connect news filter to economic calendar service
**Problem:** News_Filter_Service._calendar is always empty in production. Pipeline news lockout never fires. Bot trades through NFP, FOMC, CPI.
**Root cause:** Economic_Calendar_Service and News_Filter_Service are parallel and disconnected.

**Changes:**
1. `src/Atlas_Market_Data/Services/News_Filter_Service.cs`
   - Add field: `private readonly Economic_Calendar_Service? _calendar_service;`
   - Add constructor: `News_Filter_Service(Economic_Calendar_Service? calendar_service = null)`
   - In Is_News_Lockout_Active_Async(), replace `_calendar` lookup with:
     ```csharp
     List<News_Event_BO> events;
     if (_calendar_service != null)
         events = await _calendar_service.Get_High_Impact_Events_Async(24);
     else
         events = _calendar.Where(e => e.Is_High_Impact).ToList();
     ```
   - Use `events` instead of `_calendar` for the foreach loop

2. `src/Atlas_Application/Services/Atlas_Bot_Bootstrap.cs`
   - Pass `calendar` (Economic_Calendar_Service) into `News_Filter_Service` constructor

**Tests to add in `src/Atlas_Tests/News_Filter_Integration_Tests.cs`:**
- Seed calendar with NFP 30 minutes from now → EURUSD blocked
- Seed calendar with FOMC 90 minutes from now → XAUUSD blocked (120 min window)
- Seed calendar with past event (2 hours ago, 30 min after window) → not blocked
- Seed calendar with AUD event → EURUSD not blocked

---

### P0-3 — Fix lot size: round DOWN not nearest
**Problem:** Math.Round(lot_size, 2) rounds 0.125 → 0.13, overexposing the account. Spec requires round DOWN.

**Changes:**
1. `src/Atlas_Risk/Services/Risk_Manager.cs` — Calculate_Lot_Size(), last line:
   ```csharp
   // BEFORE:
   return Math.Round(lot_size, 2);
   // AFTER:
   return Math.Floor(lot_size * 100) / 100;
   ```

**Tests to update in `src/Atlas_Tests/Risk_Manager_Tests.cs`:**
- Rename existing round test to: Calculate_Lot_Size_Floors_Not_Rounds
- Add case: equity=10000, risk=1.25%, sl=33, pip=10 → lot=0.037... → floors to 0.03
- Add case: lot=0.125 exactly → floors to 0.12

---

### P0-4 — Enforce broker lot step in lot calculation
**Problem:** Market_Symbol_BO.Lot_Step exists but is never applied. Lot 0.127 with step 0.01 should be 0.12, not 0.13.

**Changes:**
1. `src/Atlas_Domain/BusinessObjects/Market_Symbol_BO.cs`
   - Ensure default: `public decimal Lot_Step { get; set; } = 0.01m;`
   - In Default_Universe(), confirm all entries have Lot_Step set (add if absent)

2. `src/Atlas_Risk/Services/Risk_Manager.cs` — Calculate_Lot_Size():
   - Add parameter: `decimal lot_step = 0.01m`
   - Replace floor line:
     ```csharp
     if (lot_step <= 0) lot_step = 0.01m;
     decimal floored = Math.Floor(lot_size / lot_step) * lot_step;
     return Math.Round(floored, 8); // remove floating-point noise
     ```

3. `src/Atlas_Application/Services/Trade_Pipeline_Service.cs` — Evaluate_Symbol_Async():
   - Update Calculate_Lot_Size call to pass symbol.Lot_Step:
     ```csharp
     decimal lot = _risk_manager.Calculate_Lot_Size(
         account.Equity, risk_pct, signal.Stop_Loss_Pips,
         symbol.Pip_Value_Per_Lot, symbol.Lot_Step);
     ```

**Tests to add:**
- lot_step=0.01, raw lot=0.127 → returns 0.12
- lot_step=0.10, raw lot=0.87 → returns 0.80
- lot_step=0.01, raw lot=0.01 → returns 0.01 (minimum)

---

### P0-5 — Fix Trade_Result_BO R_Multiple wrong for Gold and JPY
**Problem:** Trade_Result_BO.R_Multiple uses hardcoded * 10m. Gold pip value is $100/lot so R appears 10× too small. USDJPY is ~$9/lot so slightly wrong.

**Changes:**
1. `src/Atlas_Domain/BusinessObjects/Trade_Result_BO.cs`
   - Add property: `public decimal Pip_Value_Per_Lot { get; set; } = 10m;`
   - Replace R_Multiple computed property:
     ```csharp
     public decimal R_Multiple =>
         (Initial_Stop_Distance_Pips > 0 && Pip_Value_Per_Lot > 0 && Lot_Size > 0)
             ? Math.Round(Net_PnL_Currency / (Initial_Stop_Distance_Pips * Lot_Size * Pip_Value_Per_Lot), 3)
             : 0;
     ```

2. `src/Atlas_Data_Access/Database_Schema.cs`
   - Add migration in Ensure_Created() try/catch block:
     ```csharp
     try { conn.Execute("ALTER TABLE trade_results ADD COLUMN pip_value_per_lot REAL NOT NULL DEFAULT 10.0;"); } catch { }
     ```

3. `src/Atlas_Data_Access/Repositories/Trade_Result_Repository.cs`
   - Add pip_value_per_lot to INSERT statement
   - Add pip_value_per_lot to SELECT mapping

4. `src/Atlas_Application/Backtest/Backtest_Engine.cs`
   - When creating Trade_Result_BO from a closed simulated position, set Pip_Value_Per_Lot from the symbol config

5. Wherever else Trade_Result_BO is constructed:
   - Search solution for `new Trade_Result_BO` → set Pip_Value_Per_Lot from Market_Symbol_BO

**Tests to add in `src/Atlas_Tests/Risk_Manager_Tests.cs` (or new file):**
- Gold: SL=10 pips, lot=0.1, gross_pnl=+$100, pip_value=100 → R = +1.000
- EURUSD: SL=20 pips, lot=0.5, gross_pnl=+$100, pip_value=10 → R = +1.000

---

### P0-6 — Fix Emergency Stop mode never recovers
**Problem:** Emergency_Stop_Service.Evaluate_Drawdown_Mode_Async() else branch:
  `new_mode = Current_Mode == Emergency_Stop ? Current_Mode : Current_Mode;`
Both branches return Current_Mode — total no-op. Mode never transitions back to normal when drawdown recovers.

**Changes:**
1. `src/Atlas_Execution/Services/Emergency_Stop_Service.cs`
   - Add field: `private Bot_Mode_Type _base_mode = Bot_Mode_Type.Demo;`
   - Add method: `public void Set_Base_Mode(Bot_Mode_Type mode) => _base_mode = mode;`
   - Replace Evaluate_Drawdown_Mode_Async() body:
     ```csharp
     if (Kill_Switch_Active) return Task.CompletedTask;
     var dd = account.Drawdown_From_Peak;
     Bot_Mode_Type new_mode;
     if      (dd >= risk_settings.Full_Stop_Drawdown_Percent)    new_mode = Bot_Mode_Type.Emergency_Stop;
     else if (dd >= risk_settings.Protection_Drawdown_Percent)   new_mode = Bot_Mode_Type.Emergency_Stop;
     else if (dd >= risk_settings.Recovery_Drawdown_Percent)     new_mode = Bot_Mode_Type.Micro_Live;
     else if (dd >= risk_settings.Caution_Drawdown_Percent)      new_mode = Bot_Mode_Type.Demo;
     else                                                         new_mode = _base_mode;
     if (new_mode != Current_Mode)
     {
         Current_Mode = new_mode;
         On_Mode_Changed?.Invoke(new_mode);
     }
     return Task.CompletedTask;
     ```

2. `src/Atlas_Application/Services/Atlas_Bot_Bootstrap.cs`
   - After `var emergency_stop = new Emergency_Stop_Service();` add:
     `emergency_stop.Set_Base_Mode(risk_settings.Mode);`

**Tests to add in `src/Atlas_Tests/Emergency_Stop_Tests.cs` (new file):**
- DD=0% → mode stays at base (Demo)
- DD=2.5% (caution) → mode becomes Demo
- DD=4.5% (recovery) → mode becomes Micro_Live
- DD=6.5% (protection/full stop) → mode becomes Emergency_Stop, kill switch NOT active
- DD=8.5% (full stop) → Emergency_Stop via Drawdown_Guard.Evaluate_And_Apply_Async
- After DD=4.5% → drops to 1% → mode returns to base

---

### P1-1 — Create Strategy_Performance_Repository and persist to DB
**Problem:** strategy_performance table exists in DB but nothing writes to it. Performance history lost on restart. Auto-disable has no historical basis after restart.

**Changes:**
1. Create `src/Atlas_Data_Access/Repositories/Strategy_Performance_Repository.cs`
   - Methods: Save_Snapshot_Async(Strategy_Performance_BO), Get_All_Async(), Get_By_Strategy_Async(Strategy_Type)
   - Use Dapper + SqliteConnection like existing repositories
   - Map all columns in strategy_performance table

2. `src/Atlas_Application/Services/Performance_Monitor_Service.cs`
   - Add optional field: `private readonly Strategy_Performance_Repository? _repo;`
   - Add constructor overload accepting `Strategy_Performance_Repository? repo = null`
   - In Update_Strategy_Performance(), after updating perf object:
     `if (_repo != null) _ = _repo.Save_Snapshot_Async(perf);`
   - Add method: `Task Load_From_Repository_Async()` — loads all snapshots, reconstructs _performance dict

3. `src/Atlas_Application/Services/Atlas_Bot_Bootstrap.cs`
   - Create `Strategy_Performance_Repository`
   - Pass to Performance_Monitor_Service
   - After Create() wiring: call `await perf_monitor.Load_From_Repository_Async();`

**Tests to add in `src/Atlas_Tests/Performance_Stats_Tests.cs`:**
- Save snapshot → reload from DB → performance matches original

---

### P1-2 — Add all risk parameters to appsettings.json + Atlas_Config
**Problem:** Only 3 of 15 risk settings are in appsettings.json. Operators must recompile to change weekly loss %, max trades, quality score thresholds etc.

**Changes:**
1. `src/Atlas_WinForms/appsettings.json` — add full Risk section:
   ```json
   "Risk": {
     "ForexRiskPct": 0.25,
     "GoldRiskPct": 0.20,
     "MaxDailyLossPct": 0.75,
     "MaxWeeklyLossPct": 2.0,
     "MaxDrawdownPct": 5.0,
     "FullStopDrawdownPct": 8.0,
     "CautionDrawdownPct": 2.0,
     "RecoveryDrawdownPct": 4.0,
     "ProtectionDrawdownPct": 6.0,
     "MaxOpenTrades": 2,
     "MaxGoldTrades": 1,
     "MaxConsecutiveLosses": 2,
     "MinQualityScoreLive": 85,
     "MinQualityScoreGold": 85,
     "MinRRForex": 1.8,
     "MinRRGoldSwing": 2.0,
     "MinRRIntraday": 1.5,
     "MaxLotForex": 5.0,
     "MaxLotGold": 1.0
   }
   ```

2. `src/Atlas_WinForms/Atlas_Config.cs` — add all missing properties with correct defaults

3. `src/Atlas_WinForms/Forms/Atlas_Dashboard.cs` — Initialize_Controller():
   - Populate all fields of risk_override from the new Atlas_Config properties

---

### P1-3 — Fix dashboard session label to use Trading_Constants
**Problem:** Atlas_Dashboard.Update_Session_Label() hardcodes h >= 8 for London. Trading_Constants.London_Open = 7:00. Dashboard shows wrong session.

**Changes:**
1. `src/Atlas_WinForms/Forms/Atlas_Dashboard.cs` — Update_Session_Label():
   - Replace the hardcoded hour logic with calls using Trading_Constants values:
     ```csharp
     var now  = DateTime.UtcNow;
     var time = TimeOnly.FromTimeSpan(now.TimeOfDay);
     bool ldn     = time >= Trading_Constants.London_Open && time < Trading_Constants.London_Close;
     bool ny      = time >= Trading_Constants.NY_Open     && time < Trading_Constants.NY_Close;
     bool overlap = ldn && ny;
     // Then build countdown strings using the same constant times
     ```

---

### P1-4 — Add max lot cap
**Problem:** No maximum lot size. Large accounts or misconfigured risk % generate dangerous lot sizes with no ceiling.

**Changes:**
1. `src/Atlas_Domain/BusinessObjects/Risk_Setting_BO.cs`
   - Add: `public decimal Max_Lot_Size_Forex { get; set; } = 5.0m;`
   - Add: `public decimal Max_Lot_Size_Gold  { get; set; } = 1.0m;`

2. `src/Atlas_Risk/Services/Risk_Manager.cs` — Calculate_Lot_Size():
   - Add parameter: `decimal max_lot = 5.0m`
   - Before return: `floored = Math.Min(floored, max_lot);`

3. `src/Atlas_Application/Services/Trade_Pipeline_Service.cs`
   - Pass max lot to Calculate_Lot_Size:
     ```csharp
     decimal max_lot = is_gold ? _risk_settings.Max_Lot_Size_Gold : _risk_settings.Max_Lot_Size_Forex;
     decimal lot = _risk_manager.Calculate_Lot_Size(
         account.Equity, risk_pct, signal.Stop_Loss_Pips,
         symbol.Pip_Value_Per_Lot, symbol.Lot_Step, max_lot);
     ```

**Tests:** lot on $1M account capped at max_lot_forex=5.0

---

### P1-5 — Load trade history from DB into Performance_Monitor on startup
**Problem:** Performance_Monitor_Service._results starts empty each restart. Equity curve, Sharpe, Sortino all reset to zero.

**Changes:**
1. `src/Atlas_Application/Services/Performance_Monitor_Service.cs`
   - Add method:
     ```csharp
     public async Task Load_History_From_Db_Async(Trade_Result_Repository repo)
     {
         var trades = await repo.Get_All_Results_Async();
         _results.Clear();
         _performance.Clear();
         foreach (var t in trades.OrderBy(t => t.Closed_At_UTC))
             Record_Trade_Result(t);
     }
     ```

2. `src/Atlas_Application/Services/Atlas_Bot_Bootstrap.cs`
   - After wiring, call: `await perf_monitor.Load_History_From_Db_Async(result_repo);`

---

### P2-1 — Fix Signal_Reject_Reason: assign correct enum per risk check
**Problem:** Pipeline assigns Daily_Loss_Limit_Reached for ALL risk failures (max trades, consecutive losses, R:R failures, etc.)

**Changes:**
1. `src/Atlas_Domain/Enums/Signal_Reject_Reason.cs` — add values:
   Weekly_Loss_Limit_Reached, Max_Open_Trades_Reached, Max_Gold_Trades_Reached,
   Consecutive_Loss_Pause, Minimum_RR_Not_Met, Same_Symbol_Already_Open, Drawdown_Circuit_Breaker

2. `src/Atlas_Domain/Interfaces/I_Risk_Manager.cs` — change Validate_Trade_Risk_Async return type:
   `Task<(bool Approved, Signal_Reject_Reason Reason, string Detail)>`

3. `src/Atlas_Risk/Services/Risk_Manager.cs` — Validate_Trade_Risk_Async():
   - Each check returns its specific Signal_Reject_Reason instead of a string

4. `src/Atlas_Application/Services/Trade_Pipeline_Service.cs`
   - Update call site to use the new return type and assign signal.Reject_Reason correctly

---

### P2-2 — Add request correlation IDs to MT5 bridge
**Problem:** No way to match responses to requests. No debuggability when bridge sends unexpected data.

**Changes:**
1. `src/Atlas_Execution/Protocol/MT5_Message.cs`
   - Add to MT5_Request: `public string Req_Id { get; set; } = Guid.NewGuid().ToString("N")[..8];`
   - Add to MT5_Response: `public string? Req_Id { get; set; }`

2. `MQL5/ATLAS_Bridge.mq5`
   - In Dispatch(): parse req_id with Json_Get(req, "req_id")
   - Prepend to every response: `"\"req_id\":\"" + req_id + "\","` inside the JSON

3. `src/Atlas_Execution/MT5/MT5_Bridge_Client.cs`
   - After deserialization, optionally log if req_id doesn't match (non-blocking)

---

### P2-3 — Add Modify_Take_Profit support
**Problem:** I_Execution_Service has no TP modification method. Cannot adjust targets after entry.

**Changes:**
1. `src/Atlas_Domain/Interfaces/I_Execution_Service.cs`
   - Add: `Task<bool> Modify_Take_Profit_Async(long ticket, decimal new_take_profit);`

2. `src/Atlas_Execution/MT5/MT5_Execution_Service.cs`
   - Implement Modify_Take_Profit_Async using MT5_Command.MODIFY_TP

3. `src/Atlas_Execution/Protocol/MT5_Message.cs`
   - Add: `MODIFY_TP` to MT5_Command constants

4. `MQL5/ATLAS_Bridge.mq5`
   - Add MODIFY_TP handler in Dispatch() and implement Modify_TP() function

---

### P2-4 — Secure credentials via environment variable override
**Problem:** Email password and Telegram token stored in plaintext appsettings.json.

**Changes:**
1. `src/Atlas_WinForms/Atlas_Config.cs` — override sensitive values from env vars:
   ```csharp
   public static string Telegram_Bot_Token =>
       Environment.GetEnvironmentVariable("ATLAS_TELEGRAM_TOKEN")
       ?? _config["Telegram:BotToken"] ?? string.Empty;
   public static string Email_Password =>
       Environment.GetEnvironmentVariable("ATLAS_EMAIL_PASSWORD")
       ?? _config["Email:Password"] ?? string.Empty;
   ```

2. Add `appsettings.json` to `.gitignore` and create `appsettings.template.json` with empty credential fields

3. `src/Atlas_WinForms/Forms/Atlas_Settings_Form.cs`
   - Mask password/token fields with PasswordChar = '*'
   - Add hint label: "Use ATLAS_TELEGRAM_TOKEN / ATLAS_EMAIL_PASSWORD env vars for security"

---

### P2-5 — Fix dual MT5_Bridge_Client competition
**Problem:** Connection_Status_Panel creates its own MT5_Bridge_Client. Both it and the pipeline compete for the same single-connection EA port.

**Changes:**
1. `src/Atlas_Application/Services/Atlas_Bot_Bootstrap.cs`
   - Return `bridge` from Create(): change return tuple to include bridge:
     ```csharp
     return (controller, db, account_svc, bridge);
     ```

2. `src/Atlas_WinForms/Forms/Atlas_Dashboard.cs` — Initialize_Controller():
   - Use the bridge returned from Bootstrap instead of creating `new MT5_Bridge_Client(host, port)`
   - Pass the shared bridge to Connection_Status_Panel

3. `src/Atlas_WinForms/Forms/Connection_Status_Panel.cs`
   - Update constructor to accept an existing MT5_Bridge_Client (remove internal new() call)

---

### P2-6 — Add Ai_Signal_Filter stub wired into pipeline
**Problem:** Specification requires Ai_Signal_Filter. Component completely absent.

**Changes:**
1. Create `src/Atlas_Domain/Interfaces/I_Ai_Signal_Filter.cs`:
   ```csharp
   public interface I_Ai_Signal_Filter
   {
       Task<(bool Approved, int Confidence_Pct, string Reason)>
           Evaluate_Async(Trade_Signal_BO signal, Market_Context_BO context);
   }
   ```

2. Create `src/Atlas_Application/Services/Ai_Signal_Filter.cs`:
   - Implements I_Ai_Signal_Filter
   - Default implementation: returns (true, 100, "Pass-through — no AI model configured")
   - Add virtual so it can be subclassed with a real model later

3. `src/Atlas_Application/Services/Trade_Pipeline_Service.cs`:
   - Add optional: `private readonly I_Ai_Signal_Filter? _ai_filter;`
   - After quality score gate, before risk validation:
     ```csharp
     if (_ai_filter != null)
     {
         var (ai_ok, ai_conf, ai_reason) = await _ai_filter.Evaluate_Async(signal, context);
         if (!ai_ok)
         {
             signal.Is_Approved = false;
             signal.Reject_Reason = Signal_Reject_Reason.Low_Quality_Score;
             signal.Reject_Detail = $"AI filter rejected: {ai_reason}";
             On_Signal_Rejected?.Invoke(signal);
             continue;
         }
     }
     ```

4. `src/Atlas_Application/Services/Atlas_Bot_Bootstrap.cs`:
   - Optionally accept I_Ai_Signal_Filter parameter in Create() (null by default)

---

### P3-1 — Strategy signal tests: all 5 strategies
**Problem:** 0% test coverage for strategy entry/exit/rejection logic.

**Create:** `src/Atlas_Tests/Strategy_Signal_Tests.cs`

**Tests for Strategy A (Trend Pullback):**
- Buy signal: higher TFs aligned Buy, last M15 pulled back to EMA50-H1 zone, bullish rejection candle → signal generated
- Sell signal: mirror case → signal generated
- No signal: regime is Range (wrong regime)
- No signal: TFs not aligned
- No signal: no pullback (price far from EMAs)
- Signal SL placed below M15.Low minus ATR buffer
- Signal TP gives ≥ 2.0R

**Tests for Strategy B (Session Breakout):**
- Bullish breakout: regime Compression, London session, M15 closes above Asian high with retest → signal
- Bearish breakout: mirror case → signal
- No signal: Asian session active (wrong session)
- No signal: range too wide (> 1.5× ATR)
- No signal: range too compressed (< 0.3× ATR)

**Tests for Strategy C (Liquidity Sweep):**
- Bullish sweep: prev bar's Low < H1 range low, current bar closes back above H1 low, is bullish → signal
- Bearish sweep: mirror case → signal
- Gold confirmation: requires close beyond prev Open
- No signal: regime Trend_Swing (incompatible)

**Tests for Strategy D (Breakout Retest):**
- Bullish retest: prev M15 broke above H4[^3].High, current bar touches level and holds with bullish close → signal
- Bearish retest: mirror → signal
- No signal: no retest (price never returned to level)

**Tests for Strategy E (Mean Reversion):**
- Buy: Range regime, RSI < 30, price below EMA20-H1, bullish M15 close → signal
- Sell: RSI > 70, price above EMA20, bearish close → signal
- No signal: TFs aligned (contradicts mean reversion)
- No signal: R:R < 1.5 even if all other conditions met

**Helper:** Create a static TestDataFactory class in Atlas_Tests producing synthetic Candle_BO lists with known EMA/ATR properties.

---

### P3-2 — Market_Regime_Detector tests
**Problem:** 0% test coverage for regime classification.

**Create:** `src/Atlas_Tests/Market_Regime_Detector_Tests.cs`

**Tests:**
- Trend_Swing detected: D1/H4/H1 all aligned bullish, HH/HL structure clear, normal ATR → Trend_Swing
- Compression_Breakout detected: range < 2× ATR, no clear direction → Compression_Breakout
- Range detected: no direction, volatility normal, not compressed → Range
- Abnormal_No_Trade: fewer than 20 D1 candles → Abnormal_No_Trade
- D1 bias Buy: last D1 close > EMA200-D1 → D1_Bias == Buy
- H4 bias Sell: H4 below EMA200 AND LH/LL structure → H4_Bias == Sell
- Higher_Timeframes_Aligned true: D1=Buy, H4=Buy, H1=Buy
- Score components: Score_Trend_Alignment + Score_Volatility + etc. sum correctly

---

### P3-3 — News_Filter_Service tests
**Problem:** 0% test coverage. Bug in P0-2 was invisible because there were no tests.

**Create:** `src/Atlas_Tests/News_Filter_Integration_Tests.cs`

**Tests:**
- NFP in 30 min: EURUSD blocked (60-min before window)
- NFP 2h ago: EURUSD NOT blocked (90-min after window elapsed)
- FOMC in 60 min: XAUUSD blocked (120-min before window)
- CPI in 45 min: GBPUSD blocked (60-min before window)
- Standard High Impact in 20 min: any symbol blocked (30-min before window)
- AUD RBA event: EURUSD NOT blocked (wrong currency)
- Low impact event: not blocked regardless of timing
- Gold FOMC: XAUUSD blocked 180 min before (Gold has extended window)

---

### P3-4 — Emergency_Stop_Service tests
**Problem:** 0% test coverage for kill switch and mode transitions.

**Create:** `src/Atlas_Tests/Emergency_Stop_Tests.cs`

**Tests:**
- Kill_Switch_Active starts false
- Activate_Emergency_Stop sets Kill_Switch_Active = true and Current_Mode = Emergency_Stop
- On_Emergency_Stop event fires once with correct reason string
- Set_Base_Mode(Live) → normal DD (0%) → Current_Mode = Live
- DD 2.5% (caution threshold 2%) → Current_Mode = Demo
- DD 4.5% (recovery threshold 4%) → Current_Mode = Micro_Live
- DD 7% (protection threshold 6%) → Current_Mode = Emergency_Stop
- Recovery: DD was 4.5%, drops to 1% → Current_Mode returns to base
- On_Mode_Changed fires each transition
- Reset_Kill_Switch: Kill_Switch_Active = false, mode returns to base

---

### P3-5 — MT5 bridge mock integration tests
**Problem:** 0% test coverage for TCP communication layer.

**Create:** `src/Atlas_Tests/MT5_Bridge_Mock_Tests.cs`

**Approach:** Inner class `Mock_MT5_Server` — starts a local TcpListener, reads one JSON line, writes a canned response, closes.

**Tests:**
- Connect: bridge connects to mock server → Is_Connected = true
- GET_TICK: sends request, mock returns tick JSON → bid/ask/spread populated correctly
- GET_CANDLES: mock returns 3-candle array → List<Candle_BO> with correct OHLCV
- GET_POSITIONS: mock returns 2 positions → List<Position_BO> with correct fields
- SEND_ORDER: mock returns {status:ok, ticket:12345} → (true, 12345, "...")
- SEND_ORDER failure: mock returns {status:error, error:"Insufficient margin"} → (false, 0, "Insufficient margin")
- Demo mode: Send_Order_Async returns simulated ticket WITHOUT calling bridge
- Bridge disconnect: mock closes connection immediately → Send_Async returns null
- Reconnect: bridge auto-reconnects on second Send_Async call after disconnect

---

### P4-1 — Remove legacy AIForexBot project
**Problem:** Old project still in solution, adds confusion and build noise.

**Changes:**
1. Delete directory: `AIForexBot/`
2. Remove project reference from `ATLAS.sln`
3. Verify `dotnet build ATLAS.sln` succeeds

---

### P4-2 — Extract Market_Context_Builder as discrete service
**Problem:** Context enrichment logic (score computation, session/spread/news/correlation injection) is embedded inline in Trade_Pipeline_Service.Evaluate_Symbol_Async(). Not testable in isolation.

**Changes:**
1. Create `src/Atlas_Market_Data/Services/Market_Context_Builder.cs`
   - Method: `Market_Context_BO Enrich(Market_Context_BO raw_context, Session_Type session, decimal spread, bool news_block, int correlation_score, Market_Symbol_BO symbol)`
   - Move score recomputation, MTF bonus, and Is_Tradeable evaluation into this class

2. `src/Atlas_Application/Services/Trade_Pipeline_Service.cs`
   - Replace inline enrichment block with: `context = _context_builder.Enrich(context, ...)`

---

### P4-3 — Add Exit_Reason_Enum
**Problem:** Trade_Result_BO.Close_Reason is a free-text string — not queryable, not consistent.

**Changes:**
1. Create `src/Atlas_Domain/Enums/Exit_Reason_Type.cs`:
   Take_Profit_Hit, Stop_Loss_Hit, Trailing_Stop_Hit, Breakeven_Stop, Partial_Close, Manual_Close, Emergency_Stop, Daily_Loss_Limit, Unknown

2. `src/Atlas_Domain/BusinessObjects/Trade_Result_BO.cs`
   - Add: `public Exit_Reason_Type Exit_Reason { get; set; } = Exit_Reason_Type.Unknown;`
   - Keep Close_Reason string for free-text notes

3. Schema migration: add exit_reason column to trade_results table

---

### P4-4 — Add System_Log_Service with log levels
**Problem:** Logging is a raw string event chain. No levels, no filtering, no DB persistence.

**Changes:**
1. Create `src/Atlas_Application/Services/System_Log_Service.cs`
   - Enum: LogLevel { Info, Warning, Error, Trade }
   - Method: Log(LogLevel level, string message, string? source = null)
   - Stores to in-memory ring buffer (last 1000 entries)
   - Optional: writes to system_logs DB table
   - Exposes: On_Log event for UI subscription

2. Wire into Bot_Controller as replacement for raw On_Log string events

---

## COMPLETION STATUS

**Phase 0 (Critical):** 6/6 done
**Phase 1 (High):** 5/5 done
**Phase 2 (Medium):** 6/6 done
**Phase 3 (Tests):** 5/5 done
**Phase 4 (Cleanup):** 4/4 done

**Total:** 26/26 tasks done

**System status:** All planned fixes complete. 239/239 tests passing, solution builds with 0 errors. Demo-mode forward testing may proceed; still recommend a human review of live-mode credentials/config (P2-4) and a fresh demo soak test before enabling live trading.
