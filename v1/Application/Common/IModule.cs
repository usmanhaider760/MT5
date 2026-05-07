namespace MT5TradingBot.Core
{
    public interface IModule
    {
        string Name        { get; }
        string Icon        { get; }
        string Description { get; }
        Task<ModuleStatus> CheckAsync(CancellationToken ct = default);
    }

    public sealed record ModuleStatus(bool IsOk, string Message);
}
