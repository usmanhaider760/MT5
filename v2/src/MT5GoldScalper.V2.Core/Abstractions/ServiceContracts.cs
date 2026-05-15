using MT5GoldScalper.V2.Core.Models;

namespace MT5GoldScalper.V2.Core.Abstractions;

public interface IMarketDataService
{
    Task<MarketDataModel> GetMarketDataAsync(string symbol, CancellationToken ct = default);
}

public interface IAccountDataService
{
    Task<AccountRiskModel> GetAccountRiskAsync(string symbol, CancellationToken ct = default);
}

public interface INewsService
{
    Task<SessionNewsModel> GetSessionNewsAsync(string symbol, CancellationToken ct = default);
}

public interface IIndicatorService
{
    Task<StrategySignalModel> GetStrategySignalAsync(string symbol, CancellationToken ct = default);
}

public interface IOrderSafetyService
{
    Task<ExecutionSafetyModel> EvaluateAsync(TradingDecisionSnapshot snapshot, CancellationToken ct = default);
}

public interface IPipCalculationService
{
    decimal GetPipSize(MarketDataModel market);
    decimal PriceDistanceToPips(MarketDataModel market, decimal priceDistance);
    int PriceDistanceToPoints(MarketDataModel market, decimal priceDistance);
}

public interface ISpreadCalculationService
{
    void Calculate(TradingDecisionSnapshot snapshot);
}

public interface IRiskCalculationService
{
    void Calculate(TradingDecisionSnapshot snapshot);
}

public interface ILotSizeCalculationService
{
    void Calculate(TradingDecisionSnapshot snapshot);
    bool IsVolumeValid(AccountRiskModel account, MarketDataModel market);
}

public interface ITargetCalculationService
{
    void Calculate(TradingDecisionSnapshot snapshot);
    bool AreTargetsValid(TradingDecisionSnapshot snapshot);
}

public interface IStopValidationService
{
    void Calculate(TradingDecisionSnapshot snapshot);
    bool AreStopsValid(TradingDecisionSnapshot snapshot);
}

public interface IRewardRiskCalculationService
{
    void Calculate(TradingDecisionSnapshot snapshot);
}

public interface IDecisionEngine
{
    TradingDecisionSnapshot Evaluate(TradingDecisionSnapshot snapshot);
}

public interface ITradingDecisionSnapshotService
{
    Task<TradingDecisionSnapshot> CreateAsync(string symbol, CancellationToken ct = default);
}
