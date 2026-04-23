using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using MT5TradingBot.Models;
using Newtonsoft.Json;
using Serilog;

namespace MT5TradingBot.Services
{
    /// <summary>
    /// Production MT5Bridge.
    /// — Persistent named-pipe OR TCP connection with auto-reconnect loop
    /// — Thread-safe: one request at a time via SemaphoreSlim
    /// — Full timeout + cancellation support
    /// — Reconnect fires OnConnectionChanged so UI always reflects real state
    /// </summary>
    public sealed class MT5Bridge : IDisposable
    {
        // ── Config ────────────────────────────────────────────────
        private readonly MT5Settings _cfg;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();

        // ── State ─────────────────────────────────────────────────
        private volatile bool _connected;
        private int _reconnectAttempts;
        private bool _disposed;

        // ── Events ────────────────────────────────────────────────
        public event Action<string>? OnLog;
        public event Action<bool>? OnConnectionChanged;

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

        public async Task<TradeResult> OpenTradeAsync(TradeRequest req)
        {
            Log($"→ OPEN {req.TradeType} {req.Pair} Lots:{req.LotSize:F2} SL:{req.StopLoss:F5} TP:{req.TakeProfit:F5}");
            try
            {
                var r = await SendAsync("OPEN_TRADE", req).ConfigureAwait(false);
                if (r == null) return Fail(req.Id, "MT5_NO_RESPONSE", "No response from EA");
                if (!r.Success) return Fail(req.Id, "MT5_REJECTED", r.Error);

                var result = Deserialize<TradeResult>(r.Data)
                          ?? new TradeResult { RequestId = req.Id, Status = TradeStatus.Submitted };
                Log($"← {result}");
                return result;
            }
            catch (Exception ex)
            {
                Log($"OpenTrade exception: {ex.Message}");
                return Fail(req.Id, "EXCEPTION", ex.Message);
            }
        }

        public async Task<bool> CloseTradeAsync(long ticket)
        {
            Log($"→ CLOSE #{ticket}");
            var r = await SendAsync("CLOSE_TRADE", new { ticket }).ConfigureAwait(false);
            bool ok = r?.Success == true;
            Log(ok ? $"← Closed #{ticket}" : $"← Close failed: {r?.Error}");
            return ok;
        }

        public async Task<List<LivePosition>> GetPositionsAsync()
        {
            var r = await SendAsync("GET_POSITIONS", null).ConfigureAwait(false);
            if (r?.Success != true) return [];
            return Deserialize<List<LivePosition>>(r.Data) ?? [];
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

        /// <summary>Start background reconnect loop. Fires OnConnectionChanged on state change.</summary>
        public void StartReconnectLoop()
        {
            _ = Task.Run(ReconnectLoopAsync, _cts.Token);
        }

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
                        Log("🔗 Connected to MT5 EA");
                    }
                    else
                    {
                        _reconnectAttempts++;
                        if (_cfg.MaxReconnectAttempts > 0 &&
                            _reconnectAttempts >= _cfg.MaxReconnectAttempts)
                        {
                            Log($"❌ Max reconnect attempts ({_cfg.MaxReconnectAttempts}) reached. Stopping loop.");
                            return;
                        }
                    }
                }
                else
                {
                    // Heartbeat: verify still alive
                    bool alive = await PingAsync().ConfigureAwait(false);
                    if (!alive) Log("⚠ MT5 connection lost — will retry");
                }

                await Task.Delay(_cfg.ReconnectIntervalMs, _cts.Token)
                          .ConfigureAwait(false);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CORE SEND — thread-safe, dispatches to pipe or socket
        // ══════════════════════════════════════════════════════════

        private async Task<IpcResponse?> SendAsync(string cmd, object? data)
        {
            if (_disposed) return null;

            await _lock.WaitAsync(_cts.Token).ConfigureAwait(false);
            try
            {
                var msg = new IpcMessage { Command = cmd, Data = data };
                string json = JsonConvert.SerializeObject(msg, Formatting.None);

                return _cfg.Mode == ConnectionMode.NamedPipe
                    ? await SendPipeAsync(json).ConfigureAwait(false)
                    : await SendSocketAsync(json).ConfigureAwait(false);
            }
            finally { _lock.Release(); }
        }

