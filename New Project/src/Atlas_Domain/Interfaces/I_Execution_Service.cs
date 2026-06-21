using Atlas_Domain.BusinessObjects;

namespace Atlas_Domain.Interfaces;

public interface I_Execution_Service
{
    Task<(bool Success, long Ticket, string Broker_Message)> Send_Order_Async(Trade_Signal_BO signal, decimal lot_size);
    Task<bool> Modify_Stop_Loss_Async(long ticket, decimal new_stop_loss);
    Task<bool> Close_Position_Async(long ticket, string reason);
    Task<List<Position_BO>> Get_Open_Positions_Async();
}
