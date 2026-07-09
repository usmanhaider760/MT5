using System.Net;
using System.Net.Sockets;
using System.Text;
using Atlas_Execution.MT5;
using Atlas_Execution.Protocol;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Verifies the req_id correlation-id round trip (P2-2): every request carries a fresh id,
/// and a response with a mismatched id is flagged via On_Log without failing the call.
/// Uses a real loopback TCP listener standing in for the MT5 EA.
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
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var server_task = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync();
            var buffer = new byte[4096];
            await socket.ReceiveAsync(buffer, SocketFlags.None); // read the request, ignore contents

            // Deliberately respond with a req_id that does not match the request
            var response = Encoding.UTF8.GetBytes("{\"req_id\":\"mismatched\",\"status\":\"ok\"}\n");
            await socket.SendAsync(response, SocketFlags.None);
        });

        using var client = new MT5_Bridge_Client("127.0.0.1", port);
        string? logged = null;
        client.On_Log += msg => logged = msg;

        var result = await client.Send_Async<MT5_Response>(new MT5_Request { Command = MT5_Command.PING });

        await server_task;
        listener.Stop();

        Assert.NotNull(result);
        Assert.True(result!.Is_Ok);
        Assert.NotNull(logged);
        Assert.Contains("req_id", logged);
    }
}
