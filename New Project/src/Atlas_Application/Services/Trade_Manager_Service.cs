using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Atlas_Market_Data.Services;

namespace Atlas_Application.Services;

/// <summary>
/// Manages open positions each cycle: moves SL to breakeven at 1.0R profit,
/// then trails behind the 5-bar M15 swing low/high at ≥1.5R.
/// Only ever moves SL in the direction that locks in more profit (never widens).
/// </summary>
public class Trade_Manager_Service
{
    private readonly I_Market_Data_Service _market_data;
    private readonly I_Execution_Service   _execution;

    public event Action<string>? On_Log;

    public Trade_Manager_Service(I_Market_Data_Service market_data, I_Execution_Service execution)
    {
        _market_data = market_data;
        _execution   = execution;
    }

    public async Task Manage_Open_Positions_Async(List<Position_BO> positions)
    {
        foreach (var pos in positions)
        {
            try { await Manage_Position_Async(pos); }
            catch { /* SL management is best-effort; never abort the cycle on a single position */ }
        }
    }

    private async Task Manage_Position_Async(Position_BO pos)
    {
        if (pos.Entry_Price == 0 || pos.Stop_Loss_Price == 0) return;

        decimal pip_size     = Get_Pip_Size(pos.Symbol_Name);
        // Prefer the recorded initial distance (stays correct even after SL moves to breakeven)
        decimal initial_pips = pos.Initial_Stop_Distance_Pips > 0
            ? pos.Initial_Stop_Distance_Pips
            : Math.Abs(pos.Entry_Price - pos.Stop_Loss_Price) / pip_size;
        if (initial_pips < 1) return;

        // Use last 10 M15 candles for current price and swing calculation
        var m15 = await _market_data.Get_Candles_Async(pos.Symbol_Name, "M15", 10);
        if (m15.Count == 0) return;

        decimal current_price = m15.Last().Close;
        bool    is_buy        = pos.Direction == Trade_Direction_Type.Buy;
        decimal pnl_pips      = is_buy
            ? (current_price - pos.Entry_Price) / pip_size
            : (pos.Entry_Price - current_price) / pip_size;
        decimal r_multiple    = pnl_pips / initial_pips;

        // Rule 1: at 1.0R — scale out 50% then move SL to breakeven (only once)
        if (r_multiple >= 1.0m && !pos.Is_At_Breakeven)
        {
            if (!pos.Partial_Profit_Taken && pos.Lot_Size > 0)
            {
                decimal half_lots = Math.Round(pos.Lot_Size * 0.5m, 2);
                if (half_lots > 0)
                {
                    bool partial = await _execution.Partial_Close_Async(pos.Broker_Ticket, half_lots);
                    if (partial)
                    {
                        pos.Partial_Profit_Taken = true;
                        On_Log?.Invoke($"[SCALE] {pos.Symbol_Name} #{pos.Broker_Ticket} → closed {half_lots} lots at 1.0R");
                    }
                }
            }

            bool moved = await _execution.Modify_Stop_Loss_Async(pos.Broker_Ticket, pos.Entry_Price);
            if (moved)
            {
                pos.Is_At_Breakeven = true;
                pos.Stop_Loss_Price = pos.Entry_Price;
                On_Log?.Invoke($"[TRAIL] {pos.Symbol_Name} #{pos.Broker_Ticket} → BE at {pos.Entry_Price:F5}");
            }
            return; // wait for next cycle before starting trail
        }

        // Rule 2: trail 5-bar M15 swing low/high at ≥1.5R
        if (r_multiple < 1.5m || !pos.Is_At_Breakeven) return;

        var last5 = m15.TakeLast(5).ToList();
        if (last5.Count < 5) return;

        decimal new_sl    = is_buy ? last5.Min(c => c.Low) : last5.Max(c => c.High);
        bool    improving = is_buy ? new_sl > pos.Stop_Loss_Price : new_sl < pos.Stop_Loss_Price;
        if (!improving) return;

        bool trailed = await _execution.Modify_Stop_Loss_Async(pos.Broker_Ticket, new_sl);
        if (trailed)
        {
            pos.Stop_Loss_Price = new_sl;
            On_Log?.Invoke($"[TRAIL] {pos.Symbol_Name} #{pos.Broker_Ticket} → SL trailed to {new_sl:F5} (R={r_multiple:F2})");
        }
    }

    private static decimal Get_Pip_Size(string symbol)
    {
        if (symbol.Contains("JPY"))                              return 0.01m;
        if (symbol.Contains("XAU") || symbol.Contains("GOLD")) return 0.10m;
        return 0.0001m;
    }
}
