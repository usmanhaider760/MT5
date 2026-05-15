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

public interface IDecisionEngine
{
    TradingDecisionSnapshot Evaluate(TradingDecisionSnapshot snapshot);
}

public interface ITradingDecisionSnapshotService
{
    Task<TradingDecisionSnapshot> CreateAsync(string symbol, CancellationToken ct = default);
}
