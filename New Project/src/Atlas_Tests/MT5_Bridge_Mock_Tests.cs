using System.Net.Sockets;
using System.Text;
using Atlas_Execution.MT5;
using Atlas_Execution.Protocol;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Integration tests for MT5_Bridge_Client, which is the TCP listener side of the connection
/// (MQL5 has no server-socket API — the EA must dial in via SocketConnect, so the C# side has
/// to be the one that listens). These tests stand in for the EA with plain TcpClient calls
/// against a bridge bound to an OS-assigned ephemeral port (port 0).
/// Covers connect, the read paths (tick/candles/positions), order placement success/failure,
/// and disconnect/reconnect behavior — none of which had any coverage before P3-5.
/// </summary>
public class MT5_Bridge_Mock_Tests
{
    [Fact]
    public async Task Connect_Async_Succeeds_When_The_Ea_Dials_In()
    {
        using var bridge = new MT5_Bridge_Client("127.0.0.1", 0);

        var connect_task = bridge.Connect_Async();
        int port = bridge.Local_Port;
        using var ea = await Mock_EA_Client.Connect_Async(port);

        bool connected = await connect_task;

        Assert.True(connected);
        Assert.True(bridge.Is_Connected);
    }

    [Fact]
    public async Task Get_Tick_Populates_Bid_Ask_Spread_From_Ea_Response()
    {
        using var bridge = new MT5_Bridge_Client("127.0.0.1", 0);

        var send_task = bridge.Send_Async<MT5_Tick_Response>(new MT5_Request { Command = MT5_Command.GET_TICK, Symbol = "EURUSD" });
        int port = bridge.Local_Port;
        var ea_task = Mock_EA_Client.Handle_One_Exchange_Async(port,
            "{\"status\":\"ok\",\"bid\":1.10500,\"ask\":1.10520,\"spread_pips\":2.0,\"market_open\":true}");

        var response = await send_task;
        await ea_task;

        Assert.NotNull(response);
        Assert.True(response!.Is_Ok);
        Assert.Equal(1.10500, response.Bid, 5);
        Assert.Equal(1.10520, response.Ask, 5);
        Assert.Equal(2.0, response.Spread_Pips, 2);
        Assert.True(response.Market_Open);
    }

    [Fact]
    public async Task Get_Candles_Returns_Correct_Ohlcv_List()
    {
        using var bridge = new MT5_Bridge_Client("127.0.0.1", 0);

        string candles_json =
            "{\"status\":\"ok\",\"candles\":[" +
            "{\"t\":1000,\"o\":1.1,\"h\":1.2,\"l\":1.0,\"c\":1.15,\"v\":100}," +
            "{\"t\":2000,\"o\":1.15,\"h\":1.25,\"l\":1.10,\"c\":1.20,\"v\":150}," +
            "{\"t\":3000,\"o\":1.20,\"h\":1.30,\"l\":1.15,\"c\":1.25,\"v\":200}]}";
        var send_task = bridge.Send_Async<MT5_Candles_Response>(new MT5_Request { Command = MT5_Command.GET_CANDLES, Symbol = "EURUSD", Timeframe = "H1", Count = 3 });
        int port = bridge.Local_Port;
        var ea_task = Mock_EA_Client.Handle_One_Exchange_Async(port, candles_json);

        var response = await send_task;
        await ea_task;

        Assert.NotNull(response);
        Assert.Equal(3, response!.Candles.Count);
        Assert.Equal(1.1, response.Candles[0].Open, 5);
        Assert.Equal(1.25, response.Candles[2].Close, 5);
        Assert.Equal(200, response.Candles[2].Volume);
    }

    [Fact]
    public async Task Get_Positions_Returns_Correct_Fields_For_Each_Position()
    {
        using var bridge = new MT5_Bridge_Client("127.0.0.1", 0);

        string positions_json =
            "{\"status\":\"ok\",\"positions\":[" +
            "{\"ticket\":111,\"symbol\":\"EURUSD\",\"type\":\"buy\",\"lots\":0.10,\"open_price\":1.1000,\"sl\":1.0950,\"tp\":1.1100,\"profit\":25.5,\"open_time\":1700000000,\"comment\":\"ATLAS\"}," +
            "{\"ticket\":222,\"symbol\":\"XAUUSD\",\"type\":\"sell\",\"lots\":0.05,\"open_price\":2400.0,\"sl\":2410.0,\"tp\":2380.0,\"profit\":-12.0,\"open_time\":1700001000,\"comment\":\"ATLAS\"}]}";
        var send_task = bridge.Send_Async<MT5_Positions_Response>(new MT5_Request { Command = MT5_Command.GET_POSITIONS });
        int port = bridge.Local_Port;
        var ea_task = Mock_EA_Client.Handle_One_Exchange_Async(port, positions_json);

        var response = await send_task;
        await ea_task;

        Assert.NotNull(response);
        Assert.Equal(2, response!.Positions.Count);
        Assert.Equal(111, response.Positions[0].Ticket);
        Assert.Equal("buy", response.Positions[0].Type);
        Assert.Equal(222, response.Positions[1].Ticket);
        Assert.Equal("sell", response.Positions[1].Type);
        Assert.Equal(-12.0, response.Positions[1].Profit, 2);
    }

