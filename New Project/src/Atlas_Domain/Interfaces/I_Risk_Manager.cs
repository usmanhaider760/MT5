using Atlas_Domain.BusinessObjects;

namespace Atlas_Domain.Interfaces;

public interface I_Risk_Manager
{
    Task<(bool Approved, string Reason)> Validate_Trade_Risk_Async(Trade_Signal_BO signal, Account_State_BO account, List<Position_BO> open_positions);
    decimal Calculate_Lot_Size(decimal account_equity, decimal risk_percent, decimal stop_loss_pips, decimal pip_value_per_lot);
    Task<Account_State_BO> Get_Account_State_Async();
}
