using Atlas_Domain.BusinessObjects;

namespace Atlas_Domain.Interfaces;

public interface I_Ai_Signal_Filter
{
    Task<(bool Approved, int Confidence_Pct, string Reason)>
        Evaluate_Async(Trade_Signal_BO signal, Market_Context_BO context);
}