        // ── Named Pipe (preferred for local MT5) ─────────────────

        private async Task<IpcResponse?> SendPipeAsync(string json)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            cts.CancelAfter(_cfg.TimeoutMs);

            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".", _cfg.PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous | PipeOptions.WriteThrough);

                await pipe.ConnectAsync(cts.Token).ConfigureAwait(false);
                pipe.ReadMode = PipeTransmissionMode.Byte;

                // Write — length-prefixed for reliability
                byte[] payload = Encoding.UTF8.GetBytes(json);
                byte[] lenBytes = BitConverter.GetBytes(payload.Length);
                await pipe.WriteAsync(lenBytes, cts.Token).ConfigureAwait(false);
                await pipe.WriteAsync(payload, cts.Token).ConfigureAwait(false);
                await pipe.FlushAsync(cts.Token).ConfigureAwait(false);

                // Read response — length-prefixed
                byte[] rlenBuf = new byte[4];
                await ReadExactAsync(pipe, rlenBuf, cts.Token).ConfigureAwait(false);
                int rlen = BitConverter.ToInt32(rlenBuf);

                if (rlen <= 0 || rlen > 1_048_576) // sanity: max 1 MB
                    return null;

                byte[] rbuf = new byte[rlen];
                await ReadExactAsync(pipe, rbuf, cts.Token).ConfigureAwait(false);

                string responseJson = Encoding.UTF8.GetString(rbuf);
                return JsonConvert.DeserializeObject<IpcResponse>(responseJson);
            }
            catch (OperationCanceledException)
            {
                Log("Pipe timeout — EA may be busy or not running");
                return null;
            }
            catch (Exception ex)
            {
                Log($"Pipe error: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        // ── TCP Socket (for remote MT5) ───────────────────────────

        private async Task<IpcResponse?> SendSocketAsync(string json)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            cts.CancelAfter(_cfg.TimeoutMs);

            try
            {
                using var tcp = new TcpClient { NoDelay = true };
                await tcp.ConnectAsync(_cfg.Host, _cfg.Port, cts.Token).ConfigureAwait(false);

                using var stream = tcp.GetStream();
                byte[] payload = Encoding.UTF8.GetBytes(json);
                byte[] lenBytes = BitConverter.GetBytes(payload.Length);
                await stream.WriteAsync(lenBytes, cts.Token).ConfigureAwait(false);
                await stream.WriteAsync(payload, cts.Token).ConfigureAwait(false);
                await stream.FlushAsync(cts.Token).ConfigureAwait(false);

                byte[] rlenBuf = new byte[4];
                await ReadExactAsync(stream, rlenBuf, cts.Token).ConfigureAwait(false);
                int rlen = BitConverter.ToInt32(rlenBuf);

                if (rlen <= 0 || rlen > 1_048_576) return null;

                byte[] rbuf = new byte[rlen];
                await ReadExactAsync(stream, rbuf, cts.Token).ConfigureAwait(false);

                return JsonConvert.DeserializeObject<IpcResponse>(Encoding.UTF8.GetString(rbuf));
            }
            catch (OperationCanceledException)
            {
                Log("Socket timeout");
                return null;
            }
            catch (Exception ex)
            {
                Log($"Socket error: {ex.Message}");
                return null;
            }
        }

        // ── Exact-read helper (no partial reads) ──────────────────

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
            RequestId = reqId,
            Status = TradeStatus.Error,
            ErrorCode = code,
            ErrorMessage = msg
        };

        private void Log(string msg)
        {
            Serilog.Log.Information("[Bridge] {msg}", msg);
            OnLog?.Invoke(msg);
        }

        // ══════════════════════════════════════════════════════════
        //  DISPOSE
        // ══════════════════════════════════════════════════════════

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
