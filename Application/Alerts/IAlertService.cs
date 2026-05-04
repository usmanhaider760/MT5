using MT5TradingBot.Modules.Monitoring;

namespace MT5TradingBot.Modules.Alerts
{
    public interface ISafetyAlertSink
    {
        Task<IReadOnlyList<SafetyAlert>> LoadAsync(CancellationToken cancellationToken = default);
        Task SaveAsync(IReadOnlyList<SafetyAlert> alerts, CancellationToken cancellationToken = default);
    }

    public interface IAlertService
    {
        Task<SafetyAlert?> RaiseAsync(SafetyAlertRequest request, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SafetyAlert>> RaiseRuntimeHealthAlertsAsync(
            RuntimeHealthSnapshot snapshot,
            CancellationToken cancellationToken = default);
        Task<SafetyAlert?> AlertLiveReadinessBlockedAsync(
            string message,
            IEnumerable<string>? failedCriteria = null,
            CancellationToken cancellationToken = default);
        Task<SafetyAlert?> AlertKillSwitchTriggeredAsync(
            string message,
            CancellationToken cancellationToken = default);
        Task<SafetyAlert?> AlertRepeatedOrderRejectionAsync(
            string relatedCode,
            string message,
            int recentRejectionCount,
            CancellationToken cancellationToken = default);
        Task<SafetyAlert?> AlertEmergencyCloseAsync(
            bool failed,
            string message,
            CancellationToken cancellationToken = default);
        Task<bool> AcknowledgeAsync(string alertId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SafetyAlert>> GetAlertsAsync(CancellationToken cancellationToken = default);
    }
}
