using Anthropic;
using Anthropic.Models.Messages;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using Newtonsoft.Json;
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
        private readonly Func<TradeRequest, Task<TradeResult>> _execute;
        private AnthropicClient? _client;

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
        {
            _bridge  = bridge;
            _cfg     = cfg;
            _execute = execute;
        }

        public void UpdateConfig(ClaudeConfig cfg) => _cfg = cfg;

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
                catch (Exception ex) { Log($"⚠ Analysis error: {ex.Message}"); }

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_cfg.PollIntervalSeconds),
                        _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }
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

            // Gather market data from MT5
            var account = await _bridge.GetAccountInfoAsync().ConfigureAwait(false);
            if (account == null) { Log("⚠ Cannot fetch account info"); return; }

            var symbolPrices = new List<(string Symbol, SymbolInfo? Info)>();
            foreach (var sym in _cfg.WatchSymbols)
                symbolPrices.Add((sym, await _bridge.GetSymbolInfoAsync(sym).ConfigureAwait(false)));

            var positions = await _bridge.GetPositionsAsync().ConfigureAwait(false);

            string marketData = BuildMarketDataPrompt(account, symbolPrices, positions);
            Log($"🔍 Analyzing {_cfg.WatchSymbols.Count} symbol(s) via Claude...");

            // ── Call Claude API ─────────────────────────────────────
            // System prompt is cached (stable); market data is volatile (not cached).
            var response = await _client!.Messages.Create(new MessageCreateParams
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
                Messages =
                [
                    new() { Role = Role.User, Content = marketData }
                ]
            }).ConfigureAwait(false);

            // ── Extract text block ──────────────────────────────────
            string? responseText = null;
            foreach (var block in response.Content)
            {
                if (block.TryPickText(out TextBlock? tb))
                { responseText = tb!.Text; break; }
            }

            if (string.IsNullOrWhiteSpace(responseText))
            { Log("⚠ Claude returned no text"); return; }

            Log($"🤖 Claude: {responseText[..Math.Min(300, responseText.Length)]}");

            // Cache stats (informational)
            if (response.Usage.CacheCreationInputTokens > 0)
                Log($"💾 Cache created: {response.Usage.CacheCreationInputTokens} tokens");
            else if (response.Usage.CacheReadInputTokens > 0)
                Log($"⚡ Cache hit: {response.Usage.CacheReadInputTokens} tokens saved");

            await ParseAndExecuteAsync(responseText).ConfigureAwait(false);
        }

        // ══════════════════════════════════════════════════════════
        //  PARSE CLAUDE JSON → TRADE REQUEST → EXECUTE
        // ══════════════════════════════════════════════════════════

        private async Task ParseAndExecuteAsync(string text)
        {
            string json = ExtractJson(text);
            if (string.IsNullOrEmpty(json)) { Log("⚠ No JSON found in response"); return; }

            ClaudeSignal? sig;
            try { sig = JsonConvert.DeserializeObject<ClaudeSignal>(json); }
            catch (Exception ex) { Log($"⚠ JSON parse error: {ex.Message}"); return; }

            if (sig == null) { Log("⚠ Could not parse signal"); return; }

            string action = sig.Action?.ToUpperInvariant() ?? "";

            if (action == "NO_TRADE")
            {
                Log($"💤 No trade: {sig.Reason}");
                return;
            }

            if (action != "TRADE")
            {
                Log($"⚠ Unknown action: '{sig.Action}'");
                return;
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
