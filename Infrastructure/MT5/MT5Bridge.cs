using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.Backtesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace MT5TradingBot.Modules.BrokerIntegration
{
    /// <summary>
    /// Persistent named-pipe OR TCP connection to MT5 EA with auto-reconnect.
    /// Thread-safe: one request at a time via SemaphoreSlim.
    /// </summary>
    public sealed class MT5Bridge : IDisposable
    {
        private readonly MT5Settings _cfg;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private volatile bool _connected;
        private int _reconnectAttempts;
        private bool _disposed;

        public event Action<string>? OnLog;
        public event Action<bool>?   OnConnectionChanged;
        public bool IsConnected => _connected;

        public MT5Bridge(MT5Settings cfg) => _cfg = cfg;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public async Task<bool> PingAsync()
        {
            try
            {
                var r = await SendAsync("PING", null).ConfigureAwait(false);
                SetConnected(r?.Success == true);
                return _connected;
            }
            catch { SetConnected(false); return false; }
        }

        public async Task<(bool Success, EaHealthInfo? Health, string Error)> TryGetEaHealthAsync()
        {
            var r = await SendAsync("GET_EA_HEALTH", null).ConfigureAwait(false);
            if (r?.Success != true)
                return (false, null, r?.Error ?? "No EA health response from MT5");

            var health = Deserialize<EaHealthInfo>(r.Data);
            return health != null
                ? (true, health, "")
                : (false, null, "Invalid EA health response from MT5");
        }

        public async Task<TradeResult> OpenTradeAsync(TradeRequest req)
        {
            Log($"OPEN {req.TradeType} {req.Pair} Lots:{req.LotSize:F2} SL:{req.StopLoss:F5} TP:{req.TakeProfit:F5}");
            try
            {
                var payload = JsonConvert.SerializeObject(ToMt5TradePayload(req), Formatting.None);
                var r = await SendAsync("OPEN_TRADE", payload).ConfigureAwait(false);
                if (r == null) return Fail(req.Id, "MT5_NO_RESPONSE", "No response from EA");
                if (!r.Success) return Fail(req.Id, "MT5_REJECTED", r.Error);

                var result = Deserialize<TradeResult>(r.Data)
                          ?? new TradeResult { RequestId = req.Id, Status = TradeStatus.Submitted };
                Log($"MT5 response: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Log($"OpenTrade exception: {ex.Message}");
                return Fail(req.Id, "EXCEPTION", ex.Message);
            }
        }

        private static object ToMt5TradePayload(TradeRequest req) => new
        {
            req.Id,
            req.Pair,
            req.TradeType,
            req.OrderType,
            req.EntryPrice,
            req.StopLoss,
            req.TakeProfit,
            req.TakeProfit2,
            req.LotSize,
            req.Comment,
            req.MagicNumber,
            req.ExpiryMinutes,
            req.MoveSLToBreakevenAfterTP1,
            req.CreatedAt
        };

        public async Task<bool> CloseTradeAsync(long ticket)
        {
            Log($"CLOSE #{ticket}");
            var r = await SendAsync("CLOSE_TRADE", new { ticket }).ConfigureAwait(false);
            bool ok = r?.Success == true;
            Log(ok ? $"Closed #{ticket}" : $"Close failed: {r?.Error}");
            return ok;
        }

        public async Task<List<LivePosition>> GetPositionsAsync()
        {
            var result = await TryGetPositionsAsync().ConfigureAwait(false);
            return result.Success ? result.Positions : [];
        }

        public async Task<(bool Success, List<LivePosition> Positions)> TryGetPositionsAsync()
        {
            var r = await SendAsync("GET_POSITIONS", null).ConfigureAwait(false);
            if (r?.Success != true) return (false, []);
            var positions = Deserialize<List<LivePosition>>(r.Data);
            return positions != null ? (true, positions) : (false, []);
        }

        public async Task<AccountInfo?> GetAccountInfoAsync()
        {
            var r = await SendAsync("GET_ACCOUNT", null).ConfigureAwait(false);
            if (r?.Success != true) return null;
            var info = Deserialize<AccountInfo>(r.Data);
            if (info != null) { info.IsConnected = true; info.LastUpdated = DateTime.UtcNow; }
            return info;
        }

        public async Task<bool> ModifyPositionAsync(long ticket, double sl, double tp)
        {
            var r = await SendAsync("MODIFY_POSITION",
                new { ticket, stop_loss = sl, take_profit = tp }).ConfigureAwait(false);
            return r?.Success == true;
        }

        public async Task<SymbolInfo?> GetSymbolInfoAsync(string symbol)
        {
            var r = await SendAsync("GET_SYMBOL_INFO", new { symbol }).ConfigureAwait(false);
            if (r?.Success != true) return null;
            return Deserialize<SymbolInfo>(r.Data);
        }

        public async Task<(bool Success, MarginEstimate? Estimate, string Error)> TryGetMarginEstimateAsync(
            string symbol,
            TradeType tradeType,
            double lots,
            double price)
        {
            var r = await SendAsync(
                "GET_MARGIN_ESTIMATE",
                new
                {
                    symbol,
                    trade_type = tradeType.ToString(),
                    lots,
                    price
                }).ConfigureAwait(false);

            if (r?.Success != true)
                return (false, null, r?.Error ?? "No margin estimate response from MT5");

            var estimate = Deserialize<MarginEstimate>(r.Data);
            return estimate != null
                ? (true, estimate, "")
                : (false, null, "Invalid margin estimate response from MT5");
        }

        public async Task<(bool Success, OrderCheckResult? Result, string Error)> TryCheckOrderAsync(
            TradeRequest request,
            double price)
        {
            var r = await SendAsync(
                "CHECK_ORDER",
                new
                {
                    symbol = request.Pair,
                    trade_type = request.TradeType.ToString(),
                    order_type = request.OrderType.ToString(),
                    lots = request.LotSize,
                    price,
                    stop_loss = request.StopLoss,
                    take_profit = request.TakeProfit,
                    magic_number = request.MagicNumber
                }).ConfigureAwait(false);

            if (r?.Success != true)
                return (false, null, r?.Error ?? "No OrderCheck response from MT5");

            var result = Deserialize<OrderCheckResult>(r.Data);
            return result != null
                ? (true, result, "")
                : (false, null, "Invalid OrderCheck response from MT5");
        }

        public async Task<JObject?> GetMarketSnapshotAsync(TradeRequest req, BotConfig bot)
        {
            var payload = JsonConvert.SerializeObject(new
            {
                symbol = req.Pair,
                trade_type = req.TradeType.ToString(),
                order_type = req.OrderType.ToString(),
                entry_price = req.EntryPrice,
                stop_loss = req.StopLoss,
                take_profit = req.TakeProfit,
                take_profit_2 = req.TakeProfit2,
                lot_size = req.LotSize,
                max_risk_pct = bot.MaxRiskPercent,
                daily_loss_limit_pct = bot.EmergencyCloseDrawdownPct,
                max_spread_pips = bot.MaxSpreadPips
            }, Formatting.None);

            var r = await SendAsync("GET_MARKET_SNAPSHOT", payload).ConfigureAwait(false);
            if (r?.Success != true) return null;
            return Deserialize<JObject>(r.Data);
        }

        public async Task<(bool Success, IReadOnlyList<BacktestTick> Ticks, string Error)> TryGetHistoricalTicksAsync(
            string symbol,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows)
        {
            Log($"GET_TICKS {symbol} {fromUtc:O} -> {toUtc:O} maxRows:{Math.Max(1, maxRows)}");
            var r = await SendAsync(
                "GET_TICKS",
                new
                {
                    symbol,
                    from_unix_ms = ToUnixMilliseconds(fromUtc),
                    to_unix_ms = ToUnixMilliseconds(toUtc),
                    max_rows = Math.Max(1, maxRows)
                }).ConfigureAwait(false);

            if (r?.Success != true)
                return (false, [], r?.Error ?? "No historical tick response from MT5");

            var ticks = Deserialize<List<BacktestTick>>(r.Data);
            Log($"GET_TICKS {symbol} parsed rows:{ticks?.Count ?? 0}");
            return ticks != null
                ? (true, ticks, "")
                : (false, [], "Invalid historical tick response from MT5");
        }

        public async Task<(bool Success, IReadOnlyList<BacktestOhlcCandle> Candles, string Error)> TryGetHistoricalRatesAsync(
            string symbol,
            string timeframe,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows)
        {
            string resolvedTimeframe = string.IsNullOrWhiteSpace(timeframe) ? "M1" : timeframe;
            Log($"GET_RATES {symbol} {resolvedTimeframe} {fromUtc:O} -> {toUtc:O} maxRows:{Math.Max(1, maxRows)}");
            var r = await SendAsync(
                "GET_RATES",
                new
                {
                    symbol,
                    timeframe = resolvedTimeframe,
                    from_unix_ms = ToUnixMilliseconds(fromUtc),
                    to_unix_ms = ToUnixMilliseconds(toUtc),
                    max_rows = Math.Max(1, maxRows)
                }).ConfigureAwait(false);

            if (r?.Success != true)
                return (false, [], r?.Error ?? "No historical OHLC response from MT5");

            var candles = Deserialize<List<BacktestOhlcCandle>>(r.Data);
            Log($"GET_RATES {symbol} {resolvedTimeframe} parsed rows:{candles?.Count ?? 0}");
            return candles != null
                ? (true, candles, "")
                : (false, [], "Invalid historical OHLC response from MT5");
        }

        public void StartReconnectLoop() =>
            _ = Task.Run(ReconnectLoopAsync, _cts.Token);

        // ══════════════════════════════════════════════════════════
        //  RECONNECT LOOP
        // ══════════════════════════════════════════════════════════

        private async Task ReconnectLoopAsync()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                if (!_connected)
                {
                    bool ok = await PingAsync().ConfigureAwait(false);
                    if (ok)
                    {
                        _reconnectAttempts = 0;
                        Log("Connected to MT5 EA");
                    }
                    else
                    {
                        _reconnectAttempts++;
                        if (_cfg.MaxReconnectAttempts > 0 &&
                            _reconnectAttempts >= _cfg.MaxReconnectAttempts)
                        {
                            Log($"[ERROR] Max reconnect attempts ({_cfg.MaxReconnectAttempts}) reached.");
                            return;
                        }
                    }
                }
                else
                {
                    bool alive = await PingAsync().ConfigureAwait(false);
                    if (!alive) Log("[WARN] MT5 connection lost - will retry");
                }

                await Task.Delay(_cfg.ReconnectIntervalMs, _cts.Token).ConfigureAwait(false);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CORE SEND
        // ══════════════════════════════════════════════════════════

        private async Task<IpcResponse?> SendAsync(string cmd, object? data)
        {
            if (_disposed) return null;
            await _lock.WaitAsync(_cts.Token).ConfigureAwait(false);
            try
            {
                var msg  = new IpcMessage { Command = cmd, Data = data };
                string json = JsonConvert.SerializeObject(msg, Formatting.None);
                return _cfg.Mode == ConnectionMode.NamedPipe
                    ? await SendPipeAsync(json).ConfigureAwait(false)
                    : await SendSocketAsync(json).ConfigureAwait(false);
            }
            finally { _lock.Release(); }
        }

        private async Task<IpcResponse?> SendPipeAsync(string json)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            cts.CancelAfter(_cfg.TimeoutMs);
            try
            {
                using var pipe = new NamedPipeServerStream(_cfg.PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.WriteThrough);
                await pipe.WaitForConnectionAsync(cts.Token).ConfigureAwait(false);

                byte[] payload  = Encoding.UTF8.GetBytes(json);
                byte[] lenBytes = BitConverter.GetBytes(payload.Length);
                await pipe.WriteAsync(lenBytes, cts.Token).ConfigureAwait(false);
                await pipe.WriteAsync(payload,  cts.Token).ConfigureAwait(false);
                await pipe.FlushAsync(cts.Token).ConfigureAwait(false);

                byte[] rlenBuf = new byte[4];
                await ReadExactAsync(pipe, rlenBuf, cts.Token).ConfigureAwait(false);
                int rlen = BitConverter.ToInt32(rlenBuf);
                if (rlen <= 0 || rlen > 1_048_576) return null;

                byte[] rbuf = new byte[rlen];
                await ReadExactAsync(pipe, rbuf, cts.Token).ConfigureAwait(false);
                return JsonConvert.DeserializeObject<IpcResponse>(Encoding.UTF8.GetString(rbuf));
            }
            catch (OperationCanceledException) { Log("Pipe timeout"); return null; }
            catch (Exception ex)               { Log($"Pipe error: {ex.Message}"); return null; }
        }

        private async Task<IpcResponse?> SendSocketAsync(string json)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            cts.CancelAfter(_cfg.TimeoutMs);
            try
            {
                using var tcp = new TcpClient { NoDelay = true };
                await tcp.ConnectAsync(_cfg.Host, _cfg.Port, cts.Token).ConfigureAwait(false);
                using var stream = tcp.GetStream();

                byte[] payload  = Encoding.UTF8.GetBytes(json);
                byte[] lenBytes = BitConverter.GetBytes(payload.Length);
                await stream.WriteAsync(lenBytes, cts.Token).ConfigureAwait(false);
                await stream.WriteAsync(payload,  cts.Token).ConfigureAwait(false);
                await stream.FlushAsync(cts.Token).ConfigureAwait(false);

                byte[] rlenBuf = new byte[4];
                await ReadExactAsync(stream, rlenBuf, cts.Token).ConfigureAwait(false);
                int rlen = BitConverter.ToInt32(rlenBuf);
                if (rlen <= 0 || rlen > 1_048_576) return null;

                byte[] rbuf = new byte[rlen];
                await ReadExactAsync(stream, rbuf, cts.Token).ConfigureAwait(false);
                return JsonConvert.DeserializeObject<IpcResponse>(Encoding.UTF8.GetString(rbuf));
            }
            catch (OperationCanceledException) { Log("Socket timeout"); return null; }
            catch (Exception ex)               { Log($"Socket error: {ex.Message}"); return null; }
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buf, CancellationToken ct)
        {
            int offset = 0;
            while (offset < buf.Length)
            {
                int read = await stream.ReadAsync(buf.AsMemory(offset, buf.Length - offset), ct)
                                       .ConfigureAwait(false);
                if (read == 0) throw new IOException("Connection closed by MT5 EA");
                offset += read;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════

        private void SetConnected(bool value)
        {
            if (_connected == value) return;
            _connected = value;
            OnConnectionChanged?.Invoke(value);
        }

        private static T? Deserialize<T>(object? data)
        {
            if (data == null) return default;
            string json = data is string s ? s : JsonConvert.SerializeObject(data);
            return JsonConvert.DeserializeObject<T>(json);
        }

        private static TradeResult Fail(string reqId, string code, string msg) => new()
        {
            RequestId    = reqId,
            Status       = TradeStatus.Error,
            ErrorCode    = code,
            ErrorMessage = msg
        };

        private static long ToUnixMilliseconds(DateTime utc) =>
            new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        private void Log(string msg)
        {
            Serilog.Log.Information("[Bridge] {msg}", msg);
            OnLog?.Invoke(msg);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
            _lock.Dispose();
        }
    }
}
