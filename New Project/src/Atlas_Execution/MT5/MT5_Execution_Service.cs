using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Atlas_Execution.Protocol;

namespace Atlas_Execution.MT5;

public class MT5_Execution_Service : I_Execution_Service
{
    private readonly MT5_Bridge_Client _bridge;
    private readonly bool _demo_mode;

    public MT5_Execution_Service(MT5_Bridge_Client bridge, bool demo_mode = true)
    {
        _bridge    = bridge;
        _demo_mode = demo_mode;
    }

    public async Task<(bool Success, long Ticket, string Broker_Message)> Send_Order_Async(Trade_Signal_BO signal, decimal lot_size)
    {
        if (_demo_mode)
        {
            // In demo mode: simulate order acceptance without sending to broker
            var fake_ticket = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return (true, fake_ticket, $"[DEMO] Simulated {signal.Direction} {signal.Symbol_Name} @ {signal.Entry_Price:F5}");
        }

        var response = await _bridge.Send_Async<MT5_Order_Response>(new MT5_Request
        {
            Command    = MT5_Command.SEND_ORDER,
            Symbol     = signal.Symbol_Name,
            Direction  = signal.Direction == Trade_Direction_Type.Buy ? "buy" : "sell",
            Lot        = (double)lot_size,
            Price      = (double)signal.Entry_Price,
            StopLoss   = (double)signal.Stop_Loss_Price,
            TakeProfit = (double)signal.Take_Profit_Price,
            Comment    = $"ATLAS_{signal.Strategy}"
        });

        if (response?.Is_Ok != true)
            return (false, 0, response?.Error ?? "No response from MT5 bridge");

        return (true, response.Ticket, response.Broker_Message);
    }

    public async Task<bool> Modify_Stop_Loss_Async(long ticket, decimal new_stop_loss)
    {
        if (_demo_mode) return true;

        var response = await _bridge.Send_Async<MT5_Order_Response>(new MT5_Request
        {
            Command  = MT5_Command.MODIFY_SL,
            Ticket   = ticket,
            StopLoss = (double)new_stop_loss
        });

        return response?.Is_Ok == true;
    }

    public async Task<bool> Close_Position_Async(long ticket, string reason)
    {
        if (_demo_mode) return true;

        var response = await _bridge.Send_Async<MT5_Order_Response>(new MT5_Request
        {
            Command = MT5_Command.CLOSE_POSITION,
            Ticket  = ticket,
            Comment = reason
        });

        return response?.Is_Ok == true;
    }

    public async Task<List<Position_BO>> Get_Open_Positions_Async()
    {
        if (_demo_mode) return [];

        var response = await _bridge.Send_Async<MT5_Positions_Response>(new MT5_Request
        {
            Command = MT5_Command.GET_POSITIONS
        });

        if (response?.Is_Ok != true) return [];

        return response.Positions.Select(p => new Position_BO
        {
            Broker_Ticket  = p.Ticket,
            Symbol_Name    = p.Symbol,
            Direction      = p.Type.Equals("buy", StringComparison.OrdinalIgnoreCase)
                             ? Trade_Direction_Type.Buy : Trade_Direction_Type.Sell,
            Lot_Size       = (decimal)p.Lots,
            Entry_Price    = (decimal)p.Open_Price,
            Stop_Loss_Price= (decimal)p.Sl,
            Take_Profit_Price = (decimal)p.Tp,
            Unrealized_PnL = (decimal)p.Profit,
            Opened_At_UTC  = DateTimeOffset.FromUnixTimeSeconds(p.Open_Time_Unix).UtcDateTime,
            Status         = Order_Status_Type.Filled
        }).ToList();
    }
}
