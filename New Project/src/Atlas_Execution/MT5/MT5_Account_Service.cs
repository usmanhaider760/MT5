using Atlas_Domain.BusinessObjects;
using Atlas_Execution.Protocol;

namespace Atlas_Execution.MT5;

public class MT5_Account_Service
{
    private readonly MT5_Bridge_Client _bridge;
    private decimal _day_open_balance;
    private decimal _week_open_balance;
    private decimal _peak_equity;
    private int _consecutive_losses;

    public MT5_Account_Service(MT5_Bridge_Client bridge)
    {
        _bridge = bridge;
    }

    public async Task<Account_State_BO> Get_Account_State_Async(int open_trade_count)
    {
        var response = await _bridge.Send_Async<MT5_Account_Response>(new MT5_Request
        {
            Command = MT5_Command.GET_ACCOUNT_INFO
        });

        if (response?.Is_Ok != true)
            return new Account_State_BO();

        var balance = (decimal)response.Balance;
        var equity  = (decimal)response.Equity;

        // Initialize reference values on first call
        if (_day_open_balance  == 0) _day_open_balance  = balance;
        if (_week_open_balance == 0) _week_open_balance = balance;
        if (_peak_equity       == 0) _peak_equity       = equity;

        if (equity > _peak_equity) _peak_equity = equity;

        var account = new Account_State_BO
        {
            Snapshot_UTC         = DateTime.UtcNow,
            Balance              = balance,
            Equity               = equity,
            Margin_Used          = (decimal)response.Margin,
            Free_Margin          = (decimal)response.Free_Margin,
            Margin_Level_Percent = (decimal)response.Margin_Level,
            Day_Open_Balance     = _day_open_balance,
            Week_Open_Balance    = _week_open_balance,
            Peak_Equity          = _peak_equity,
            Open_Trade_Count     = open_trade_count,
            Consecutive_Losses   = _consecutive_losses
        };

        return account;
    }

    public void Reset_Day_Reference(decimal balance) => _day_open_balance  = balance;
    public void Reset_Week_Reference(decimal balance) => _week_open_balance = balance;

    public void Record_Trade_Closed(bool is_winner)
    {
        if (is_winner) _consecutive_losses = 0;
        else           _consecutive_losses++;
    }
}
