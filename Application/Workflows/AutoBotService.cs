using MT5TradingBot.Core;
using MT5TradingBot.Data;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Modules.Deployment;
using MT5TradingBot.Modules.NewsFilter;
using MT5TradingBot.Modules.PairSettings;
using MT5TradingBot.Modules.RiskManagement;
using MT5TradingBot.Modules.TradeExecution;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private readonly ITradeExecutionService _tradeExecution;
        private readonly ITelegramService _telegram;
        private readonly ITradeRepository? _tradeDb;
        private readonly Func<DateTime> _utcNow;

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
        private readonly Dictionary<long, string> _entryH1Trends = []; // H1 trend at entry per ticket
        private volatile bool _running;
        private int _tradesToday;
        private DateTime _dayReset = DateTime.Today;
        private double _startOfDayEquity;
        private bool _emergencyStopFired;
        private KillSwitchState _killSwitchState = new();
        private bool _killSwitchLoaded;
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
        public bool IsKillSwitchActive
        {
            get
            {
                EnsureKillSwitchLoaded();
                return _killSwitchState.KillSwitchActive;
            }
        }

        public KillSwitchState CurrentKillSwitchState
        {
            get
            {
                EnsureKillSwitchLoaded();
                return CopyKillSwitchState(_killSwitchState);
            }
        }

        private BotMode _currentMode = BotMode.ManualApproval;
        public BotMode CurrentMode => _currentMode;

        // Derived read-only for backward compatibility with card logic that reads this.
        public bool ManualExecuteOnly => _currentMode == BotMode.ManualApproval;

        public event Action<BotMode>? OnModeChanged;

        public void SetMode(BotMode newMode)
        {
            if (newMode == _currentMode) return;
            EnsureKillSwitchLoaded();

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
        private string KillSwitchFile => string.IsNullOrWhiteSpace(_cfg.KillSwitchStateFile)
            ? Path.Combine(Core.AppPaths.ConfigDirectory, "kill_switch.json")
            : _cfg.KillSwitchStateFile;

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
            ITradeExecutionService? tradeExecution = null,
            ITradeRepository? tradeRepository = null,
            Func<DateTime>? utcNowProvider = null)
        {
            _bridge = bridge;
            _cfg = cfg;
            _pairSettings = pairSettings;
            _newsCalendar = newsCalendar;
            _apiConfig = apiConfig ?? new ApiIntegrationConfig();
            _riskManager = riskManager ?? new RiskManager(_pairSettings);
            _tradeExecution = tradeExecution ?? new TradeExecutionService(bridge);
            _tradeDb = tradeRepository;
            _utcNow = utcNowProvider ?? (() => DateTime.UtcNow);
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
            EnsureKillSwitchLoaded();
            _emergencyStopFired = _killSwitchState.KillSwitchActive;
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
            _killSwitchLoaded = false;
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
            var commission = CommissionCalculator.EstimateRoundTurn(req.LotSize, _cfg);
            double estimatedCommission = commission.Success ? commission.Amount : 0;
            var slippage = SlippageCalculator.EstimateCost(req.Pair, req.LotSize, _cfg);
            double estimatedSlippageCost = slippage.Success ? slippage.CostUsd : 0;

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
                EstimatedCommission = estimatedCommission,
                EstimatedSlippageCost = estimatedSlippageCost,
                EstimatedSlippagePips = slippage.Success ? slippage.Pips : 0,
                MagicNumber  = req.MagicNumber,
                Comment      = "[PAPER] " + req.Comment,
                OpenTime     = DateTime.UtcNow
            };

            lock (_paperPositions)
                _paperPositions.Add(pos);

            Log($"[PAPER] Simulated {req.TradeType} {req.Pair} lot={req.LotSize:F2} " +
                $"@ {fillPrice:F5}  SL={req.StopLoss:F5}  TP={req.TakeProfit:F5}  ticket=#{ticket}");

            if (estimatedCommission > 0)
                Log($"[PAPER] Estimated commission for #{ticket}: {estimatedCommission:F2} {commission.Currency}");

            if (estimatedSlippageCost > 0)
                Log($"[PAPER] Estimated slippage for #{ticket}: {slippage.Pips:F1} pips, " +
                    $"cost {estimatedSlippageCost:F2} USD");

            return new TradeResult
            {
                RequestId     = req.Id,
                Status        = TradeStatus.Filled,
                Ticket        = ticket,
                ExecutedPrice = fillPrice,
                ExecutedLots  = req.LotSize,
                EstimatedCommission = estimatedCommission,
                CommissionCurrency = commission.Currency,
                EstimatedSlippageCost = estimatedSlippageCost,
                EstimatedSlippagePips = slippage.Success ? slippage.Pips : 0,
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
                    pos.CurrentPrice = closePrice;
                    pos.Profit       = PipCalculator.ProfitUsd(
                        pos.Type,
                        pos.OpenPrice,
                        closePrice,
                        pos.Lots,
                        pos.Symbol) - pos.EstimatedCommission - pos.EstimatedSlippageCost;

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
                double profitUsd = PipCalculator.ProfitUsd(
                    pos.Type,
                    pos.OpenPrice,
                    closePrice,
                    pos.Lots,
                    pos.Symbol) - pos.EstimatedCommission - pos.EstimatedSlippageCost;
                pos.Profit = profitUsd;

                Log($"[PAPER] #{pos.Ticket} {pos.Symbol} {pos.Type} closed at {reason} " +
                    $"{closePrice:F5} | P&L: {profitUsd:+0.00;-0.00} USD" +
                    (pos.EstimatedCommission > 0 ? $" (commission {pos.EstimatedCommission:F2})" : "") +
                    (pos.EstimatedSlippageCost > 0 ? $" (slippage {pos.EstimatedSlippageCost:F2})" : ""));
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
            _killSwitchLoaded = false;
        }

        public void UpdateApiConfig(ApiIntegrationConfig newCfg)
        {
            _apiConfig = newCfg;
        }

        public void ClearKillSwitchByUser(string reason = "Manual clear")
        {
            EnsureKillSwitchLoaded();
            _killSwitchState = new KillSwitchState
            {
                KillSwitchActive = false,
                KillSwitchReason = string.IsNullOrWhiteSpace(reason) ? "Manual clear" : reason
            };
            _emergencyStopFired = false;
            _killSwitchLoaded = true;
            PersistKillSwitchState();
            Log("[SAFETY] Kill switch cleared by explicit user action.");
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
                        EnsureKillSwitchLoaded();
                        _emergencyStopFired = _killSwitchState.KillSwitchActive;
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

                    // H1 trend reversal management
                    await CheckH1TrendReversalAsync().ConfigureAwait(false);

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

            if (_cfg.PaperTrading || !IsOrderRetryPolicyEnabled(_cfg))
                return await ExecuteTradeWithValidationCoreAsync(request).ConfigureAwait(false);

            int attempts = GetMaxOrderSendAttempts(_cfg);

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                result = await ExecuteTradeWithValidationCoreAsync(request).ConfigureAwait(false);
                result.OrderSendAttempts = attempt;

                if (result.IsSuccess) return result;

                OrderFailureClassifier.ApplyClassification(result);
                Log($"[BOT] Order attempt {attempt}/{attempts} failed: " +
                    $"{result.ErrorCode} {result.ErrorMessage}");

                if (!OrderFailureClassifier.IsRetryable(result))
                {
                    if (IsBrokerOrderFailure(result))
                        result.ErrorCode = string.IsNullOrWhiteSpace(result.OrderFailureCode)
                            ? "BROKER_REJECTED_PERMANENT"
                            : result.OrderFailureCode;
                    return result;
                }

                if (attempt < attempts)
                {
                    int delayMs = GetOrderRetryDelayMs(_cfg);
                    Log($"[BOT] Retrying transient order failure {attempt}/{attempts} in {delayMs}ms. " +
                        "Live safety gates will be rechecked.");
                    await Task.Delay(delayMs, _cts.Token).ConfigureAwait(false);
                }
            }

            if (!result.IsSuccess && OrderFailureClassifier.IsRetryable(result))
            {
                string finalFailure = string.IsNullOrWhiteSpace(result.OrderFailureCode)
                    ? result.ErrorCode
                    : result.OrderFailureCode;
                result.ErrorCode = "ORDER_RETRY_EXHAUSTED";
                result.ErrorMessage = $"Order retry policy exhausted after {attempts} attempt(s). " +
                    $"Final failure: {finalFailure} - {result.ErrorMessage}";
                Log($"[BOT] Final order result: {result.ErrorCode} {result.ErrorMessage}");
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════
        //  TRADE VALIDATION + EXECUTION  (public for manual trades)
        // ══════════════════════════════════════════════════════════

        public async Task<TradeResult> ExecuteTradeWithValidationAsync(TradeRequest request)
        {
            return await ExecuteWithRetryAsync(request).ConfigureAwait(false);
        }

        private async Task<TradeResult> ExecuteTradeWithValidationCoreAsync(TradeRequest request)
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

            EnsureKillSwitchLoaded();
            if (!_cfg.PaperTrading && (_killSwitchState.KillSwitchActive || _emergencyStopFired))
            {
                string reason = string.IsNullOrWhiteSpace(_killSwitchState.KillSwitchReason)
                    ? "Kill switch is active after emergency drawdown."
                    : _killSwitchState.KillSwitchReason;
                return Fail(request.Id, "KILL_SWITCH_ACTIVE", reason);
            }

            await _tradeLock.WaitAsync(_cts.Token).ConfigureAwait(false);
            try
            {
                bool liveMode = !_cfg.PaperTrading;
                var layerLog = new List<LayerResult>();
                bool tradeExecuted = false;
                try
                {

                var rolloutBlock = CheckRolloutStage(request.Id, liveMode);
                if (rolloutBlock != null)
                    return rolloutBlock;

                var noTradeWindowBlock = CheckNoTradeWindow(request.Id, liveMode);
                if (noTradeWindowBlock != null)
                    return noTradeWindowBlock;

                // ── Session time gate (London + NY: 07:00–16:00 UTC) ───────
                {
                    int utcHour = DateTime.UtcNow.Hour;
                    bool sessionOk = utcHour >= 7 && utcHour < 16;
                    layerLog.Add(new LayerResult("SESSION_GATE", sessionOk, $"UTC={DateTime.UtcNow:HH:mm}"));
                    if (!sessionOk)
                        return Fail(request.Id, "SESSION_CLOSED",
                            $"UTC {DateTime.UtcNow:HH:mm} is outside the London/NY session window " +
                            $"(07:00–16:00 UTC). No trade.");
                }

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
                AccountInfo? account;
                try
                {
                    account = await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log($"[SAFETY] Account data unavailable: {ex.Message}");
                    return Fail(request.Id, "NO_ACCOUNT", "Could not fetch account info from MT5");
                }

                if (account == null || !HasUsableAccountData(account))
                    return Fail(request.Id, "NO_ACCOUNT", "Could not fetch account info from MT5");

                // ── 5. Fetch live symbol info (price + spread) ─────
                // Single call reused for risk validation and slippage checks.
                SymbolInfo? symbolInfo;
                try
                {
                    symbolInfo = await _bridge.GetSymbolInfoAsync(request.Pair).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (liveMode)
                    {
                        Log($"[SAFETY] Symbol/spread data unavailable: {ex.Message}");
                        return Fail(request.Id, "NO_SYMBOL_DATA",
                            $"Could not fetch valid symbol/spread data for {request.Pair} from MT5");
                    }

                    Log($"[WARN] Symbol/spread data unavailable in paper mode: {ex.Message}");
                    symbolInfo = null;
                }
                var pairRules = _pairSettings?.GetForPair(request.Pair);
                if (pairRules != null)
                    Log($"[BOT] Pair-specific rules loaded for {pairRules.Pair}.");

                if (liveMode && !HasUsableSymbolSafetyData(symbolInfo))
                    return Fail(request.Id, "NO_SYMBOL_DATA",
                        $"Could not fetch valid symbol/spread data for {request.Pair} from MT5");

                var sessionSpreadBlock = CheckSessionSpread(
                    request.Id,
                    request.Pair,
                    symbolInfo!,
                    liveMode);
                if (sessionSpreadBlock != null)
                    return sessionSpreadBlock;

                // Real market price: Ask for BUY, Bid for SELL
                double livePrice = symbolInfo != null
                    ? (request.TradeType == Models.TradeType.BUY ? symbolInfo.Ask : symbolInfo.Bid)
                    : 0;
                ApplyTradePageSlTp(request, symbolInfo, livePrice);

                // ── 6. Risk validation (delegated to RiskManager) ──
                var stopLevelBlock = CheckBrokerStopLevel(
                    request.Id,
                    request,
                    symbolInfo,
                    livePrice,
                    liveMode);
                if (stopLevelBlock != null)
                    return stopLevelBlock;

                var freezeLevelBlock = CheckBrokerFreezeLevel(
                    request.Id,
                    request,
                    symbolInfo,
                    livePrice,
                    liveMode);
                if (freezeLevelBlock != null)
                    return freezeLevelBlock;

                List<LivePosition> openPositions;
                try
                {
                    var positionsResult = await _bridge.TryGetPositionsAsync().ConfigureAwait(false);
                    if (!positionsResult.Success)
                    {
                        if (liveMode)
                        {
                            bool exposureEnabled = IsSymbolExposureLimitEnabled(_cfg);
                            string code = exposureEnabled
                                ? "SYMBOL_EXPOSURE_DATA_UNAVAILABLE"
                                : "RISK_DATA_UNAVAILABLE";
                            string message = exposureEnabled
                                ? "Could not fetch open-position exposure data from MT5"
                                : "Could not fetch open-position risk data from MT5";
                            return Fail(request.Id, code, message);
                        }

                        Log("[WARN] Open-position data unavailable in paper mode.");
                        openPositions = [];
                    }
                    else
                    {
                        openPositions = positionsResult.Positions;
                    }
                }
                catch (Exception ex)
                {
                    if (liveMode)
                    {
                        bool exposureEnabled = IsSymbolExposureLimitEnabled(_cfg);
                        string code = exposureEnabled
                            ? "SYMBOL_EXPOSURE_DATA_UNAVAILABLE"
                            : "RISK_DATA_UNAVAILABLE";
                        string message = exposureEnabled
                            ? "Could not fetch open-position exposure data from MT5"
                            : "Could not fetch open-position risk data from MT5";
                        Log($"[SAFETY] Open-position data unavailable: {ex.Message}");
                        return Fail(request.Id, code, message);
                    }

                    Log($"[WARN] Open-position data unavailable in paper mode: {ex.Message}");
                    openPositions = [];
                }

                // Include simulated positions so risk and correlation checks see them
                if (_cfg.PaperTrading && _paperPositions.Count > 0)
                {
                    lock (_paperPositions)
                        openPositions = [.. openPositions, .. _paperPositions];
                }

                var dailyLossBlock = await CheckDailyLossHardStopAsync(
                    request.Id,
                    account,
                    openPositions,
                    liveMode).ConfigureAwait(false);
                if (dailyLossBlock != null)
                    return dailyLossBlock;

                var weeklyLossBlock = await CheckWeeklyLossHardStopAsync(
                    request.Id,
                    account,
                    openPositions,
                    liveMode).ConfigureAwait(false);
                if (weeklyLossBlock != null)
                    return weeklyLossBlock;

                var symbolExposureBlock = CheckSymbolExposureLimit(
                    request.Id,
                    request,
                    account,
                    openPositions,
                    livePrice,
                    liveMode);
                if (symbolExposureBlock != null)
                    return symbolExposureBlock;

                // Max concurrent positions cap
                if (_cfg.MaxConcurrentPositions > 0)
                {
                    int botPositions = openPositions.Count(p => p.MagicNumber == _cfg.MagicNumber);
                    if (botPositions >= _cfg.MaxConcurrentPositions)
                        return Fail(request.Id, "MAX_CONCURRENT_POSITIONS",
                            $"Already have {botPositions} open position(s) " +
                            $"(max {_cfg.MaxConcurrentPositions}). Close one first.");
                }

                RiskValidationResult? riskResult;
                try
                {
                    riskResult = await _riskManager.ValidateAsync(
                        request, account, symbolInfo, openPositions, _cfg, _cts.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log($"[SAFETY] Risk validation unavailable: {ex.Message}");
                    return Fail(request.Id, "RISK_DATA_UNAVAILABLE",
                        "Risk validation failed or safety data was unavailable");
                }

                if (riskResult == null)
                    return Fail(request.Id, "RISK_DATA_UNAVAILABLE",
                        "Risk validation returned no result");

                if (liveMode && !HasCompleteRiskData(riskResult))
                    return Fail(request.Id, "RISK_DATA_UNAVAILABLE",
                        "Risk validation returned incomplete safety data");

                layerLog.Add(new LayerResult("RISK", riskResult.IsApproved,
                    $"RR={riskResult.RiskRewardRatio:F2} risk={riskResult.RiskPercent:F1}%"));
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

                var lotSizeBlock = CheckBrokerLotSize(
                    request.Id,
                    request,
                    symbolInfo,
                    liveMode);
                if (lotSizeBlock != null)
                    return lotSizeBlock;

                var commissionBlock = CheckCommissionModel(
                    request.Id,
                    request,
                    liveMode,
                    out var commissionEstimate);
                if (commissionBlock != null)
                    return commissionBlock;

                var slippageBlock = CheckSlippageModel(
                    request.Id,
                    request,
                    liveMode,
                    out var slippageEstimate);
                if (slippageBlock != null)
                    return slippageBlock;

                symbolExposureBlock = CheckSymbolExposureLimit(
                    request.Id,
                    request,
                    account,
                    openPositions,
                    livePrice,
                    liveMode);
                if (symbolExposureBlock != null)
                    return symbolExposureBlock;

                var marginBlock = await CheckProjectedMarginHardStopAsync(
                    request.Id,
                    request,
                    account,
                    livePrice,
                    liveMode).ConfigureAwait(false);
                if (marginBlock != null)
                    return marginBlock;

                Log($"[BOT] Risk OK: lot={request.LotSize:F2} " +
                    $"risk={riskResult.RiskPercent:F1}% (${riskResult.DollarRisk:F2}) " +
                    $"R:R={riskResult.RiskRewardRatio:F2} spread={riskResult.SpreadPips:F1}pips");

                if (commissionEstimate.Success && commissionEstimate.Amount > 0)
                    Log($"[BOT] Estimated commission: {commissionEstimate.Amount:F2} {commissionEstimate.Currency} " +
                        $"for {request.LotSize:F2} lot(s).");

                if (slippageEstimate.Success && slippageEstimate.CostUsd > 0)
                    Log($"[BOT] Estimated slippage: {slippageEstimate.Pips:F1} pips, " +
                        $"cost {slippageEstimate.CostUsd:F2} USD for {request.LotSize:F2} lot(s).");

                foreach (var warning in riskResult.Warnings)
                    Log($"[WARN] {warning}");

                // ── Round number awareness (XAUUSD) ───────────────────────
                if (request.Pair.Contains("XAU", StringComparison.OrdinalIgnoreCase))
                {
                    const double xauPipSize = 0.1;
                    if (IsNearRoundNumber(request.TakeProfit, xauPipSize))
                        Log($"[WARN] ROUND_NUMBER_WARNING | TP {request.TakeProfit:F2} is within 20 pips of a round level | {request.Pair}");
                    if (IsNearRoundNumber(request.StopLoss, xauPipSize))
                        Log($"[WARN] ROUND_NUMBER_WARNING | SL {request.StopLoss:F2} is within 20 pips of a round level | {request.Pair}");
                }

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
                bool newsProviderDisabled = string.Equals(
                    _apiConfig.NewsProvider,
                    "None",
                    StringComparison.OrdinalIgnoreCase);

                if (liveMode && !newsProviderDisabled && _newsCalendar == null)
                    return Fail(request.Id, "NEWS_UNAVAILABLE",
                        "News calendar service is unavailable in live mode");

                if (_newsCalendar != null)
                {
                    NewsRiskSnapshot? news = null;
                    try
                    {
                        news = await _newsCalendar.GetRiskSnapshotAsync(request.Pair, _apiConfig)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (liveMode && !newsProviderDisabled)
                        {
                            Log($"[SAFETY] News data unavailable: {ex.Message}");
                            return Fail(request.Id, "NEWS_UNAVAILABLE",
                                "News risk data is unavailable in live mode");
                        }

                        Log($"[WARN] News check unavailable: {ex.Message}");
                    }

                    if (newsProviderDisabled)
                    {
                        Log("[BOT] News filter disabled in AI API Config.");
                    }
                    else if (news == null || !news.IsConfigured)
                    {
                        string reason = news?.Reason ?? "News risk data is unavailable";
                        if (liveMode || _apiConfig.BlockTradesWhenNewsUnavailable)
                            return Fail(request.Id, "NEWS_UNAVAILABLE", reason);

                        Log($"[WARN] News check unavailable: {reason}");
                    }
                    else
                    {
                        Log($"[BOT] News risk: {news.RiskLevel} - {news.Reason}");
                        bool newsBlocked = _apiConfig.BlockTradesOnHighImpactNews &&
                            (news.IsBlackoutActive || news.HighImpactNext60Minutes);
                        layerLog.Add(new LayerResult("NEWS", !newsBlocked, news.Reason));
                        if (newsBlocked)
                        {
                            return Fail(request.Id, "NEWS_BLACKOUT", news.Reason);
                        }
                    }
                }

                // ── ADX ranging block + AI SL vs swing validation ─────────
                string capturedH1Trend = "";
                try
                {
                    JObject? adxSnapshot = await _bridge.GetMarketSnapshotAsync(request, _cfg).ConfigureAwait(false);
                    if (adxSnapshot != null)
                    {
                        double snapshotAdx = adxSnapshot["indicators"]?["m5"]?["adx"]?.Value<double>() ?? 0;
                        bool adxOk = !(snapshotAdx > 0 && snapshotAdx < 20);
                        layerLog.Add(new LayerResult("ADX_RANGING", adxOk, $"ADX={snapshotAdx:F1}"));
                        if (!adxOk)
                            return Fail(request.Id, "ADX_RANGING",
                                $"ADX {snapshotAdx:F1} — market is ranging. No trade.");

                        capturedH1Trend = adxSnapshot["structure"]?["trend_h1"]?.ToString() ?? "";

                        // Warn if AI SL is far from the nearest swing level
                        double swingLow  = adxSnapshot["structure"]?["swing_low"]?.Value<double>() ?? 0;
                        double swingHigh = adxSnapshot["structure"]?["swing_high"]?.Value<double>() ?? 0;
                        double slPipSize = request.Pair.Contains("XAU", StringComparison.OrdinalIgnoreCase) ? 0.1 : 0.0001;
                        if (request.TradeType == Models.TradeType.BUY && swingLow > 0 && request.StopLoss > 0)
                        {
                            double distPips = Math.Abs(request.StopLoss - swingLow) / slPipSize;
                            if (distPips > 50)
                                Log($"[WARN] AI_SL_WARNING | SL {request.StopLoss:F5} is {distPips:F0} pips from swing low {swingLow:F5} | {request.Pair}");
                        }
                        if (request.TradeType == Models.TradeType.SELL && swingHigh > 0 && request.StopLoss > 0)
                        {
                            double distPips = Math.Abs(request.StopLoss - swingHigh) / slPipSize;
                            if (distPips > 50)
                                Log($"[WARN] AI_SL_WARNING | SL {request.StopLoss:F5} is {distPips:F0} pips from swing high {swingHigh:F5} | {request.Pair}");
                        }
                    }
                }
                catch (Exception adxEx)
                {
                    Log($"[WARN] ADX/swing snapshot unavailable — gate skipped: {adxEx.Message}");
                }

                if (request.TakeProfit2 > 0)
                    Log($"[BOT] TP2 {request.TakeProfit2:F5} detected but one-click mode opens only one trade using TP {request.TakeProfit:F5}.");

                Log($"[BOT] Sending trade to MT5 (R:R {riskResult.RiskRewardRatio:F2}, " +
                    $"lot {request.LotSize:F2})");
                var result = _cfg.PaperTrading
                    ? SimulatePaperTrade(request, livePrice)
                    : await _tradeExecution.ExecuteAsync(
                        request,
                        riskResult,
                        CreateWorkflowApproval(request),
                        _cts.Token,
                        livePrice).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    if (commissionEstimate.Success)
                    {
                        result.EstimatedCommission = commissionEstimate.Amount;
                        result.CommissionCurrency = commissionEstimate.Currency;
                    }

                    if (slippageEstimate.Success)
                    {
                        result.EstimatedSlippageCost = slippageEstimate.CostUsd;
                        result.EstimatedSlippagePips = slippageEstimate.Pips;
                    }

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
                        double slippagePips = PipCalculator.DistanceInPips(result.ExecutedPrice, livePrice, pipSize);
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

                tradeExecuted = result.IsSuccess;
                if (result.IsSuccess && result.Ticket > 0 && !string.IsNullOrEmpty(capturedH1Trend))
                    _entryH1Trends[result.Ticket] = capturedH1Trend;
                return result;

                } // inner try
                finally
                {
                    int auditPassed = layerLog.Count(l => l.Passed);
                    string auditFailed = string.Join(", ", layerLog.Where(l => !l.Passed).Select(l => l.Layer));
                    Log($"TRADE_AUDIT | {request.Pair} {request.TradeType} | " +
                        $"{auditPassed}/{layerLog.Count} layers passed | " +
                        $"Failed: [{auditFailed}] | Executed: {tradeExecuted}");
                }
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
        //  H1 TREND REVERSAL MANAGEMENT
        // ══════════════════════════════════════════════════════════

        private async Task CheckH1TrendReversalAsync()
        {
            if (!_bridge.IsConnected || _entryH1Trends.Count == 0) return;
            List<LivePosition> positions;
            try { positions = await _bridge.GetPositionsAsync().ConfigureAwait(false); }
            catch { return; }

            var openTickets = new HashSet<long>(positions.Select(p => p.Ticket));
            var staleKeys = _entryH1Trends.Keys.Where(t => !openTickets.Contains(t)).ToList();
            foreach (var t in staleKeys) _entryH1Trends.Remove(t);

            foreach (var pos in positions)
            {
                if (pos.MagicNumber != _cfg.MagicNumber) continue;
                if (!_entryH1Trends.TryGetValue(pos.Ticket, out string? entryTrend)) continue;
                if (_slMovedTickets.Contains(pos.Ticket)) continue;

                JObject? snap = null;
                try
                {
                    var probe = new TradeRequest { Pair = pos.Symbol, MagicNumber = pos.MagicNumber };
                    snap = await _bridge.GetMarketSnapshotAsync(probe, _cfg).ConfigureAwait(false);
                }
                catch { continue; }

                string currentH1Trend = snap?["structure"]?["trend_h1"]?.ToString() ?? "UNKNOWN";
                if (currentH1Trend == "UNKNOWN" || currentH1Trend == entryTrend) continue;

                Log($"[WARN] TREND_REVERSAL | {pos.Symbol} #{pos.Ticket} | Entry: {entryTrend} → Current: {currentH1Trend} — moving SL to breakeven");
                bool ok = await _bridge.ModifyPositionAsync(pos.Ticket, pos.OpenPrice, pos.TakeProfit).ConfigureAwait(false);
                if (ok)
                {
                    _slMovedTickets.Add(pos.Ticket);
                    _entryH1Trends.Remove(pos.Ticket);
                    Log($"[OK] SL moved to breakeven for #{pos.Ticket} on H1 trend reversal");
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

                double profitPips = PipCalculator.MoveInPips(
                    pos.Type,
                    pos.OpenPrice,
                    pos.CurrentPrice,
                    pipSize);

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
            EnsureKillSwitchLoaded();
            if (_emergencyStopFired || _killSwitchState.KillSwitchActive || _startOfDayEquity <= 0) return;

            AccountInfo? account;
            try { account = await _bridge.GetAccountInfoAsync().ConfigureAwait(false); }
            catch (Exception ex)
            {
                Log($"[SAFETY] Drawdown account check unavailable: {ex.Message}");
                return;
            }

            if (account == null) return;

            double drawdownPct = (_startOfDayEquity - account.Equity) / _startOfDayEquity * 100.0;

            if (drawdownPct >= _cfg.EmergencyCloseDrawdownPct)
            {
                string reason = $"Emergency drawdown {drawdownPct:F1}% exceeded limit {_cfg.EmergencyCloseDrawdownPct:F1}%.";
                ActivateKillSwitch(reason, drawdownPct, account);
                Log($"🚨 EMERGENCY STOP: Drawdown {drawdownPct:F1}% exceeded limit " +
                    $"{_cfg.EmergencyCloseDrawdownPct:F1}% — CLOSING ALL POSITIONS");

                var positions = await _bridge.GetPositionsAsync().ConfigureAwait(false);
                if (positions.Count == 0)
                    Log("[SAFETY] Emergency close-all found no open positions or position data was unavailable.");

                bool anyCloseFailed = false;
                foreach (var pos in positions)
                {
                    Log($"🚨 Emergency close #{pos.Ticket} {pos.Symbol}");
                    bool closed;
                    try
                    {
                        closed = await _bridge.CloseTradeAsync(pos.Ticket).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        anyCloseFailed = true;
                        Log($"[SAFETY] Emergency close failed #{pos.Ticket} {pos.Symbol}: {ex.Message}");
                        continue;
                    }

                    if (closed)
                    {
                        Log($"[SAFETY] Emergency close succeeded #{pos.Ticket} {pos.Symbol}");
                    }
                    else
                    {
                        anyCloseFailed = true;
                        Log($"[SAFETY] Emergency close failed #{pos.Ticket} {pos.Symbol}");
                    }
                }

                Log("[SAFETY] Emergency close-all attempts completed. Kill switch remains active until explicit clear.");
                Log(anyCloseFailed
                    ? "[SAFETY] Emergency close-all had failures. Kill switch remains active."
                    : "[SAFETY] Kill switch remains active until explicit clear.");
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
                    result.ErrorCode,
                    result.OrderFailureCode,
                    result.BrokerRetcode?.ToString() ?? "",
                    $"\"{result.BrokerComment}\"",
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
                    "Time,Id,Pair,Direction,Lots,Entry,SL,TP,Ticket,Status,ExecutedPrice,ErrorCode,OrderFailureCode,BrokerRetcode,BrokerComment,Error\n");
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════

        private void EnsureKillSwitchLoaded()
        {
            if (_killSwitchLoaded) return;
            LoadKillSwitchState();
            _killSwitchLoaded = true;
        }

        private void LoadKillSwitchState()
        {
            _killSwitchState = new KillSwitchState();
            _emergencyStopFired = false;

            if (!File.Exists(KillSwitchFile)) return;

            try
            {
                var state = JsonConvert.DeserializeObject<KillSwitchState>(
                    File.ReadAllText(KillSwitchFile));

                if (state == null) return;

                _killSwitchState = state;
                _emergencyStopFired = state.KillSwitchActive;

                if (state.KillSwitchActive)
                    Log($"[SAFETY] Kill switch loaded: {state.KillSwitchReason}");
            }
            catch (Exception ex)
            {
                _killSwitchState = new KillSwitchState
                {
                    KillSwitchActive = true,
                    KillSwitchReason = "Kill-switch state file could not be loaded; fail closed.",
                    KillSwitchTriggeredAtUtc = DateTime.UtcNow
                };
                _emergencyStopFired = true;
                Log($"[SAFETY] Kill switch load failed: {ex.Message}. Live trading fail-closed.");
            }
        }

        private void ActivateKillSwitch(string reason, double drawdownPct, AccountInfo account)
        {
            _killSwitchState = new KillSwitchState
            {
                KillSwitchActive = true,
                KillSwitchReason = reason,
                KillSwitchTriggeredAtUtc = DateTime.UtcNow,
                DrawdownPercentAtTrigger = drawdownPct,
                AccountBalance = account.Balance,
                AccountEquity = account.Equity
            };
            _emergencyStopFired = true;
            _killSwitchLoaded = true;
            PersistKillSwitchState();
        }

        private void PersistKillSwitchState()
        {
            try
            {
                string? directory = Path.GetDirectoryName(KillSwitchFile);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(
                    KillSwitchFile,
                    JsonConvert.SerializeObject(_killSwitchState, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Log($"[SAFETY] Kill switch persistence failed: {ex.Message}");
            }
        }

        private static KillSwitchState CopyKillSwitchState(KillSwitchState state) => new()
        {
            KillSwitchActive = state.KillSwitchActive,
            KillSwitchReason = state.KillSwitchReason,
            KillSwitchTriggeredAtUtc = state.KillSwitchTriggeredAtUtc,
            DrawdownPercentAtTrigger = state.DrawdownPercentAtTrigger,
            AccountBalance = state.AccountBalance,
            AccountEquity = state.AccountEquity
        };

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

        private TradeResult? CheckCommissionModel(
            string requestId,
            TradeRequest request,
            bool liveMode,
            out CommissionEstimate estimate)
        {
            estimate = CommissionCalculator.EstimateRoundTurn(request.LotSize, _cfg);
            if (!CommissionCalculator.IsEnabled(_cfg))
                return null;

            if (estimate.Success)
                return null;

            string message = string.IsNullOrWhiteSpace(estimate.Error)
                ? "Commission data is unavailable"
                : estimate.Error;

            if (liveMode)
                return Fail(requestId, "COMMISSION_DATA_UNAVAILABLE", message);

            Log($"[WARN] Commission model unavailable in paper mode: {message}");
            return null;
        }

        private TradeResult? CheckSlippageModel(
            string requestId,
            TradeRequest request,
            bool liveMode,
            out SlippageEstimate estimate)
        {
            estimate = SlippageCalculator.EstimateCost(request.Pair, request.LotSize, _cfg);
            if (!SlippageCalculator.IsEnabled(_cfg))
                return null;

            if (estimate.Success)
                return null;

            string message = string.IsNullOrWhiteSpace(estimate.Error)
                ? "Slippage data is unavailable"
                : estimate.Error;

            if (liveMode)
                return Fail(requestId, "SLIPPAGE_DATA_UNAVAILABLE", message);

            Log($"[WARN] Slippage model unavailable in paper mode: {message}");
            return null;
        }

        private TradeResult? CheckBrokerStopLevel(
            string requestId,
            TradeRequest request,
            SymbolInfo? symbolInfo,
            double livePrice,
            bool liveMode)
        {
            if (!liveMode)
                return null;

            var check = BrokerStopLevelValidator.Validate(request, symbolInfo, livePrice);
            if (check.Success)
            {
                Log($"[SAFETY] Broker stop level OK: minimum {check.StopLevelPips:F1} pips, " +
                    $"SL distance {check.StopLossDistancePips:F1} pips, " +
                    $"TP distance {check.TakeProfitDistancePips:F1} pips.");
                return null;
            }

            string code = check.DataUnavailable
                ? "BROKER_STOP_LEVEL_DATA_UNAVAILABLE"
                : "BROKER_STOP_LEVEL_VIOLATION";
            return Fail(requestId, code, check.Message);
        }

        private TradeResult? CheckBrokerFreezeLevel(
            string requestId,
            TradeRequest request,
            SymbolInfo? symbolInfo,
            double livePrice,
            bool liveMode)
        {
            if (!liveMode)
                return null;

            var check = BrokerFreezeLevelValidator.Validate(request, symbolInfo, livePrice);
            if (check.Success)
            {
                Log($"[SAFETY] Broker freeze level OK: minimum {check.FreezeLevelPips:F1} pips, " +
                    $"SL distance {check.StopLossDistancePips:F1} pips, " +
                    $"TP distance {check.TakeProfitDistancePips:F1} pips.");
                return null;
            }

            string code = check.DataUnavailable
                ? "BROKER_FREEZE_LEVEL_DATA_UNAVAILABLE"
                : "BROKER_FREEZE_LEVEL_VIOLATION";
            return Fail(requestId, code, check.Message);
        }

        private TradeResult? CheckBrokerLotSize(
            string requestId,
            TradeRequest request,
            SymbolInfo? symbolInfo,
            bool liveMode)
        {
            if (!liveMode)
                return null;

            var check = BrokerLotSizeValidator.Validate(request.LotSize, symbolInfo);
            if (check.Success)
            {
                string volumeLimit = check.VolumeLimit > 0
                    ? $", volume limit {check.VolumeLimit:F2}"
                    : "";
                Log($"[SAFETY] Broker lot size OK: lot {check.LotSize:F2}, " +
                    $"min {check.MinLot:F2}, max {check.MaxLot:F2}, step {check.LotStep:F2}{volumeLimit}.");
                return null;
            }

            string code = check.DataUnavailable
                ? "BROKER_LOT_DATA_UNAVAILABLE"
                : "BROKER_LOT_SIZE_VIOLATION";
            return Fail(requestId, code, check.Message);
        }

        private TradeResult? CheckNoTradeWindow(string requestId, bool liveMode)
        {
            if (!liveMode)
                return null;

            var check = NoTradeWindowValidator.Validate(_cfg, _utcNow());
            if (check.Success)
                return null;

            string code = check.InvalidConfig
                ? "NO_TRADE_WINDOW_CONFIG_INVALID"
                : "ROLLOVER_NO_TRADE_WINDOW";
            return Fail(requestId, code, check.Message);
        }

        private TradeResult? CheckRolloutStage(string requestId, bool liveMode)
        {
            if (!liveMode || !_cfg.EnableStagedRollout)
                return null;

            var result = new RolloutEvaluator().Evaluate(new RolloutEvaluationInput
            {
                Config = _cfg,
                IsLiveOrderRequested = true,
                LiveReadinessGatePassed = true,
                ExplicitUserConfirmation = true,
                KillSwitchActive = _killSwitchState.KillSwitchActive || _emergencyStopFired
            });

            if (result.Action != RolloutAction.Block)
                return null;

            string detail = result.FailedCriteria.Count > 0
                ? string.Join(" ", result.FailedCriteria)
                : result.Reason;
            Log($"[SAFETY] Rollout stage blocked live order: {detail}");
            return Fail(requestId, "ROLLOUT_STAGE_BLOCKED", detail);
        }

        private TradeResult? CheckSessionSpread(
            string requestId,
            string symbol,
            SymbolInfo symbolInfo,
            bool liveMode)
        {
            if (!liveMode)
                return null;

            var check = SessionSpreadValidator.Validate(_cfg, symbolInfo, symbol, _utcNow());
            if (check.Success)
            {
                if (_cfg.EnableSessionSpreadProtection)
                    Log($"[SAFETY] Session spread OK: {check.SpreadPips:F1}/{check.LimitPips:F1} pips ({check.RuleName}).");
                return null;
            }

            string code = check.InvalidConfig
                ? "SPREAD_SESSION_CONFIG_INVALID"
                : "SPREAD_SESSION_LIMIT";
            return Fail(requestId, code, check.Message);
        }

        private async Task<TradeResult?> CheckProjectedMarginHardStopAsync(
            string requestId,
            TradeRequest request,
            AccountInfo account,
            double livePrice,
            bool liveMode)
        {
            if (!IsProjectedMarginValidationEnabled(_cfg))
                return null;

            if (!liveMode)
                return null;

            double minimumMarginLevel = _cfg.MinProjectedMarginLevelPercent;
            if (!IsFinitePositive(minimumMarginLevel))
                return Fail(requestId, "MARGIN_DATA_UNAVAILABLE",
                    "Projected margin validation is enabled but no positive minimum margin level is configured");

            if (!TryValidateMarginAccountData(account, out string accountError))
                return Fail(requestId, "MARGIN_DATA_UNAVAILABLE", accountError);

            if (IsFinitePositive(account.MarginLevel) &&
                account.MarginLevel < minimumMarginLevel)
            {
                return Fail(requestId, "MARGIN_LEVEL_LIMIT",
                    $"Current margin level {account.MarginLevel:F2}% is below minimum {minimumMarginLevel:F2}%");
            }

            double price = IsFinitePositive(livePrice)
                ? livePrice
                : request.EntryPrice;
            if (!IsFinitePositive(price) || !IsFinitePositive(request.LotSize))
                return Fail(requestId, "MARGIN_DATA_UNAVAILABLE",
                    "Trade price or lot size is unavailable for projected margin validation");

            (bool Success, MarginEstimate? Estimate, string Error) estimateResult;
            try
            {
                estimateResult = await _bridge
                    .TryGetMarginEstimateAsync(request.Pair, request.TradeType, request.LotSize, price)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log($"[SAFETY] Margin estimate unavailable: {ex.Message}");
                return Fail(requestId, "MARGIN_DATA_UNAVAILABLE",
                    "Margin estimate is unavailable from MT5");
            }

            if (!estimateResult.Success ||
                estimateResult.Estimate == null ||
                !IsFinitePositive(estimateResult.Estimate.RequiredMargin))
            {
                string reason = string.IsNullOrWhiteSpace(estimateResult.Error)
                    ? "Margin estimate is unavailable from MT5"
                    : estimateResult.Error;
                return Fail(requestId, "MARGIN_DATA_UNAVAILABLE", reason);
            }

            double projectedUsedMargin = account.Margin + estimateResult.Estimate.RequiredMargin;
            if (!IsFinitePositive(projectedUsedMargin))
                return Fail(requestId, "MARGIN_DATA_UNAVAILABLE",
                    "Projected used margin could not be calculated");

            double projectedMarginLevel = account.Equity / projectedUsedMargin * 100.0;
            if (!IsFinitePositive(projectedMarginLevel))
                return Fail(requestId, "MARGIN_DATA_UNAVAILABLE",
                    "Projected margin level could not be calculated");

            Log($"[SAFETY] Projected margin OK: current used=${account.Margin:F2}, " +
                $"required=${estimateResult.Estimate.RequiredMargin:F2}, " +
                $"projected level={projectedMarginLevel:F2}%/{minimumMarginLevel:F2}%");

            if (projectedMarginLevel < minimumMarginLevel)
                return Fail(requestId, "MARGIN_LEVEL_LIMIT",
                    $"Projected margin level {projectedMarginLevel:F2}% is below minimum {minimumMarginLevel:F2}%");

            return null;
        }

        private TradeResult? CheckSymbolExposureLimit(
            string requestId,
            TradeRequest request,
            AccountInfo account,
            IReadOnlyList<LivePosition> openPositions,
            double livePrice,
            bool liveMode)
        {
            if (!IsSymbolExposureLimitEnabled(_cfg))
                return null;

            if (!TryCalculateSymbolExposure(
                    request,
                    account,
                    openPositions,
                    livePrice,
                    _cfg,
                    out var exposure,
                    out string error))
            {
                if (liveMode)
                    return Fail(requestId, "SYMBOL_EXPOSURE_DATA_UNAVAILABLE", error);

                Log($"[WARN] Symbol exposure data unavailable in paper mode: {error}");
                return null;
            }

            if (_cfg.BlockOppositeSymbolExposure && exposure.HasOppositeDirection)
                return Fail(requestId, "SYMBOL_EXPOSURE_LIMIT",
                    $"Opposite-direction {request.Pair} exposure already exists.");

            if (_cfg.MaxSameSymbolPositions > 0 &&
                exposure.ProjectedPositionCount >= _cfg.MaxSameSymbolPositions)
            {
                return Fail(requestId, "SYMBOL_EXPOSURE_LIMIT",
                    $"{request.Pair} projected position count {exposure.ProjectedPositionCount} " +
                    $"reaches limit {_cfg.MaxSameSymbolPositions}.");
            }

            if (IsFinitePositive(_cfg.MaxSymbolLots) &&
                exposure.ProjectedLots >= _cfg.MaxSymbolLots)
            {
                return Fail(requestId, "SYMBOL_EXPOSURE_LIMIT",
                    $"{request.Pair} projected gross lots {exposure.ProjectedLots:F2} " +
                    $"reaches limit {_cfg.MaxSymbolLots:F2}.");
            }

            if (IsFinitePositive(_cfg.MaxSymbolRiskPercent) &&
                exposure.ProjectedRiskPercent >= _cfg.MaxSymbolRiskPercent)
            {
                return Fail(requestId, "SYMBOL_EXPOSURE_LIMIT",
                    $"{request.Pair} projected gross risk {exposure.ProjectedRiskPercent:F2}% " +
                    $"reaches limit {_cfg.MaxSymbolRiskPercent:F2}%.");
            }

            Log($"[SAFETY] Symbol exposure OK {request.Pair}: " +
                $"positions={exposure.ProjectedPositionCount}, " +
                $"lots={exposure.ProjectedLots:F2}, " +
                $"risk={exposure.ProjectedRiskPercent:F2}%");
            return null;
        }

        private static bool TryCalculateSymbolExposure(
            TradeRequest request,
            AccountInfo account,
            IReadOnlyList<LivePosition> openPositions,
            double livePrice,
            BotConfig config,
            out SymbolExposure exposure,
            out string error)
        {
            exposure = default;
            error = "";

            if (string.IsNullOrWhiteSpace(request.Pair))
            {
                error = "Requested symbol is unavailable for exposure calculation";
                return false;
            }

            if (!IsFinitePositive(request.LotSize))
            {
                error = "Requested lot size is unavailable for exposure calculation";
                return false;
            }

            bool calculateRisk = IsFinitePositive(config.MaxSymbolRiskPercent);
            double projectedRiskUsd = 0;

            if (calculateRisk)
            {
                double referenceEntry = IsFinitePositive(request.EntryPrice)
                    ? request.EntryPrice
                    : livePrice;

                if (!IsFinitePositive(account.Equity) ||
                    !IsFinitePositive(referenceEntry) ||
                    !IsFinitePositive(request.StopLoss))
                {
                    error = "Requested trade risk data is unavailable for symbol exposure calculation";
                    return false;
                }

                projectedRiskUsd += LotCalculator.DollarRisk(
                    request.LotSize,
                    referenceEntry,
                    request.StopLoss,
                    request.Pair);
            }

            double projectedLots = request.LotSize;
            int projectedPositionCount = 1;
            bool hasOppositeDirection = false;

            foreach (var position in openPositions)
            {
                if (string.IsNullOrWhiteSpace(position.Symbol))
                {
                    error = "Open-position symbol data is unavailable for exposure calculation";
                    return false;
                }

                if (!IsSameSymbol(position.Symbol, request.Pair))
                    continue;

                if (!IsFiniteNonNegative(position.Lots))
                {
                    error = $"Open-position lot data is unavailable for {request.Pair}";
                    return false;
                }

                projectedLots += Math.Abs(position.Lots);
                projectedPositionCount++;

                if (position.Type != request.TradeType)
                    hasOppositeDirection = true;

                if (calculateRisk)
                {
                    if (!IsFinitePositive(position.OpenPrice) ||
                        !IsFinitePositive(position.StopLoss))
                    {
                        error = $"Open-position risk data is unavailable for {request.Pair}";
                        return false;
                    }

                    projectedRiskUsd += LotCalculator.DollarRisk(
                        Math.Abs(position.Lots),
                        position.OpenPrice,
                        position.StopLoss,
                        position.Symbol);
                }
            }

            if (!IsFiniteNonNegative(projectedLots) ||
                !IsFiniteNonNegative(projectedRiskUsd))
            {
                error = "Projected symbol exposure could not be calculated";
                return false;
            }

            double projectedRiskPercent = calculateRisk
                ? projectedRiskUsd / account.Equity * 100.0
                : 0;

            if (!IsFiniteNonNegative(projectedRiskPercent))
            {
                error = "Projected symbol risk percent could not be calculated";
                return false;
            }

            exposure = new SymbolExposure(
                projectedLots,
                projectedPositionCount,
                projectedRiskPercent,
                hasOppositeDirection);
            return true;
        }

        private async Task<TradeResult?> CheckDailyLossHardStopAsync(
            string requestId,
            AccountInfo account,
            IReadOnlyList<LivePosition> openPositions,
            bool liveMode)
        {
            if (!liveMode || !IsDailyLossLimitEnabled(_cfg))
                return null;

            double limitAmount = ResolveDailyLossLimitAmount(_cfg, account);
            if (!IsFinitePositive(limitAmount))
                return Fail(requestId, "DAILY_LOSS_DATA_UNAVAILABLE",
                    "Daily loss limit is enabled but no positive threshold is configured");

            if (_tradeDb == null)
                return Fail(requestId, "DAILY_LOSS_DATA_UNAVAILABLE",
                    "Daily loss trade-history data is unavailable");

            if (!TrySumFloatingProfit(openPositions, out double floatingPnl))
                return Fail(requestId, "DAILY_LOSS_DATA_UNAVAILABLE",
                    "Daily loss floating P/L data is unavailable");

            DateTime dayStartUtc = DateTime.UtcNow.Date;
            DateTime dayEndUtc = dayStartUtc.AddDays(1);
            IReadOnlyList<TradeRecord> closedToday;
            try
            {
                closedToday = await _tradeDb
                    .GetClosedByCloseDateRangeAsync(dayStartUtc, dayEndUtc, _cts.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log($"[SAFETY] Daily loss trade-history data unavailable: {ex.Message}");
                return Fail(requestId, "DAILY_LOSS_DATA_UNAVAILABLE",
                    "Daily loss trade-history data is unavailable");
            }

            if (closedToday == null || !TrySumRealizedProfit(closedToday, out double realizedPnl))
                return Fail(requestId, "DAILY_LOSS_DATA_UNAVAILABLE",
                    "Daily realized P/L data is unavailable");

            double totalPnl = realizedPnl + floatingPnl;
            double totalLoss = Math.Max(0, -totalPnl);
            Log($"[SAFETY] Daily loss check UTC {dayStartUtc:yyyy-MM-dd}: " +
                $"realized=${realizedPnl:F2}, floating=${floatingPnl:F2}, " +
                $"loss=${totalLoss:F2}/${limitAmount:F2}");

            if (totalLoss >= limitAmount)
                return Fail(requestId, "DAILY_LOSS_LIMIT",
                    $"Daily loss limit reached: ${totalLoss:F2} of ${limitAmount:F2}");

            return null;
        }

        private async Task<TradeResult?> CheckWeeklyLossHardStopAsync(
            string requestId,
            AccountInfo account,
            IReadOnlyList<LivePosition> openPositions,
            bool liveMode)
        {
            if (!liveMode || !IsWeeklyLossLimitEnabled(_cfg))
                return null;

            double limitAmount = ResolveWeeklyLossLimitAmount(_cfg, account);
            if (!IsFinitePositive(limitAmount))
                return Fail(requestId, "WEEKLY_LOSS_DATA_UNAVAILABLE",
                    "Weekly loss limit is enabled but no positive threshold is configured");

            if (_tradeDb == null)
                return Fail(requestId, "WEEKLY_LOSS_DATA_UNAVAILABLE",
                    "Weekly loss trade-history data is unavailable");

            if (!TrySumFloatingProfit(openPositions, out double floatingPnl))
                return Fail(requestId, "WEEKLY_LOSS_DATA_UNAVAILABLE",
                    "Weekly loss floating P/L data is unavailable");

            DateTime weekStartUtc = GetUtcWeekStart(DateTime.UtcNow);
            DateTime weekEndUtc = weekStartUtc.AddDays(7);
            IReadOnlyList<TradeRecord> closedThisWeek;
            try
            {
                closedThisWeek = await _tradeDb
                    .GetClosedByCloseDateRangeAsync(weekStartUtc, weekEndUtc, _cts.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log($"[SAFETY] Weekly loss trade-history data unavailable: {ex.Message}");
                return Fail(requestId, "WEEKLY_LOSS_DATA_UNAVAILABLE",
                    "Weekly loss trade-history data is unavailable");
            }

            if (closedThisWeek == null || !TrySumRealizedProfit(closedThisWeek, out double realizedPnl))
                return Fail(requestId, "WEEKLY_LOSS_DATA_UNAVAILABLE",
                    "Weekly realized P/L data is unavailable");

            double totalPnl = realizedPnl + floatingPnl;
            double totalLoss = Math.Max(0, -totalPnl);
            Log($"[SAFETY] Weekly loss check UTC {weekStartUtc:yyyy-MM-dd}..{weekEndUtc:yyyy-MM-dd}: " +
                $"realized=${realizedPnl:F2}, floating=${floatingPnl:F2}, " +
                $"loss=${totalLoss:F2}/${limitAmount:F2}");

            if (totalLoss >= limitAmount)
                return Fail(requestId, "WEEKLY_LOSS_LIMIT",
                    $"Weekly loss limit reached: ${totalLoss:F2} of ${limitAmount:F2}");

            return null;
        }

        private static bool IsDailyLossLimitEnabled(BotConfig config) =>
            config.EnableDailyLossLimit ||
            config.MaxDailyLossAmount > 0 ||
            config.MaxDailyLossPercent > 0;

        private static bool IsWeeklyLossLimitEnabled(BotConfig config) =>
            config.EnableWeeklyLossLimit ||
            config.MaxWeeklyLossAmount > 0 ||
            config.MaxWeeklyLossPercent > 0;

        private static bool IsSymbolExposureLimitEnabled(BotConfig config) =>
            config.EnableSymbolExposureLimit ||
            config.MaxSymbolLots > 0 ||
            config.MaxSymbolRiskPercent > 0 ||
            config.MaxSameSymbolPositions > 0 ||
            config.BlockOppositeSymbolExposure;

        private static bool IsProjectedMarginValidationEnabled(BotConfig config) =>
            config.EnableProjectedMarginValidation ||
            config.MinProjectedMarginLevelPercent > 0;

        private static bool IsOrderRetryPolicyEnabled(BotConfig config) =>
            config.EnableOrderRetryPolicy ?? config.RetryOnFail;

        private static int GetMaxOrderSendAttempts(BotConfig config)
        {
            if (config.MaxOrderSendRetries > 0)
                return Math.Max(1, config.MaxOrderSendRetries + 1);

            return Math.Max(1, config.RetryCount);
        }

        private static int GetOrderRetryDelayMs(BotConfig config)
        {
            if (config.OrderRetryDelayMs > 0)
                return config.OrderRetryDelayMs;

            return Math.Max(0, config.RetryDelayMs);
        }

        private static bool IsBrokerOrderFailure(TradeResult result) =>
            !string.IsNullOrWhiteSpace(result.OrderFailureCode) ||
            (result.ErrorCode?.StartsWith("ORDER_", StringComparison.OrdinalIgnoreCase) ?? false) ||
            (result.ErrorCode?.StartsWith("MT5_", StringComparison.OrdinalIgnoreCase) ?? false);

        private static bool TryValidateMarginAccountData(AccountInfo account, out string error)
        {
            error = "";
            if (!IsFinitePositive(account.Equity))
            {
                error = "Account equity is unavailable for projected margin validation";
                return false;
            }

            if (!IsFiniteNonNegative(account.Margin))
            {
                error = "Account used margin is unavailable for projected margin validation";
                return false;
            }

            if (account.Margin > 0 && !IsFinitePositive(account.MarginLevel))
            {
                error = "Current margin level is unavailable for projected margin validation";
                return false;
            }

            return true;
        }

        private static bool IsSameSymbol(string left, string right) =>
            string.Equals(
                PipCalculator.NormalizeSymbol(left),
                PipCalculator.NormalizeSymbol(right),
                StringComparison.OrdinalIgnoreCase);

        private static double ResolveDailyLossLimitAmount(BotConfig config, AccountInfo account)
        {
            var limits = new List<double>(2);
            if (IsFinitePositive(config.MaxDailyLossAmount))
                limits.Add(config.MaxDailyLossAmount);

            if (IsFinitePositive(config.MaxDailyLossPercent))
            {
                double basis = IsFinitePositive(account.Balance)
                    ? account.Balance
                    : account.Equity;
                limits.Add(basis * config.MaxDailyLossPercent / 100.0);
            }

            return limits.Count == 0 ? 0 : limits.Min();
        }

        private static double ResolveWeeklyLossLimitAmount(BotConfig config, AccountInfo account)
        {
            var limits = new List<double>(2);
            if (IsFinitePositive(config.MaxWeeklyLossAmount))
                limits.Add(config.MaxWeeklyLossAmount);

            if (IsFinitePositive(config.MaxWeeklyLossPercent))
            {
                double basis = IsFinitePositive(account.Balance)
                    ? account.Balance
                    : account.Equity;
                limits.Add(basis * config.MaxWeeklyLossPercent / 100.0);
            }

            return limits.Count == 0 ? 0 : limits.Min();
        }

        private static DateTime GetUtcWeekStart(DateTime utcNow)
        {
            DateTime utcDate = utcNow.Date;
            int daysSinceMonday = ((int)utcDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return utcDate.AddDays(-daysSinceMonday);
        }

        private static bool TrySumRealizedProfit(
            IReadOnlyList<TradeRecord> closedTrades,
            out double realizedPnl)
        {
            realizedPnl = 0;
            foreach (var trade in closedTrades)
            {
                if (!trade.ClosedAt.HasValue || !IsFinite(trade.ProfitUsd))
                    return false;

                realizedPnl += trade.ProfitUsd;
            }

            return true;
        }

        private static bool TrySumFloatingProfit(
            IReadOnlyList<LivePosition> openPositions,
            out double floatingPnl)
        {
            floatingPnl = 0;
            foreach (var position in openPositions)
            {
                if (!IsFinite(position.Profit))
                    return false;

                floatingPnl += position.Profit;
            }

            return true;
        }

        private static bool HasUsableAccountData(AccountInfo? account) =>
            account != null &&
            account.IsConnected &&
            IsFinitePositive(account.Equity) &&
            IsFiniteNonNegative(account.Balance) &&
            IsFiniteNonNegative(account.FreeMargin);

        private static bool HasUsableSymbolSafetyData(SymbolInfo? symbolInfo) =>
            symbolInfo != null &&
            IsFinitePositive(symbolInfo.Ask) &&
            IsFinitePositive(symbolInfo.Bid) &&
            symbolInfo.Ask >= symbolInfo.Bid &&
            IsFinitePositive(symbolInfo.Spread) &&
            IsFinitePositive(symbolInfo.SpreadPips);

        private static bool HasCompleteRiskData(RiskValidationResult riskResult)
        {
            if (!riskResult.IsApproved) return true;

            return IsFinitePositive(riskResult.ReferenceEntryPrice) &&
                   IsFinitePositive(riskResult.ValidatedLotSize) &&
                   IsFiniteNonNegative(riskResult.RiskPercent) &&
                   IsFinitePositive(riskResult.DollarRisk) &&
                   IsFinitePositive(riskResult.RiskRewardRatio);
        }

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

        private static bool IsFiniteNonNegative(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private void ApplyTradePageSlTp(TradeRequest request, SymbolInfo? symbolInfo, double livePrice)
        {
            if (symbolInfo == null || !IsFinitePositive(livePrice))
                return;

            bool scalpingStrategy = string.Equals(request.Strategy, "Scalping", StringComparison.OrdinalIgnoreCase)
                || (!string.Equals(request.Strategy, "Normal", StringComparison.OrdinalIgnoreCase) && _cfg.Scalping.Enabled);
            double slPips = scalpingStrategy
                ? _cfg.Scalping.StopLossPips
                : _cfg.NormalTrading.StopLossPips;
            double tpPips = scalpingStrategy
                ? _cfg.Scalping.TakeProfitPips
                : _cfg.NormalTrading.TakeProfitPips;
            if (!IsFinitePositive(slPips) || !IsFinitePositive(tpPips))
                return;

            double pipSize = LotCalculator.GetPipSize(request.Pair.ToUpperInvariant());
            if (request.TradeType == Models.TradeType.BUY)
            {
                request.StopLoss = livePrice - slPips * pipSize;
                request.TakeProfit = livePrice + tpPips * pipSize;
            }
            else
            {
                request.StopLoss = livePrice + slPips * pipSize;
                request.TakeProfit = livePrice - tpPips * pipSize;
            }
            request.Strategy = scalpingStrategy ? "Scalping" : "Normal";
        }

        private readonly record struct SymbolExposure(
            double ProjectedLots,
            int ProjectedPositionCount,
            double ProjectedRiskPercent,
            bool HasOppositeDirection);

        private static double EstimateMarketPrice(TradeRequest r)
        {
            // Last-resort estimate when entry=0 and symbol info unavailable
            return r.TradeType == Models.TradeType.BUY
                ? r.StopLoss * 1.002
                : r.StopLoss * 0.998;
        }

        private UserApprovalDecision CreateWorkflowApproval(TradeRequest request) => new()
        {
            SignalId = request.Id,
            IsApproved = true,
            ApprovedBy = _cfg.PaperTrading ? "PaperTrading" : "ExecutionGate",
            ApprovalMode = _cfg.PaperTrading ? "PaperTrading" : _currentMode.ToString(),
            Notes = "Approved by existing workflow before centralized execution gate."
        };

        private static TradeRequest ShallowClone(TradeRequest r) =>
            JsonConvert.DeserializeObject<TradeRequest>(JsonConvert.SerializeObject(r))!;

        private static TradeResult Fail(string reqId, string code, string msg)
        {
            Log_Static($"🚫 [{code}] {msg}");
            return new TradeResult { RequestId = reqId, Status = TradeStatus.Rejected,
                ErrorCode = code, ErrorMessage = msg };
        }

        private static bool IsNearRoundNumber(double price, double pipSize, int warningPips = 20)
        {
            double roundedTo50  = Math.Round(price / 50.0) * 50.0;
            double roundedTo100 = Math.Round(price / 100.0) * 100.0;
            double distTo50  = Math.Abs(price - roundedTo50)  / pipSize;
            double distTo100 = Math.Abs(price - roundedTo100) / pipSize;
            return distTo50 < warningPips || distTo100 < warningPips;
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

        private record LayerResult(string Layer, bool Passed, string Reason);
    }
}
