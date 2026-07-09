using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Atlas_Execution.Protocol;

namespace Atlas_Execution.MT5;

/// <summary>
/// TCP client that communicates with the ATLAS_Bridge EA running in MetaTrader 5.
/// The EA must be attached to any chart and listening on the configured port.
/// Protocol: JSON request terminated by newline → JSON response terminated by newline.
/// </summary>
public class MT5_Bridge_Client : IDisposable
{
    private readonly string _host;
    private readonly int    _port;
    private readonly int    _timeout_ms;
    private TcpClient?      _client;
    private NetworkStream?  _stream;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool Is_Connected => _client?.Connected == true;

    /// <summary>Fired (non-blocking) when a response's req_id doesn't match the request that was sent — a debuggability aid, never blocks the call.</summary>
    public event Action<string>? On_Log;

    public MT5_Bridge_Client(string host = "127.0.0.1", int port = 9090, int timeout_ms = 5000)
    {
        _host       = host;
        _port       = port;
        _timeout_ms = timeout_ms;
    }

    public async Task<bool> Connect_Async()
    {
        try
        {
            _client = new TcpClient();
            _client.ReceiveTimeout = _timeout_ms;
            _client.SendTimeout    = _timeout_ms;
            await _client.ConnectAsync(_host, _port);
            _stream = _client.GetStream();
            return true;
        }
        catch
        {
            _client?.Dispose();
            _client = null;
            return false;
        }
    }

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
        _lock.Dispose();
    }
}
