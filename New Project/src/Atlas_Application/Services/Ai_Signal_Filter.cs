using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Interfaces;

namespace Atlas_Application.Services;

/// <summary>
/// Stub AI signal filter required by the ATLAS specification. Passes every signal through
/// unchanged until a real model is wired in — subclass and override Evaluate_Async for that.
/// </summary>
public class Ai_Signal_Filter : I_Ai_Signal_Filter
{
    public virtual Task<(bool Approved, int Confidence_Pct, string Reason)> Evaluate_Async(
        Trade_Signal_BO signal, Market_Context_BO context) =>
        Task.FromResult((true, 100, "Pass-through — no AI model configured"));
}
