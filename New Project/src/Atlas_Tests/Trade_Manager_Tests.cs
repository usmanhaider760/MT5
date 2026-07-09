using Atlas_Application.Services;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Tests for Trade_Manager_Service breakeven and trailing-stop logic.
/// Uses in-memory stubs — no MT5 bridge required.
/// </summary>
public class Trade_Manager_Tests
{
    // ── Breakeven at 1.0R ────────────────────────────────────────────

    [Fact]
    public async Task Breakeven_Triggered_When_R_Reaches_One()
    {
        // EURUSD Buy: entry=1.10000, SL=1.09900 (10-pip SL, pip_size=0.0001)
        // At 1R → price = 1.10100 (10 pips profit)
        // Close = 1.10100 → r_multiple = 1.0 exactly
        decimal current = 1.10100m;
        var execution   = new Execution_Spy();
        var market_data = new Market_Data_Stub(current);
        var manager     = new Trade_Manager_Service(market_data, execution);

        var pos = new Position_BO
        {
            Broker_Ticket              = 1001,
            Symbol_Name                = "EURUSD",
            Direction                  = Trade_Direction_Type.Buy,
            Entry_Price                = 1.10000m,
            Stop_Loss_Price            = 1.09900m,
            Lot_Size                   = 0.1m,
            Is_At_Breakeven            = false,
            Partial_Profit_Taken       = false
        };

        await manager.Manage_Open_Positions_Async([pos]);

        Assert.True(pos.Is_At_Breakeven, "Position should be at breakeven after 1R");
        Assert.Equal(pos.Entry_Price, pos.Stop_Loss_Price);
        Assert.True(execution.Sl_Modified, "SL should have been sent to broker");
        Assert.Equal(1.10000m, execution.Last_New_Sl);
    }

    [Fact]
    public async Task Breakeven_Not_Triggered_Below_One_R()
    {
        // Only 0.5R profit — should not trigger breakeven yet
        decimal current = 1.10050m;
        var execution   = new Execution_Spy();
        var market_data = new Market_Data_Stub(current);
        var manager     = new Trade_Manager_Service(market_data, execution);

        var pos = new Position_BO
        {
            Broker_Ticket   = 1002,
            Symbol_Name     = "EURUSD",
            Direction       = Trade_Direction_Type.Buy,
            Entry_Price     = 1.10000m,
            Stop_Loss_Price = 1.09900m,
            Lot_Size        = 0.1m,
            Is_At_Breakeven = false
        };

        await manager.Manage_Open_Positions_Async([pos]);

        Assert.False(pos.Is_At_Breakeven);
        Assert.False(execution.Sl_Modified);
    }

    [Fact]
    public async Task Partial_Close_Sent_At_One_R_If_Not_Already_Taken()
    {
        decimal current = 1.10100m;
        var execution   = new Execution_Spy();
        var market_data = new Market_Data_Stub(current);
        var manager     = new Trade_Manager_Service(market_data, execution);

        var pos = new Position_BO
        {
            Broker_Ticket        = 1003,
            Symbol_Name          = "EURUSD",
            Direction            = Trade_Direction_Type.Buy,
            Entry_Price          = 1.10000m,
            Stop_Loss_Price      = 1.09900m,
            Lot_Size             = 0.2m,
            Is_At_Breakeven      = false,
            Partial_Profit_Taken = false
        };

        await manager.Manage_Open_Positions_Async([pos]);

        Assert.True(pos.Partial_Profit_Taken);
        Assert.Equal(0.1m, execution.Last_Partial_Lots); // 50% of 0.2
    }

    [Fact]
    public async Task Partial_Close_Not_Repeated_When_Already_Taken()
    {
        decimal current = 1.10100m;
        var execution   = new Execution_Spy();
        var market_data = new Market_Data_Stub(current);
        var manager     = new Trade_Manager_Service(market_data, execution);

        var pos = new Position_BO
        {
            Broker_Ticket        = 1004,
            Symbol_Name          = "EURUSD",
            Direction            = Trade_Direction_Type.Buy,
            Entry_Price          = 1.10000m,
            Stop_Loss_Price      = 1.09900m,
            Lot_Size             = 0.2m,
            Is_At_Breakeven      = false,
            Partial_Profit_Taken = true  // already taken
        };

        await manager.Manage_Open_Positions_Async([pos]);

        Assert.Equal(0m, execution.Last_Partial_Lots);
    }

    // ── Trailing SL at ≥1.5R ─────────────────────────────────────────

    [Fact]
    public async Task Trail_Triggered_At_One_Point_Five_R_When_At_Breakeven()
    {
        // 15-pip profit = 1.5R, at breakeven → trail SL to 5-bar swing low
        // Initial SL was 10 pips → stored in Initial_Stop_Distance_Pips
        decimal current     = 1.10150m;
        decimal swing_low   = 1.10020m; // the 5-bar min Low
        var execution       = new Execution_Spy();
        var market_data     = new Market_Data_Stub(current, swing_low: swing_low);
        var manager         = new Trade_Manager_Service(market_data, execution);

        var pos = new Position_BO
        {
            Broker_Ticket              = 1005,
            Symbol_Name                = "EURUSD",
            Direction                  = Trade_Direction_Type.Buy,
            Entry_Price                = 1.10000m,
            Stop_Loss_Price            = 1.10000m, // already at BE
            Initial_Stop_Distance_Pips = 10m,      // original SL was 10 pips
            Lot_Size                   = 0.1m,
            Is_At_Breakeven            = true
        };

        await manager.Manage_Open_Positions_Async([pos]);

        Assert.True(execution.Sl_Modified);
        Assert.Equal(swing_low, execution.Last_New_Sl);
        Assert.Equal(swing_low, pos.Stop_Loss_Price);
    }

