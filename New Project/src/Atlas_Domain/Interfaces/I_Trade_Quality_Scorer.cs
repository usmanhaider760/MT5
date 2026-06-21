using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;

namespace Atlas_Domain.Interfaces;

public interface I_Trade_Quality_Scorer
{
    (int Score, Trade_Quality_Grade Grade) Score_Trade(Trade_Signal_BO signal, Market_Context_BO context);
}
