using System.Net.Sockets;
using System.Text;
using Atlas_Execution.MT5;
using Atlas_Execution.Protocol;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Verifies the req_id correlation-id round trip (P2-2): every request carries a fresh id,
/// and a response with a mismatched id is flagged via On_Log without failing the call.
/// MT5_Bridge_Client is the TCP listener (MQL5 has no server-socket API, so the EA must be
/// the one that dials in) — these tests stand in for the EA with a plain TcpClient.
/// </summary>
public class MT5_Bridge_Client_Tests
{
    [Fact]
    public void Each_Request_Gets_A_Fresh_Non_Empty_Req_Id()
    {
        var a = new MT5_Request { Command = MT5_Command.PING };
        var b = new MT5_Request { Command = MT5_Command.PING };

        Assert.False(string.IsNullOrEmpty(a.Req_Id));
        Assert.NotEqual(a.Req_Id, b.Req_Id);
    }

    [Fact]
    public async Task Mismatched_Response_Req_Id_Fires_On_Log_Without_Failing_The_Call()
    {
        using var bridge = new MT5_Bridge_Client("127.0.0.1", 0);
        string? logged = null;
        bridge.On_Log += msg => logged = msg;

        var send_task = bridge.Send_Async<MT5_Response>(new MT5_Request { Command = MT5_Command.PING });
        int port = bridge.Local_Port;

        using var ea = new TcpClient();
        await ea.ConnectAsync("127.0.0.1", port);
        var stream = ea.GetStream();
        var req_buffer = new byte[4096];
        await stream.ReadAsync(req_buffer); // read the request, ignore contents

        // Deliberately respond with a req_id that does not match the request
        var response = Encoding.UTF8.GetBytes("{\"req_id\":\"mismatched\",\"status\":\"ok\"}\n");
        await stream.WriteAsync(response);

        var result = await send_task;

        Assert.NotNull(result);
        Assert.True(result!.Is_Ok);
        Assert.NotNull(logged);
        Assert.Contains("req_id", logged);
    }
}
