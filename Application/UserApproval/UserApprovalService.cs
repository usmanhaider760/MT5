using MT5TradingBot.Models;
using Serilog;

namespace MT5TradingBot.Modules.UserApproval
{
    public sealed class UserApprovalService : IUserApprovalService
    {
        private readonly bool _demoAutoApprove;
        private readonly string _operatorName;

        public UserApprovalService(bool demoAutoApprove = false, string operatorName = "System")
        {
            _demoAutoApprove = demoAutoApprove;
            _operatorName = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName;
        }

        public Task<UserApprovalDecision> RequestApprovalAsync(
            MarketSignal signal,
            RiskValidationResult riskResult,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!riskResult.IsApproved)
                return Task.FromResult(Deny(signal.Id, $"Risk validation failed: {riskResult.Reason}"));

            if (signal.Direction == SignalDirection.Hold)
                return Task.FromResult(Deny(signal.Id, "Signal direction is HOLD; no trade approval requested."));

            if (!_demoAutoApprove)
            {
                Log.Information(
                    "[Approval] Manual approval required for {Pair} {Direction}. No automatic approval granted.",
                    signal.Pair,
                    signal.Direction);

                return Task.FromResult(Deny(
                    signal.Id,
                    "Manual user approval required before live trade execution."));
            }

            Log.Information(
                "[Approval] Demo auto-approval granted for {Pair} {Direction}.",
                signal.Pair,
                signal.Direction);

            return Task.FromResult(new UserApprovalDecision
            {
                SignalId = signal.Id,
                IsApproved = true,
                ApprovedBy = _operatorName,
                ApprovalMode = "DemoAutoApprove",
                Notes = "Approved only because demo auto-approval mode is enabled.",
                DecidedAt = DateTime.UtcNow
            });
        }

        private static UserApprovalDecision Deny(string signalId, string reason) =>
            new()
            {
                SignalId = signalId,
                IsApproved = false,
                ApprovedBy = "System",
                ApprovalMode = "ManualRequired",
                Notes = reason,
                DecidedAt = DateTime.UtcNow
            };
    }
}
