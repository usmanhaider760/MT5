using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Atlas_Execution.Protocol;

namespace Atlas_Execution.MT5;

/// <summary>
/// TCP server that the ATLAS_Bridge EA (running inside MetaTrader 5) connects out to.
/// MQL5 has no server-socket API (no bind/listen/accept) — only client-side sockets — so
/// this side must be the one that listens; the EA dials in via SocketConnect and the same
/// JSON-request / JSON-response-over-newline protocol runs in whichever direction the byte
/// stream goes once the connection is established.
/// </summary>
public class MT5_Bridge_Client : IDisposable
{
    private readonly string _host;
    private readonly int    _port;
    private readonly int    _timeout_ms;
    private TcpListener?    _listener;
    private Task<TcpClient>? _pending_accept;
    private TcpClient?      _client;
    private NetworkStream?  _stream;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool Is_Connected => _client?.Connected == true;

    /// <summary>The actual bound listening port — useful when constructed with port 0 (OS-assigned ephemeral port), e.g. in tests.</summary>
    public int Local_Port => (_listener?.LocalEndpoint as IPEndPoint)?.Port ?? _port;

    /// <summary>Fired (non-blocking) when a response's req_id doesn't match the request that was sent — a debuggability aid, never blocks the call.</summary>
    public event Action<string>? On_Log;

    public MT5_Bridge_Client(string host = "127.0.0.1", int port = 9090, int timeout_ms = 5000)
    {
        _host       = host;
        _port       = port;
        _timeout_ms = timeout_ms;
    }

    /// <summary>Ensures the listener is running and waits (up to timeout_ms) for the EA to connect in.</summary>
    public async Task<bool> Connect_Async()
    {
        try
        {
            if (_client?.Connected == true) return true;

            if (_listener == null)
            {
                var addr = IPAddress.TryParse(_host, out var parsed) ? parsed : IPAddress.Loopback;
                _listener = new TcpListener(addr, _port);
                _listener.Start();
            }

            // Reuse a still-pending accept across calls instead of racing a fresh one each
            // time — otherwise a timeout here would leave an orphaned accept that silently
            // swallows the EA's next connection attempt.
            _pending_accept ??= _listener.AcceptTcpClientAsync();

            var completed = await Task.WhenAny(_pending_accept, Task.Delay(_timeout_ms));
            if (completed != _pending_accept)
                return false; // EA hasn't connected yet — keep waiting on future calls

            _client = await _pending_accept;
            _pending_accept = null;
            _client.ReceiveTimeout = _timeout_ms;
            _client.SendTimeout    = _timeout_ms;
            _stream = _client.GetStream();
            return true;
        }
        catch
        {
            _client?.Dispose();
            _client = null;
            _pending_accept = null;
            return false;
        }
    }

    /// <summary>Drops the current EA connection. The listener stays up so a future Connect_Async can accept a reconnect.</summary>
    public void Disconnect()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
    }

    public async Task<T?> Send_Async<T>(MT5_Request request) where T : MT5_Response
    {
        await _lock.WaitAsync();
        try
        {
            if (!Is_Connected && !await Connect_Async())
                return null;

            var json    = JsonSerializer.Serialize(request) + "\n";
            var bytes   = Encoding.UTF8.GetBytes(json);
            await _stream!.WriteAsync(bytes);

            var buffer   = new byte[65536];
            var sb       = new StringBuilder();
            int total    = 0;

            while (true)
            {
                int read = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                if (read == 0) break;
                total += read;
                var chunk = Encoding.UTF8.GetString(buffer, 0, read);
                sb.Append(chunk);
                if (sb.ToString().TrimEnd().EndsWith('\n') || sb.ToString().TrimEnd().EndsWith('}'))
                    break;
            }

            if (sb.Length == 0) return null;
            var response = JsonSerializer.Deserialize<T>(sb.ToString().Trim());

            if (response != null && !string.IsNullOrEmpty(response.Req_Id) && response.Req_Id != request.Req_Id)
                On_Log?.Invoke($"MT5 bridge: response req_id '{response.Req_Id}' does not match request req_id '{request.Req_Id}' for {request.Command}");

            return response;
        }
        catch
        {
            Disconnect();
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        Disconnect();
        _listener?.Stop();
        _lock.Dispose();
    }
}
