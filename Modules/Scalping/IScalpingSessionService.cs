using MT5TradingBot.Models;
using Newtonsoft.Json.Linq;

namespace MT5TradingBot.Modules.Scalping
{
    public interface IScalpingSessionService
    {
        bool IsRunning { get; }
        event Action<string>? OnLog;
        event Action<string>? OnStatusChanged;
        Task StartAsync(ScalpingSessionRequest request, CancellationToken cancellationToken = default);
        Task StopAsync();
    }

    public sealed record ScalpingSessionRequest(
        string Pair,
        TradeType SignalDirection,
        double LotSize,
        int MagicNumber,
        ScalpingConfig Config,
        Func<TradeRequest, Task<TradeResult>> ExecuteAsync,
        Func<DateTime, Task<double>>? GetSessionProfitAsync = null,
        Func<JObject, TradeType, Task<ScalpingAiConfirmation>>? ConfirmWithAiAsync = null);

    public sealed record ScalpingAiConfirmation(bool Approved, string Reason);

    internal sealed record ScalpingDecision(
        bool Approved,
        int Score,
        TradeType Direction,
        string Reason,
        string Detail = "",
        double? SuggestedSlPips = null,
        double? SuggestedTpPips = null);
}