    [Fact]
    public async Task Send_Order_Success_Returns_Ticket_And_Broker_Message()
    {
        using var bridge = new MT5_Bridge_Client("127.0.0.1", 0);
        var execution = new MT5_Execution_Service(bridge, demo_mode: false);

        var signal = new Atlas_Domain.BusinessObjects.Trade_Signal_BO
        {
            Symbol_Name = "EURUSD",
            Direction = Atlas_Domain.Enums.Trade_Direction_Type.Buy,
            Entry_Price = 1.1050m,
            Stop_Loss_Price = 1.1000m,
            Take_Profit_Price = 1.1150m
        };
        var send_task = execution.Send_Order_Async(signal, 0.10m);
        int port = bridge.Local_Port;
        var ea_task = Mock_EA_Client.Handle_One_Exchange_Async(port,
            "{\"status\":\"ok\",\"ticket\":12345,\"broker_message\":\"filled\",\"executed_price\":1.10501}");

        var (success, ticket, message) = await send_task;
        await ea_task;

        Assert.True(success);
        Assert.Equal(12345, ticket);
        Assert.Equal("filled", message);
    }

    [Fact]
    public async Task Send_Order_Failure_Returns_Broker_Error_Message()
    {
        using var bridge = new MT5_Bridge_Client("127.0.0.1", 0);
        var execution = new MT5_Execution_Service(bridge, demo_mode: false);

        var signal = new Atlas_Domain.BusinessObjects.Trade_Signal_BO
        {
            Symbol_Name = "EURUSD",
            Direction = Atlas_Domain.Enums.Trade_Direction_Type.Buy,
            Entry_Price = 1.1050m,
            Stop_Loss_Price = 1.1000m,
            Take_Profit_Price = 1.1150m
        };
        var send_task = execution.Send_Order_Async(signal, 0.10m);
        int port = bridge.Local_Port;
        var ea_task = Mock_EA_Client.Handle_One_Exchange_Async(port,
            "{\"status\":\"error\",\"error\":\"Insufficient margin\"}");

        var (success, ticket, message) = await send_task;
        await ea_task;

        Assert.False(success);
        Assert.Equal(0, ticket);
        Assert.Equal("Insufficient margin", message);
    }

    [Fact]
    public async Task Bridge_Disconnect_Mid_Response_Returns_Null()
    {
        using var bridge = new MT5_Bridge_Client("127.0.0.1", 0);

        var send_task = bridge.Send_Async<MT5_Response>(new MT5_Request { Command = MT5_Command.PING });
        int port = bridge.Local_Port;
        var ea_task = Mock_EA_Client.Connect_Read_Then_Drop_Async(port);

        var response = await send_task;
        await ea_task;

        Assert.Null(response);
    }

    [Fact]
    public async Task Bridge_Auto_Reconnects_On_Next_Send_Async_After_A_Disconnect()
    {
        using var bridge = new MT5_Bridge_Client("127.0.0.1", 0);

        var first_send = bridge.Send_Async<MT5_Response>(new MT5_Request { Command = MT5_Command.PING });
        int port = bridge.Local_Port;
        var first_ea = Mock_EA_Client.Handle_One_Exchange_Async(port, "{\"status\":\"ok\"}");
        var first_response = await first_send;
        await first_ea;

        Assert.NotNull(first_response);
        Assert.True(bridge.Is_Connected);

        bridge.Disconnect();
        Assert.False(bridge.Is_Connected);

        // Send_Async must transparently accept a fresh EA connection because Is_Connected is now false
        var second_send = bridge.Send_Async<MT5_Response>(new MT5_Request { Command = MT5_Command.PING });
        var second_ea = Mock_EA_Client.Handle_One_Exchange_Async(port, "{\"status\":\"ok\"}");
        var second_response = await second_send;
        await second_ea;

        Assert.NotNull(second_response);
        Assert.True(second_response!.Is_Ok);
        Assert.True(bridge.Is_Connected);
    }
}

/// <summary>Minimal loopback TCP stand-in for the ATLAS_Bridge MT5 EA — connects OUT to the bridge, as the real EA does.</summary>
file static class Mock_EA_Client
{
    public static async Task<TcpClient> Connect_Async(int port)
    {
        var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);
        return client;
    }

    /// <summary>Connects, reads one request, writes the given canned response, then disconnects.</summary>
    public static async Task Handle_One_Exchange_Async(int port, string response_json)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);
        var stream = client.GetStream();
        var buffer = new byte[8192];
        await stream.ReadAsync(buffer);
        var bytes = Encoding.UTF8.GetBytes(response_json + "\n");
        await stream.WriteAsync(bytes);
    }

    /// <summary>Connects, reads one request, then disconnects without ever responding.</summary>
    public static async Task Connect_Read_Then_Drop_Async(int port)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);
        var stream = client.GetStream();
        var buffer = new byte[4096];
        await stream.ReadAsync(buffer);
    }
}