    [Fact]
    public async Task Trail_Not_Applied_When_New_Sl_Would_Widen()
    {
        // swing_low is below the current SL → widening → should NOT update
        decimal current     = 1.10150m;
        decimal swing_low   = 1.09950m; // below current SL = would widen
        var execution       = new Execution_Spy();
        var market_data     = new Market_Data_Stub(current, swing_low: swing_low);
        var manager         = new Trade_Manager_Service(market_data, execution);

        var pos = new Position_BO
        {
            Broker_Ticket              = 1006,
            Symbol_Name                = "EURUSD",
            Direction                  = Trade_Direction_Type.Buy,
            Entry_Price                = 1.10000m,
            Stop_Loss_Price            = 1.10000m,
            Initial_Stop_Distance_Pips = 10m,
            Lot_Size                   = 0.1m,
            Is_At_Breakeven            = true
        };

        await manager.Manage_Open_Positions_Async([pos]);

        Assert.False(execution.Sl_Modified);
    }

    // ── Gold pip size ─────────────────────────────────────────────────

    [Fact]
    public async Task Gold_Pip_Size_Treated_As_Point_One()
    {
        // XAUUSD: pip_size = 0.10 → 10-pip SL needs $1 move
        // Entry=2000, SL=1999 (10 pips at 0.10) → 1R at 2001
        decimal current = 2001m;
        var execution   = new Execution_Spy();
        var market_data = new Market_Data_Stub(current);
        var manager     = new Trade_Manager_Service(market_data, execution);

        var pos = new Position_BO
        {
            Broker_Ticket   = 1007,
            Symbol_Name     = "XAUUSD",
            Direction       = Trade_Direction_Type.Buy,
            Entry_Price     = 2000m,
            Stop_Loss_Price = 1999m,
            Lot_Size        = 0.01m,
            Is_At_Breakeven = false
        };

        await manager.Manage_Open_Positions_Async([pos]);

        Assert.True(pos.Is_At_Breakeven, "Gold position should reach BE at 1.0R");
        Assert.Equal(2000m, pos.Stop_Loss_Price);
    }
}

// ── Test doubles ──────────────────────────────────────────────────────

file sealed class Market_Data_Stub : I_Market_Data_Service
{
    private readonly decimal _close;
    private readonly decimal _swing_low;

    public Market_Data_Stub(decimal close, decimal swing_low = 0m)
    {
        _close     = close;
        _swing_low = swing_low > 0 ? swing_low : close - 0.0001m;
    }

    public Task<List<Candle_BO>> Get_Candles_Async(string sym, string tf, int count)
    {
        // Return 10 candles whose Close = _close and Low = _swing_low (for trailing test)
        var candles = Enumerable.Range(0, count).Select(i => new Candle_BO
        {
            Symbol_Name  = sym,
            Open_Time_UTC = DateTime.UtcNow.AddMinutes(-count + i),
            Open  = _close,
            High  = _close + 0.0005m,
            Low   = _swing_low,
            Close = _close
        }).ToList();
        return Task.FromResult(candles);
    }

    public Task<decimal> Get_Current_Spread_Pips_Async(string sym) => Task.FromResult(0.8m);
    public Task<decimal> Get_Current_Price_Async(string sym, bool is_bid = true) => Task.FromResult(_close);
    public Task<bool>    Is_Market_Open_Async(string sym) => Task.FromResult(true);
}

file sealed class Execution_Spy : I_Execution_Service
{
    public bool    Sl_Modified       { get; private set; }
    public decimal Last_New_Sl       { get; private set; }
    public decimal Last_Partial_Lots { get; private set; }

    public Task<bool> Modify_Stop_Loss_Async(long ticket, decimal new_sl)
    {
        Sl_Modified = true;
        Last_New_Sl = new_sl;
        return Task.FromResult(true);
    }

    public Task<bool> Modify_Take_Profit_Async(long ticket, decimal new_take_profit) =>
        Task.FromResult(true);

    public Task<bool> Partial_Close_Async(long ticket, decimal lots)
    {
        Last_Partial_Lots = lots;
        return Task.FromResult(true);
    }

    public Task<(bool, long, string)> Send_Order_Async(Trade_Signal_BO s, decimal lot) =>
        Task.FromResult((true, 0L, ""));
    public Task<bool> Close_Position_Async(long ticket, string reason) =>
        Task.FromResult(true);
    public Task<List<Position_BO>> Get_Open_Positions_Async() =>
        Task.FromResult(new List<Position_BO>());
}
