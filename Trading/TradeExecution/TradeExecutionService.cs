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
            CancellationToken cancellationToken = default,
            double brokerPrice = 0)
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
                double orderCheckPrice = ResolveOrderCheckPrice(request, riskResult, brokerPrice);
                (bool Success, OrderCheckResult? Result, string Error) orderCheck;
                try
                {
                    Log.Information(
                        "[TradeExecution] Broker OrderCheck request {RequestId}: {Pair} {Type} lots={Lots:F2} price={Price:F5} sl={StopLoss:F5} tp={TakeProfit:F5}",
                        request.Id,
                        request.Pair,
                        request.TradeType,
                        request.LotSize,
                        orderCheckPrice,
                        request.StopLoss,
                        request.TakeProfit);
                    orderCheck = await _bridge.TryCheckOrderAsync(request, orderCheckPrice).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[TradeExecution] Broker OrderCheck unavailable for {RequestId}", request.Id);
                    return Rejected(request.Id, "BROKER_ORDERCHECK_UNAVAILABLE",
                        "Broker OrderCheck is unavailable");
                }

                if (!orderCheck.Success || orderCheck.Result == null)
                    return Rejected(
                        request.Id,
                        "BROKER_ORDERCHECK_UNAVAILABLE",
                        string.IsNullOrWhiteSpace(orderCheck.Error)
                            ? "Broker OrderCheck is unavailable"
                            : orderCheck.Error);

                if (!orderCheck.Result.IsAccepted)
                    return Rejected(
                        request.Id,
                        "BROKER_ORDERCHECK_REJECTED",
                        FormatOrderCheckDetails(orderCheck.Result));

                Log.Information(
                    "[TradeExecution] Broker OrderCheck accepted {RequestId}: retcode={Retcode} comment={Comment} margin={Margin:F2} free={FreeMargin:F2} level={MarginLevel:F2}% volume={Volume:F2} price={Price:F5}",
                    request.Id,
                    orderCheck.Result.Retcode,
                    orderCheck.Result.Comment,
                    orderCheck.Result.Margin,
                    orderCheck.Result.MarginFree,
                    orderCheck.Result.MarginLevel,
                    orderCheck.Result.Volume,
                    orderCheck.Result.Price);

                var openResult = await _bridge.OpenTradeAsync(request).ConfigureAwait(false);
                openResult.RequestId = request.Id;

                if (!openResult.IsSuccess)
                {
                    OrderFailureClassifier.ApplyClassification(openResult);
                    Log.Warning(
                        "[TradeExecution] Broker order send failed {RequestId}: code={Code} failure={Failure} retcode={Retcode} message={Message}",
                        request.Id,
                        openResult.ErrorCode,
                        openResult.OrderFailureCode,
                        openResult.BrokerRetcode,
                        openResult.ErrorMessage);
                }

                return openResult;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TradeExecution] Execution failed for {RequestId}", request.Id);
                return OrderFailureClassifier.ApplyClassification(
                    Rejected(request.Id, "EXECUTION_EXCEPTION", ex.Message, TradeStatus.Error));
            }
        }

        private static double ResolveOrderCheckPrice(
            TradeRequest request,
            RiskValidationResult riskResult,
            double brokerPrice)
        {
            if (brokerPrice > 0) return brokerPrice;
            if (request.OrderType != OrderType.MARKET && riskResult.ReferenceEntryPrice > 0)
                return riskResult.ReferenceEntryPrice;
            if (request.EntryPrice > 0) return request.EntryPrice;
            return riskResult.ReferenceEntryPrice;
        }

        private static string FormatOrderCheckDetails(OrderCheckResult result)
        {
            var parts = new List<string>
            {
                $"retcode={result.Retcode}",
                $"comment={result.Comment}",
                $"margin={result.Margin:F2}",
                $"margin_free={result.MarginFree:F2}",
                $"margin_level={result.MarginLevel:F2}",
                $"volume={result.Volume:F2}",
                $"price={result.Price:F5}",
                $"sl={result.StopLoss:F5}",
                $"tp={result.TakeProfit:F5}"
            };

            return "Broker OrderCheck rejected trade: " + string.Join(", ", parts);
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
