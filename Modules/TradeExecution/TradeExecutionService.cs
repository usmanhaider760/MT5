using MT5TradingBot.Models;
using MT5TradingBot.Modules.BrokerIntegration;
using Serilog;

namespace MT5TradingBot.Modules.TradeExecution
{
    public sealed class TradeExecutionService : ITradeExecutionService
    {
        private readonly MT5Bridge _bridge;

        public TradeExecutionService(MT5Bridge bridge) => _bridge = bridge;

        public async Task<TradeResult> ExecuteAsync(
            TradeRequest request,
            RiskValidationResult riskResult,
            UserApprovalDecision approval,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (valid, validationError) = request.Validate();
            if (!valid)
                return Rejected(request.Id, "VALIDATION", validationError);

            if (!riskResult.IsApproved)
                return Rejected(request.Id, "RISK_BLOCKED", riskResult.Reason);

            if (!approval.IsApproved)
                return Rejected(request.Id, "USER_APPROVAL_REQUIRED", approval.Notes);

            if (riskResult.ValidatedLotSize >= 0.01)
                request.LotSize = riskResult.ValidatedLotSize;

            if (riskResult.ReferenceEntryPrice > 0 && request.OrderType != OrderType.MARKET)
                request.EntryPrice = riskResult.ReferenceEntryPrice;

            Log.Information(
                "[TradeExecution] Executing approved trade {RequestId}: {Pair} {Type} lots={Lots:F2} risk={RiskPercent:F2}% approval={ApprovalMode}",
                request.Id,
                request.Pair,
                request.TradeType,
                request.LotSize,
                riskResult.RiskPercent,
                approval.ApprovalMode);

            try
            {
                return await _bridge.OpenTradeAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TradeExecution] Execution failed for {RequestId}", request.Id);
                return Rejected(request.Id, "EXECUTION_EXCEPTION", ex.Message, TradeStatus.Error);
            }
        }

        private static TradeResult Rejected(
            string requestId,
            string code,
            string message,
            TradeStatus status = TradeStatus.Rejected) =>
            new()
            {
                RequestId = requestId,
                Status = status,
                ErrorCode = code,
                ErrorMessage = message,
                ExecutedAt = DateTime.UtcNow
            };
    }
}
