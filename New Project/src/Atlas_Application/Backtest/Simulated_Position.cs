using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;

namespace Atlas_Application.Backtest;

/// <summary>
/// An open trade held in memory during a backtest run.
/// Checks SL/TP hit on each bar and returns a Trade_Result_BO when closed.
/// </summary>
public class Simulated_Position
{
    private readonly Trade_Signal_BO _signal;
    private readonly decimal         _lot;
    private readonly Backtest_Config _config;
    private readonly decimal         _entry_actual; // includes slippage
    private readonly decimal         _stop_distance_pips;
    private readonly decimal         _pip_value;

    public DateTime Opened_At  { get; }
    public bool     Is_Open    { get; private set; } = true;

    public Simulated_Position(Trade_Signal_BO signal, decimal lot, Candle_BO entry_bar, Backtest_Config config, decimal pip_value_per_lot = 10m)
    {
        _signal  = signal;
        _lot     = lot;
        _config  = config;
        Opened_At = entry_bar.Open_Time_UTC;

        // Apply slippage to entry
        _entry_actual = signal.Direction == Trade_Direction_Type.Buy
            ? signal.Entry_Price + (config.Slippage_Pips / 10000m)
            : signal.Entry_Price - (config.Slippage_Pips / 10000m);

        _stop_distance_pips = signal.Stop_Loss_Pips;

        // Pip value per lot from the symbol config — $10 for standard 5-decimal pairs, $100 for Gold, ~$9 for JPY pairs
        _pip_value = pip_value_per_lot;
    }

    /// <summary>Returns a closed Trade_Result_BO if SL or TP is hit on this bar, else null.</summary>
    public Trade_Result_BO? Check_And_Close(Candle_BO bar, Backtest_Config config)
    {
        if (!Is_Open) return null;

        bool is_buy = _signal.Direction == Trade_Direction_Type.Buy;

        // Check SL hit (worst case — SL at bar low/high)
        bool sl_hit = is_buy  ? bar.Low  <= _signal.Stop_Loss_Price
                               : bar.High >= _signal.Stop_Loss_Price;

        // Check TP hit
        bool tp_hit = is_buy  ? bar.High >= _signal.Take_Profit_Price
                               : bar.Low  <= _signal.Take_Profit_Price;

        // If both hit on same bar, assume SL (conservative)
        if (sl_hit || tp_hit)
        {
            decimal exit_price = sl_hit ? _signal.Stop_Loss_Price : _signal.Take_Profit_Price;
            string reason      = sl_hit ? "Stop loss hit" : "Take profit hit";
            var exit_reason    = sl_hit ? Exit_Reason_Type.Stop_Loss_Hit : Exit_Reason_Type.Take_Profit_Hit;
            return Close(exit_price, bar.Open_Time_UTC, reason, exit_reason, config);
        }

        return null;
    }

    public Trade_Result_BO Force_Close(decimal price, string reason, Backtest_Config config) =>
        Close(price, DateTime.UtcNow, reason, Exit_Reason_Type.Manual_Close, config);

    private Trade_Result_BO Close(decimal exit_price, DateTime closed_at, string reason, Exit_Reason_Type exit_reason, Backtest_Config config)
    {
        Is_Open = false;

        bool is_buy  = _signal.Direction == Trade_Direction_Type.Buy;
        decimal pnl_pips = is_buy ? exit_price - _entry_actual : _entry_actual - exit_price;
        decimal gross_pnl = pnl_pips * _lot * _pip_value * 10_000m;

        decimal commission = config.Commission_Per_Lot * _lot;
        decimal net_pnl    = gross_pnl - commission;
        decimal r_multiple = _stop_distance_pips > 0 ? pnl_pips / (_stop_distance_pips / 10_000m) / 1m : 0;

        return new Trade_Result_BO
        {
            Signal_Id              = _signal.Signal_Id,
            Broker_Ticket          = 0,
            Symbol_Name            = _signal.Symbol_Name,
            Strategy               = _signal.Strategy,
            Regime_At_Entry        = _signal.Regime_At_Signal,
            Session_At_Entry       = _signal.Session_At_Signal,
            Direction              = _signal.Direction,
            Lot_Size               = _lot,
            Entry_Price            = _entry_actual,
            Exit_Price             = exit_price,
            Stop_Loss_Price        = _signal.Stop_Loss_Price,
            Take_Profit_Price      = _signal.Take_Profit_Price,
            Opened_At_UTC          = Opened_At,
            Closed_At_UTC          = closed_at,
            Gross_PnL_Currency     = gross_pnl,
            Commission             = commission,
            Swap                   = 0,
            Slippage_Pips          = config.Slippage_Pips,
            Initial_Stop_Distance_Pips = _stop_distance_pips,
            Pip_Value_Per_Lot      = _pip_value,
            Exit_Reason            = exit_reason,
            Close_Reason           = reason
        };
    }

    public Position_BO As_Position_BO() => new()
    {
        Signal_Id     = _signal.Signal_Id,
        Symbol_Name   = _signal.Symbol_Name,
        Direction     = _signal.Direction,
        Lot_Size      = _lot,
        Entry_Price   = _entry_actual,
        Stop_Loss_Price    = _signal.Stop_Loss_Price,
        Take_Profit_Price  = _signal.Take_Profit_Price,
        Opened_At_UTC      = Opened_At,
        Status        = Order_Status_Type.Filled
    };
}
