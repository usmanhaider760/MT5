using MT5TradingBot.Core;
using MT5TradingBot.Models;
using Newtonsoft.Json;
using Serilog;

namespace MT5TradingBot.Services
{
    /// <summary>
    /// Production AutoBotService.
    ///
    /// ✅ FileSystemWatcher + polling backup (no missed signals)
    /// ✅ Atomic file-lock: only processes each file exactly once
    /// ✅ Full trade validation (directional, SL/TP, R:R, spread, equity)
    /// ✅ Retry logic with configurable backoff
    /// ✅ SL → Breakeven management
    /// ✅ Drawdown protection (emergency close all)
    /// ✅ Daily trade counter with midnight reset
    /// ✅ Trade history log (CSV)
    /// ✅ Thread-safe via SemaphoreSlim
    /// ✅ Clean shutdown via CancellationToken
    /// </summary>
    public sealed class AutoBotService : IAsyncDisposable
    {
        // ── Dependencies ──────────────────────────────────────────
        private readonly MT5Bridge _bridge;
        private BotConfig _cfg;

        // ── Concurrency ───────────────────────────────────────────
        private readonly SemaphoreSlim _tradeLock = new(1, 1);
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();

        // ── Watcher + polling ─────────────────────────────────────
        private FileSystemWatcher? _watcher;
        private Task? _heartbeatTask;

        // ── State ─────────────────────────────────────────────────
        private readonly HashSet<string> _processing = [];   // files currently being handled
        private readonly HashSet<long> _slMovedTickets = []; // tickets where SL was already moved to BE
        private readonly Dictionary<long, LivePosition> _knownPositions = []; // for close detection
        private volatile bool _running;
        private int _tradesToday;
        private DateTime _dayReset = DateTime.Today;
        private double _startOfDayEquity;
        private bool _emergencyStopFired;

        // ── Events ────────────────────────────────────────────────
        public event Action<string>? OnLog;
        public event Action<TradeResult>? OnTradeExecuted;
        public event Action<bool>? OnBotStatusChanged;

        public bool IsRunning => _running;

        // ── Paths ─────────────────────────────────────────────────
        private string ExecutedDir    => Path.Combine(_cfg.WatchFolder, "executed");
        private string RejectedDir    => Path.Combine(_cfg.WatchFolder, "rejected");
        private string ErrorDir       => Path.Combine(_cfg.WatchFolder, "error");
        private string LogFile        => Path.Combine(_cfg.WatchFolder, "trade_history.csv");
        private string ProcessedIdsFile => Path.Combine(_cfg.WatchFolder, "processed_ids.txt");

        // ── Processed signal ID registry ──────────────────────────
        // Key: signal ID, Value: UTC timestamp when processed
        private readonly Dictionary<string, DateTime> _processedIds = [];

        // ═════════════════════════════════════════════════════════
        public AutoBotService(MT5Bridge bridge, BotConfig cfg)
        {
            _bridge = bridge;
            _cfg = cfg;
        }

        // ══════════════════════════════════════════════════════════
        //  START / STOP
        // ══════════════════════════════════════════════════════════

        public async Task StartAsync()
        {
            if (_running) return;
            _running = true;
            _emergencyStopFired = false;

            EnsureFolders();
            EnsureTradeLogHeader();
            LoadProcessedIds();

            // Capture baseline equity for drawdown protection
            var account = await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
            _startOfDayEquity = account?.Equity ?? 0;

            SetupFileWatcher();
            _heartbeatTask = Task.Run(HeartbeatLoopAsync, _cts.Token);

            Log("🤖 Bot STARTED. Watching: " + _cfg.WatchFolder);
            OnBotStatusChanged?.Invoke(true);
        }

