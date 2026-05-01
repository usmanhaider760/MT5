using MT5TradingBot.Core;
using MT5TradingBot.Data;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Modules.NewsFilter;
using MT5TradingBot.Modules.PairSettings;
using MT5TradingBot.Modules.RiskManagement;
using Newtonsoft.Json;
using Serilog;
using Core = MT5TradingBot.Core;

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
        private readonly IPairSettingsService? _pairSettings;
        private readonly INewsCalendarService? _newsCalendar;
        private BotConfig _cfg;
        private ApiIntegrationConfig _apiConfig;
        private readonly IRiskManager _riskManager;
        private readonly ITelegramService _telegram;
        private readonly ITradeRepository? _tradeDb;

        // ── Concurrency ───────────────────────────────────────────
        private readonly SemaphoreSlim _tradeLock = new(1, 1);
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();

        // ── Watcher + polling ─────────────────────────────────────
        private FileSystemWatcher? _watcher;
        private Task? _heartbeatTask;

        // ── Paper trading ─────────────────────────────────────────
        private readonly List<LivePosition> _paperPositions = [];
        private long _paperTicketCounter = 90_000_000; // high range avoids collision with real tickets

        // ── State ─────────────────────────────────────────────────
        private readonly HashSet<string> _processing = [];   // files currently being handled
        private readonly HashSet<string> _shownPaths = [];  // files queued in manual-execute mode
        private readonly HashSet<long> _slMovedTickets = []; // tickets where SL was already moved to BE
        private readonly HashSet<long> _trailingActiveTickets = [];
        private readonly Dictionary<long, LivePosition> _knownPositions = []; // for close detection
        private volatile bool _running;
        private int _tradesToday;
        private DateTime _dayReset = DateTime.Today;
        private double _startOfDayEquity;
        private bool _emergencyStopFired;
        private bool _edgePaused;
        private EdgeHealthMonitor? _edgeMonitor;

        // ── Events ────────────────────────────────────────────────
        public event Action<string>?         OnLog;
        public event Action<TradeResult>?    OnTradeExecuted;
        public event Action<bool>?           OnBotStatusChanged;
        public event Action<EdgeStatus>?     OnEdgeStatusChanged;
        public event Action<SignalCardInfo>? OnSignalUpdate;

        public bool IsRunning => _running;
        public bool IsEdgePaused => _edgePaused;
        public bool IsPaperTrading => _cfg.PaperTrading;

        private BotMode _currentMode = BotMode.ManualApproval;
        public BotMode CurrentMode => _currentMode;

        // Derived read-only for backward compatibility with card logic that reads this.
        public bool ManualExecuteOnly => _currentMode == BotMode.ManualApproval;

        public event Action<BotMode>? OnModeChanged;

        public void SetMode(BotMode newMode)
        {
            if (newMode == _currentMode) return;

            if (newMode == BotMode.FullAuto && (_edgePaused || _emergencyStopFired))
            {
                Log($"[Mode] Cannot switch to FullAuto — " +
                    (_edgePaused ? "edge paused" : "emergency stop active") + ".");
                return;
            }

            BotMode previous = _currentMode;
            _currentMode = newMode;
            Log($"[Mode] {previous} → {newMode}");
            OnModeChanged?.Invoke(newMode);
        }

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
        public AutoBotService(
            MT5Bridge bridge,
            BotConfig cfg,
            IPairSettingsService? pairSettings = null,
            INewsCalendarService? newsCalendar = null,
            ApiIntegrationConfig? apiConfig = null,
            IRiskManager? riskManager = null,
            ITradeRepository? tradeRepository = null)
        {
            _bridge = bridge;
            _cfg = cfg;
            _pairSettings = pairSettings;
            _newsCalendar = newsCalendar;
            _apiConfig = apiConfig ?? new ApiIntegrationConfig();
            _riskManager = riskManager ?? new RiskManager(_pairSettings);
            _tradeDb = tradeRepository;
            _telegram = (!string.IsNullOrWhiteSpace(_apiConfig.TelegramBotToken) &&
                         !string.IsNullOrWhiteSpace(_apiConfig.TelegramChatId))
                ? new TelegramService(_apiConfig)
                : NullTelegramService.Instance;
        }

        // ══════════════════════════════════════════════════════════
        //  START / STOP
        // ══════════════════════════════════════════════════════════

        public async Task StartAsync()
        {
            if (_running) return;
            _currentMode = _cfg.OperatingMode;
            _running = true;
            _emergencyStopFired = false;
            _edgePaused = false;
            _edgeMonitor = null;

            if (_cfg.EdgeMonitorEnabled && _tradeDb != null)
            {
                _edgeMonitor = new EdgeHealthMonitor(
                    _cfg.EdgeWindowTrades,
                    _cfg.MinWinRatePct,
                    _cfg.MaxConsecutiveLosses);

                var history = await _tradeDb.GetRecentClosedAsync(_cfg.EdgeWindowTrades)
                    .ConfigureAwait(false);
                _edgeMonitor.Seed(history.Reverse().Select(r => r.ProfitUsd));
                var status = _edgeMonitor.GetStatus();
                Log($"[EdgeMonitor] Seeded with {history.Count} closed trades. " +
                    $"Win rate: {status.WinRatePct:F1}%");
                OnEdgeStatusChanged?.Invoke(status);
            }

            EnsureFolders();
            EnsureTradeLogHeader();
            LoadProcessedIds();

            // Capture baseline equity for drawdown protection
            var account = await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
            _startOfDayEquity = account?.Equity ?? 0;

            SetupFileWatcher();
            _heartbeatTask = Task.Run(HeartbeatLoopAsync, _cts.Token);

            var pendingSignals = Directory.GetFiles(_cfg.WatchFolder, "*.json");
            Log("[BOT] Bot STARTED. Watching: " + _cfg.WatchFolder);
            Log(pendingSignals.Length == 0
                ? "[BOT] Watch folder is ready. No pending .json signal files found."
                : $"[BOT] Watch folder has {pendingSignals.Length} pending .json signal file(s).");
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

            Log("[BOT] Bot STOPPED.");
            OnBotStatusChanged?.Invoke(false);
        }

        public async Task RestartAsync(BotConfig newCfg)
        {
            _cfg = newCfg;
            await StopAsync().ConfigureAwait(false);
            await StartAsync().ConfigureAwait(false);
        }

        // ══════════════════════════════════════════════════════════
        //  PAPER TRADING — SIMULATE OPEN + HEARTBEAT CLOSE DETECTION
        // ══════════════════════════════════════════════════════════

        private TradeResult SimulatePaperTrade(TradeRequest req, double livePrice)
        {
            long ticket = Interlocked.Increment(ref _paperTicketCounter);
            double fillPrice = livePrice > 0 ? livePrice : req.EntryPrice;

            var pos = new LivePosition
            {
                Ticket       = ticket,
                Symbol       = req.Pair,
                Type         = req.TradeType,
                Lots         = req.LotSize,
                OpenPrice    = fillPrice,
                CurrentPrice = fillPrice,
                StopLoss     = req.StopLoss,
                TakeProfit   = req.TakeProfit,
                Profit       = 0,
                MagicNumber  = req.MagicNumber,
                Comment      = "[PAPER] " + req.Comment,
                OpenTime     = DateTime.UtcNow
            };

            lock (_paperPositions)
                _paperPositions.Add(pos);

            Log($"[PAPER] Simulated {req.TradeType} {req.Pair} lot={req.LotSize:F2} " +
                $"@ {fillPrice:F5}  SL={req.StopLoss:F5}  TP={req.TakeProfit:F5}  ticket=#{ticket}");

            return new TradeResult
            {
                RequestId     = req.Id,
                Status        = TradeStatus.Filled,
                Ticket        = ticket,
                ExecutedPrice = fillPrice,
                ExecutedLots  = req.LotSize,
                ExecutedAt    = DateTime.UtcNow
            };
        }

        private async Task CheckPaperPositionsAsync()
        {
            if (!_cfg.PaperTrading) return;

            List<LivePosition> snapshot;
            lock (_paperPositions)
            {
                if (_paperPositions.Count == 0) return;
                snapshot = [.. _paperPositions];
            }

            // Fetch live prices for every unique symbol
            var symbols = snapshot.Select(p => p.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var prices  = new Dictionary<string, (double Bid, double Ask)>(StringComparer.OrdinalIgnoreCase);
            foreach (var sym in symbols)
            {
                var info = await _bridge.GetSymbolInfoAsync(sym).ConfigureAwait(false);
                if (info != null) prices[sym] = (info.Bid, info.Ask);
            }

            var toClose = new List<(LivePosition Pos, double ClosePrice, string Reason)>();

            lock (_paperPositions)
            {
                foreach (var pos in _paperPositions)
                {
                    if (!prices.TryGetValue(pos.Symbol, out var px)) continue;

                    double closePrice = pos.Type == TradeType.BUY ? px.Bid : px.Ask;
                    double pipSize    = Core.LotCalculator.GetPipSize(pos.Symbol.ToUpperInvariant());
                    double pipValue   = Core.LotCalculator.GetPipValuePerLot(pos.Symbol.ToUpperInvariant());
                    double priceDiff  = pos.Type == TradeType.BUY
                        ? closePrice - pos.OpenPrice
                        : pos.OpenPrice - closePrice;

                    pos.CurrentPrice = closePrice;
                    pos.Profit       = pipSize > 0 ? priceDiff / pipSize * pipValue * pos.Lots : 0;

                    // SL → Breakeven for paper positions
                    if (!_slMovedTickets.Contains(pos.Ticket) && pos.TakeProfit > 0)
                    {
                        double tpDist  = Math.Abs(pos.TakeProfit - pos.OpenPrice);
                        double moved   = pos.Type == TradeType.BUY
                            ? closePrice - pos.OpenPrice
                            : pos.OpenPrice - closePrice;
                        double bePct   = _cfg.SlToBeTrigerPct > 0 && _cfg.SlToBeTrigerPct <= 1.0
                            ? _cfg.SlToBeTrigerPct : 0.6;

                        if (tpDist > 0 && moved >= tpDist * bePct)
                        {
                            pos.StopLoss = pos.OpenPrice;
                            _slMovedTickets.Add(pos.Ticket);
                            Log($"[PAPER] SL→BE #{pos.Ticket} {pos.Symbol} → {pos.OpenPrice:F5}");
                        }
                    }

                    // SL hit?
                    if (pos.StopLoss > 0)
                    {
                        bool slHit = pos.Type == TradeType.BUY
                            ? closePrice <= pos.StopLoss
                            : closePrice >= pos.StopLoss;
                        if (slHit) { toClose.Add((pos, pos.StopLoss, "SL")); continue; }
                    }

                    // TP hit?
                    if (pos.TakeProfit > 0)
                    {
                        bool tpHit = pos.Type == TradeType.BUY
                            ? closePrice >= pos.TakeProfit
                            : closePrice <= pos.TakeProfit;
                        if (tpHit) { toClose.Add((pos, pos.TakeProfit, "TP")); }
                    }
                }

                foreach (var (pos, _, _) in toClose)
                    _paperPositions.Remove(pos);
            }

            foreach (var (pos, closePrice, reason) in toClose)
            {
                double pipSize   = Core.LotCalculator.GetPipSize(pos.Symbol.ToUpperInvariant());
                double pipValue  = Core.LotCalculator.GetPipValuePerLot(pos.Symbol.ToUpperInvariant());
                double priceDiff = pos.Type == TradeType.BUY
                    ? closePrice - pos.OpenPrice
                    : pos.OpenPrice - closePrice;
                double profitUsd = pipSize > 0 ? priceDiff / pipSize * pipValue * pos.Lots : 0;

                Log($"[PAPER] #{pos.Ticket} {pos.Symbol} {pos.Type} closed at {reason} " +
                    $"{closePrice:F5} | P&L: {profitUsd:+0.00;-0.00} USD");
                LogClose(pos);

                if (_tradeDb != null)
                    _ = _tradeDb.UpdateCloseAsync(pos.Ticket, profitUsd, DateTime.UtcNow);

                _ = _telegram.SendTradeClosedAsync(pos.Symbol, profitUsd, pos.Ticket)
                              .ConfigureAwait(false);

                if (_edgeMonitor != null)
                {
                    var status = _edgeMonitor.Record(profitUsd);
                    Log($"[EdgeMonitor] Win rate: {status.WinRatePct:F1}% " +
                        $"({status.SampleSize} trades), consecutive losses: {status.ConsecutiveLosses}");
                    OnEdgeStatusChanged?.Invoke(status);

                    if (status.IsDegraded && !_edgePaused)
                    {
                        _edgePaused = true;
                        Log("[EdgeMonitor] Edge degraded — auto-pausing.");
                        _ = _telegram.SendRiskBlockedAsync("ALL",
                            $"Edge degraded: {status.WinRatePct:F1}% win rate, " +
                            $"{status.ConsecutiveLosses} consecutive losses. Bot paused.")
                            .ConfigureAwait(false);
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  HEARTBEAT LOOP  (runs on background thread)
        // ══════════════════════════════════════════════════════════

        public void UpdateConfig(BotConfig newCfg)
        {
            _cfg = newCfg;
        }

        public void UpdateApiConfig(ApiIntegrationConfig newCfg)
        {
            _apiConfig = newCfg;
        }

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

                    // Trailing stop
                    await CheckTrailingStopAsync().ConfigureAwait(false);

                    // Detect and log closed positions
                    await CheckClosedPositionsAsync().ConfigureAwait(false);

                    // Simulate SL/TP closes for paper positions
                    await CheckPaperPositionsAsync().ConfigureAwait(false);

                    // Poll for unprocessed files (watcher backup)
                    await PollFolderAsync().ConfigureAwait(false);

                    await Task.Delay(_cfg.PollIntervalMs, _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Log($"[WARN] Heartbeat error: {ex.Message}");
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
            Log($"[WARN] FileWatcher error: {e.GetException().Message} - polling will compensate");
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

        private SignalCardInfo MakeCard(TradeRequest req, string path,
            SignalCardStatus status, string statusText, long ticket = 0, string rawJson = "")
        {
            // When file has been archived, compute its new location so delete works correctly
            string resolvedPath = status switch
            {
                SignalCardStatus.Executed => Path.Combine(_cfg.WatchFolder, "executed", Path.GetFileName(path)),
                SignalCardStatus.Rejected => Path.Combine(_cfg.WatchFolder, "rejected", Path.GetFileName(path)),
                SignalCardStatus.Error    => Path.Combine(_cfg.WatchFolder, "error",    Path.GetFileName(path)),
                _                         => path
            };
            return new SignalCardInfo
            {
                SignalId   = req.Id,
                FileName   = Path.GetFileName(path),
                FilePath   = resolvedPath,
                RawJson    = rawJson,
                Pair       = req.Pair,
                TradeType  = req.TradeType.ToString(),
                StopLoss   = req.StopLoss,
                TakeProfit = req.TakeProfit,
                LotSize    = req.LotSize,
                CreatedAt  = req.CreatedAt.ToLocalTime(),
                Status     = status,
                StatusText = statusText,
                Ticket     = ticket
            };
        }

        private async Task ProcessSignalFileAsync(string path)
        {
            // Monitor mode: ignore signal files entirely — heartbeat still runs
            if (_currentMode == BotMode.Monitor)
            {
                Log($"[BOT] Signal ignored in Monitor mode: {Path.GetFileName(path)}");
                return;
            }

            // ManualApproval mode: skip files already shown to the user
            if (_currentMode == BotMode.ManualApproval)
            {
                await _fileLock.WaitAsync(_cts.Token).ConfigureAwait(false);
                bool already = _shownPaths.Contains(path);
                _fileLock.Release();
                if (already) return;
            }

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

                Log($"[BOT] Signal file detected: {Path.GetFileName(path)}");

                // Read with retry (file may be locked briefly by writer)
                string json = await ReadFileWithRetryAsync(path).ConfigureAwait(false);
                Log($"[BOT] Signal file read: {Path.GetFileName(path)}");

                request = JsonConvert.DeserializeObject<TradeRequest>(json);
                if (request == null)
                {
                    Log($"[WARN] Could not deserialize: {Path.GetFileName(path)}");
                    Archive(path, ErrorDir);
                    return;
                }

                Log($"[BOT] Parsed signal: {request}");
                OnSignalUpdate?.Invoke(MakeCard(request, path, SignalCardStatus.Pending, "Pending", rawJson: json));

                // Manual-execute mode: show card and stop — user clicks ▶ to trade
                if (ManualExecuteOnly)
                {
                    await _fileLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                    _shownPaths.Add(path);
                    _fileLock.Release();
                    Log($"[BOT] Signal {request.Id} queued — click ▶ Execute on the card to place trade.");
                    _ = _telegram.SendApprovalNeededAsync(
                        request.Pair, request.TradeType.ToString(), request.LotSize)
                        .ConfigureAwait(false);
                    return;
                }

                // Duplicate signal ID check (survives restarts)
                if (_processedIds.ContainsKey(request.Id))
                {
                    Log($"[BOT] Duplicate signal ID [{request.Id}] already processed - skipping");
                    Archive(path, RejectedDir);
                    OnSignalUpdate?.Invoke(MakeCard(request, path, SignalCardStatus.Rejected, "Duplicate ID"));
                    return;
                }

                Log($"[BOT] Executing signal {request.Id}...");
                OnSignalUpdate?.Invoke(MakeCard(request, path, SignalCardStatus.Executing, "Executing..."));
                result = await ExecuteWithRetryAsync(request).ConfigureAwait(false);

                // Record ID after any execution attempt (success or rejection - not error)
                RecordProcessedId(request.Id);

                Archive(path, result.IsSuccess ? ExecutedDir : RejectedDir);
                Log(result.IsSuccess
                    ? $"[BOT] Signal {request.Id} executed and archived to executed."
                    : $"[BOT] Signal {request.Id} rejected and archived to rejected: {result.ErrorMessage}");
                OnSignalUpdate?.Invoke(MakeCard(request, path,
                    result.IsSuccess ? SignalCardStatus.Executed : SignalCardStatus.Rejected,
                    result.IsSuccess ? $"#{result.Ticket}" : result.ErrorMessage,
                    result.IsSuccess ? result.Ticket : 0));
                LogTrade(request, result);
                OnTradeExecuted?.Invoke(result);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log($"[ERROR] Error processing {Path.GetFileName(path)}: {ex.Message}");
                Archive(path, ErrorDir);
                if (request != null)
                {
                    OnSignalUpdate?.Invoke(MakeCard(request, path, SignalCardStatus.Error, ex.Message));
                    if (result == null)
                        LogTrade(request, Fail(request.Id, "EXCEPTION", ex.Message));
                }
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

                // Do not retry validation failures; they will not change.
                if (result.ErrorCode is "VALIDATION" or "REJECTED_CONFIG" or "DAILY_LIMIT"
                                         or "RISK_BLOCKED" or "EDGE_PAUSED")
                    return result;

                if (attempt < attempts)
                {
                    Log($"[BOT] Retry {attempt}/{attempts} in {_cfg.RetryDelayMs}ms...");
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
            if (_edgePaused)
            {
                return new TradeResult
                {
                    RequestId = request.Id,
                    Status = TradeStatus.Rejected,
                    ErrorCode = "EDGE_PAUSED",
                    ErrorMessage = "Edge health monitor paused new trade execution.",
                    ExecutedAt = DateTime.UtcNow
                };
            }

            if (_emergencyStopFired)
                return Fail(request.Id, "EMERGENCY_STOP",
                    "Emergency stop active - max drawdown hit. Restart bot to resume.");

            await _tradeLock.WaitAsync(_cts.Token).ConfigureAwait(false);
            try
            {
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

                // ── 2b. Apply broker symbol suffix (e.g. "m" → GBPUSDm for Exness) ──
                if (!string.IsNullOrEmpty(_cfg.SymbolSuffix) &&
                    !request.Pair.EndsWith(_cfg.SymbolSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    request.Pair = request.Pair.ToUpperInvariant() + _cfg.SymbolSuffix;
                    Log($"[BOT] Symbol suffix applied: {request.Pair}");
                }

                // ── 3. Daily limit ─────────────────────────────────
                if (_tradesToday >= _cfg.MaxTradesPerDay)
                    return Fail(request.Id, "DAILY_LIMIT",
                        $"Daily trade limit {_cfg.MaxTradesPerDay} reached");

                // ── 4. Get live account ────────────────────────────
                var account = await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
                if (account == null)
                    return Fail(request.Id, "NO_ACCOUNT", "Could not fetch account info from MT5");

                // ── 5. Fetch live symbol info (price + spread) ─────
                // Single call reused for risk validation and slippage checks.
                var symbolInfo = await _bridge.GetSymbolInfoAsync(request.Pair).ConfigureAwait(false);
                var pairRules = _pairSettings?.GetForPair(request.Pair);
                if (pairRules != null)
                    Log($"[BOT] Pair-specific rules loaded for {pairRules.Pair}: max spread {pairRules.MaxSpreadPips:F1}, SL {pairRules.MinSlPips:F1}-{pairRules.MaxSlPips:F1} pips, min TP {pairRules.MinTpPips:F1} pips, min R:R {pairRules.ScalpingMinRR:F2}.");

                // Real market price: Ask for BUY, Bid for SELL
                double livePrice = symbolInfo != null
                    ? (request.TradeType == Models.TradeType.BUY ? symbolInfo.Ask : symbolInfo.Bid)
                    : 0;

                // ── 6. Risk validation (delegated to RiskManager) ──
                var openPositions = await _bridge.GetPositionsAsync().ConfigureAwait(false);

                // Include simulated positions so risk and correlation checks see them
                if (_cfg.PaperTrading && _paperPositions.Count > 0)
                {
                    lock (_paperPositions)
                        openPositions = [.. openPositions, .. _paperPositions];
                }

                // Max concurrent positions cap
                if (_cfg.MaxConcurrentPositions > 0)
                {
                    int botPositions = openPositions.Count(p => p.MagicNumber == _cfg.MagicNumber);
                    if (botPositions >= _cfg.MaxConcurrentPositions)
                        return Fail(request.Id, "MAX_CONCURRENT_POSITIONS",
                            $"Already have {botPositions} open position(s) " +
                            $"(max {_cfg.MaxConcurrentPositions}). Close one first.");
                }

                var riskResult = await _riskManager.ValidateAsync(
                    request, account, symbolInfo, openPositions, _cfg, _cts.Token)
                    .ConfigureAwait(false);

                if (!riskResult.IsApproved)
                {
                    Log($"[RISK BLOCKED] {riskResult.Reason}");
                    await _telegram.NotifyRiskBlockedAsync(request, riskResult.Reason)
                        .ConfigureAwait(false);
                    return Fail(request.Id, "RISK_BLOCKED", riskResult.Reason);
                }

                // Apply validated lot size from RiskManager
                request.LotSize = riskResult.ValidatedLotSize >= 0.01
                    ? riskResult.ValidatedLotSize
                    : request.LotSize;

                Log($"[BOT] Risk OK: lot={request.LotSize:F2} " +
                    $"risk={riskResult.RiskPercent:F1}% (${riskResult.DollarRisk:F2}) " +
                    $"R:R={riskResult.RiskRewardRatio:F2} spread={riskResult.SpreadPips:F1}pips");

                foreach (var warning in riskResult.Warnings)
                    Log($"[WARN] {warning}");

                // ── 9b. Correlation check ──────────────────────────────
                if (_cfg.CorrelationCheckEnabled)
                {
                    var openSymbols = openPositions
                        .Where(p => p.MagicNumber == _cfg.MagicNumber)
                        .Select(p => p.Symbol);

                    string? blocking = Core.CorrelationGroups.FindBlockingSymbol(
                        request.Pair, openSymbols, _cfg.SymbolSuffix);

                    if (blocking != null)
                        return Fail(request.Id, "CORRELATION_BLOCK",
                            $"Correlated position already open: {blocking}. " +
                            $"Close it first or set correlation_check_enabled=false to override.");
                }

                // ── 11. Execute ────────────────────────────────────
                if (_newsCalendar != null)
                {
                    var news = await _newsCalendar.GetRiskSnapshotAsync(request.Pair, _apiConfig).ConfigureAwait(false);
                    bool providerDisabled = string.Equals(_apiConfig.NewsProvider, "None", StringComparison.OrdinalIgnoreCase);

                    if (providerDisabled)
                    {
                        Log("[BOT] News filter disabled in AI API Config.");
                    }
                    else if (!news.IsConfigured)
                    {
                        if (_apiConfig.BlockTradesWhenNewsUnavailable)
                            return Fail(request.Id, "NEWS_UNAVAILABLE", news.Reason);

                        Log($"[WARN] News check unavailable: {news.Reason}");
                    }
                    else
                    {
                        Log($"[BOT] News risk: {news.RiskLevel} - {news.Reason}");
                        if (_apiConfig.BlockTradesOnHighImpactNews &&
                            (news.IsBlackoutActive || news.HighImpactNext60Minutes))
                        {
                            return Fail(request.Id, "NEWS_BLACKOUT", news.Reason);
                        }
                    }
                }

                if (request.TakeProfit2 > 0)
                    Log($"[BOT] TP2 {request.TakeProfit2:F5} detected but one-click mode opens only one trade using TP {request.TakeProfit:F5}.");

                Log($"[BOT] Sending trade to MT5 (R:R {riskResult.RiskRewardRatio:F2}, " +
                    $"lot {request.LotSize:F2})");
                var result = _cfg.PaperTrading
                    ? SimulatePaperTrade(request, livePrice)
                    : await _bridge.OpenTradeAsync(request).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    _tradesToday++;
                    Log($"[OK] MT5 accepted ticket #{result.Ticket} | Trades today: {_tradesToday}/{_cfg.MaxTradesPerDay}");
                    await _telegram.NotifyTradeOpenedAsync(result, request)
                        .ConfigureAwait(false);

                    // Slippage check: only for MARKET orders where we have a live reference price
                    double maxSlippagePips = pairRules?.MaxSlippagePips > 0
                        ? pairRules.MaxSlippagePips
                        : _cfg.MaxSlippagePips;
                    if (request.OrderType == OrderType.MARKET &&
                        maxSlippagePips > 0 &&
                        livePrice > 0 &&
                        result.ExecutedPrice > 0)
                    {
                        double pipSize = LotCalculator.GetPipSize(request.Pair.ToUpperInvariant());
                        double slippagePips = Math.Abs(result.ExecutedPrice - livePrice) / pipSize;
                        if (slippagePips > maxSlippagePips * 2)
                        {
                            Log($"[RISK] Extreme slippage ({slippagePips:F1} pips > {maxSlippagePips * 2:F1} limit×2)" +
                                $" — closing #{result.Ticket}");

                            bool closed = await _bridge.CloseTradeAsync(result.Ticket).ConfigureAwait(false);

                            Log(closed
                                ? $"[RISK] Position #{result.Ticket} closed due to extreme slippage."
                                : $"[ERROR] Failed to close #{result.Ticket} after extreme slippage.");

                            await _telegram.SendAsync(
                                $"<b>⚠️ EXTREME SLIPPAGE — POSITION CLOSED</b>\n" +
                                $"Ticket: #{result.Ticket}  {request.Pair}\n" +
                                $"Slippage: {slippagePips:F1} pips (max: {maxSlippagePips:F1})\n" +
                                $"Expected: {livePrice:F5}  Filled: {result.ExecutedPrice:F5}\n" +
                                $"Position {(closed ? "CLOSED ✅" : "CLOSE FAILED ❌")}")
                                .ConfigureAwait(false);
                        }
                        else if (slippagePips > maxSlippagePips)
                        {
                            Log($"[WARN] HIGH SLIPPAGE on #{result.Ticket}: {slippagePips:F1} pips " +
                                $"(expected {livePrice:F5}, filled {result.ExecutedPrice:F5})");

                            await _telegram.SendAsync(
                                $"<b>⚠️ High Slippage Warning</b>\n" +
                                $"#{result.Ticket} {request.Pair}: {slippagePips:F1} pips slippage " +
                                $"(max {maxSlippagePips:F1})")
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            Log($"[BOT] Slippage: {slippagePips:F1} pips (max {maxSlippagePips:F1})");
                        }
                    }
                }
                else
                {
                    Log($"[ERROR] MT5 rejected: {result.ErrorMessage}");
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
            _trailingActiveTickets.IntersectWith(openTickets);

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

                double beTriggerPct = _cfg.SlToBeTrigerPct > 0 && _cfg.SlToBeTrigerPct <= 1.0 ? _cfg.SlToBeTrigerPct : 0.6;
                bool shouldMoveSL = currentMove >= tpDistance * beTriggerPct;

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
        //  TRAILING STOP
        // ══════════════════════════════════════════════════════════

        private async Task CheckTrailingStopAsync()
        {
            if (!_bridge.IsConnected) return;

            List<LivePosition> positions;
            try { positions = await _bridge.GetPositionsAsync().ConfigureAwait(false); }
            catch { return; }

            foreach (var pos in positions)
            {
                if (pos.MagicNumber != _cfg.MagicNumber) continue;

                var rules = _pairSettings?.GetForPair(pos.Symbol);
                if (rules == null || rules.TrailingStartPips <= 0 || rules.TrailingStepPips <= 0)
                    continue;

                double pipSize = rules.PipSize > 0
                    ? rules.PipSize
                    : LotCalculator.GetPipSize(pos.Symbol);

                if (pipSize <= 0) continue;

                double profitPips = pos.Type == Models.TradeType.BUY
                    ? (pos.CurrentPrice - pos.OpenPrice) / pipSize
                    : (pos.OpenPrice - pos.CurrentPrice) / pipSize;

                if (profitPips < rules.TrailingStartPips) continue;

                // Ideal trailing SL: keep TrailingStepPips behind current price
                double idealSl = pos.Type == Models.TradeType.BUY
                    ? pos.CurrentPrice - rules.TrailingStepPips * pipSize
                    : pos.CurrentPrice + rules.TrailingStepPips * pipSize;

                // Round to the same decimal precision as the current SL to avoid noise
                int digits = pos.StopLoss.ToString("F5").TrimEnd('0').Length - 1;
                digits = Math.Max(4, Math.Min(digits, 5));
                idealSl = Math.Round(idealSl, digits);

                // Only move SL if it improves position (never move backward)
                bool improvesPosition = pos.Type == Models.TradeType.BUY
                    ? idealSl > pos.StopLoss
                    : idealSl < pos.StopLoss;

                if (!improvesPosition) continue;

                // Must not exceed TakeProfit boundary
                if (pos.Type == Models.TradeType.BUY && idealSl >= pos.TakeProfit) continue;
                if (pos.Type == Models.TradeType.SELL && idealSl <= pos.TakeProfit) continue;

                Log($"📈 Trailing SL #{pos.Ticket} {pos.Symbol}: {pos.StopLoss:F5} → {idealSl:F5} " +
                    $"(profit {profitPips:F1} pips, step {rules.TrailingStepPips:F1} pips)");

                bool ok = await _bridge.ModifyPositionAsync(
                    pos.Ticket, idealSl, pos.TakeProfit).ConfigureAwait(false);

                if (ok)
                {
                    _trailingActiveTickets.Add(pos.Ticket);

                    // If trailing is now past breakeven, mark as BE-moved so the
                    // BE check does not attempt a redundant modify on the same ticket.
                    bool pastBreakeven = pos.Type == Models.TradeType.BUY
                        ? idealSl >= pos.OpenPrice
                        : idealSl <= pos.OpenPrice;
                    if (pastBreakeven)
                        _slMovedTickets.Add(pos.Ticket);
                }
            }
        }

        // CLOSED POSITION DETECTION
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

                    if (_tradeDb != null)
                        _ = _tradeDb.UpdateCloseAsync(closed.Ticket, closed.Profit, DateTime.UtcNow);

                    if (_edgeMonitor != null)
                    {
                        var status = _edgeMonitor.Record(closed.Profit);
                        Log($"[EdgeMonitor] Win rate: {status.WinRatePct:F1}% " +
                            $"({status.SampleSize} trades), " +
                            $"Consecutive losses: {status.ConsecutiveLosses}");

                        if (status.IsDegraded && !_edgePaused)
                        {
                            _edgePaused = true;
                            Log("[EdgeMonitor] Edge degraded - auto-pausing new trade execution.");
                            _ = _telegram.SendRiskBlockedAsync("ALL",
                                $"Edge health degraded: win rate {status.WinRatePct:F1}% " +
                                $"({status.SampleSize} trades), " +
                                $"{status.ConsecutiveLosses} consecutive losses. " +
                                "Bot paused for new entries.").ConfigureAwait(false);
                        }

                        OnEdgeStatusChanged?.Invoke(status);
                    }
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

                if (_tradeDb != null)
                    _ = _tradeDb.InsertAsync(req, result);

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

        /// <summary>Called by MainForm after manually archiving a signal file, so the watcher stops tracking it.</summary>
        public void SignalFileArchived(string originalPath) => _shownPaths.Remove(originalPath);

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            _cts.Dispose();
            _tradeLock.Dispose();
            _fileLock.Dispose();
        }
    }
}
