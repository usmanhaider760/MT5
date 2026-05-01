using Anthropic;
using Anthropic.Models.Messages;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace MT5TradingBot.Services
{
    /// <summary>
    /// Polls MT5 for live market data, sends it to Claude (with prompt caching),
    /// parses the structured JSON signal, and routes it through the validation
    /// pipeline via the provided execute delegate.
    /// </summary>
    public sealed class ClaudeSignalService : IAsyncDisposable
    {
        // ── Dependencies ──────────────────────────────────────────
        private readonly MT5Bridge _bridge;
        private ClaudeConfig _cfg;
        private BotConfig _botConfig;
        private readonly Func<TradeRequest, Task<TradeResult>> _execute;
        private AnthropicClient? _client;
        private readonly IAiContextManager _contextManager;

        // ── Concurrency ───────────────────────────────────────────
        private readonly CancellationTokenSource _cts = new();
        private Task? _loopTask;
        private volatile bool _running;

        // ── Events ────────────────────────────────────────────────
        public event Action<string>? OnLog;
        public event Action<TradeRequest>? OnSignalGenerated;
        public event Action<bool>? OnStatusChanged;

        public bool IsRunning => _running;

        public ClaudeSignalService(
            MT5Bridge bridge,
            ClaudeConfig cfg,
            Func<TradeRequest, Task<TradeResult>> execute)
            : this(bridge, cfg, null, execute) { }

        public ClaudeSignalService(
            MT5Bridge bridge,
            ClaudeConfig cfg,
            BotConfig? botConfig,
            Func<TradeRequest, Task<TradeResult>> execute,
            IAiContextManager? contextManager = null)
        {
            _bridge         = bridge;
            _cfg            = cfg;
            _botConfig      = botConfig ?? new BotConfig();
            _execute        = execute;
            _contextManager = contextManager ?? new AiContextManager();
        }

        public void UpdateConfig(ClaudeConfig cfg) => _cfg = cfg;
        public void UpdateBotConfig(BotConfig cfg) => _botConfig = cfg;

        // ══════════════════════════════════════════════════════════
        //  START / STOP
        // ══════════════════════════════════════════════════════════

        public Task StartAsync()
        {
            if (_running) return Task.CompletedTask;
            if (string.IsNullOrWhiteSpace(_cfg.ApiKey))
                throw new InvalidOperationException(
                    "AI API key is required. Enter it in the AI API Config tab.");

            _client  = new AnthropicClient { ApiKey = _cfg.ApiKey };
            _running = true;
            _loopTask = Task.Run(RunLoopAsync, _cts.Token);

            Log("🧠 Claude Signal Service STARTED");
            OnStatusChanged?.Invoke(true);
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (!_running) return;
            _running = false;
            _cts.Cancel();
            if (_loopTask != null)
            {
                try   { await _loopTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            Log("🛑 Claude Signal Service STOPPED");
            OnStatusChanged?.Invoke(false);
        }

        // ══════════════════════════════════════════════════════════
        //  BACKGROUND POLLING LOOP
        // ══════════════════════════════════════════════════════════

        private async Task RunLoopAsync()
        {
            while (!_cts.Token.IsCancellationRequested && _running)
            {
                try { await AnalyzeAndSignalAsync().ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    string friendly = CategorizeError(ex);
                    Log($"[ERROR] {friendly}");
                    // Stop the loop on auth errors — key is wrong, retrying won't help
                    if (ex.Message.Contains("401") || ex.Message.Contains("authentication_error") ||
                        ex.Message.Contains("invalid_api_key"))
                    {
                        Log("[ERROR] Stopping AI monitor — fix the API key in the AI API Config tab.");
                        _running = false;
                        OnStatusChanged?.Invoke(false);
                        break;
                    }
                }

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_cfg.PollIntervalSeconds),
                        _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        private static string CategorizeError(Exception ex)
        {
            string msg = ex.Message;
            if (msg.Contains("401") || msg.Contains("authentication_error") || msg.Contains("invalid_api_key"))
                return "API authentication failed (401) — invalid API key";
            if (msg.Contains("403"))
                return "API forbidden (403) — key lacks permissions for this model";
            if (msg.Contains("429") || msg.Contains("rate_limit"))
                return "Rate limited (429) — will retry after interval";
            if (msg.Contains("529") || msg.Contains("overloaded"))
                return "API overloaded (529) — will retry after interval";
            if (msg.Contains("model_not_found"))
                return $"Model not found — check model name in settings";
            if (msg.Contains("SocketException") || msg.Contains("HttpRequestException"))
                return "Network error — check internet connection";
            return $"Analysis error: {(msg.Length > 120 ? msg[..120] + "…" : msg)}";
        }

        // ══════════════════════════════════════════════════════════
        //  CORE: FETCH → CLAUDE → EXECUTE
        // ══════════════════════════════════════════════════════════

        private async Task AnalyzeAndSignalAsync()
        {
            if (!_bridge.IsConnected)
            {
                Log("⚠ MT5 not connected — skipping analysis");
                return;
            }

            var account = await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
            if (account == null) { Log("⚠ Cannot fetch account info"); return; }

            var positions = await _bridge.GetPositionsAsync().ConfigureAwait(false);

            var ct = _cts.Token;
            ct.ThrowIfCancellationRequested();

            var tasks = _cfg.WatchSymbols
                .Select(sym => Task.Run(async () =>
                {
                    try
                    {
                        await AnalyzeSymbolAsync(sym, account, positions)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Log($"[ERROR] Analysis failed for {sym}: {ex.Message}");
                    }
                }, ct))
                .ToList();

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task AnalyzeSymbolAsync(
            string symbol,
            AccountInfo account,
            List<LivePosition> positions)
        {
            Log($"🔍 Analyzing {symbol} via Claude ({_cfg.Model})...");

            string userMessage;
            bool usingRichPrompt = false;

            try
            {
                // Attempt rich snapshot: provides candles, indicators,
                // market structure, S/R levels, session, news.
                var snapshotReq = new TradeRequest { Pair = symbol };
                JObject? snapshot = await _bridge.GetMarketSnapshotAsync(
                    snapshotReq, _botConfig).ConfigureAwait(false);

                if (snapshot != null)
                {
                    userMessage = AiPrompts.BuildFilledAiInputPrompt(
                        snapshot.ToString(Newtonsoft.Json.Formatting.None));
                    usingRichPrompt = true;
                }
                else
                {
                    // EA does not support GET_MARKET_SNAPSHOT — fall back.
                    var info = await _bridge.GetSymbolInfoAsync(symbol)
                        .ConfigureAwait(false);
                    userMessage = BuildMarketDataPrompt(
                        account, [(symbol, info)], positions);
                    Log($"⚠ Snapshot unavailable for {symbol} — using minimal prompt");
                }
            }
            catch (Exception ex)
            {
                Log($"⚠ Snapshot error for {symbol}: {ex.Message} — using minimal prompt");
                var info = await _bridge.GetSymbolInfoAsync(symbol)
                    .ConfigureAwait(false);
                userMessage = BuildMarketDataPrompt(
                    account, [(symbol, info)], positions);
            }

            MessageCreateParams requestParams;

            if (usingRichPrompt)
            {
                // Rich path: AiInputPromptTemplate is the system prompt (cached).
                // Market snapshot data is the user message.
                requestParams = new MessageCreateParams
                {
                    Model     = _cfg.Model,
                    MaxTokens = 16000,
                    Thinking  = new ThinkingConfigAdaptive(),
                    System = new List<TextBlockParam>
                    {
                        new()
                        {
                            Text         = AiPrompts.AiInputPromptTemplate,
                            CacheControl = new CacheControlEphemeral(),
                        }
                    },
                    Messages = [new() { Role = Role.User, Content = userMessage }]
                };
            }
            else
            {
                // Minimal fallback path: original system prompt (cached).
                requestParams = new MessageCreateParams
                {
                    Model     = _cfg.Model,
                    MaxTokens = 16000,
                    Thinking  = new ThinkingConfigAdaptive(),
                    System = new List<TextBlockParam>
                    {
                        new()
                        {
                            Text         = _cfg.SystemPrompt,
                            CacheControl = new CacheControlEphemeral(),
                        }
                    },
                    Messages = [new() { Role = Role.User, Content = userMessage }]
                };
            }

            var response = await _client!.Messages.Create(requestParams)
                .ConfigureAwait(false);

            string? responseText = null;
            foreach (var block in response.Content)
            {
                if (block.TryPickText(out TextBlock? tb))
                { responseText = tb!.Text; break; }
            }

            if (string.IsNullOrWhiteSpace(responseText))
            { Log($"⚠ Claude returned no text for {symbol}"); return; }

            Log($"🤖 Claude ({symbol}): " +
                $"{responseText[..Math.Min(300, responseText.Length)]}");

            if (response.Usage.CacheCreationInputTokens > 0)
                Log($"💾 Cache created: {response.Usage.CacheCreationInputTokens} tokens");
            else if (response.Usage.CacheReadInputTokens > 0)
                Log($"⚡ Cache hit: {response.Usage.CacheReadInputTokens} tokens saved");

            await ParseAndExecuteAsync(responseText, symbol).ConfigureAwait(false);
        }

        // ══════════════════════════════════════════════════════════
        //  PARSE CLAUDE JSON → TRADE REQUEST → EXECUTE
        // ══════════════════════════════════════════════════════════

        private async Task ParseAndExecuteAsync(string text, string symbol = "")
        {
            string json = ExtractJson(text);
            if (string.IsNullOrEmpty(json)) { Log("⚠ No JSON found in response"); return; }

            ClaudeSignal? sig;
            try { sig = JsonConvert.DeserializeObject<ClaudeSignal>(json); }
            catch (Exception ex) { Log($"⚠ JSON parse error: {ex.Message}"); return; }

            if (sig == null) { Log("⚠ Could not parse signal"); return; }

            string action = sig.Action?.Trim().ToUpperInvariant() ?? "";
            string tradeTypeText = sig.TradeType?.Trim().ToUpperInvariant() ?? "";
            string reasonText = !string.IsNullOrWhiteSpace(sig.Reason)
                ? sig.Reason!
                : sig.Comment ?? "";
            if (string.IsNullOrWhiteSpace(action) && tradeTypeText == "NO_TRADE")
                action = "NO_TRADE";

            if (action == "NO_TRADE")
            {
                Log($"💤 No trade: {reasonText}");
                // Update regime so a previous BUY/SELL bias is cleared.
                string noTradePair = !string.IsNullOrWhiteSpace(sig.Pair) ? sig.Pair : symbol;
                if (!string.IsNullOrWhiteSpace(noTradePair))
                    _contextManager.Update(noTradePair, "NO_TRADE", reasonText);
                return;
            }

            if (action != "TRADE")
            {
                Log($"⚠ Unknown action: '{sig.Action}'");
                return;
            }

            if (sig.StopLoss == 0)
            {
                Log("⚠ Signal rejected: stop_loss is 0 or missing - update the system prompt to always include a valid SL price level");
                return;
            }

            if (sig.TakeProfit == 0)
            {
                Log("⚠ Signal rejected: take_profit is 0 or missing - update the system prompt to always include a valid TP price level");
                return;
            }

            // Context conflict guard
            if (_cfg.AiContextMaxAgeMinutes > 0 && _cfg.AiContextBlockConflicts)
            {
                string checkPair = !string.IsNullOrWhiteSpace(sig.Pair) ? sig.Pair : symbol;
                string newDirection = (sig.TradeType ?? "").Trim().ToUpperInvariant();
                var maxAge = TimeSpan.FromMinutes(_cfg.AiContextMaxAgeMinutes);

                if (_contextManager.HasConflict(checkPair, newDirection, maxAge))
                {
                    var cached = _contextManager.GetCurrent(checkPair, maxAge);
                    Log($"⚠ Regime conflict blocked: new={newDirection} " +
                        $"conflicts cached={cached?.Direction} " +
                        $"({(DateTime.UtcNow - (cached?.CapturedAt ?? DateTime.UtcNow)).TotalMinutes:F1} min ago). " +
                        $"Skipping until cache expires or direction aligns.");
                    return;
                }
            }

            var req = new TradeRequest
            {
                Pair        = sig.Pair ?? "",
                TradeType   = Enum.TryParse<TradeType>(sig.TradeType, true, out var tt) ? tt : TradeType.BUY,
                OrderType   = Enum.TryParse<OrderType>(sig.OrderType, true, out var ot) ? ot : OrderType.MARKET,
                EntryPrice  = sig.EntryPrice,
                StopLoss    = sig.StopLoss,
                TakeProfit  = sig.TakeProfit,
                TakeProfit2 = sig.TakeProfit2,
                LotSize     = sig.LotSize > 0 ? sig.LotSize : 0.01,
                Comment     = string.IsNullOrWhiteSpace(sig.Comment) ? "Claude_AI" : sig.Comment,
                MagicNumber = sig.MagicNumber > 0 ? sig.MagicNumber : 999001,
                MoveSLToBreakevenAfterTP1 = sig.MoveSLToBreakevenAfterTP1,
                CreatedAt   = DateTime.UtcNow
            };

            Log($"🤖 Signal: {req}");
            OnSignalGenerated?.Invoke(req);

            var result = await _execute(req).ConfigureAwait(false);
            Log(result.IsSuccess
                ? $"✅ Trade executed: {result}"
                : $"❌ Rejected: [{result.ErrorCode}] {result.ErrorMessage}");

            // Update regime cache after every execution attempt
            string tradePair = !string.IsNullOrWhiteSpace(req.Pair) ? req.Pair : symbol;
            if (!string.IsNullOrWhiteSpace(tradePair))
            {
                string executedDirection = req.TradeType.ToString().ToUpperInvariant();
                _contextManager.Update(tradePair, executedDirection,
                    result.IsSuccess ? $"Executed #{result.Ticket}" : result.ErrorMessage);
            }
        }
        // ══════════════════════════════════════════════════════════
        //  MARKET DATA PROMPT BUILDER
        // ══════════════════════════════════════════════════════════

        private static string BuildMarketDataPrompt(
            AccountInfo account,
            List<(string Symbol, SymbolInfo? Info)> prices,
            List<LivePosition> positions)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== LIVE MARKET DATA — {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ===");
            sb.AppendLine();
            sb.AppendLine($"Account: Balance=${account.Balance:F2}  " +
                          $"Equity=${account.Equity:F2}  " +
                          $"Free Margin=${account.FreeMargin:F2}");
            sb.AppendLine();
            sb.AppendLine("Live Prices:");
            foreach (var (sym, info) in prices)
            {
                sb.AppendLine(info != null
                    ? $"  {sym}: Ask={info.Ask:F5}  Bid={info.Bid:F5}  Spread={info.SpreadPips:F1} pips"
                    : $"  {sym}: (price unavailable)");
            }
            sb.AppendLine();
            sb.AppendLine($"Open Positions: {positions.Count}");
            foreach (var p in positions)
                sb.AppendLine($"  #{p.Ticket} {p.Type} {p.Symbol} {p.Lots:F2}L " +
                              $"@ {p.OpenPrice:F5}  P&L=${p.Profit:F2} ({p.ProfitPips:F1} pips)");
            sb.AppendLine();
            sb.AppendLine("Provide your trading decision as a JSON object.");
            return sb.ToString();
        }

        // ── Helpers ───────────────────────────────────────────────

        private static string ExtractJson(string text)
        {
            int start = text.IndexOf('{');
            int end   = text.LastIndexOf('}');
            return start >= 0 && end > start ? text[start..(end + 1)] : string.Empty;
        }

        private void Log(string msg)
        {
            Serilog.Log.Information("[Claude] {msg}", msg);
            OnLog?.Invoke(msg);
        }

        // ══════════════════════════════════════════════════════════
        //  DISPOSE
        // ══════════════════════════════════════════════════════════

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            _cts.Dispose();
        }
    }

    // ── Claude signal DTO — maps Claude's JSON response ──────────────

    internal sealed class ClaudeSignal
    {
        [JsonProperty("action")]
        public string? Action { get; set; }

        [JsonProperty("pair")]
        public string? Pair { get; set; }

        [JsonProperty("trade_type")]
        public string? TradeType { get; set; }

        [JsonProperty("order_type")]
        public string? OrderType { get; set; }

        [JsonProperty("entry_price")]
        public double EntryPrice { get; set; }

        [JsonProperty("stop_loss")]
        public double StopLoss { get; set; }

        [JsonProperty("take_profit")]
        public double TakeProfit { get; set; }

        [JsonProperty("take_profit_2")]
        public double TakeProfit2 { get; set; }

        [JsonProperty("lot_size")]
        public double LotSize { get; set; }

        [JsonProperty("comment")]
        public string? Comment { get; set; }

        [JsonProperty("magic_number")]
        public int MagicNumber { get; set; }

        [JsonProperty("move_sl_to_be_after_tp1")]
        public bool MoveSLToBreakevenAfterTP1 { get; set; }

        [JsonProperty("reason")]
        public string? Reason { get; set; }
    }
}