        public async Task StopAsync()
        {
            if (!_running) return;
            _running = false;

            _cts.Cancel();
            _watcher?.Dispose();
            _watcher = null;

            if (_heartbeatTask != null)
            {
                try { await _heartbeatTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            Log("🛑 Bot STOPPED.");
            OnBotStatusChanged?.Invoke(false);
        }

        public async Task RestartAsync(BotConfig newCfg)
        {
            _cfg = newCfg;
            await StopAsync().ConfigureAwait(false);
            await StartAsync().ConfigureAwait(false);
        }

        // ══════════════════════════════════════════════════════════
        //  HEARTBEAT LOOP  (runs on background thread)
        // ══════════════════════════════════════════════════════════

        private async Task HeartbeatLoopAsync()
        {
            while (!_cts.Token.IsCancellationRequested && _running)
            {
                try
                {
                    // Reset daily counter at midnight
                    if (DateTime.Today != _dayReset)
                    {
                        _tradesToday = 0;
                        _dayReset = DateTime.Today;
                        _emergencyStopFired = false;
                        var acct = await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
                        _startOfDayEquity = acct?.Equity ?? _startOfDayEquity;
                        PruneProcessedIds();
                        Log("📅 Daily counters reset");
                    }

                    // Drawdown protection
                    if (_cfg.DrawdownProtectionEnabled)
                        await CheckDrawdownAsync().ConfigureAwait(false);

                    // SL → Breakeven
                    await CheckSLToBreakevenAsync().ConfigureAwait(false);

                    // Detect and log closed positions
                    await CheckClosedPositionsAsync().ConfigureAwait(false);

                    // Poll for unprocessed files (watcher backup)
                    await PollFolderAsync().ConfigureAwait(false);

                    await Task.Delay(_cfg.PollIntervalMs, _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Log($"⚠ Heartbeat error: {ex.Message}");
                    await Task.Delay(2000, _cts.Token).ConfigureAwait(false);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  FILE WATCHER
        // ══════════════════════════════════════════════════════════

        private void SetupFileWatcher()
        {
            _watcher = new FileSystemWatcher(_cfg.WatchFolder, "*.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _watcher.Created += OnFileCreated;
            _watcher.Changed += OnFileCreated;
            _watcher.Error   += OnWatcherError;
        }

        private void OnFileCreated(object _, FileSystemEventArgs e)
        {
            _ = Task.Run(() => ProcessSignalFileAsync(e.FullPath), _cts.Token);
        }

        private void OnWatcherError(object _, ErrorEventArgs e)
        {
            Log($"⚠ FileWatcher error: {e.GetException().Message} — polling will compensate");
            // Watcher can fail on network drives; polling backup covers it
        }

        private async Task PollFolderAsync()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_cfg.WatchFolder, "*.json"))
                {
                    await _fileLock.WaitAsync(_cts.Token).ConfigureAwait(false);
                    bool alreadyQueued = _processing.Contains(file);
                    _fileLock.Release();

                    if (!alreadyQueued)
                        await ProcessSignalFileAsync(file).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log($"PollFolder: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════
        //  PROCESS SIGNAL FILE
        // ══════════════════════════════════════════════════════════

        private async Task ProcessSignalFileAsync(string path)
        {
            // Atomic lock: ensure each file handled exactly once
            await _fileLock.WaitAsync(_cts.Token).ConfigureAwait(false);
            bool added = _processing.Add(path);
            _fileLock.Release();

            if (!added) return;

            // Brief delay: let writer finish (avoid partial reads)
            await Task.Delay(300, _cts.Token).ConfigureAwait(false);

            TradeResult? result = null;
            TradeRequest? request = null;

            try
            {
                if (!File.Exists(path)) return;

                // Read with retry (file may be locked briefly by writer)
                string json = await ReadFileWithRetryAsync(path).ConfigureAwait(false);

                request = JsonConvert.DeserializeObject<TradeRequest>(json);
                if (request == null)
                {
                    Log($"⚠ Could not deserialize: {Path.GetFileName(path)}");
                    Archive(path, ErrorDir);
                    return;
                }

                // Duplicate signal ID check (survives restarts)
                if (_processedIds.ContainsKey(request.Id))
                {
                    Log($"⏭ Duplicate signal ID [{request.Id}] already processed — skipping");
                    Archive(path, RejectedDir);
                    return;
                }

                Log($"📄 Signal: {request}");
                result = await ExecuteWithRetryAsync(request).ConfigureAwait(false);

                // Record ID after any execution attempt (success or rejection — not error)
                RecordProcessedId(request.Id);

                Archive(path, result.IsSuccess ? ExecutedDir : RejectedDir);
                LogTrade(request, result);
                OnTradeExecuted?.Invoke(result);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log($"❌ Error processing {Path.GetFileName(path)}: {ex.Message}");
                Archive(path, ErrorDir);
                if (request != null && result == null)
                    LogTrade(request, Fail(request.Id, "EXCEPTION", ex.Message));
            }
            finally
            {
                await _fileLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                _processing.Remove(path);
                _fileLock.Release();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EXECUTE WITH RETRY
        // ══════════════════════════════════════════════════════════

        private async Task<TradeResult> ExecuteWithRetryAsync(TradeRequest request)
        {
            TradeResult result = Fail(request.Id, "NOT_RUN", "Not executed");

            int attempts = _cfg.RetryOnFail ? _cfg.RetryCount : 1;

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                result = await ExecuteTradeWithValidationAsync(request).ConfigureAwait(false);

                if (result.IsSuccess) return result;

                // Don't retry validation failures — they won't change
                if (result.ErrorCode is "VALIDATION" or "REJECTED_CONFIG" or "DAILY_LIMIT")
                    return result;

                if (attempt < attempts)
                {
                    Log($"⏳ Retry {attempt}/{attempts} in {_cfg.RetryDelayMs}ms...");
                    await Task.Delay(_cfg.RetryDelayMs, _cts.Token).ConfigureAwait(false);
                }
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════
        //  TRADE VALIDATION + EXECUTION  (public for manual trades)
        // ══════════════════════════════════════════════════════════

        public async Task<TradeResult> ExecuteTradeWithValidationAsync(TradeRequest request)
        {
            if (_emergencyStopFired)
                return Fail(request.Id, "EMERGENCY_STOP",
                    "Emergency stop active — max drawdown hit. Restart bot to resume.");

            await _tradeLock.WaitAsync(_cts.Token).ConfigureAwait(false);
            try
            {
                // ── 1. Basic model validation ──────────────────────
                var (valid, validErr) = request.Validate();
                if (!valid) return Fail(request.Id, "VALIDATION", validErr);

                // ── 1b. Signal age check ───────────────────────────
                if (request.ExpiryMinutes > 0)
                {
                    double ageMinutes = (DateTime.UtcNow - request.CreatedAt).TotalMinutes;
                    if (ageMinutes > request.ExpiryMinutes)
                        return Fail(request.Id, "SIGNAL_EXPIRED",
                            $"Signal is {ageMinutes:F0} min old (limit {request.ExpiryMinutes} min). Discard.");
                }

                // ── 2. Pair allowlist ──────────────────────────────
                if (_cfg.AllowedPairs.Count > 0 &&
                    !_cfg.AllowedPairs.Contains(request.Pair.ToUpperInvariant()))
                    return Fail(request.Id, "REJECTED_CONFIG",
                        $"Pair {request.Pair} not in allowed list: [{string.Join(", ", _cfg.AllowedPairs)}]");

                // ── 3. Daily limit ─────────────────────────────────
                if (_tradesToday >= _cfg.MaxTradesPerDay)
                    return Fail(request.Id, "DAILY_LIMIT",
                        $"Daily trade limit {_cfg.MaxTradesPerDay} reached");

                // ── 4. Get live account ────────────────────────────
                var account = await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
                if (account == null)
                    return Fail(request.Id, "NO_ACCOUNT", "Could not fetch account info from MT5");

                // ── 5. Fetch live symbol info (price + spread) ─────
                // Single call reused for auto-lot, R:R, portfolio risk, and spread check.
                var symbolInfo = await _bridge.GetSymbolInfoAsync(request.Pair).ConfigureAwait(false);

                // Real market price: Ask for BUY, Bid for SELL
                double livePrice = symbolInfo != null
                    ? (request.TradeType == Models.TradeType.BUY ? symbolInfo.Ask : symbolInfo.Bid)
                    : 0;

                double refEntry = request.EntryPrice > 0 ? request.EntryPrice
                                : livePrice > 0          ? livePrice
                                : EstimateMarketPrice(request); // last-resort fallback

                // ── 6. Auto lot calculation ────────────────────────
                if (_cfg.AutoLotCalculation)
                {
                    request.LotSize = LotCalculator.Calculate(
                        account.Equity, _cfg.MaxRiskPercent,
                        refEntry, request.StopLoss, request.Pair);

                    Log($"📊 Auto lot: {request.LotSize:F2} " +
                        $"(${LotCalculator.DollarRisk(request.LotSize, refEntry, request.StopLoss, request.Pair):F2} risk)" +
                        (livePrice > 0 ? $" @ live {(request.TradeType == Models.TradeType.BUY ? "Ask" : "Bid")} {livePrice:F5}" : " @ estimated price"));
                }

                // ── 7. R:R check ───────────────────────────────────
                double rr = LotCalculator.RiskRewardRatio(refEntry, request.StopLoss, request.TakeProfit);
                if (_cfg.EnforceRR && rr < _cfg.MinRRRatio)
                    return Fail(request.Id, "REJECTED_CONFIG",
                        $"R:R {rr:F2} is below minimum {_cfg.MinRRRatio:F2}");

                if (rr < _cfg.MinRRRatio)
                    Log($"⚠ R:R {rr:F2} below minimum {_cfg.MinRRRatio:F2} — proceeding (enforce_rr=false)");

                // ── 8. Margin check ────────────────────────────────
                if (account.FreeMargin < account.Balance * 0.05)
                    return Fail(request.Id, "LOW_MARGIN",
                        $"Free margin ${account.FreeMargin:F2} is critically low");

                // ── 9. Total portfolio risk cap ────────────────────
                if (_cfg.MaxTotalRiskPercent > 0 && account.Equity > 0)
                {
                    var openPositions = await _bridge.GetPositionsAsync().ConfigureAwait(false);
                    double totalOpenRisk = openPositions
                        .Where(p => p.StopLoss > 0)
                        .Sum(p => LotCalculator.DollarRisk(p.Lots, p.OpenPrice, p.StopLoss, p.Symbol));

                    double newTradeRisk = LotCalculator.DollarRisk(
                        request.LotSize, refEntry, request.StopLoss, request.Pair);

                    double totalRiskPct = (totalOpenRisk + newTradeRisk) / account.Equity * 100.0;

                    if (totalRiskPct > _cfg.MaxTotalRiskPercent)
                        return Fail(request.Id, "PORTFOLIO_RISK_CAP",
                            $"Total risk would be {totalRiskPct:F1}% (${totalOpenRisk + newTradeRisk:F0}) — cap is {_cfg.MaxTotalRiskPercent:F1}%");

                    Log($"📊 Portfolio risk: {totalRiskPct:F1}% / {_cfg.MaxTotalRiskPercent:F1}% cap");
                }

                // ── 10. Spread check ───────────────────────────────
                if (_cfg.MaxSpreadPips > 0)
                {
                    if (symbolInfo != null)
                    {
                        if (symbolInfo.SpreadPips > _cfg.MaxSpreadPips)
                            return Fail(request.Id, "HIGH_SPREAD",
                                $"{request.Pair} spread {symbolInfo.SpreadPips:F1} pips exceeds max {_cfg.MaxSpreadPips:F1} pips");

                        Log($"📡 Spread: {symbolInfo.SpreadPips:F1} pips (max {_cfg.MaxSpreadPips:F1})");
                    }
                    else
                    {
                        Log($"⚠ Could not fetch spread for {request.Pair} — proceeding without check");
                    }
                }

                // ── 11. Execute ────────────────────────────────────
                bool hasTp2 = request.TakeProfit2 > 0;

                if (hasTp2)
                {
                    // Split into two half-lot positions: TP1 + TP2
                    double halfLot = Math.Max(0.01, Math.Round(request.LotSize / 2.0, 2));
                    Log($"⚡ TP2 split: 2 × {halfLot:F2} lots — TP1:{request.TakeProfit:F5} TP2:{request.TakeProfit2:F5}");

                    var req1 = ShallowClone(request);
                    req1.LotSize = halfLot;
                    req1.TakeProfit = request.TakeProfit;
                    req1.Comment = request.Comment + "_TP1";

                    var req2 = ShallowClone(request);
                    req2.LotSize = halfLot;
                    req2.TakeProfit = request.TakeProfit2;
                    req2.Comment = request.Comment + "_TP2";

                    var r1 = await _bridge.OpenTradeAsync(req1).ConfigureAwait(false);
                    if (r1.IsSuccess)
                    {
                        _tradesToday++;
                        Log($"✅ TP1 #{r1.Ticket} filled @ {r1.ExecutedPrice:F5}");
                    }
                    else
                    {
                        Log($"❌ TP1 rejected: {r1.ErrorMessage}");
                        return r1; // don't open TP2 if TP1 failed
                    }

                    var r2 = await _bridge.OpenTradeAsync(req2).ConfigureAwait(false);
                    if (r2.IsSuccess)
                    {
                        _tradesToday++;
                        Log($"✅ TP2 #{r2.Ticket} filled @ {r2.ExecutedPrice:F5}");
                    }
                    else
                    {
                        Log($"⚠ TP2 rejected (TP1 is open): {r2.ErrorMessage}");
                    }

                    Log($"📊 Trades today: {_tradesToday}/{_cfg.MaxTradesPerDay}");
                    return r1; // return TP1 result as the primary
                }

                Log($"⚡ Executing trade (R:R {rr:F2}, lot {request.LotSize:F2})");
                var result = await _bridge.OpenTradeAsync(request).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    _tradesToday++;
                    Log($"✅ #{result.Ticket} | Trades today: {_tradesToday}/{_cfg.MaxTradesPerDay}");

                    // Slippage check: only for MARKET orders where we have a live reference price
                    if (request.OrderType == OrderType.MARKET &&
                        _cfg.MaxSlippagePips > 0 &&
                        livePrice > 0 &&
                        result.ExecutedPrice > 0)
                    {
                        double pipSize = LotCalculator.GetPipSize(request.Pair.ToUpperInvariant());
                        double slippagePips = Math.Abs(result.ExecutedPrice - livePrice) / pipSize;
                        if (slippagePips > _cfg.MaxSlippagePips)
                            Log($"⚠ HIGH SLIPPAGE on #{result.Ticket}: {slippagePips:F1} pips " +
                                $"(expected {livePrice:F5}, filled {result.ExecutedPrice:F5})");
                        else
                            Log($"📌 Slippage: {slippagePips:F1} pips (max {_cfg.MaxSlippagePips:F1})");
                    }
                }
                else
                {
                    Log($"❌ MT5 rejected: {result.ErrorMessage}");
                }

                return result;
            }
            finally { _tradeLock.Release(); }
        }

        // ══════════════════════════════════════════════════════════
        //  SL → BREAKEVEN
        // ══════════════════════════════════════════════════════════

        private async Task CheckSLToBreakevenAsync()
        {
            if (!_bridge.IsConnected) return;
            List<LivePosition> positions;

            try { positions = await _bridge.GetPositionsAsync().ConfigureAwait(false); }
            catch { return; }

            // Prune tickets that are no longer open
            var openTickets = new HashSet<long>(positions.Select(p => p.Ticket));
            _slMovedTickets.IntersectWith(openTickets);

            foreach (var pos in positions)
            {
                if (pos.MagicNumber != _cfg.MagicNumber) continue;

                // Skip if we already moved SL to BE for this ticket this session
                if (_slMovedTickets.Contains(pos.Ticket)) continue;

                // Skip if SL is already at or past breakeven on the broker side
                bool alreadyAtBE = pos.Type == Models.TradeType.BUY
                    ? pos.StopLoss >= pos.OpenPrice - 0.00001
                    : pos.StopLoss <= pos.OpenPrice + 0.00001;
                if (alreadyAtBE)
                {
                    _slMovedTickets.Add(pos.Ticket); // broker already has it, don't check again
                    continue;
                }

                double tpDistance = Math.Abs(pos.TakeProfit - pos.OpenPrice);
                double currentMove = pos.Type == Models.TradeType.BUY
                    ? pos.CurrentPrice - pos.OpenPrice
                    : pos.OpenPrice - pos.CurrentPrice;

                bool shouldMoveSL = currentMove >= tpDistance * 0.6;

                if (shouldMoveSL)
                {
                    Log($"🔄 SL→BE: #{pos.Ticket} {pos.Symbol} " +
                        $"move SL from {pos.StopLoss:F5} → {pos.OpenPrice:F5}");
                    bool ok = await _bridge.ModifyPositionAsync(
                        pos.Ticket, pos.OpenPrice, pos.TakeProfit).ConfigureAwait(false);
                    if (ok)
                    {
                        _slMovedTickets.Add(pos.Ticket); // persist across heartbeat ticks
                        Log($"✅ SL moved to breakeven for #{pos.Ticket}");
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CLOSED POSITION DETECTION
        // ══════════════════════════════════════════════════════════

        private async Task CheckClosedPositionsAsync()
        {
            if (!_bridge.IsConnected) return;
            List<LivePosition> current;
            try { current = await _bridge.GetPositionsAsync().ConfigureAwait(false); }
            catch { return; }

            var currentTickets = new HashSet<long>(current.Select(p => p.Ticket));

            foreach (var kv in _knownPositions)
            {
                if (!currentTickets.Contains(kv.Key))
                {
                    var closed = kv.Value;
                    Log($"📕 Closed: #{closed.Ticket} {closed.Symbol} {closed.Type} " +
                        $"P&L: ${closed.Profit:F2}");
                    LogClose(closed);
                }
            }

            // Update snapshot: add new positions, remove closed ones
            foreach (var pos in current)
                _knownPositions[pos.Ticket] = pos;

            foreach (var t in _knownPositions.Keys.Except(currentTickets).ToList())
                _knownPositions.Remove(t);
        }

        private void LogClose(LivePosition pos)
        {
            try
            {
                string line = string.Join(",",
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    "CLOSE", pos.Symbol, pos.Type,
                    pos.Lots.ToString("F2"),
                    pos.OpenPrice.ToString("F5"),
                    pos.StopLoss.ToString("F5"),
                    pos.TakeProfit.ToString("F5"),
                    pos.Ticket, "Closed",
                    pos.CurrentPrice.ToString("F5"),
                    $"\"P&L: ${pos.Profit:F2}\"");

                File.AppendAllText(LogFile, line + Environment.NewLine);
            }
            catch (Exception ex) { Log($"LogClose error: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════
        //  DRAWDOWN PROTECTION
        // ══════════════════════════════════════════════════════════

        private async Task CheckDrawdownAsync()
        {
            if (_emergencyStopFired || _startOfDayEquity <= 0) return;

            AccountInfo? account;
            try { account = await _bridge.GetAccountInfoAsync().ConfigureAwait(false); }
            catch { return; }

            if (account == null) return;

            double drawdownPct = (_startOfDayEquity - account.Equity) / _startOfDayEquity * 100.0;

            if (drawdownPct >= _cfg.EmergencyCloseDrawdownPct)
            {
                _emergencyStopFired = true;
                Log($"🚨 EMERGENCY STOP: Drawdown {drawdownPct:F1}% exceeded limit " +
                    $"{_cfg.EmergencyCloseDrawdownPct:F1}% — CLOSING ALL POSITIONS");

                var positions = await _bridge.GetPositionsAsync().ConfigureAwait(false);
                foreach (var pos in positions)
                {
                    Log($"🚨 Emergency close #{pos.Ticket} {pos.Symbol}");
                    await _bridge.CloseTradeAsync(pos.Ticket).ConfigureAwait(false);
                }

                Log("🚨 All positions closed. Bot paused. Fix the issue then restart.");
                OnBotStatusChanged?.Invoke(false);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  TRADE LOG (CSV)
        // ══════════════════════════════════════════════════════════

        private void LogTrade(TradeRequest req, TradeResult result)
        {
            try
            {
                string line = string.Join(",",
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    req.Id, req.Pair, req.TradeType, req.LotSize.ToString("F2"),
                    req.EntryPrice.ToString("F5"),
                    req.StopLoss.ToString("F5"),
                    req.TakeProfit.ToString("F5"),
                    result.Ticket, result.Status, result.ExecutedPrice.ToString("F5"),
                    $"\"{result.ErrorMessage}\"");

                File.AppendAllText(LogFile, line + Environment.NewLine);
            }
            catch (Exception ex) { Log($"Log write error: {ex.Message}"); }
        }

        private void EnsureTradeLogHeader()
        {
            if (!File.Exists(LogFile))
                File.WriteAllText(LogFile,
                    "Time,Id,Pair,Direction,Lots,Entry,SL,TP,Ticket,Status,ExecutedPrice,Error\n");
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════

        private static async Task<string> ReadFileWithRetryAsync(string path)
        {
            for (int i = 0; i < 5; i++)
            {
                try { return await File.ReadAllTextAsync(path).ConfigureAwait(false); }
                catch (IOException) { await Task.Delay(200).ConfigureAwait(false); }
            }
            return await File.ReadAllTextAsync(path).ConfigureAwait(false);
        }

        private static void Archive(string src, string destDir)
        {
            try
            {
                if (!File.Exists(src)) return;
                string dest = Path.Combine(destDir,
                    $"{Path.GetFileNameWithoutExtension(src)}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.Move(src, dest, overwrite: true);
            }
            catch (Exception ex)
            {
                Log_Static($"Archive error: {ex.Message}");
            }
        }

        private static double EstimateMarketPrice(TradeRequest r)
        {
            // Last-resort estimate when entry=0 and symbol info unavailable
            return r.TradeType == Models.TradeType.BUY
                ? r.StopLoss * 1.002
                : r.StopLoss * 0.998;
        }

        private static TradeRequest ShallowClone(TradeRequest r) =>
            JsonConvert.DeserializeObject<TradeRequest>(JsonConvert.SerializeObject(r))!;

        private static TradeResult Fail(string reqId, string code, string msg)
        {
            Log_Static($"🚫 [{code}] {msg}");
            return new TradeResult { RequestId = reqId, Status = TradeStatus.Rejected,
                ErrorCode = code, ErrorMessage = msg };
        }

        private void EnsureFolders()
        {
            Directory.CreateDirectory(_cfg.WatchFolder);
            Directory.CreateDirectory(ExecutedDir);
            Directory.CreateDirectory(RejectedDir);
            Directory.CreateDirectory(ErrorDir);
        }

        // ── Processed ID persistence ──────────────────────────────

        private void LoadProcessedIds()
        {
            _processedIds.Clear();
            if (!File.Exists(ProcessedIdsFile)) return;
            try
            {
                foreach (var line in File.ReadAllLines(ProcessedIdsFile))
                {
                    var parts = line.Split('\t');
                    if (parts.Length == 2 && DateTime.TryParse(parts[1], out var ts))
                        _processedIds[parts[0]] = ts;
                }
                Log($"📋 Loaded {_processedIds.Count} processed signal IDs");
            }
            catch (Exception ex) { Log($"ProcessedIds load error: {ex.Message}"); }
        }

        private void RecordProcessedId(string id)
        {
            _processedIds[id] = DateTime.UtcNow;
            try { File.AppendAllText(ProcessedIdsFile, $"{id}\t{DateTime.UtcNow:O}{Environment.NewLine}"); }
            catch (Exception ex) { Log($"ProcessedIds write error: {ex.Message}"); }
        }

        private void PruneProcessedIds()
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            var stale = _processedIds.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();
            foreach (var k in stale) _processedIds.Remove(k);

            try
            {
                var lines = _processedIds.Select(kv => $"{kv.Key}\t{kv.Value:O}");
                File.WriteAllLines(ProcessedIdsFile, lines);
                if (stale.Count > 0) Log($"🗑 Pruned {stale.Count} old signal IDs from registry");
            }
            catch (Exception ex) { Log($"ProcessedIds prune error: {ex.Message}"); }
        }

        private void Log(string msg)
        {
            Serilog.Log.Information("[AutoBot] {msg}", msg);
            OnLog?.Invoke(msg);
        }

        private static void Log_Static(string msg) =>
            Serilog.Log.Information("[AutoBot] {msg}", msg);

        // ══════════════════════════════════════════════════════════
        //  DISPOSE
        // ══════════════════════════════════════════════════════════

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            _cts.Dispose();
            _tradeLock.Dispose();
            _fileLock.Dispose();
        }
    }
}
