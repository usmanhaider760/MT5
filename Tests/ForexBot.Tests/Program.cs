using System.Reflection;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MT5TradingBot.Core;
using MT5TradingBot.Data;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.Backtesting;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Modules.NewsFilter;
using MT5TradingBot.Modules.RiskManagement;
using MT5TradingBot.Modules.Scalping;
using MT5TradingBot.Modules.TradeExecution;
using MT5TradingBot.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForexBot.Tests;

internal static class Program
{
    private static readonly TestCase[] Tests =
    [
        new("lot sizing uses equity risk and stop distance", LotSizingUsesRiskFormula),
        new("risk manager returns validated auto lot size", RiskManagerReturnsValidatedLotSize),
        new("daily trade count stop blocks when limit reached", DailyTradeLimitBlocksAtConfiguredLimit),
        new("daily loss below limit allows live trade to continue", DailyLossBelowLimitAllowsLiveTrade),
        new("daily realized loss at limit blocks live trade", DailyRealizedLossAtLimitBlocksLiveTrade),
        new("daily realized loss above limit blocks live trade", DailyRealizedLossAboveLimitBlocksLiveTrade),
        new("floating loss contributes to daily loss hard stop", FloatingLossContributesToDailyLossHardStop),
        new("missing daily trade history blocks live trade", MissingDailyTradeHistoryBlocksLiveTrade),
        new("missing floating P/L data blocks live trade", MissingFloatingPnlBlocksLiveTrade),
        new("paper mode is separate from daily loss hard stop", PaperModeIsSeparateFromDailyLossHardStop),
        new("weekly loss below limit allows live trade to continue", WeeklyLossBelowLimitAllowsLiveTrade),
        new("weekly realized loss at limit blocks live trade", WeeklyRealizedLossAtLimitBlocksLiveTrade),
        new("weekly realized loss above limit blocks live trade", WeeklyRealizedLossAboveLimitBlocksLiveTrade),
        new("floating loss contributes to weekly loss hard stop", FloatingLossContributesToWeeklyLossHardStop),
        new("trades outside current UTC week are excluded", TradesOutsideCurrentUtcWeekAreExcluded),
        new("missing weekly trade history blocks live trade", MissingWeeklyTradeHistoryBlocksLiveTrade),
        new("missing weekly floating P/L data blocks live trade", MissingWeeklyFloatingPnlBlocksLiveTrade),
        new("paper mode is separate from weekly loss hard stop", PaperModeIsSeparateFromWeeklyLossHardStop),
        new("same-symbol lots below cap allows live trade", SameSymbolLotsBelowCapAllowsLiveTrade),
        new("same-symbol lots at cap blocks live trade", SameSymbolLotsAtCapBlocksLiveTrade),
        new("same-symbol position count at cap blocks live trade", SameSymbolPositionCountAtCapBlocksLiveTrade),
        new("same-symbol risk at cap blocks live trade", SameSymbolRiskAtCapBlocksLiveTrade),
        new("opposite same-symbol exposure counts gross lots", OppositeSameSymbolExposureCountsGrossLots),
        new("different symbols do not count toward symbol cap", DifferentSymbolsDoNotCountTowardSymbolCap),
        new("missing symbol exposure data blocks live trade", MissingSymbolExposureDataBlocksLiveTrade),
        new("paper mode includes paper positions in exposure checks", PaperModeIncludesPaperPositionsInExposureChecks),
        new("healthy projected margin level allows live trade", HealthyProjectedMarginLevelAllowsLiveTrade),
        new("projected margin level below minimum blocks live trade", ProjectedMarginLevelBelowMinimumBlocksLiveTrade),
        new("existing margin level below minimum blocks live trade", ExistingMarginLevelBelowMinimumBlocksLiveTrade),
        new("missing margin estimate blocks live trade", MissingMarginEstimateBlocksLiveTrade),
        new("missing account margin data blocks live trade", MissingAccountMarginDataBlocksLiveTrade),
        new("paper mode is separate from projected margin validation", PaperModeIsSeparateFromProjectedMarginValidation),
        new("emergency drawdown persists kill switch", EmergencyDrawdownPersistsKillSwitch),
        new("restart loads active kill switch", RestartLoadsActiveKillSwitch),
        new("active kill switch blocks central live execution gate", ActiveKillSwitchBlocksCentralLiveExecutionGate),
        new("explicit clear disables kill switch", ExplicitClearDisablesKillSwitch),
        new("failed emergency close keeps kill switch active", FailedEmergencyCloseKeepsKillSwitchActive),
        new("paper mode is separate from kill switch", PaperModeIsSeparateFromKillSwitch),
        new("commission is calculated correctly for lot size", CommissionIsCalculatedCorrectlyForLotSize),
        new("commission is deducted from paper trade P/L", CommissionIsDeductedFromPaperTradePnl),
        new("commission is deducted from backtest P/L", CommissionIsDeductedFromBacktestPnl),
        new("invalid commission config blocks live trade", InvalidCommissionConfigBlocksLiveTrade),
        new("disabled commission model preserves live behavior", DisabledCommissionModelPreservesLiveBehavior),
        new("slippage cost is calculated correctly", SlippageCostIsCalculatedCorrectly),
        new("invalid slippage config blocks live trade", InvalidSlippageConfigBlocksLiveTrade),
        new("disabled slippage model preserves live behavior", DisabledSlippageModelPreservesLiveBehavior),
        new("slippage is deducted from paper trade P/L", SlippageIsDeductedFromPaperTradePnl),
        new("slippage is deducted from backtest P/L", SlippageIsDeductedFromBacktestPnl),
        new("BUY SL too close blocks broker stop level", BuyStopLossTooCloseBlocksBrokerStopLevel),
        new("BUY TP too close blocks broker stop level", BuyTakeProfitTooCloseBlocksBrokerStopLevel),
        new("SELL SL too close blocks broker stop level", SellStopLossTooCloseBlocksBrokerStopLevel),
        new("SELL TP too close blocks broker stop level", SellTakeProfitTooCloseBlocksBrokerStopLevel),
        new("valid broker stop level distances allow live trade", ValidBrokerStopLevelDistancesAllowLiveTrade),
        new("missing broker stop metadata blocks live trade", MissingBrokerStopMetadataBlocksLiveTrade),
        new("paper mode is separate from broker stop metadata", PaperModeIsSeparateFromBrokerStopMetadata),
        new("BUY SL inside freeze level blocks live trade", BuyStopLossInsideFreezeLevelBlocksLiveTrade),
        new("BUY TP inside freeze level blocks live trade", BuyTakeProfitInsideFreezeLevelBlocksLiveTrade),
        new("SELL SL inside freeze level blocks live trade", SellStopLossInsideFreezeLevelBlocksLiveTrade),
        new("SELL TP inside freeze level blocks live trade", SellTakeProfitInsideFreezeLevelBlocksLiveTrade),
        new("valid broker freeze level distances allow live trade", ValidBrokerFreezeLevelDistancesAllowLiveTrade),
        new("missing broker freeze metadata blocks live trade", MissingBrokerFreezeMetadataBlocksLiveTrade),
        new("paper mode is separate from broker freeze metadata", PaperModeIsSeparateFromBrokerFreezeMetadata),
        new("lot below broker minimum blocks live trade", LotBelowBrokerMinimumBlocksLiveTrade),
        new("lot above broker maximum blocks live trade", LotAboveBrokerMaximumBlocksLiveTrade),
        new("lot not aligned with broker step blocks live trade", LotNotAlignedWithBrokerStepBlocksLiveTrade),
        new("valid broker lot size allows live trade", ValidBrokerLotSizeAllowsLiveTrade),
        new("volume limit exceeded blocks live trade", VolumeLimitExceededBlocksLiveTrade),
        new("missing broker lot metadata blocks live trade", MissingBrokerLotMetadataBlocksLiveTrade),
        new("paper mode is separate from broker lot metadata", PaperModeIsSeparateFromBrokerLotMetadata),
        new("live trade inside same-day no-trade window blocks", LiveTradeInsideSameDayNoTradeWindowBlocks),
        new("live trade outside same-day no-trade window allows", LiveTradeOutsideSameDayNoTradeWindowAllows),
        new("live trade inside cross-midnight no-trade window blocks", LiveTradeInsideCrossMidnightNoTradeWindowBlocks),
        new("live trade outside cross-midnight no-trade window allows", LiveTradeOutsideCrossMidnightNoTradeWindowAllows),
        new("invalid enabled no-trade window config blocks live trade", InvalidEnabledNoTradeWindowConfigBlocksLiveTrade),
        new("paper mode is separate from no-trade window", PaperModeIsSeparateFromNoTradeWindow),
        new("same-day session spread limit blocks high spread", SameDaySessionSpreadLimitBlocksHighSpread),
        new("same-day session spread limit allows acceptable spread", SameDaySessionSpreadLimitAllowsAcceptableSpread),
        new("cross-midnight session spread limit blocks high spread", CrossMidnightSessionSpreadLimitBlocksHighSpread),
        new("default session spread limit applies without matching rule", DefaultSessionSpreadLimitAppliesWithoutMatchingRule),
        new("stricter old max spread wins over session spread", StricterOldMaxSpreadWinsOverSessionSpread),
        new("invalid session spread config blocks live trade", InvalidSessionSpreadConfigBlocksLiveTrade),
        new("missing symbol data with session spread still blocks with NO_SYMBOL_DATA", MissingSymbolDataWithSessionSpreadStillBlocksWithNoSymbolData),
        new("paper mode is separate from session spread protection", PaperModeIsSeparateFromSessionSpreadProtection),
        new("OrderCheck pass allows live execution", OrderCheckPassAllowsLiveExecution),
        new("OrderCheck rejection blocks live trade", OrderCheckRejectionBlocksLiveTrade),
        new("OrderCheck unavailable blocks live trade", OrderCheckUnavailableBlocksLiveTrade),
        new("OrderCheck uses final validated lot size", OrderCheckUsesFinalValidatedLotSize),
        new("paper mode is separate from OrderCheck", PaperModeIsSeparateFromOrderCheck),
        new("successful first order send still works", SuccessfulFirstOrderSendStillWorks),
        new("retryable timeout retries up to configured max", RetryableTimeoutRetriesUpToConfiguredMax),
        new("requote retry stops when current spread fails", RequoteRetryStopsWhenCurrentSpreadFails),
        new("requote retry succeeds when current gates still pass", RequoteRetrySucceedsWhenCurrentGatesStillPass),
        new("invalid stops does not retry order send", InvalidStopsDoesNotRetryOrderSend),
        new("no money does not retry order send", NoMoneyDoesNotRetryOrderSend),
        new("market closed does not retry order send", MarketClosedDoesNotRetryOrderSend),
        new("trade disabled does not retry order send", TradeDisabledDoesNotRetryOrderSend),
        new("OrderCheck rejection does not retry order send", OrderCheckRejectionDoesNotRetryOrderSend),
        new("paper mode does not use live order retry policy", PaperModeDoesNotUseLiveOrderRetryPolicy),
        new("backtest/live mismatch report can be generated", BacktestLiveMismatchReportCanBeGenerated),
        new("backtest/live mismatch report marks commission and slippage present", BacktestLiveMismatchReportMarksCommissionAndSlippagePresent),
        new("backtest/live mismatch report marks spread realism missing", BacktestLiveMismatchReportMarksSpreadRealismMissing),
        new("backtest/live mismatch report marks intrabar SL/TP missing", BacktestLiveMismatchReportMarksIntrabarSlTpMissing),
        new("valid tick market data CSV loads correctly", ValidTickMarketDataCsvLoadsCorrectly),
        new("invalid tick market data CSV fails clearly", InvalidTickMarketDataCsvFailsClearly),
        new("valid OHLC market data CSV loads correctly", ValidOhlcMarketDataCsvLoadsCorrectly),
        new("invalid OHLC market data CSV fails clearly", InvalidOhlcMarketDataCsvFailsClearly),
        new("market data CSV is sorted by UTC timestamp", MarketDataCsvIsSortedByUtcTimestamp),
        new("duplicate market data timestamps are rejected", DuplicateMarketDataTimestampsAreRejected),
        new("market data symbol filtering works", MarketDataSymbolFilteringWorks),
        new("backtest spread cost is calculated from tick bid ask", BacktestSpreadCostIsCalculatedFromTickBidAsk),
        new("backtest spread cost is calculated from OHLC configured spread", BacktestSpreadCostIsCalculatedFromOhlcConfiguredSpread),
        new("backtest commission cost is included when enabled", BacktestCommissionCostIsIncludedWhenEnabled),
        new("backtest slippage cost is included when enabled", BacktestSlippageCostIsIncludedWhenEnabled),
        new("backtest total execution cost sums components", BacktestTotalExecutionCostSumsComponents),
        new("backtest missing spread data returns clear warning", BacktestMissingSpreadDataReturnsClearWarning),
        new("backtest disabled commission and slippage are zero cost", BacktestDisabledCommissionAndSlippageAreZeroCost),
        new("BUY tick intrabar exits at TP first", BuyTickIntrabarExitsAtTpFirst),
        new("BUY tick intrabar exits at SL first", BuyTickIntrabarExitsAtSlFirst),
        new("SELL tick intrabar exits at TP first", SellTickIntrabarExitsAtTpFirst),
        new("SELL tick intrabar exits at SL first", SellTickIntrabarExitsAtSlFirst),
        new("OHLC BUY only SL hit exits at SL", OhlcBuyOnlySlHitExitsAtSl),
        new("OHLC BUY only TP hit exits at TP", OhlcBuyOnlyTpHitExitsAtTp),
        new("OHLC SELL only SL hit exits at SL", OhlcSellOnlySlHitExitsAtSl),
        new("OHLC SELL only TP hit exits at TP", OhlcSellOnlyTpHitExitsAtTp),
        new("OHLC same candle both SL and TP uses SL first", OhlcSameCandleBothSlAndTpUsesSlFirst),
        new("intrabar no SL or TP hit remains open", IntrabarNoSlOrTpHitRemainsOpen),
        new("OHLC ambiguous result includes ambiguity flag", OhlcAmbiguousResultIncludesAmbiguityFlag),
        new("backtest broker-rule simulation allows valid trade", BacktestBrokerRuleSimulationAllowsValidTrade),
        new("backtest broker-rule simulation rejects stop-level violation", BacktestBrokerRuleSimulationRejectsStopLevelViolation),
        new("backtest broker-rule simulation rejects freeze-level violation", BacktestBrokerRuleSimulationRejectsFreezeLevelViolation),
        new("backtest broker-rule simulation rejects lot below minimum", BacktestBrokerRuleSimulationRejectsLotBelowMinimum),
        new("backtest broker-rule simulation rejects lot above maximum", BacktestBrokerRuleSimulationRejectsLotAboveMaximum),
        new("backtest broker-rule simulation rejects lot step violation", BacktestBrokerRuleSimulationRejectsLotStepViolation),
        new("backtest broker-rule simulation rejects volume limit violation", BacktestBrokerRuleSimulationRejectsVolumeLimitViolation),
        new("backtest broker-rule simulation rejects insufficient margin", BacktestBrokerRuleSimulationRejectsInsufficientMargin),
        new("backtest broker-rule simulation fails clearly on missing metadata", BacktestBrokerRuleSimulationFailsClearlyOnMissingMetadata),
        new("backtest broker-rule simulation rejects simulated OrderCheck failure", BacktestBrokerRuleSimulationRejectsSimulatedOrderCheckFailure),
        new("max spread filter blocks high spread", MaxSpreadFilterBlocksHighSpread),
        new("stop-loss placement rejects wrong side of entry", StopLossPlacementRejectsWrongSide),
        new("take-profit placement rejects wrong side of entry", TakeProfitPlacementRejectsWrongSide),
        new("trade execution rejects validation, risk, and approval failures", TradeExecutionHandlesRejections),
        new("no direct live MT5 order bypass exists outside execution service", NoDirectLiveOrderBypassExists),
        new("missing account blocks live trade", MissingAccountBlocksLiveTrade),
        new("missing symbol/spread blocks live trade", MissingSymbolDataBlocksLiveTrade),
        new("news unavailable blocks live trade", NewsUnavailableBlocksLiveTrade),
        new("risk manager exception blocks live trade", RiskManagerExceptionBlocksLiveTrade),
        new("incomplete risk result blocks live trade", IncompleteRiskResultBlocksLiveTrade),
        new("paper mode allows missing symbol data separately", PaperModeAllowsMissingSymbolDataSeparately),
        new("duplicate signal id registry persists processed ids", DuplicateSignalRegistryPersistsIds),
        new("maximum open trade limit blocks at cap", MaximumOpenTradeLimitBlocksAtCap),
        new("disabled market session blocks scalping decision", DisabledSessionBlocksScalpingDecision)
    ];

    public static async Task<int> Main()
    {
        int failed = 0;

        foreach (var test in Tests)
        {
            try
            {
                await test.Body().ConfigureAwait(false);
                Console.WriteLine($"[PASS] {test.Name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[FAIL] {test.Name}");
                Console.WriteLine($"       {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{Tests.Length - failed}/{Tests.Length} tests passed.");
        return failed == 0 ? 0 : 1;
    }

    private static Task LotSizingUsesRiskFormula()
    {
        double lots = LotCalculator.Calculate(
            equity: 10_000,
            riskPercent: 1,
            entryPrice: 1.1000,
            stopLoss: 1.0950,
            symbol: "EURUSD");

        AssertClose(0.20, lots, 0.0001, "Expected 1% of 10,000 over 50 pips at $10/pip to be 0.20 lots.");
        return Task.CompletedTask;
    }

    private static async Task RiskManagerReturnsValidatedLotSize()
    {
        var result = await NewRiskManager().ValidateAsync(
            BuyRequest(),
            Account(),
            Symbol(spreadPoints: 10),
            [],
            Config()).ConfigureAwait(false);

        AssertTrue(result.IsApproved, result.Reason);
        AssertClose(0.20, result.ValidatedLotSize, 0.0001, "RiskManager should apply auto-lot sizing.");
        AssertClose(1.00, result.RiskPercent, 0.01, "Risk percent should remain near configured max.");
        AssertClose(2.00, result.RiskRewardRatio, 0.01, "R:R should be based on entry, SL, and TP.");
    }

    private static async Task DailyTradeLimitBlocksAtConfiguredLimit()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);

        await using var bot = new AutoBotService(
            Bridge(),
            ConfigWithFolder(folder, maxTradesPerDay: 0));

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertEqual("DAILY_LIMIT", result.ErrorCode, "Existing daily trade-count limit should reject immediately at zero.");
        AssertFalse(result.IsSuccess, "Daily limit rejection must not be successful.");
    }

    private static async Task DailyLossBelowLimitAllowsLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            DailyLossConfig(maxDailyLossAmount: 500),
            apiConfig: NewsDisabled(),
            tradeRepository: new FakeTradeRepository(ClosedTrade(profitUsd: -100)));

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Daily loss below the configured limit should allow validation to continue.");
        AssertEqual(1, mt5.OpenTradeCalls, "Live execution should be reached when daily loss is below limit.");
    }

    private static async Task DailyRealizedLossAtLimitBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            DailyLossConfig(maxDailyLossAmount: 100),
            apiConfig: NewsDisabled(),
            tradeRepository: new FakeTradeRepository(ClosedTrade(profitUsd: -100)));

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Daily realized loss at the limit must block live trading.");
        AssertEqual("DAILY_LOSS_LIMIT", result.ErrorCode, "Daily realized loss at limit should use the hard-stop code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Daily loss hard stop must run before broker execution.");
    }

    private static async Task DailyRealizedLossAboveLimitBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            DailyLossConfig(maxDailyLossAmount: 100),
            apiConfig: NewsDisabled(),
            tradeRepository: new FakeTradeRepository(ClosedTrade(profitUsd: -125)));

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Daily realized loss above the limit must block live trading.");
        AssertEqual("DAILY_LOSS_LIMIT", result.ErrorCode, "Daily realized loss above limit should use the hard-stop code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Daily loss hard stop must run before broker execution.");
    }

    private static async Task FloatingLossContributesToDailyLossHardStop()
    {
        var positions = new List<LivePosition>
        {
            Position(ticket: 10, profit: -50)
        };

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10), positions);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            DailyLossConfig(maxDailyLossAmount: 100),
            apiConfig: NewsDisabled(),
            tradeRepository: new FakeTradeRepository(ClosedTrade(profitUsd: -50)));

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Realized plus floating loss at the daily limit must block live trading.");
        AssertEqual("DAILY_LOSS_LIMIT", result.ErrorCode, "Combined daily loss should use the hard-stop code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Daily loss hard stop must include floating P/L before execution.");
    }

    private static async Task MissingDailyTradeHistoryBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            DailyLossConfig(maxDailyLossAmount: 100),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when daily trade-history data is missing.");
        AssertEqual("DAILY_LOSS_DATA_UNAVAILABLE", result.ErrorCode, "Missing trade history should use the data-unavailable code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Missing daily loss data must block before execution.");
    }

    private static async Task MissingFloatingPnlBlocksLiveTrade()
    {
        var positions = new List<LivePosition>
        {
            Position(ticket: 10, profit: double.NaN)
        };

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10), positions);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            DailyLossConfig(maxDailyLossAmount: 100),
            apiConfig: NewsDisabled(),
            tradeRepository: new FakeTradeRepository());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when floating P/L cannot be calculated.");
        AssertEqual("DAILY_LOSS_DATA_UNAVAILABLE", result.ErrorCode, "Missing floating P/L should use the data-unavailable code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Missing floating P/L data must block before execution.");
    }

    private static async Task PaperModeIsSeparateFromDailyLossHardStop()
    {
        var config = DailyLossConfig(maxDailyLossAmount: 100);
        config.PaperTrading = true;

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Paper mode should stay explicitly separate from live daily loss fail-closed behavior.");
        AssertEqual(0, mt5.OpenTradeCalls, "Paper mode must not send broker orders.");
    }

    private static async Task WeeklyLossBelowLimitAllowsLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            WeeklyLossConfig(maxWeeklyLossAmount: 500),
            apiConfig: NewsDisabled(),
            tradeRepository: new FakeTradeRepository(ClosedTrade(profitUsd: -100)));

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Weekly loss below the configured limit should allow validation to continue.");
        AssertEqual(1, mt5.OpenTradeCalls, "Live execution should be reached when weekly loss is below limit.");
    }

    private static async Task WeeklyRealizedLossAtLimitBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            WeeklyLossConfig(maxWeeklyLossAmount: 100),
            apiConfig: NewsDisabled(),
            tradeRepository: new FakeTradeRepository(ClosedTrade(profitUsd: -100)));

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Weekly realized loss at the limit must block live trading.");
        AssertEqual("WEEKLY_LOSS_LIMIT", result.ErrorCode, "Weekly realized loss at limit should use the hard-stop code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Weekly loss hard stop must run before broker execution.");
    }

    private static async Task WeeklyRealizedLossAboveLimitBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            WeeklyLossConfig(maxWeeklyLossAmount: 100),
            apiConfig: NewsDisabled(),
            tradeRepository: new FakeTradeRepository(ClosedTrade(profitUsd: -125)));

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Weekly realized loss above the limit must block live trading.");
        AssertEqual("WEEKLY_LOSS_LIMIT", result.ErrorCode, "Weekly realized loss above limit should use the hard-stop code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Weekly loss hard stop must run before broker execution.");
    }

    private static async Task FloatingLossContributesToWeeklyLossHardStop()
    {
        var positions = new List<LivePosition>
        {
            Position(ticket: 10, profit: -50)
        };

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10), positions);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            WeeklyLossConfig(maxWeeklyLossAmount: 100),
            apiConfig: NewsDisabled(),
            tradeRepository: new FakeTradeRepository(ClosedTrade(profitUsd: -50)));

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Realized plus floating loss at the weekly limit must block live trading.");
        AssertEqual("WEEKLY_LOSS_LIMIT", result.ErrorCode, "Combined weekly loss should use the hard-stop code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Weekly loss hard stop must include floating P/L before execution.");
    }

    private static async Task TradesOutsideCurrentUtcWeekAreExcluded()
    {
        DateTime outsideThisWeek = CurrentUtcWeekStart().AddTicks(-1);

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            WeeklyLossConfig(maxWeeklyLossAmount: 100),
            apiConfig: NewsDisabled(),
            tradeRepository: new FakeTradeRepository(
                ClosedTrade(profitUsd: -1_000, closedAtUtc: outsideThisWeek)));

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Losses before the current UTC trading week should be excluded.");
        AssertEqual(1, mt5.OpenTradeCalls, "Outside-week losses must not trigger the weekly hard stop.");
    }

    private static async Task MissingWeeklyTradeHistoryBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            WeeklyLossConfig(maxWeeklyLossAmount: 100),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when weekly trade-history data is missing.");
        AssertEqual("WEEKLY_LOSS_DATA_UNAVAILABLE", result.ErrorCode, "Missing weekly trade history should use the data-unavailable code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Missing weekly loss data must block before execution.");
    }

    private static async Task MissingWeeklyFloatingPnlBlocksLiveTrade()
    {
        var positions = new List<LivePosition>
        {
            Position(ticket: 10, profit: double.NaN)
        };

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10), positions);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            WeeklyLossConfig(maxWeeklyLossAmount: 100),
            apiConfig: NewsDisabled(),
            tradeRepository: new FakeTradeRepository());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when weekly floating P/L cannot be calculated.");
        AssertEqual("WEEKLY_LOSS_DATA_UNAVAILABLE", result.ErrorCode, "Missing weekly floating P/L should use the data-unavailable code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Missing weekly floating P/L data must block before execution.");
    }

    private static async Task PaperModeIsSeparateFromWeeklyLossHardStop()
    {
        var config = WeeklyLossConfig(maxWeeklyLossAmount: 100);
        config.PaperTrading = true;

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Paper mode should stay explicitly separate from live weekly loss fail-closed behavior.");
        AssertEqual(0, mt5.OpenTradeCalls, "Paper mode must not send broker orders.");
    }

    private static async Task SameSymbolLotsBelowCapAllowsLiveTrade()
    {
        var positions = new List<LivePosition>
        {
            Position(ticket: 20, lots: 0.40)
        };

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10), positions);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SymbolExposureConfig(maxSymbolLots: 1.00),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Projected same-symbol lots below cap should allow validation to continue.");
        AssertEqual(1, mt5.OpenTradeCalls, "Live execution should be reached when same-symbol lots are below cap.");
    }

    private static async Task SameSymbolLotsAtCapBlocksLiveTrade()
    {
        var positions = new List<LivePosition>
        {
            Position(ticket: 20, lots: 0.90)
        };

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10), positions);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SymbolExposureConfig(maxSymbolLots: 1.00),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Projected same-symbol lots at cap must block live trading.");
        AssertEqual("SYMBOL_EXPOSURE_LIMIT", result.ErrorCode, "Same-symbol lots at cap should use the exposure limit code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Symbol exposure hard stop must run before broker execution.");
    }

    private static async Task SameSymbolPositionCountAtCapBlocksLiveTrade()
    {
        var positions = new List<LivePosition>
        {
            Position(ticket: 20)
        };

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10), positions);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SymbolExposureConfig(maxSameSymbolPositions: 2),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Projected same-symbol position count at cap must block live trading.");
        AssertEqual("SYMBOL_EXPOSURE_LIMIT", result.ErrorCode, "Same-symbol position count at cap should use the exposure limit code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Symbol exposure position-count stop must run before broker execution.");
    }

    private static async Task SameSymbolRiskAtCapBlocksLiveTrade()
    {
        var positions = new List<LivePosition>
        {
            Position(ticket: 20, lots: 0.10)
        };

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10), positions);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SymbolExposureConfig(maxSymbolRiskPercent: 1.00),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Projected same-symbol risk at cap must block live trading.");
        AssertEqual("SYMBOL_EXPOSURE_LIMIT", result.ErrorCode, "Same-symbol risk at cap should use the exposure limit code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Symbol exposure risk stop must run before broker execution.");
    }

    private static async Task OppositeSameSymbolExposureCountsGrossLots()
    {
        var positions = new List<LivePosition>
        {
            Position(ticket: 20, type: TradeType.SELL, lots: 0.90, stopLoss: 1.1050)
        };

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10), positions);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SymbolExposureConfig(maxSymbolLots: 1.00),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Opposite-direction same-symbol positions must count as gross exposure.");
        AssertEqual("SYMBOL_EXPOSURE_LIMIT", result.ErrorCode, "Gross opposite-direction exposure should use the exposure limit code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Gross exposure hard stop must run before broker execution.");
    }

    private static async Task DifferentSymbolsDoNotCountTowardSymbolCap()
    {
        var positions = new List<LivePosition>
        {
            Position(ticket: 20, symbol: "GBPUSD", lots: 0.90)
        };

        var config = SymbolExposureConfig(maxSymbolLots: 0.50);
        config.CorrelationCheckEnabled = false;

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10), positions);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Different symbols should not count toward the requested symbol exposure cap.");
        AssertEqual(1, mt5.OpenTradeCalls, "Different-symbol exposure should not block live execution.");
    }

    private static async Task MissingSymbolExposureDataBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(
            Account(),
            Symbol(spreadPoints: 10),
            positionsAvailable: false);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SymbolExposureConfig(maxSymbolLots: 1.00),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when symbol exposure data is unavailable.");
        AssertEqual("SYMBOL_EXPOSURE_DATA_UNAVAILABLE", result.ErrorCode, "Missing exposure data should use the data-unavailable code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Missing exposure data must block before broker execution.");
    }

    private static async Task PaperModeIncludesPaperPositionsInExposureChecks()
    {
        var config = SymbolExposureConfig(maxSameSymbolPositions: 2);
        config.PaperTrading = true;

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var first = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);
        var second = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(first.IsSuccess, "First paper trade should open when projected symbol count is below cap.");
        AssertFalse(second.IsSuccess, "Paper-mode exposure checks should include existing paper positions.");
        AssertEqual("SYMBOL_EXPOSURE_LIMIT", second.ErrorCode, "Paper symbol exposure cap should use the exposure limit code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Paper mode must not send broker orders.");
    }

    private static async Task HealthyProjectedMarginLevelAllowsLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(
            Account(margin: 1_000),
            Symbol(spreadPoints: 10),
            marginEstimate: MarginEstimate(requiredMargin: 500));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            MarginConfig(minProjectedMarginLevelPercent: 200),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Healthy projected margin level should allow validation to continue.");
        AssertEqual(1, mt5.OpenTradeCalls, "Live execution should be reached when projected margin is healthy.");
    }

    private static async Task ProjectedMarginLevelBelowMinimumBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(
            Account(margin: 9_000),
            Symbol(spreadPoints: 10),
            marginEstimate: MarginEstimate(requiredMargin: 2_000));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            MarginConfig(minProjectedMarginLevelPercent: 100),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Projected margin level below minimum must block live trading.");
        AssertEqual("MARGIN_LEVEL_LIMIT", result.ErrorCode, "Low projected margin should use the margin limit code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Projected margin hard stop must run before broker execution.");
    }

    private static async Task ExistingMarginLevelBelowMinimumBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(
            Account(margin: 5_000, marginLevel: 150),
            Symbol(spreadPoints: 10),
            marginEstimate: MarginEstimate(requiredMargin: 100));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            MarginConfig(minProjectedMarginLevelPercent: 200),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Existing margin level below minimum must block live trading.");
        AssertEqual("MARGIN_LEVEL_LIMIT", result.ErrorCode, "Low current margin should use the margin limit code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Existing margin hard stop must run before broker execution.");
    }

    private static async Task MissingMarginEstimateBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(
            Account(margin: 1_000),
            Symbol(spreadPoints: 10),
            marginEstimateAvailable: false);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            MarginConfig(minProjectedMarginLevelPercent: 200),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when margin estimate is unavailable.");
        AssertEqual("MARGIN_DATA_UNAVAILABLE", result.ErrorCode, "Missing margin estimate should use the margin data code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Missing margin estimate must block before broker execution.");
    }

    private static async Task MissingAccountMarginDataBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(
            Account(margin: -1),
            Symbol(spreadPoints: 10),
            marginEstimate: MarginEstimate(requiredMargin: 100));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            MarginConfig(minProjectedMarginLevelPercent: 200),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when account margin data is unavailable.");
        AssertEqual("MARGIN_DATA_UNAVAILABLE", result.ErrorCode, "Missing account margin data should use the margin data code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Missing account margin data must block before broker execution.");
    }

    private static async Task PaperModeIsSeparateFromProjectedMarginValidation()
    {
        var config = MarginConfig(minProjectedMarginLevelPercent: 200);
        config.PaperTrading = true;

        await using var mt5 = new FakeMt5Server(
            Account(margin: -1),
            Symbol(spreadPoints: 10),
            marginEstimateAvailable: false);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Paper mode should stay explicitly separate from live projected margin validation.");
        AssertEqual(0, mt5.OpenTradeCalls, "Paper mode must not send broker orders.");
    }

    private static async Task EmergencyDrawdownPersistsKillSwitch()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);

        var positions = new List<LivePosition> { Position(ticket: 70) };
        await using var mt5 = new FakeMt5Server(
            Account(balance: 10_000, equity: 8_500),
            Symbol(spreadPoints: 10),
            positions);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            KillSwitchConfigWithFolder(folder),
            apiConfig: NewsDisabled());

        SetPrivateField(bot, "_startOfDayEquity", 10_000.0);
        await InvokePrivateAsync(bot, "CheckDrawdownAsync").ConfigureAwait(false);

        var state = ReadKillSwitchFile(folder);
        AssertTrue(state.KillSwitchActive, "Emergency drawdown must persist an active kill switch.");
        AssertContains("Emergency drawdown", state.KillSwitchReason);
        AssertTrue(state.KillSwitchTriggeredAtUtc.HasValue, "Kill switch trigger time must be persisted.");
        AssertClose(15.0, state.DrawdownPercentAtTrigger, 0.01, "Drawdown percent at trigger should be persisted.");
        AssertClose(10_000, state.AccountBalance, 0.01, "Account balance should be persisted.");
        AssertClose(8_500, state.AccountEquity, 0.01, "Account equity should be persisted.");
        AssertTrue(bot.IsKillSwitchActive, "Bot should keep active kill switch in memory after drawdown.");
        AssertEqual(1, mt5.CloseTradeCalls, "Emergency close-all should attempt to close open positions.");
    }

    private static async Task RestartLoadsActiveKillSwitch()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);
        WriteKillSwitchFile(folder, "Persisted emergency stop");

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            ConfigWithFolder(folder),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Persisted active kill switch must block live trades after reload.");
        AssertEqual("KILL_SWITCH_ACTIVE", result.ErrorCode, "Reloaded kill switch should use the live safety code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Kill switch must block before broker execution.");
    }

    private static async Task ActiveKillSwitchBlocksCentralLiveExecutionGate()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);
        WriteKillSwitchFile(folder, "Emergency stop still active");

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            ConfigWithFolder(folder),
            apiConfig: NewsDisabled());

        foreach (string id in new[] { "MANUAL001", "JSON001", "AI001", "AUTO001" })
        {
            var request = BuyRequest();
            request.Id = id;
            var result = await bot.ExecuteTradeWithValidationAsync(request).ConfigureAwait(false);
            AssertFalse(result.IsSuccess, $"{id} must be blocked by the central live execution gate.");
            AssertEqual("KILL_SWITCH_ACTIVE", result.ErrorCode, $"{id} should use the kill-switch code.");
        }

        AssertEqual(0, mt5.OpenTradeCalls, "Active kill switch must prevent all central live execution attempts.");
    }

    private static async Task ExplicitClearDisablesKillSwitch()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);
        WriteKillSwitchFile(folder, "Emergency stop still active");

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            ConfigWithFolder(folder),
            apiConfig: NewsDisabled());

        var blocked = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);
        AssertEqual("KILL_SWITCH_ACTIVE", blocked.ErrorCode, "Precondition: kill switch should start active.");

        bot.ClearKillSwitchByUser("UnitTest explicit clear");
        var request = BuyRequest();
        request.Id = "AFTER_CLEAR";
        var result = await bot.ExecuteTradeWithValidationAsync(request).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Explicit clear should allow normal validation to continue.");
        AssertEqual(1, mt5.OpenTradeCalls, "Broker execution should be reached after explicit clear.");
        AssertFalse(ReadKillSwitchFile(folder).KillSwitchActive, "Explicit clear should persist inactive kill-switch state.");
    }

    private static async Task FailedEmergencyCloseKeepsKillSwitchActive()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);

        var positions = new List<LivePosition> { Position(ticket: 80) };
        await using var mt5 = new FakeMt5Server(
            Account(balance: 10_000, equity: 8_500),
            Symbol(spreadPoints: 10),
            positions,
            closeFailureTickets: new HashSet<long> { 80 });
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            KillSwitchConfigWithFolder(folder),
            apiConfig: NewsDisabled());

        SetPrivateField(bot, "_startOfDayEquity", 10_000.0);
        await InvokePrivateAsync(bot, "CheckDrawdownAsync").ConfigureAwait(false);
        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(ReadKillSwitchFile(folder).KillSwitchActive, "Failed emergency close must keep kill switch active.");
        AssertEqual(1, mt5.CloseTradeCalls, "Emergency close should be attempted even when the broker rejects it.");
        AssertEqual("KILL_SWITCH_ACTIVE", result.ErrorCode, "New live trades must stay blocked after failed emergency close.");
        AssertEqual(0, mt5.OpenTradeCalls, "Failed emergency close must not allow a new broker order.");
    }

    private static async Task PaperModeIsSeparateFromKillSwitch()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);
        WriteKillSwitchFile(folder, "Live emergency stop still active");

        var config = ConfigWithFolder(folder);
        config.PaperTrading = true;

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Paper mode should stay explicitly separate from live kill-switch blocking.");
        AssertEqual(0, mt5.OpenTradeCalls, "Paper mode must not send broker orders.");
    }

    private static Task CommissionIsCalculatedCorrectlyForLotSize()
    {
        var config = CommissionConfig(commissionPerLotPerSide: 3.50);

        var estimate = CommissionCalculator.EstimateRoundTurn(0.20, config);

        AssertTrue(estimate.Success, estimate.Error);
        AssertClose(1.40, estimate.Amount, 0.0001, "0.20 lots at $3.50 per side should cost $1.40 round-turn.");
        AssertEqual("USD", estimate.Currency, "Commission currency should default to USD.");
        return Task.CompletedTask;
    }

    private static async Task CommissionIsDeductedFromPaperTradePnl()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);

        var config = CommissionConfig(commissionPerLotPerSide: 3.50);
        config.PaperTrading = true;
        config.WatchFolder = folder;
        config.KillSwitchStateFile = Path.Combine(folder, "kill_switch.json");

        var repo = new FakeTradeRepository();
        await using var mt5 = new FakeMt5Server(Account(), SymbolAt(bid: 1.1100, ask: 1.1101));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled(),
            tradeRepository: repo);

        var request = BuyRequest();
        request.LotSize = 0.10;

        var open = InvokePrivateResult<TradeResult>(bot, "SimulatePaperTrade", request, 1.1000);
        await InvokePrivateAsync(bot, "CheckPaperPositionsAsync").ConfigureAwait(false);

        AssertClose(0.70, open.EstimatedCommission, 0.0001, "Paper trade should carry estimated commission.");
        AssertTrue(repo.LastClosedProfitUsd.HasValue, "Paper close should update simulated P/L.");
        AssertClose(99.30, repo.LastClosedProfitUsd!.Value, 0.0001,
            "Paper P/L should subtract $0.70 commission from a $100.00 raw profit.");
    }

    private static async Task CommissionIsDeductedFromBacktestPnl()
    {
        var config = CommissionConfig(commissionPerLotPerSide: 3.50);
        var trade = new BacktestTrade
        {
            Signal = new MarketSignal
            {
                Id = "BT-COMM",
                Pair = "EURUSD",
                Direction = SignalDirection.Buy,
                EntryPrice = 1.1000
            },
            EntryPrice = 1.1000,
            ExitPrice = 1.1100,
            Lots = 0.10,
            OpenedAt = DateTime.UtcNow.AddHours(-2),
            ClosedAt = DateTime.UtcNow.AddHours(-1)
        };

        var result = await new BacktestingService()
            .RunAsync([trade], config)
            .ConfigureAwait(false);

        AssertClose(99.30, result.NetProfitUsd, 0.0001,
            "Backtest P/L should subtract $0.70 commission from a $100.00 raw profit.");
        AssertClose(0.70, result.TotalCommissionUsd, 0.0001, "Backtest should report total commission.");
    }

    private static async Task InvalidCommissionConfigBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            CommissionConfig(commissionPerLotPerSide: 0),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when enabled commission config is invalid.");
        AssertEqual("COMMISSION_DATA_UNAVAILABLE", result.ErrorCode, "Invalid commission config should use the commission data code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Commission failure must block before broker execution.");
    }

    private static async Task DisabledCommissionModelPreservesLiveBehavior()
    {
        var config = Config();
        config.EnableCommissionModel = false;
        config.CommissionPerLotPerSide = -5;

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Disabled commission model should preserve existing live behavior.");
        AssertEqual(1, mt5.OpenTradeCalls, "Broker execution should still be reached when commission model is disabled.");
    }

    private static Task SlippageCostIsCalculatedCorrectly()
    {
        var config = SlippageConfig(estimatedSlippagePips: 1.5, maxAllowedSlippagePips: 3.0);

        var estimate = SlippageCalculator.EstimateCost("EURUSD", 0.20, config);

        AssertTrue(estimate.Success, estimate.Error);
        AssertClose(3.00, estimate.CostUsd, 0.0001, "1.5 pips on 0.20 EURUSD lots should cost $3.00.");
        AssertClose(1.5, estimate.Pips, 0.0001, "Estimated slippage pips should be preserved.");
        return Task.CompletedTask;
    }

    private static async Task InvalidSlippageConfigBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SlippageConfig(estimatedSlippagePips: -1, maxAllowedSlippagePips: 3),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when enabled slippage config is invalid.");
        AssertEqual("SLIPPAGE_DATA_UNAVAILABLE", result.ErrorCode, "Invalid slippage config should use the slippage data code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Slippage failure must block before broker execution.");
    }

    private static async Task DisabledSlippageModelPreservesLiveBehavior()
    {
        var config = Config();
        config.EnableSlippageModel = false;
        config.EstimatedSlippagePips = -5;
        config.MaxAllowedSlippagePips = 0;

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Disabled slippage model should preserve existing live behavior.");
        AssertEqual(1, mt5.OpenTradeCalls, "Broker execution should still be reached when slippage model is disabled.");
    }

    private static async Task SlippageIsDeductedFromPaperTradePnl()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);

        var config = SlippageConfig(estimatedSlippagePips: 1.0, maxAllowedSlippagePips: 3.0);
        config.PaperTrading = true;
        config.WatchFolder = folder;
        config.KillSwitchStateFile = Path.Combine(folder, "kill_switch.json");

        var repo = new FakeTradeRepository();
        await using var mt5 = new FakeMt5Server(Account(), SymbolAt(bid: 1.1100, ask: 1.1101));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled(),
            tradeRepository: repo);

        var request = BuyRequest();
        request.LotSize = 0.10;

        var open = InvokePrivateResult<TradeResult>(bot, "SimulatePaperTrade", request, 1.1000);
        await InvokePrivateAsync(bot, "CheckPaperPositionsAsync").ConfigureAwait(false);

        AssertClose(1.00, open.EstimatedSlippageCost, 0.0001, "Paper trade should carry estimated slippage cost.");
        AssertTrue(repo.LastClosedProfitUsd.HasValue, "Paper close should update simulated P/L.");
        AssertClose(99.00, repo.LastClosedProfitUsd!.Value, 0.0001,
            "Paper P/L should subtract $1.00 slippage from a $100.00 raw profit.");
    }

    private static async Task SlippageIsDeductedFromBacktestPnl()
    {
        var config = SlippageConfig(estimatedSlippagePips: 1.0, maxAllowedSlippagePips: 3.0);
        var trade = new BacktestTrade
        {
            Signal = new MarketSignal
            {
                Id = "BT-SLIP",
                Pair = "EURUSD",
                Direction = SignalDirection.Buy,
                EntryPrice = 1.1000
            },
            EntryPrice = 1.1000,
            ExitPrice = 1.1100,
            Lots = 0.10,
            OpenedAt = DateTime.UtcNow.AddHours(-2),
            ClosedAt = DateTime.UtcNow.AddHours(-1)
        };

        var result = await new BacktestingService()
            .RunAsync([trade], config)
            .ConfigureAwait(false);

        AssertClose(99.00, result.NetProfitUsd, 0.0001,
            "Backtest P/L should subtract $1.00 slippage from a $100.00 raw profit.");
        AssertClose(1.00, result.TotalSlippageUsd, 0.0001, "Backtest should report total slippage cost.");
    }

    private static async Task BuyStopLossTooCloseBlocksBrokerStopLevel()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithStopLevel(stopLevelPoints: 20));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest(sl: 1.09990)).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "BUY stop-loss inside broker stop level must block live execution.");
        AssertEqual("BROKER_STOP_LEVEL_VIOLATION", result.ErrorCode, "Too-close SL should use broker stop-level rejection.");
        AssertEqual(0, mt5.OpenTradeCalls, "Broker stop-level guard must block before broker execution.");
    }

    private static async Task BuyTakeProfitTooCloseBlocksBrokerStopLevel()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithStopLevel(stopLevelPoints: 20));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest(tp: 1.10020)).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "BUY take-profit inside broker stop level must block live execution.");
        AssertEqual("BROKER_STOP_LEVEL_VIOLATION", result.ErrorCode, "Too-close TP should use broker stop-level rejection.");
        AssertEqual(0, mt5.OpenTradeCalls, "Broker stop-level guard must block before broker execution.");
    }

    private static async Task SellStopLossTooCloseBlocksBrokerStopLevel()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithStopLevel(stopLevelPoints: 20));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(SellRequest(sl: 1.10010)).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "SELL stop-loss inside broker stop level must block live execution.");
        AssertEqual("BROKER_STOP_LEVEL_VIOLATION", result.ErrorCode, "Too-close SL should use broker stop-level rejection.");
        AssertEqual(0, mt5.OpenTradeCalls, "Broker stop-level guard must block before broker execution.");
    }

    private static async Task SellTakeProfitTooCloseBlocksBrokerStopLevel()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithStopLevel(stopLevelPoints: 20));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(SellRequest(tp: 1.09980)).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "SELL take-profit inside broker stop level must block live execution.");
        AssertEqual("BROKER_STOP_LEVEL_VIOLATION", result.ErrorCode, "Too-close TP should use broker stop-level rejection.");
        AssertEqual(0, mt5.OpenTradeCalls, "Broker stop-level guard must block before broker execution.");
    }

    private static async Task ValidBrokerStopLevelDistancesAllowLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithStopLevel(stopLevelPoints: 20));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Valid SL/TP distances should allow validation to continue.");
        AssertEqual(1, mt5.OpenTradeCalls, "Broker execution should be reached when stop-level distances are valid.");
    }

    private static async Task MissingBrokerStopMetadataBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithoutStopLevelMetadata());
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when broker stop-level data is missing.");
        AssertEqual("BROKER_STOP_LEVEL_DATA_UNAVAILABLE", result.ErrorCode,
            "Missing stop-level metadata should use the stop-level data-unavailable code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Missing stop-level metadata must block before broker execution.");
    }

    private static async Task PaperModeIsSeparateFromBrokerStopMetadata()
    {
        var config = Config();
        config.PaperTrading = true;

        await using var mt5 = new FakeMt5Server(Account(), SymbolWithoutStopLevelMetadata());
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Paper mode should stay separate from live broker stop-level fail-closed behavior.");
        AssertEqual(0, mt5.OpenTradeCalls, "Paper mode must not send broker orders.");
    }

    private static async Task BuyStopLossInsideFreezeLevelBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithFreezeLevel(freezeLevelPoints: 20));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest(sl: 1.09990)).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "BUY stop-loss inside broker freeze level must block live execution.");
        AssertEqual("BROKER_FREEZE_LEVEL_VIOLATION", result.ErrorCode,
            "SL inside freeze level should use broker freeze-level rejection.");
        AssertEqual(0, mt5.OpenTradeCalls, "Broker freeze-level guard must block before broker execution.");
    }

    private static async Task BuyTakeProfitInsideFreezeLevelBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithFreezeLevel(freezeLevelPoints: 20));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest(tp: 1.10020)).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "BUY take-profit inside broker freeze level must block live execution.");
        AssertEqual("BROKER_FREEZE_LEVEL_VIOLATION", result.ErrorCode,
            "TP inside freeze level should use broker freeze-level rejection.");
        AssertEqual(0, mt5.OpenTradeCalls, "Broker freeze-level guard must block before broker execution.");
    }

    private static async Task SellStopLossInsideFreezeLevelBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithFreezeLevel(freezeLevelPoints: 20));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(SellRequest(sl: 1.10010)).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "SELL stop-loss inside broker freeze level must block live execution.");
        AssertEqual("BROKER_FREEZE_LEVEL_VIOLATION", result.ErrorCode,
            "SL inside freeze level should use broker freeze-level rejection.");
        AssertEqual(0, mt5.OpenTradeCalls, "Broker freeze-level guard must block before broker execution.");
    }

    private static async Task SellTakeProfitInsideFreezeLevelBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithFreezeLevel(freezeLevelPoints: 20));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(SellRequest(tp: 1.09980)).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "SELL take-profit inside broker freeze level must block live execution.");
        AssertEqual("BROKER_FREEZE_LEVEL_VIOLATION", result.ErrorCode,
            "TP inside freeze level should use broker freeze-level rejection.");
        AssertEqual(0, mt5.OpenTradeCalls, "Broker freeze-level guard must block before broker execution.");
    }

    private static async Task ValidBrokerFreezeLevelDistancesAllowLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithFreezeLevel(freezeLevelPoints: 20));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Valid SL/TP distances outside freeze level should allow validation to continue.");
        AssertEqual(1, mt5.OpenTradeCalls, "Broker execution should be reached when freeze-level distances are valid.");
    }

    private static async Task MissingBrokerFreezeMetadataBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithoutFreezeLevelMetadata());
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when broker freeze-level data is missing.");
        AssertEqual("BROKER_FREEZE_LEVEL_DATA_UNAVAILABLE", result.ErrorCode,
            "Missing freeze-level metadata should use the freeze-level data-unavailable code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Missing freeze-level metadata must block before broker execution.");
    }

    private static async Task PaperModeIsSeparateFromBrokerFreezeMetadata()
    {
        var config = Config();
        config.PaperTrading = true;

        await using var mt5 = new FakeMt5Server(Account(), SymbolWithoutFreezeLevelMetadata());
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Paper mode should stay separate from live broker freeze-level fail-closed behavior.");
        AssertEqual(0, mt5.OpenTradeCalls, "Paper mode must not send broker orders.");
    }

    private static async Task LotBelowBrokerMinimumBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithLotRules(minLot: 0.30, maxLot: 100, lotStep: 0.01));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Final risk-adjusted lot below broker minimum must block live execution.");
        AssertEqual("BROKER_LOT_SIZE_VIOLATION", result.ErrorCode,
            "Lot below broker minimum should use broker lot-size rejection.");
        AssertEqual(0, mt5.OpenTradeCalls, "Broker lot-size guard must block before broker execution.");
    }

    private static async Task LotAboveBrokerMaximumBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithLotRules(minLot: 0.01, maxLot: 0.15, lotStep: 0.01));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Final risk-adjusted lot above broker maximum must block live execution.");
        AssertEqual("BROKER_LOT_SIZE_VIOLATION", result.ErrorCode,
            "Lot above broker maximum should use broker lot-size rejection.");
        AssertEqual(0, mt5.OpenTradeCalls, "Broker lot-size guard must block before broker execution.");
    }

    private static async Task LotNotAlignedWithBrokerStepBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithLotRules(minLot: 0.01, maxLot: 100, lotStep: 0.03));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Final risk-adjusted lot not aligned with broker step must block live execution.");
        AssertEqual("BROKER_LOT_SIZE_VIOLATION", result.ErrorCode,
            "Lot step mismatch should use broker lot-size rejection.");
        AssertEqual(0, mt5.OpenTradeCalls, "Broker lot-size guard must block before broker execution.");
    }

    private static async Task ValidBrokerLotSizeAllowsLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithLotRules(minLot: 0.01, maxLot: 100, lotStep: 0.01));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Broker-valid final lot size should allow validation to continue.");
        AssertEqual(1, mt5.OpenTradeCalls, "Broker execution should be reached when final lot size is valid.");
    }

    private static async Task VolumeLimitExceededBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(),
            SymbolWithLotRules(minLot: 0.01, maxLot: 100, lotStep: 0.01, volumeLimit: 0.15));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Final risk-adjusted lot above broker volume limit must block live execution.");
        AssertEqual("BROKER_LOT_SIZE_VIOLATION", result.ErrorCode,
            "Volume-limit breach should use broker lot-size rejection.");
        AssertEqual(0, mt5.OpenTradeCalls, "Broker lot-size guard must block before broker execution.");
    }

    private static async Task MissingBrokerLotMetadataBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), SymbolWithoutLotMetadata());
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when broker lot metadata is missing.");
        AssertEqual("BROKER_LOT_DATA_UNAVAILABLE", result.ErrorCode,
            "Missing lot metadata should use the broker lot data-unavailable code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Missing lot metadata must block before broker execution.");
    }

    private static async Task PaperModeIsSeparateFromBrokerLotMetadata()
    {
        var config = Config();
        config.PaperTrading = true;

        await using var mt5 = new FakeMt5Server(Account(), SymbolWithoutLotMetadata());
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Paper mode should stay separate from live broker lot fail-closed behavior.");
        AssertEqual(0, mt5.OpenTradeCalls, "Paper mode must not send broker orders.");
    }

    private static async Task LiveTradeInsideSameDayNoTradeWindowBlocks()
    {
        var now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            RolloverConfig("11:30", "12:30"),
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade inside same-day no-trade window must be blocked.");
        AssertEqual("ROLLOVER_NO_TRADE_WINDOW", result.ErrorCode,
            "Blocked rollover/no-trade window should use the configured rejection code.");
        AssertEqual(0, mt5.OpenTradeCalls, "No-trade window must block before broker execution.");
    }

    private static async Task LiveTradeOutsideSameDayNoTradeWindowAllows()
    {
        var now = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            RolloverConfig("11:30", "12:30"),
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Live trade outside same-day no-trade window should continue.");
        AssertEqual(1, mt5.OpenTradeCalls, "Broker execution should be reached outside the no-trade window.");
    }

    private static async Task LiveTradeInsideCrossMidnightNoTradeWindowBlocks()
    {
        var now = new DateTime(2026, 1, 15, 0, 5, 0, DateTimeKind.Utc);
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            RolloverConfig("23:55", "00:10"),
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade inside cross-midnight no-trade window must be blocked.");
        AssertEqual("ROLLOVER_NO_TRADE_WINDOW", result.ErrorCode,
            "Cross-midnight rollover/no-trade window should use the configured rejection code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Cross-midnight no-trade window must block before broker execution.");
    }

    private static async Task LiveTradeOutsideCrossMidnightNoTradeWindowAllows()
    {
        var now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            RolloverConfig("23:55", "00:10"),
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Live trade outside cross-midnight no-trade window should continue.");
        AssertEqual(1, mt5.OpenTradeCalls, "Broker execution should be reached outside the cross-midnight window.");
    }

    private static async Task InvalidEnabledNoTradeWindowConfigBlocksLiveTrade()
    {
        var now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            RolloverConfig("bad", "12:30"),
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when enabled no-trade window config is invalid.");
        AssertEqual("NO_TRADE_WINDOW_CONFIG_INVALID", result.ErrorCode,
            "Invalid no-trade config should use the config-invalid rejection code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Invalid no-trade config must block before broker execution.");
    }

    private static async Task PaperModeIsSeparateFromNoTradeWindow()
    {
        var now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var config = RolloverConfig("11:30", "12:30");
        config.PaperTrading = true;

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Paper mode should stay separate from live no-trade window blocking.");
        AssertEqual(0, mt5.OpenTradeCalls, "Paper mode must not send broker orders.");
    }

    private static async Task SameDaySessionSpreadLimitBlocksHighSpread()
    {
        var now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 25));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SessionSpreadConfig(defaultMaxSpreadPips: 10, oldMaxSpreadPips: 20,
                SpreadRule("London", "11:30", "12:30", 2.0)),
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Matching same-day session spread cap must block high spread.");
        AssertEqual("SPREAD_SESSION_LIMIT", result.ErrorCode,
            "Session spread cap should use the session spread rejection code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Session spread cap must block before broker execution.");
    }

    private static async Task SameDaySessionSpreadLimitAllowsAcceptableSpread()
    {
        var now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 15));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SessionSpreadConfig(defaultMaxSpreadPips: 10, oldMaxSpreadPips: 20,
                SpreadRule("London", "11:30", "12:30", 2.0)),
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Acceptable spread inside matching session should allow validation to continue.");
        AssertEqual(1, mt5.OpenTradeCalls, "Broker execution should be reached when session spread is acceptable.");
    }

    private static async Task CrossMidnightSessionSpreadLimitBlocksHighSpread()
    {
        var now = new DateTime(2026, 1, 15, 0, 5, 0, DateTimeKind.Utc);
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 25));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SessionSpreadConfig(defaultMaxSpreadPips: 10, oldMaxSpreadPips: 20,
                SpreadRule("Rollover", "23:55", "00:10", 2.0)),
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Matching cross-midnight session spread cap must block high spread.");
        AssertEqual("SPREAD_SESSION_LIMIT", result.ErrorCode,
            "Cross-midnight session spread cap should use the session spread rejection code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Cross-midnight session spread cap must block before broker execution.");
    }

    private static async Task DefaultSessionSpreadLimitAppliesWithoutMatchingRule()
    {
        var now = new DateTime(2026, 1, 15, 15, 0, 0, DateTimeKind.Utc);
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 25));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SessionSpreadConfig(defaultMaxSpreadPips: 2.0, oldMaxSpreadPips: 20,
                SpreadRule("London", "11:30", "12:30", 5.0)),
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Default spread cap must apply when no session rule matches.");
        AssertEqual("SPREAD_SESSION_LIMIT", result.ErrorCode,
            "Default session spread cap should use the session spread rejection code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Default session spread cap must block before broker execution.");
    }

    private static async Task StricterOldMaxSpreadWinsOverSessionSpread()
    {
        var now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 25));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SessionSpreadConfig(defaultMaxSpreadPips: 10, oldMaxSpreadPips: 2.0,
                SpreadRule("London", "11:30", "12:30", 5.0)),
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Existing max-spread protection must still block when stricter than session cap.");
        AssertEqual("RISK_BLOCKED", result.ErrorCode,
            "Existing RiskManager spread rejection should remain in force.");
        AssertEqual(0, mt5.OpenTradeCalls, "Old max-spread guard must block before broker execution.");
    }

    private static async Task InvalidSessionSpreadConfigBlocksLiveTrade()
    {
        var now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SessionSpreadConfig(defaultMaxSpreadPips: 10, oldMaxSpreadPips: 20,
                SpreadRule("Bad", "bad", "12:30", 2.0)),
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Invalid enabled session spread config must fail closed.");
        AssertEqual("SPREAD_SESSION_CONFIG_INVALID", result.ErrorCode,
            "Invalid session spread config should use the config-invalid code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Invalid session spread config must block before broker execution.");
    }

    private static async Task MissingSymbolDataWithSessionSpreadStillBlocksWithNoSymbolData()
    {
        var now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        await using var mt5 = new FakeMt5Server(Account(), symbol: null);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            SessionSpreadConfig(defaultMaxSpreadPips: 2.0, oldMaxSpreadPips: 20),
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Missing symbol/spread data must keep existing live fail-closed behavior.");
        AssertEqual("NO_SYMBOL_DATA", result.ErrorCode,
            "Missing symbol/spread data should not be replaced by session spread codes.");
        AssertEqual(0, mt5.OpenTradeCalls, "Missing symbol data must block before broker execution.");
    }

    private static async Task PaperModeIsSeparateFromSessionSpreadProtection()
    {
        var now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var config = SessionSpreadConfig(defaultMaxSpreadPips: 10, oldMaxSpreadPips: 20,
            SpreadRule("London", "11:30", "12:30", 0.5));
        config.PaperTrading = true;

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled(),
            utcNowProvider: () => now);

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Paper mode should stay separate from live session spread blocking.");
        AssertEqual(0, mt5.OpenTradeCalls, "Paper mode must not send broker orders.");
    }

    private static async Task OrderCheckPassAllowsLiveExecution()
    {
        await using var mt5 = new FakeMt5Server(
            Account(),
            Symbol(spreadPoints: 10),
            orderCheckResult: OrderCheckPass());
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Accepted broker OrderCheck should allow live execution to continue.");
        AssertEqual(1, mt5.OrderCheckCalls, "Live execution must run broker OrderCheck before send.");
        AssertEqual(1, mt5.OpenTradeCalls, "Live order should be sent only after OrderCheck accepts.");
    }

    private static async Task OrderCheckRejectionBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(
            Account(),
            Symbol(spreadPoints: 10),
            orderCheckResult: OrderCheckReject(retcode: 10016, comment: "invalid stops"));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Rejected broker OrderCheck must block live trading.");
        AssertEqual("BROKER_ORDERCHECK_REJECTED", result.ErrorCode,
            "OrderCheck rejection should use the broker-ordercheck rejection code.");
        AssertContains("10016", result.ErrorMessage);
        AssertContains("invalid stops", result.ErrorMessage);
        AssertEqual(1, mt5.OrderCheckCalls, "Rejected live trade should still record one OrderCheck attempt.");
        AssertEqual(0, mt5.OpenTradeCalls, "OrderCheck rejection must block before OPEN_TRADE.");
    }

    private static async Task OrderCheckUnavailableBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(
            Account(),
            Symbol(spreadPoints: 10),
            orderCheckAvailable: false);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when broker OrderCheck is unavailable.");
        AssertEqual("BROKER_ORDERCHECK_UNAVAILABLE", result.ErrorCode,
            "Unavailable OrderCheck should use the unavailable code.");
        AssertEqual(1, mt5.OrderCheckCalls, "Unavailable live OrderCheck should still be attempted once.");
        AssertEqual(0, mt5.OpenTradeCalls, "Unavailable OrderCheck must block before OPEN_TRADE.");
    }

    private static async Task OrderCheckUsesFinalValidatedLotSize()
    {
        await using var mt5 = new FakeMt5Server(
            Account(),
            Symbol(spreadPoints: 10),
            orderCheckResult: OrderCheckPass());
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "OrderCheck lot-size capture requires the live trade to proceed.");
        AssertClose(0.20, mt5.LastOrderCheckLots, 0.0001,
            "OrderCheck must use final risk-adjusted lot size, not the original requested lot size.");
        AssertEqual(1, mt5.OpenTradeCalls, "Accepted OrderCheck should allow broker execution.");
    }

    private static async Task PaperModeIsSeparateFromOrderCheck()
    {
        var config = Config();
        config.PaperTrading = true;

        await using var mt5 = new FakeMt5Server(
            Account(),
            Symbol(spreadPoints: 10),
            orderCheckAvailable: false);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Paper mode should stay separate from live broker OrderCheck.");
        AssertEqual(0, mt5.OrderCheckCalls, "Paper mode must not call broker OrderCheck.");
        AssertEqual(0, mt5.OpenTradeCalls, "Paper mode must not send broker orders.");
    }

    private static async Task SuccessfulFirstOrderSendStillWorks()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            OrderRetryConfig(maxRetries: 2),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Successful first broker send should still succeed.");
        AssertEqual(1, mt5.OpenTradeCalls, "Successful first send must not retry.");
        AssertEqual(1, result.OrderSendAttempts, "Attempt count should record one live send attempt.");
    }

    private static async Task RetryableTimeoutRetriesUpToConfiguredMax()
    {
        await using var mt5 = new FakeMt5Server(
            Account(),
            Symbol(spreadPoints: 10),
            openTradeResponses:
            [
                BrokerError("MT5_10031", "timeout"),
                BrokerError("MT5_10031", "timeout"),
                BrokerError("MT5_10031", "timeout")
            ]);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            OrderRetryConfig(maxRetries: 2),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Retryable timeout should fail after configured attempts are exhausted.");
        AssertEqual("ORDER_RETRY_EXHAUSTED", result.ErrorCode,
            "Final timeout failure should return clear retry-exhausted code.");
        AssertEqual(10031, (int)(result.BrokerRetcode ?? 0), "Broker retcode should be retained on final failure.");
        AssertEqual(3, mt5.OpenTradeCalls, "Max retries of 2 means 3 total send attempts.");
        AssertEqual(3, mt5.SymbolInfoCalls, "Each retry must re-fetch current symbol/spread data.");
    }

    private static async Task RequoteRetryStopsWhenCurrentSpreadFails()
    {
        await using var mt5 = new FakeMt5Server(
            Account(),
            Symbol(spreadPoints: 10),
            symbolSequence:
            [
                Symbol(spreadPoints: 10),
                Symbol(spreadPoints: 50)
            ],
            openTradeResponses: [BrokerError("MT5_10004", "requote")]);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            OrderRetryConfig(maxRetries: 2, maxSpreadPips: 3),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Requote retry must stop if refreshed spread no longer passes.");
        AssertEqual("RISK_BLOCKED", result.ErrorCode, "Second attempt should fail on refreshed spread/risk gate.");
        AssertEqual(1, mt5.OpenTradeCalls, "Spread failure on retry must block before a second broker send.");
        AssertEqual(2, mt5.SymbolInfoCalls, "Retry should re-fetch symbol data before deciding to resend.");
    }

    private static async Task RequoteRetrySucceedsWhenCurrentGatesStillPass()
    {
        await using var mt5 = new FakeMt5Server(
            Account(),
            Symbol(spreadPoints: 10),
            openTradeResponses:
            [
                BrokerError("MT5_10004", "requote"),
                BrokerSuccess()
            ]);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            OrderRetryConfig(maxRetries: 2),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Requote should retry and succeed when refreshed gates still pass.");
        AssertEqual(2, mt5.OpenTradeCalls, "Requote should perform one retry send.");
        AssertEqual(2, mt5.SymbolInfoCalls, "Requote retry should re-fetch symbol/spread data.");
    }

    private static async Task InvalidStopsDoesNotRetryOrderSend() =>
        await PermanentBrokerRejectDoesNotRetry("MT5_10016", "invalid stops", "ORDER_INVALID_STOPS");

    private static async Task NoMoneyDoesNotRetryOrderSend() =>
        await PermanentBrokerRejectDoesNotRetry("MT5_10019", "no money", "ORDER_NO_MONEY");

    private static async Task MarketClosedDoesNotRetryOrderSend() =>
        await PermanentBrokerRejectDoesNotRetry("MT5_10018", "market closed", "ORDER_MARKET_CLOSED");

    private static async Task TradeDisabledDoesNotRetryOrderSend() =>
        await PermanentBrokerRejectDoesNotRetry("MT5_10017", "trade disabled", "ORDER_TRADE_DISABLED");

    private static async Task PermanentBrokerRejectDoesNotRetry(string brokerCode, string brokerMessage, string expectedCode)
    {
        await using var mt5 = new FakeMt5Server(
            Account(),
            Symbol(spreadPoints: 10),
            openTradeResponses: [BrokerError(brokerCode, brokerMessage)]);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            OrderRetryConfig(maxRetries: 2),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, $"{expectedCode} should reject.");
        AssertEqual(expectedCode, result.ErrorCode, $"{expectedCode} should be classified explicitly.");
        AssertEqual(1, mt5.OpenTradeCalls, $"{expectedCode} must not retry.");
    }

    private static async Task OrderCheckRejectionDoesNotRetryOrderSend()
    {
        await using var mt5 = new FakeMt5Server(
            Account(),
            Symbol(spreadPoints: 10),
            orderCheckResult: OrderCheckReject(10016, "invalid stops"));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            OrderRetryConfig(maxRetries: 2),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "OrderCheck rejection must block live order send.");
        AssertEqual("BROKER_ORDERCHECK_REJECTED", result.ErrorCode,
            "OrderCheck rejection should keep its clear fail-closed code.");
        AssertEqual(1, mt5.OrderCheckCalls, "OrderCheck rejection must not retry.");
        AssertEqual(0, mt5.OpenTradeCalls, "OrderCheck rejection must block before OPEN_TRADE.");
    }

    private static async Task PaperModeDoesNotUseLiveOrderRetryPolicy()
    {
        var config = OrderRetryConfig(maxRetries: 2);
        config.PaperTrading = true;

        await using var mt5 = new FakeMt5Server(
            Account(),
            Symbol(spreadPoints: 10),
            openTradeResponses: [BrokerError("MT5_10031", "timeout")]);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Paper mode should simulate fills separately from live retry policy.");
        AssertEqual(0, mt5.OpenTradeCalls, "Paper mode must not send broker orders or retries.");
        AssertEqual(0, result.OrderSendAttempts, "Paper fills should not report live order-send attempts.");
    }

    private static Task BacktestLiveMismatchReportCanBeGenerated()
    {
        string folder = TestFolder();

        string path = BacktestLiveExecutionMismatchAudit.WriteReport(
            folder,
            new DateTime(2026, 5, 3, 0, 0, 0, DateTimeKind.Utc));
        string report = File.ReadAllText(path);

        AssertTrue(File.Exists(path), "Mismatch report file should be generated.");
        AssertEqual(
            BacktestLiveExecutionMismatchAudit.ReportFileName,
            Path.GetFileName(path),
            "Mismatch report should use the required file name.");
        AssertContains("Backtest/Live Execution Mismatch Report", report);
        AssertContains("trade-summary based", report);
        return Task.CompletedTask;
    }

    private static Task BacktestLiveMismatchReportMarksCommissionAndSlippagePresent()
    {
        string report = BacktestLiveExecutionMismatchAudit.GenerateMarkdown();

        AssertContains("| Commission | Present:", report);
        AssertContains("| Slippage | Present:", report);
        AssertContains("configured round-turn commission is deducted", report);
        AssertContains("configured fixed slippage cost is deducted", report);
        return Task.CompletedTask;
    }

    private static Task BacktestLiveMismatchReportMarksSpreadRealismMissing()
    {
        string report = BacktestLiveExecutionMismatchAudit.GenerateMarkdown();
        string spreadRow = ReportRow(report, "Spread");

        AssertContains("Missing/Unverified", spreadRow);
        AssertContains("bid-ask spread", spreadRow);
        AssertContains("Critical", spreadRow);
        return Task.CompletedTask;
    }

    private static Task BacktestLiveMismatchReportMarksIntrabarSlTpMissing()
    {
        string report = BacktestLiveExecutionMismatchAudit.GenerateMarkdown();
        string intrabarRow = ReportRow(report, "Intrabar SL/TP Behavior");

        AssertContains("Missing/Unverified", intrabarRow);
        AssertContains("cannot prove whether SL or TP was hit first", intrabarRow);
        AssertContains("Critical", intrabarRow);
        return Task.CompletedTask;
    }

    private static async Task ValidTickMarketDataCsvLoadsCorrectly()
    {
        string path = WriteTempCsv(
            "timestamp,symbol,bid,ask,volume",
            "2026-05-03T10:00:01Z,EURUSD,1.10000,1.10005,12",
            "2026-05-03T10:00:00Z,EURUSD,1.09990,1.09995,10");

        var rows = await new CsvBacktestTickDataLoader()
            .LoadAsync(path)
            .ConfigureAwait(false);

        AssertEqual(2, rows.Count, "Both valid ticks should load.");
        AssertEqual("EURUSD", rows[0].Symbol, "Symbol should normalize to uppercase.");
        AssertTrue(rows[0].TimestampUtc.Kind == DateTimeKind.Utc, "Tick timestamp should be UTC.");
        AssertClose(1.09990, rows[0].Bid, 0.0000001, "Rows should be sorted by timestamp.");
        AssertClose(1.09995, rows[0].Ask, 0.0000001, "Ask should load from CSV.");
        AssertClose(10, rows[0].Volume ?? 0, 0.0001, "Optional volume should load.");
    }

    private static async Task InvalidTickMarketDataCsvFailsClearly()
    {
        string path = WriteTempCsv(
            "timestamp,symbol,bid,ask",
            "2026-05-03T10:00:00Z,EURUSD,1.10010,1.10000");

        var ex = await AssertThrowsAsync<BacktestMarketDataLoadException>(
            () => new CsvBacktestTickDataLoader().LoadAsync(path),
            "Invalid bid/ask ordering should fail.");

        AssertContains("bid must be less than or equal to ask", ex.Message);
    }

    private static async Task ValidOhlcMarketDataCsvLoadsCorrectly()
    {
        string path = WriteTempCsv(
            "timestamp,symbol,timeframe,open,high,low,close,spread_pips,volume,bid_open,ask_open",
            "2026-05-03T10:00:00+00:00,GBPUSD,M1,1.25000,1.25100,1.24950,1.25050,0.8,100,1.24998,1.25002");

        var rows = await new CsvBacktestOhlcDataLoader()
            .LoadAsync(path)
            .ConfigureAwait(false);

        AssertEqual(1, rows.Count, "One valid candle should load.");
        AssertEqual("GBPUSD", rows[0].Symbol, "OHLC symbol should normalize to uppercase.");
        AssertEqual("M1", rows[0].Timeframe, "Timeframe should load.");
        AssertTrue(rows[0].TimestampUtc.Kind == DateTimeKind.Utc, "OHLC timestamp should be UTC.");
        AssertClose(1.25100, rows[0].High, 0.0000001, "High should load.");
        AssertClose(0.8, rows[0].SpreadPips ?? 0, 0.0001, "Optional spread should load.");
        AssertClose(1.24998, rows[0].BidOpen ?? 0, 0.0000001, "Optional bid open should load.");
        AssertClose(1.25002, rows[0].AskOpen ?? 0, 0.0000001, "Optional ask open should load.");
    }

    private static async Task InvalidOhlcMarketDataCsvFailsClearly()
    {
        string path = WriteTempCsv(
            "timestamp,symbol,timeframe,open,high,low,close",
            "2026-05-03T10:00:00Z,GBPUSD,M1,1.25200,1.25100,1.24950,1.25050");

        var ex = await AssertThrowsAsync<BacktestMarketDataLoadException>(
            () => new CsvBacktestOhlcDataLoader().LoadAsync(path),
            "Open outside OHLC range should fail.");

        AssertContains("open and close must be inside low/high range", ex.Message);
    }

    private static async Task MarketDataCsvIsSortedByUtcTimestamp()
    {
        string path = WriteTempCsv(
            "timestamp,symbol,bid,ask",
            "2026-05-03T10:00:02Z,EURUSD,1.10020,1.10025",
            "2026-05-03T10:00:00Z,EURUSD,1.10000,1.10005",
            "2026-05-03T10:00:01Z,EURUSD,1.10010,1.10015");

        var rows = await new CsvBacktestTickDataLoader()
            .LoadAsync(path)
            .ConfigureAwait(false);

        AssertClose(1.10000, rows[0].Bid, 0.0000001, "Oldest tick should be first.");
        AssertClose(1.10010, rows[1].Bid, 0.0000001, "Middle tick should be second.");
        AssertClose(1.10020, rows[2].Bid, 0.0000001, "Newest tick should be last.");
    }

    private static async Task DuplicateMarketDataTimestampsAreRejected()
    {
        string path = WriteTempCsv(
            "timestamp,symbol,bid,ask",
            "2026-05-03T10:00:00Z,EURUSD,1.10000,1.10005",
            "2026-05-03T10:00:00Z,EURUSD,1.10001,1.10006");

        var ex = await AssertThrowsAsync<BacktestMarketDataLoadException>(
            () => new CsvBacktestTickDataLoader().LoadAsync(path),
            "Duplicate tick timestamps should be rejected.");

        AssertContains("Duplicate tick timestamp", ex.Message);
    }

    private static async Task MarketDataSymbolFilteringWorks()
    {
        string path = WriteTempCsv(
            "timestamp,symbol,timeframe,open,high,low,close",
            "2026-05-03T10:00:00Z,EURUSD,M1,1.10000,1.10100,1.09950,1.10050",
            "2026-05-03T10:00:00Z,GBPUSD,M1,1.25000,1.25100,1.24950,1.25050");

        var rows = await new CsvBacktestOhlcDataLoader()
            .LoadAsync(path, symbolFilter: "gbpusd")
            .ConfigureAwait(false);

        AssertEqual(1, rows.Count, "Symbol filter should include only matching candles.");
        AssertEqual("GBPUSD", rows[0].Symbol, "Symbol filter should be case-insensitive.");
    }

    private static Task BacktestSpreadCostIsCalculatedFromTickBidAsk()
    {
        var tick = new BacktestTick
        {
            TimestampUtc = DateTime.UtcNow,
            Symbol = "EURUSD",
            Bid = 1.10000,
            Ask = 1.10010
        };

        var result = BacktestExecutionCostModel.EstimateFromTick(
            tick,
            TradeType.BUY,
            lotSize: 0.20,
            entryPrice: 1.10010,
            exitPrice: 1.10100,
            Config());

        AssertTrue(result.Success, "Valid tick bid/ask spread should produce a successful cost estimate.");
        AssertClose(1.0, result.SpreadPips ?? 0, 0.0001, "One pip spread should be derived from bid/ask.");
        AssertClose(2.00, result.SpreadCostUsd, 0.0001, "EURUSD 1 pip spread at 0.20 lots should cost $2.");
        return Task.CompletedTask;
    }

    private static Task BacktestSpreadCostIsCalculatedFromOhlcConfiguredSpread()
    {
        var candle = new BacktestOhlcCandle
        {
            TimestampUtc = DateTime.UtcNow,
            Symbol = "EURUSD",
            Timeframe = "M1",
            Open = 1.1000,
            High = 1.1010,
            Low = 1.0990,
            Close = 1.1005
        };

        var result = BacktestExecutionCostModel.EstimateFromOhlc(
            candle,
            TradeType.BUY,
            lotSize: 0.20,
            entryPrice: 1.1000,
            exitPrice: 1.1005,
            Config(),
            configuredSpreadPips: 1.5);

        AssertTrue(result.Success, "Configured OHLC spread should produce a successful cost estimate.");
        AssertClose(1.5, result.SpreadPips ?? 0, 0.0001, "Configured spread should be used for OHLC data.");
        AssertClose(3.00, result.SpreadCostUsd, 0.0001, "EURUSD 1.5 pip spread at 0.20 lots should cost $3.");
        return Task.CompletedTask;
    }

    private static Task BacktestCommissionCostIsIncludedWhenEnabled()
    {
        var result = BacktestExecutionCostModel.Estimate(CostInput(
            CommissionConfig(commissionPerLotPerSide: 3.50),
            spreadPips: 1.0));

        AssertTrue(result.Success, "Commission-enabled cost estimate should succeed with spread data.");
        AssertClose(1.40, result.CommissionCostUsd, 0.0001,
            "0.20 lots at $3.50 per side should cost $1.40 round-turn.");
        return Task.CompletedTask;
    }

    private static Task BacktestSlippageCostIsIncludedWhenEnabled()
    {
        var result = BacktestExecutionCostModel.Estimate(CostInput(
            SlippageConfig(estimatedSlippagePips: 1.0, maxAllowedSlippagePips: 3.0),
            spreadPips: 1.0));

        AssertTrue(result.Success, "Slippage-enabled cost estimate should succeed with spread data.");
        AssertClose(2.00, result.SlippageCostUsd, 0.0001,
            "EURUSD 1 pip slippage at 0.20 lots should cost $2.");
        return Task.CompletedTask;
    }

    private static Task BacktestTotalExecutionCostSumsComponents()
    {
        var config = CommissionConfig(commissionPerLotPerSide: 3.50);
        config.EnableSlippageModel = true;
        config.EstimatedSlippagePips = 1.0;
        config.MaxAllowedSlippagePips = 3.0;

        var result = BacktestExecutionCostModel.Estimate(CostInput(config, spreadPips: 1.5));

        AssertTrue(result.Success, "Complete cost input should succeed.");
        AssertClose(
            result.SpreadCostUsd + result.CommissionCostUsd + result.SlippageCostUsd,
            result.TotalCostUsd,
            0.0001,
            "Total cost should equal spread + commission + slippage.");
        AssertClose(6.40, result.TotalCostUsd, 0.0001,
            "Expected $3 spread + $1.40 commission + $2 slippage.");
        return Task.CompletedTask;
    }

    private static Task BacktestMissingSpreadDataReturnsClearWarning()
    {
        var result = BacktestExecutionCostModel.Estimate(CostInput(Config()));

        AssertFalse(result.Success, "Missing spread data should mark cost estimate unsuccessful.");
        AssertTrue(result.MissingDataFlags.Contains("SPREAD"), "Missing-data flags should include SPREAD.");
        AssertContains("Spread data is unavailable", string.Join(" ", result.Warnings));
        return Task.CompletedTask;
    }

    private static Task BacktestDisabledCommissionAndSlippageAreZeroCost()
    {
        var config = Config();
        config.EnableCommissionModel = false;
        config.EnableSlippageModel = false;
        config.CommissionPerLotPerSide = 99;
        config.EstimatedSlippagePips = 99;

        var result = BacktestExecutionCostModel.Estimate(CostInput(config, spreadPips: 1.0));

        AssertTrue(result.Success, "Disabled commission/slippage should preserve valid spread-only estimate.");
        AssertClose(0, result.CommissionCostUsd, 0.0001, "Disabled commission should produce zero commission cost.");
        AssertClose(0, result.SlippageCostUsd, 0.0001, "Disabled slippage should produce zero slippage cost.");
        AssertClose(result.SpreadCostUsd, result.TotalCostUsd, 0.0001,
            "Total should equal spread-only cost when commission/slippage are disabled.");
        return Task.CompletedTask;
    }

    private static Task BuyTickIntrabarExitsAtTpFirst()
    {
        var result = IntrabarExitSimulator.SimulateTickExit(
            TradeType.BUY,
            stopLoss: 1.0950,
            takeProfit: 1.1050,
            TickSeries(
                (0, 1.1000, 1.1002),
                (1, 1.1051, 1.1053),
                (2, 1.0949, 1.0951)));

        AssertExit(result, IntrabarExitType.TakeProfit, 1.1050, "BUY tick should hit TP first.");
        AssertFalse(result.IsAmbiguous, "Tick mode first-hit result should not be ambiguous.");
        return Task.CompletedTask;
    }

    private static Task BuyTickIntrabarExitsAtSlFirst()
    {
        var result = IntrabarExitSimulator.SimulateTickExit(
            TradeType.BUY,
            stopLoss: 1.0950,
            takeProfit: 1.1050,
            TickSeries(
                (0, 1.1000, 1.1002),
                (1, 1.0949, 1.0951),
                (2, 1.1051, 1.1053)));

        AssertExit(result, IntrabarExitType.StopLoss, 1.0950, "BUY tick should hit SL first.");
        return Task.CompletedTask;
    }

    private static Task SellTickIntrabarExitsAtTpFirst()
    {
        var result = IntrabarExitSimulator.SimulateTickExit(
            TradeType.SELL,
            stopLoss: 1.1050,
            takeProfit: 1.0950,
            TickSeries(
                (0, 1.1000, 1.1002),
                (1, 1.0947, 1.0949),
                (2, 1.1051, 1.1053)));

        AssertExit(result, IntrabarExitType.TakeProfit, 1.0950, "SELL tick should hit TP first.");
        return Task.CompletedTask;
    }

    private static Task SellTickIntrabarExitsAtSlFirst()
    {
        var result = IntrabarExitSimulator.SimulateTickExit(
            TradeType.SELL,
            stopLoss: 1.1050,
            takeProfit: 1.0950,
            TickSeries(
                (0, 1.1000, 1.1002),
                (1, 1.1049, 1.1051),
                (2, 1.0947, 1.0949)));

        AssertExit(result, IntrabarExitType.StopLoss, 1.1050, "SELL tick should hit SL first.");
        return Task.CompletedTask;
    }

    private static Task OhlcBuyOnlySlHitExitsAtSl()
    {
        var result = IntrabarExitSimulator.SimulateOhlcExit(
            TradeType.BUY,
            stopLoss: 1.0950,
            takeProfit: 1.1050,
            Candle(low: 1.0949, high: 1.1040));

        AssertExit(result, IntrabarExitType.StopLoss, 1.0950, "OHLC BUY should exit at SL when only SL is hit.");
        AssertFalse(result.IsAmbiguous, "Only one OHLC exit level hit should not be ambiguous.");
        return Task.CompletedTask;
    }

    private static Task OhlcBuyOnlyTpHitExitsAtTp()
    {
        var result = IntrabarExitSimulator.SimulateOhlcExit(
            TradeType.BUY,
            stopLoss: 1.0950,
            takeProfit: 1.1050,
            Candle(low: 1.0960, high: 1.1051));

        AssertExit(result, IntrabarExitType.TakeProfit, 1.1050, "OHLC BUY should exit at TP when only TP is hit.");
        return Task.CompletedTask;
    }

    private static Task OhlcSellOnlySlHitExitsAtSl()
    {
        var result = IntrabarExitSimulator.SimulateOhlcExit(
            TradeType.SELL,
            stopLoss: 1.1050,
            takeProfit: 1.0950,
            Candle(low: 1.0960, high: 1.1051));

        AssertExit(result, IntrabarExitType.StopLoss, 1.1050, "OHLC SELL should exit at SL when only SL is hit.");
        return Task.CompletedTask;
    }

    private static Task OhlcSellOnlyTpHitExitsAtTp()
    {
        var result = IntrabarExitSimulator.SimulateOhlcExit(
            TradeType.SELL,
            stopLoss: 1.1050,
            takeProfit: 1.0950,
            Candle(low: 1.0949, high: 1.1040));

        AssertExit(result, IntrabarExitType.TakeProfit, 1.0950, "OHLC SELL should exit at TP when only TP is hit.");
        return Task.CompletedTask;
    }

    private static Task OhlcSameCandleBothSlAndTpUsesSlFirst()
    {
        var result = IntrabarExitSimulator.SimulateOhlcExit(
            TradeType.BUY,
            stopLoss: 1.0950,
            takeProfit: 1.1050,
            Candle(low: 1.0949, high: 1.1051));

        AssertExit(result, IntrabarExitType.StopLoss, 1.0950,
            "OHLC same-candle SL/TP ambiguity should use conservative SL-first.");
        AssertTrue(result.IsAmbiguous, "Same-candle SL/TP hit should set ambiguity flag.");
        AssertContains("conservative SL-first", result.Explanation);
        return Task.CompletedTask;
    }

    private static Task IntrabarNoSlOrTpHitRemainsOpen()
    {
        var result = IntrabarExitSimulator.SimulateOhlcExit(
            TradeType.BUY,
            stopLoss: 1.0950,
            takeProfit: 1.1050,
            Candle(low: 1.0960, high: 1.1040));

        AssertFalse(result.ExitTriggered, "No SL/TP hit should remain open.");
        AssertEqual("None", result.ExitType.ToString(), "Open result should use None exit type.");
        AssertClose(0, result.ExitPrice, 0.0001, "Open result should not carry an exit price.");
        AssertTrue(result.ExitTimestampUtc == null, "Open result should not carry an exit timestamp.");
        return Task.CompletedTask;
    }

    private static Task OhlcAmbiguousResultIncludesAmbiguityFlag()
    {
        var result = IntrabarExitSimulator.SimulateOhlcExit(
            TradeType.SELL,
            stopLoss: 1.1050,
            takeProfit: 1.0950,
            Candle(low: 1.0949, high: 1.1051));

        AssertTrue(result.IsAmbiguous, "SELL same-candle SL/TP hit should set ambiguity flag.");
        AssertExit(result, IntrabarExitType.StopLoss, 1.1050,
            "SELL same-candle ambiguity should also use conservative SL-first.");
        return Task.CompletedTask;
    }

    private static Task BacktestBrokerRuleSimulationAllowsValidTrade()
    {
        var result = BacktestBrokerRuleSimulator.Validate(BrokerRuleInput());

        AssertTrue(result.Approved, "Valid backtest trade should pass broker-rule simulation.");
        AssertEqual("", result.RejectionCode, "Approved result should not carry a rejection code.");
        AssertClose(0.10, result.ValidatedLotSize, 0.0001, "Result should preserve the validated lot size.");
        AssertTrue(result.Warnings.Count == 0, "Fully supplied broker-rule input should not warn.");
        return Task.CompletedTask;
    }

    private static Task BacktestBrokerRuleSimulationRejectsStopLevelViolation()
    {
        var result = BacktestBrokerRuleSimulator.Validate(BrokerRuleInput(stopLoss: 1.09980));

        AssertBrokerRuleReject(result, "BACKTEST_BROKER_STOP_LEVEL", "stop level");
        return Task.CompletedTask;
    }

    private static Task BacktestBrokerRuleSimulationRejectsFreezeLevelViolation()
    {
        var result = BacktestBrokerRuleSimulator.Validate(BrokerRuleInput(
            stopLoss: 1.09980,
            symbolInfo: BacktestBrokerSymbol(stopLevelPoints: 10, freezeLevelPoints: 50)));

        AssertBrokerRuleReject(result, "BACKTEST_BROKER_FREEZE_LEVEL", "freeze level");
        return Task.CompletedTask;
    }

    private static Task BacktestBrokerRuleSimulationRejectsLotBelowMinimum()
    {
        var result = BacktestBrokerRuleSimulator.Validate(BrokerRuleInput(lotSize: 0.001));

        AssertBrokerRuleReject(result, "BACKTEST_BROKER_LOT_MIN", "below broker minimum");
        return Task.CompletedTask;
    }

    private static Task BacktestBrokerRuleSimulationRejectsLotAboveMaximum()
    {
        var result = BacktestBrokerRuleSimulator.Validate(BrokerRuleInput(lotSize: 2.00));

        AssertBrokerRuleReject(result, "BACKTEST_BROKER_LOT_MAX", "above broker maximum");
        return Task.CompletedTask;
    }

    private static Task BacktestBrokerRuleSimulationRejectsLotStepViolation()
    {
        var result = BacktestBrokerRuleSimulator.Validate(BrokerRuleInput(lotSize: 0.105));

        AssertBrokerRuleReject(result, "BACKTEST_BROKER_LOT_STEP", "lot step");
        return Task.CompletedTask;
    }

    private static Task BacktestBrokerRuleSimulationRejectsVolumeLimitViolation()
    {
        var result = BacktestBrokerRuleSimulator.Validate(BrokerRuleInput(existingLots: 0.10));

        AssertBrokerRuleReject(result, "BACKTEST_BROKER_VOLUME_LIMIT", "volume limit");
        return Task.CompletedTask;
    }

    private static Task BacktestBrokerRuleSimulationRejectsInsufficientMargin()
    {
        var result = BacktestBrokerRuleSimulator.Validate(BrokerRuleInput(
            margin: new BacktestBrokerMarginInput
            {
                AccountEquity = 1_000,
                CurrentUsedMargin = 900,
                EstimatedRequiredMargin = 200,
                MinProjectedMarginLevelPercent = 100
            }));

        AssertBrokerRuleReject(result, "BACKTEST_MARGIN_LEVEL_LIMIT", "Projected margin level");
        AssertClose(200, result.EstimatedRequiredMargin.GetValueOrDefault(), 0.0001,
            "Rejected margin result should include estimated required margin.");
        AssertTrue(result.ProjectedMarginLevelPercent.HasValue,
            "Rejected margin result should include projected margin level.");
        return Task.CompletedTask;
    }

    private static Task BacktestBrokerRuleSimulationFailsClearlyOnMissingMetadata()
    {
        var result = BacktestBrokerRuleSimulator.Validate(BrokerRuleInput(omitSymbolInfo: true));

        AssertBrokerRuleReject(result, "BACKTEST_BROKER_RULE_DATA_UNAVAILABLE", "Symbol metadata");
        return Task.CompletedTask;
    }

    private static Task BacktestBrokerRuleSimulationRejectsSimulatedOrderCheckFailure()
    {
        var result = BacktestBrokerRuleSimulator.Validate(BrokerRuleInput(
            orderCheck: new OrderCheckResult
            {
                IsAccepted = false,
                Retcode = 10016,
                Comment = "invalid stops"
            }));

        AssertBrokerRuleReject(result, "BACKTEST_ORDERCHECK_REJECTED", "invalid stops");
        return Task.CompletedTask;
    }

    private static async Task MaxSpreadFilterBlocksHighSpread()
    {
        var result = await NewRiskManager().ValidateAsync(
            BuyRequest(),
            Account(),
            Symbol(spreadPoints: 50),
            [],
            Config(maxSpreadPips: 3)).ConfigureAwait(false);

        AssertFalse(result.IsApproved, "High spread should be blocked.");
        AssertContains("spread", result.Reason);
    }

    private static Task StopLossPlacementRejectsWrongSide()
    {
        var badBuy = BuyRequest(sl: 1.1010);
        var (buyValid, buyError) = badBuy.Validate();
        AssertFalse(buyValid, "BUY with SL above entry should be invalid.");
        AssertContains("StopLoss", buyError);

        var badSell = SellRequest(sl: 1.0990);
        var (sellValid, sellError) = badSell.Validate();
        AssertFalse(sellValid, "SELL with SL below entry should be invalid.");
        AssertContains("StopLoss", sellError);

        return Task.CompletedTask;
    }

    private static Task TakeProfitPlacementRejectsWrongSide()
    {
        var badBuy = BuyRequest(tp: 1.0990);
        var (buyValid, buyError) = badBuy.Validate();
        AssertFalse(buyValid, "BUY with TP below entry should be invalid.");
        AssertContains("TakeProfit", buyError);

        var badSell = SellRequest(tp: 1.1010);
        var (sellValid, sellError) = badSell.Validate();
        AssertFalse(sellValid, "SELL with TP above entry should be invalid.");
        AssertContains("TakeProfit", sellError);

        return Task.CompletedTask;
    }

    private static async Task TradeExecutionHandlesRejections()
    {
        var service = new TradeExecutionService(Bridge());

        var invalid = BuyRequest(sl: 1.1010);
        var invalidResult = await service.ExecuteAsync(
            invalid,
            ApprovedRisk(),
            ApprovedUser()).ConfigureAwait(false);
        AssertEqual("VALIDATION", invalidResult.ErrorCode, "Invalid order should be rejected before broker call.");

        var riskBlocked = await service.ExecuteAsync(
            BuyRequest(),
            new RiskValidationResult { IsApproved = false, Reason = "risk blocked" },
            ApprovedUser()).ConfigureAwait(false);
        AssertEqual("RISK_BLOCKED", riskBlocked.ErrorCode, "Risk rejection should be preserved.");

        var approvalBlocked = await service.ExecuteAsync(
            BuyRequest(),
            ApprovedRisk(),
            new UserApprovalDecision { IsApproved = false, Notes = "operator denied" }).ConfigureAwait(false);
        AssertEqual("USER_APPROVAL_REQUIRED", approvalBlocked.ErrorCode, "User denial should block execution.");
    }

    private static Task NoDirectLiveOrderBypassExists()
    {
        string root = FindRepoRoot();
        string forbiddenCall = "." + "Open" + "TradeAsync(";
        string allowed = Path.GetFullPath(Path.Combine(
            root,
            "Trading",
            "TradeExecution",
            "TradeExecutionService.cs"));

        var offenders = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsUnder(path, Path.Combine(root, "Tests")))
            .Where(path => !IsUnder(path, Path.Combine(root, "bin")))
            .Where(path => !IsUnder(path, Path.Combine(root, "obj")))
            .Where(path => !PathsEqual(path, allowed))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = path,
                    Line = index + 1,
                    Text = line.Trim()
                }))
            .Where(hit => hit.Text.Contains(forbiddenCall, StringComparison.Ordinal))
            .Select(hit => $"{Path.GetRelativePath(root, hit.Path)}:{hit.Line}: {hit.Text}")
            .ToList();

        if (offenders.Count > 0)
            throw new InvalidOperationException(
                "Direct live MT5 order bypass found outside TradeExecutionService: " +
                string.Join(" | ", offenders));

        return Task.CompletedTask;
    }

    private static async Task MissingAccountBlocksLiveTrade()
    {
        await using var bot = new AutoBotService(
            Bridge(),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when account data is missing.");
        AssertEqual("NO_ACCOUNT", result.ErrorCode, "Missing account data should use the safety rejection code.");
    }

    private static async Task MissingSymbolDataBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), symbol: null);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when symbol/spread data is missing.");
        AssertEqual("NO_SYMBOL_DATA", result.ErrorCode, "Missing symbol/spread data should use the safety rejection code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Fail-closed guard must prevent broker execution.");
    }

    private static async Task NewsUnavailableBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            newsCalendar: new UnavailableNewsCalendar(),
            apiConfig: NewsRequired());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when required news data is unavailable.");
        AssertEqual("NEWS_UNAVAILABLE", result.ErrorCode, "Unavailable news should use the safety rejection code.");
        AssertEqual(0, mt5.OpenTradeCalls, "News guard must prevent broker execution.");
    }

    private static async Task RiskManagerExceptionBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled(),
            riskManager: new ThrowingRiskManager());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when risk validation throws.");
        AssertEqual("RISK_DATA_UNAVAILABLE", result.ErrorCode, "Risk exceptions should use the safety rejection code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Risk guard must prevent broker execution.");
    }

    private static async Task IncompleteRiskResultBlocksLiveTrade()
    {
        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            Config(),
            apiConfig: NewsDisabled(),
            riskManager: new IncompleteRiskManager());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Live trade must fail closed when approved risk data is incomplete.");
        AssertEqual("RISK_DATA_UNAVAILABLE", result.ErrorCode, "Incomplete risk data should use the safety rejection code.");
        AssertEqual(0, mt5.OpenTradeCalls, "Incomplete risk guard must prevent broker execution.");
    }

    private static async Task PaperModeAllowsMissingSymbolDataSeparately()
    {
        var config = Config();
        config.PaperTrading = true;

        await using var mt5 = new FakeMt5Server(Account(), symbol: null);
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsRequired());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertTrue(result.IsSuccess, "Paper mode should stay explicitly separate from live symbol fail-closed behavior.");
        AssertEqual(0, mt5.OpenTradeCalls, "Paper mode must not send broker orders.");
    }

    private static async Task DuplicateSignalRegistryPersistsIds()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);

        await using (var bot = new AutoBotService(Bridge(), ConfigWithFolder(folder)))
        {
            InvokePrivate(bot, "RecordProcessedId", "DUP123");
        }

        await using var loadedBot = new AutoBotService(Bridge(), ConfigWithFolder(folder));
        InvokePrivate(loadedBot, "LoadProcessedIds");

        var ids = GetPrivateField<Dictionary<string, DateTime>>(loadedBot, "_processedIds");
        AssertTrue(ids.ContainsKey("DUP123"), "Processed signal id should reload from processed_ids.txt.");
    }

    private static async Task MaximumOpenTradeLimitBlocksAtCap()
    {
        var open = new List<LivePosition>
        {
            Position(ticket: 1),
            Position(ticket: 2),
            Position(ticket: 3)
        };

        var result = await NewRiskManager().ValidateAsync(
            BuyRequest(),
            Account(),
            Symbol(spreadPoints: 10),
            open,
            Config(maxConcurrentPositions: 3)).ConfigureAwait(false);

        AssertFalse(result.IsApproved, "Max open trade cap should block at configured limit.");
        AssertContains("max 3", result.Reason);
    }

    private static Task DisabledSessionBlocksScalpingDecision()
    {
        var closedMarket = JObject.Parse("""
        {
          "session": { "market_open": false },
          "symbol": { "trade_allowed": true }
        }
        """);

        var decision = EvaluateScalpingSnapshot(closedMarket);
        AssertFalse(decision.Approved, "Closed market should block scalping decision.");
        AssertContains("closed", decision.Reason);

        var disabledTrading = JObject.Parse("""
        {
          "session": { "market_open": true },
          "symbol": { "trade_allowed": false }
        }
        """);

        decision = EvaluateScalpingSnapshot(disabledTrading);
        AssertFalse(decision.Approved, "Trading-disabled symbol should block scalping decision.");
        AssertContains("disabled", decision.Reason);

        return Task.CompletedTask;
    }

    private static RiskManager NewRiskManager() => new();

    private static AccountInfo Account(
        double balance = 10_000,
        double equity = 10_000,
        double margin = 0,
        double? marginLevel = null,
        double? freeMargin = null) => new()
    {
        AccountNumber = 123,
        Balance = balance,
        Equity = equity,
        Margin = margin,
        FreeMargin = freeMargin ?? (margin > 0 ? Math.Max(0, equity - margin) : equity * 0.9),
        MarginLevel = marginLevel ?? (margin > 0 ? equity / margin * 100.0 : 1_000),
        IsConnected = true,
        Leverage = 200
    };

    private static SymbolInfo Symbol(double spreadPoints) => new()
    {
        Symbol = "EURUSD",
        Ask = 1.10005,
        Bid = 1.09995,
        Spread = spreadPoints,
        Digits = 5,
        MinLot = 0.01,
        MaxLot = 100,
        LotStep = 0.01,
        VolumeLimit = 0,
        PointSize = 0.00001,
        StopLevelPoints = 10,
        FreezeLevelPoints = 10
    };

    private static SymbolInfo SymbolAt(double bid, double ask) => new()
    {
        Symbol = "EURUSD",
        Ask = ask,
        Bid = bid,
        Spread = Math.Max(0, (ask - bid) / 0.00001),
        Digits = 5,
        MinLot = 0.01,
        MaxLot = 100,
        LotStep = 0.01,
        VolumeLimit = 0,
        PointSize = 0.00001,
        StopLevelPoints = 10,
        FreezeLevelPoints = 10
    };

    private static SymbolInfo SymbolWithStopLevel(double stopLevelPoints, double bid = 1.09995, double ask = 1.10005)
    {
        var symbol = SymbolAt(bid, ask);
        symbol.StopLevelPoints = stopLevelPoints;
        return symbol;
    }

    private static SymbolInfo SymbolWithFreezeLevel(double freezeLevelPoints, double bid = 1.09995, double ask = 1.10005)
    {
        var symbol = SymbolAt(bid, ask);
        symbol.FreezeLevelPoints = freezeLevelPoints;
        return symbol;
    }

    private static SymbolInfo SymbolWithLotRules(
        double minLot,
        double maxLot,
        double lotStep,
        double volumeLimit = 0)
    {
        var symbol = SymbolAt(1.09995, 1.10005);
        symbol.MinLot = minLot;
        symbol.MaxLot = maxLot;
        symbol.LotStep = lotStep;
        symbol.VolumeLimit = volumeLimit;
        return symbol;
    }

    private static SymbolInfo SymbolWithoutStopLevelMetadata() => new()
    {
        Symbol = "EURUSD",
        Ask = 1.10005,
        Bid = 1.09995,
        Spread = 10,
        Digits = 5,
        MinLot = 0.01,
        MaxLot = 100,
        LotStep = 0.01,
        VolumeLimit = 0,
        PointSize = 0.00001,
        FreezeLevelPoints = 10
    };

    private static SymbolInfo SymbolWithoutFreezeLevelMetadata() => new()
    {
        Symbol = "EURUSD",
        Ask = 1.10005,
        Bid = 1.09995,
        Spread = 10,
        Digits = 5,
        MinLot = 0.01,
        MaxLot = 100,
        LotStep = 0.01,
        VolumeLimit = 0,
        PointSize = 0.00001,
        StopLevelPoints = 10
    };

    private static SymbolInfo SymbolWithoutLotMetadata() => new()
    {
        Symbol = "EURUSD",
        Ask = 1.10005,
        Bid = 1.09995,
        Spread = 10,
        Digits = 5,
        MinLot = 0.01,
        MaxLot = 100,
        VolumeLimit = 0,
        PointSize = 0.00001,
        StopLevelPoints = 10,
        FreezeLevelPoints = 10
    };

    private static BotConfig Config(
        int maxTradesPerDay = 5,
        double maxSpreadPips = 3,
        int maxConcurrentPositions = 3)
    {
        string folder = TestFolder();
        return new BotConfig
        {
            WatchFolder = folder,
            KillSwitchStateFile = Path.Combine(folder, "kill_switch.json"),
            MaxRiskPercent = 1,
            MaxTradesPerDay = maxTradesPerDay,
            MaxSpreadPips = maxSpreadPips,
            MaxConcurrentPositions = maxConcurrentPositions,
            MaxTotalRiskPercent = 0,
            MinRRRatio = 1.5,
            EnforceRR = true,
            AutoLotCalculation = true,
            MagicNumber = 999001,
            RetryOnFail = false
        };
    }

    private static BotConfig ConfigWithFolder(
        string folder,
        int maxTradesPerDay = 5,
        double maxSpreadPips = 3,
        int maxConcurrentPositions = 3)
    {
        var config = Config(maxTradesPerDay, maxSpreadPips, maxConcurrentPositions);
        config.WatchFolder = folder;
        config.KillSwitchStateFile = Path.Combine(folder, "kill_switch.json");
        return config;
    }

    private static BotConfig OrderRetryConfig(int maxRetries, double maxSpreadPips = 3)
    {
        var config = Config(maxSpreadPips: maxSpreadPips);
        config.EnableOrderRetryPolicy = true;
        config.MaxOrderSendRetries = maxRetries;
        config.OrderRetryDelayMs = 1;
        config.RetryOnFail = true;
        return config;
    }

    private static BotConfig KillSwitchConfigWithFolder(string folder)
    {
        var config = ConfigWithFolder(folder);
        config.DrawdownProtectionEnabled = true;
        config.EmergencyCloseDrawdownPct = 10;
        return config;
    }

    private static BotConfig DailyLossConfig(
        double maxDailyLossAmount = 0,
        double maxDailyLossPercent = 0)
    {
        var config = Config();
        config.EnableDailyLossLimit = true;
        config.MaxDailyLossAmount = maxDailyLossAmount;
        config.MaxDailyLossPercent = maxDailyLossPercent;
        return config;
    }

    private static BotConfig WeeklyLossConfig(
        double maxWeeklyLossAmount = 0,
        double maxWeeklyLossPercent = 0)
    {
        var config = Config();
        config.EnableWeeklyLossLimit = true;
        config.MaxWeeklyLossAmount = maxWeeklyLossAmount;
        config.MaxWeeklyLossPercent = maxWeeklyLossPercent;
        return config;
    }

    private static BotConfig SymbolExposureConfig(
        double maxSymbolLots = 0,
        double maxSymbolRiskPercent = 0,
        int maxSameSymbolPositions = 0,
        bool blockOppositeSymbolExposure = false)
    {
        var config = Config();
        config.EnableSymbolExposureLimit = true;
        config.MaxSymbolLots = maxSymbolLots;
        config.MaxSymbolRiskPercent = maxSymbolRiskPercent;
        config.MaxSameSymbolPositions = maxSameSymbolPositions;
        config.BlockOppositeSymbolExposure = blockOppositeSymbolExposure;
        return config;
    }

    private static BotConfig MarginConfig(double minProjectedMarginLevelPercent)
    {
        var config = Config();
        config.EnableProjectedMarginValidation = true;
        config.MinProjectedMarginLevelPercent = minProjectedMarginLevelPercent;
        return config;
    }

    private static BotConfig CommissionConfig(
        double commissionPerLotPerSide,
        string commissionMode = "PerSide")
    {
        var config = Config();
        config.EnableCommissionModel = true;
        config.CommissionPerLotPerSide = commissionPerLotPerSide;
        config.CommissionCurrency = "USD";
        config.CommissionMode = commissionMode;
        return config;
    }

    private static BotConfig SlippageConfig(
        double estimatedSlippagePips,
        double maxAllowedSlippagePips,
        string slippageMode = "Fixed")
    {
        var config = Config();
        config.EnableSlippageModel = true;
        config.EstimatedSlippagePips = estimatedSlippagePips;
        config.MaxAllowedSlippagePips = maxAllowedSlippagePips;
        config.SlippageMode = slippageMode;
        return config;
    }

    private static BotConfig RolloverConfig(string startUtc, string endUtc)
    {
        var config = Config();
        config.EnableRolloverNoTradeWindow = true;
        config.RolloverWindowStartUtc = startUtc;
        config.RolloverWindowEndUtc = endUtc;
        return config;
    }

    private static BotConfig SessionSpreadConfig(
        double defaultMaxSpreadPips,
        double oldMaxSpreadPips,
        params SessionSpreadRuleConfig[] rules)
    {
        var config = Config(maxSpreadPips: oldMaxSpreadPips);
        config.EnableSessionSpreadProtection = true;
        config.DefaultMaxSpreadPips = defaultMaxSpreadPips;
        config.SessionSpreadRules.AddRange(rules);
        return config;
    }

    private static SessionSpreadRuleConfig SpreadRule(
        string name,
        string startUtc,
        string endUtc,
        double maxSpreadPips) => new()
    {
        Name = name,
        StartUtc = startUtc,
        EndUtc = endUtc,
        MaxSpreadPips = maxSpreadPips
    };

    private static ApiIntegrationConfig NewsDisabled() => new()
    {
        NewsProvider = "None"
    };

    private static ApiIntegrationConfig NewsRequired() => new()
    {
        NewsProvider = "Financial Modeling Prep",
        BlockTradesWhenNewsUnavailable = false
    };

    private static TradeRequest BuyRequest(
        double entry = 1.1000,
        double sl = 1.0950,
        double tp = 1.1100) => new()
    {
        Id = "BUY001",
        Pair = "EURUSD",
        TradeType = TradeType.BUY,
        OrderType = OrderType.MARKET,
        EntryPrice = entry,
        StopLoss = sl,
        TakeProfit = tp,
        LotSize = 0.10,
        MagicNumber = 999001,
        CreatedAt = DateTime.UtcNow
    };

    private static TradeRequest SellRequest(
        double entry = 1.1000,
        double sl = 1.1050,
        double tp = 1.0900) => new()
    {
        Id = "SELL001",
        Pair = "EURUSD",
        TradeType = TradeType.SELL,
        OrderType = OrderType.MARKET,
        EntryPrice = entry,
        StopLoss = sl,
        TakeProfit = tp,
        LotSize = 0.10,
        MagicNumber = 999001,
        CreatedAt = DateTime.UtcNow
    };

    private static LivePosition Position(
        long ticket,
        double profit = 0,
        string symbol = "EURUSD",
        TradeType type = TradeType.BUY,
        double lots = 0.10,
        double openPrice = 1.1000,
        double currentPrice = 1.1010,
        double stopLoss = 1.0950) => new()
    {
        Ticket = ticket,
        Symbol = symbol,
        Type = type,
        Lots = lots,
        OpenPrice = openPrice,
        CurrentPrice = currentPrice,
        StopLoss = stopLoss,
        TakeProfit = 1.1100,
        Profit = profit,
        MagicNumber = 999001,
        OpenTime = DateTime.UtcNow
    };

    private static TradeRecord ClosedTrade(
        double profitUsd,
        DateTime? closedAtUtc = null) => new()
    {
        RequestId = Guid.NewGuid().ToString("N"),
        CreatedAt = DateTime.UtcNow.AddHours(-2),
        ExecutedAt = DateTime.UtcNow.AddHours(-2),
        Pair = "EURUSD",
        Direction = "BUY",
        OrderType = "MARKET",
        LotSize = 0.10,
        EntryPrice = 1.1000,
        StopLoss = 1.0950,
        TakeProfit = 1.1100,
        MagicNumber = 999001,
        Ticket = Random.Shared.NextInt64(1000, 9999),
        Status = TradeStatus.Closed.ToString(),
        ExecutedPrice = 1.1000,
        ExecutedLots = 0.10,
        ProfitUsd = profitUsd,
        ClosedAt = closedAtUtc ?? DateTime.UtcNow
    };

    private static DateTime CurrentUtcWeekStart()
    {
        DateTime today = DateTime.UtcNow.Date;
        int daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return today.AddDays(-daysSinceMonday);
    }

    private static MarginEstimate MarginEstimate(double requiredMargin) => new()
    {
        Symbol = "EURUSD",
        TradeType = "BUY",
        Lots = 0.10,
        Price = 1.10005,
        RequiredMargin = requiredMargin,
        MinLot = 0.01,
        MaxLot = 100,
        LotStep = 0.01,
        Currency = "USD"
    };

    private static OrderCheckResult OrderCheckPass(
        double volume = 0.20,
        long retcode = 10009,
        string comment = "done") => new()
    {
        IsAccepted = true,
        Retcode = retcode,
        Comment = comment,
        Margin = 100,
        MarginFree = 9_900,
        MarginLevel = 1_000,
        Volume = volume,
        Price = 1.10005,
        StopLoss = 1.0950,
        TakeProfit = 1.1100,
        Symbol = "EURUSD",
        TradeType = "BUY"
    };

    private static OrderCheckResult OrderCheckReject(long retcode, string comment) => new()
    {
        IsAccepted = false,
        Retcode = retcode,
        Comment = comment,
        Margin = 100,
        MarginFree = 9_900,
        MarginLevel = 1_000,
        Volume = 0.20,
        Price = 1.10005,
        StopLoss = 1.0950,
        TakeProfit = 1.1100,
        Symbol = "EURUSD",
        TradeType = "BUY"
    };

    private static IpcResponse BrokerError(string code, string message) => new()
    {
        RequestId = "FAKE",
        Success = false,
        Error = $"{code}: {message}"
    };

    private static IpcResponse BrokerSuccess() => new()
    {
        RequestId = "FAKE",
        Success = true,
        Data = new TradeResult
        {
            RequestId = "FAKE",
            Status = TradeStatus.Submitted,
            Ticket = 777001,
            ExecutedPrice = 1.1000,
            ExecutedLots = 0.10,
            ExecutedAt = DateTime.UtcNow
        }
    };

    private static void WriteKillSwitchFile(string folder, string reason)
    {
        Directory.CreateDirectory(folder);
        var state = new KillSwitchState
        {
            KillSwitchActive = true,
            KillSwitchReason = reason,
            KillSwitchTriggeredAtUtc = DateTime.UtcNow,
            DrawdownPercentAtTrigger = 12.5,
            AccountBalance = 10_000,
            AccountEquity = 8_750
        };

        File.WriteAllText(
            Path.Combine(folder, "kill_switch.json"),
            JsonConvert.SerializeObject(state, Formatting.Indented));
    }

    private static KillSwitchState ReadKillSwitchFile(string folder)
    {
        var state = JsonConvert.DeserializeObject<KillSwitchState>(
            File.ReadAllText(Path.Combine(folder, "kill_switch.json")));

        AssertNotNull(state, "Kill-switch state file should deserialize.");
        return state!;
    }

    private static RiskValidationResult ApprovedRisk() => new()
    {
        IsApproved = true,
        Reason = "ok",
        ValidatedLotSize = 0.10,
        ReferenceEntryPrice = 1.1000,
        RiskPercent = 1
    };

    private static UserApprovalDecision ApprovedUser() => new()
    {
        IsApproved = true,
        ApprovedBy = "Test",
        ApprovalMode = "UnitTest"
    };

    private static MT5Bridge Bridge() => Bridge(port: 9, timeoutMs: 1);

    private static MT5Bridge Bridge(int port, int timeoutMs = 1000) => new(new MT5Settings
    {
        Mode = ConnectionMode.Socket,
        Host = "127.0.0.1",
        Port = port,
        TimeoutMs = timeoutMs,
        MaxReconnectAttempts = 1
    });

    private static (bool Approved, string Reason) EvaluateScalpingSnapshot(JObject snapshot)
    {
        var method = typeof(ScalpingSessionService).GetMethod(
            "EvaluateSnapshot",
            BindingFlags.NonPublic | BindingFlags.Static);

        AssertNotNull(method, "Scalping snapshot evaluator should exist.");
        object decision = method!.Invoke(null, [snapshot, TradeType.BUY, new ScalpingConfig()])!;
        Type decisionType = decision.GetType();

        bool approved = (bool)(decisionType.GetProperty("Approved")?.GetValue(decision)
            ?? throw new InvalidOperationException("Missing Approved property."));
        string reason = (string)(decisionType.GetProperty("Reason")?.GetValue(decision)
            ?? throw new InvalidOperationException("Missing Reason property."));

        return (approved, reason);
    }

    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        AssertNotNull(method, $"Private method {methodName} should exist.");
        method!.Invoke(target, args);
    }

    private static async Task InvokePrivateAsync(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        AssertNotNull(method, $"Private method {methodName} should exist.");
        object? result = method!.Invoke(target, args);
        if (result is Task task)
            await task.ConfigureAwait(false);
    }

    private static T InvokePrivateResult<T>(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        AssertNotNull(method, $"Private method {methodName} should exist.");
        return (T)method!.Invoke(target, args)!;
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        AssertNotNull(field, $"Private field {fieldName} should exist.");
        return (T)field!.GetValue(target)!;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        AssertNotNull(field, $"Private field {fieldName} should exist.");
        field!.SetValue(target, value);
    }

    private static string TestFolder() =>
        Path.Combine(Path.GetTempPath(), "ForexBot.Tests", Guid.NewGuid().ToString("N"));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MT5TradingBot.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static bool IsUnder(string path, string directory)
    {
        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        string fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        return fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static string WriteTempCsv(params string[] lines)
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "market_data.csv");
        File.WriteAllLines(path, lines);
        return path;
    }

    private static BacktestExecutionCostInput CostInput(BotConfig config, double? spreadPips = null) => new()
    {
        Symbol = "EURUSD",
        EntrySide = TradeType.BUY,
        LotSize = 0.20,
        EntryPrice = 1.1000,
        ExitPrice = 1.1010,
        SpreadPips = spreadPips,
        CommissionAndSlippageConfig = config
    };

    private static BacktestBrokerRuleInput BrokerRuleInput(
        double lotSize = 0.10,
        double stopLoss = 1.0950,
        double takeProfit = 1.1050,
        SymbolInfo? symbolInfo = null,
        bool omitSymbolInfo = false,
        double existingLots = 0,
        BacktestBrokerMarginInput? margin = null,
        OrderCheckResult? orderCheck = null) => new()
    {
        Symbol = "EURUSD",
        TradeType = TradeType.BUY,
        OrderType = OrderType.MARKET,
        StopLoss = stopLoss,
        TakeProfit = takeProfit,
        LotSize = lotSize,
        SymbolInfo = omitSymbolInfo ? null : symbolInfo ?? BacktestBrokerSymbol(),
        ExistingSymbolLots = existingLots,
        Margin = margin ?? new BacktestBrokerMarginInput
        {
            AccountEquity = 10_000,
            CurrentUsedMargin = 500,
            EstimatedRequiredMargin = 100,
            MinProjectedMarginLevelPercent = 200
        },
        SimulatedOrderCheck = orderCheck
    };

    private static SymbolInfo BacktestBrokerSymbol(
        double stopLevelPoints = 50,
        double freezeLevelPoints = 20,
        double volumeLimit = 0.15) => new()
    {
        Symbol = "EURUSD",
        Ask = 1.10020,
        Bid = 1.10000,
        Spread = 20,
        Digits = 5,
        MinLot = 0.01,
        MaxLot = 1.00,
        LotStep = 0.01,
        VolumeLimit = volumeLimit,
        PointSize = 0.00001,
        StopLevelPoints = stopLevelPoints,
        FreezeLevelPoints = freezeLevelPoints
    };

    private static IReadOnlyList<BacktestTick> TickSeries(params (int Seconds, double Bid, double Ask)[] ticks) =>
        ticks.Select(t => new BacktestTick
        {
            TimestampUtc = new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc).AddSeconds(t.Seconds),
            Symbol = "EURUSD",
            Bid = t.Bid,
            Ask = t.Ask
        }).ToList();

    private static BacktestOhlcCandle Candle(double low, double high) => new()
    {
        TimestampUtc = new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc),
        Symbol = "EURUSD",
        Timeframe = "M1",
        Open = 1.1000,
        High = high,
        Low = low,
        Close = 1.1000
    };

    private static void AssertExit(
        IntrabarExitResult result,
        IntrabarExitType expectedType,
        double expectedPrice,
        string message)
    {
        AssertTrue(result.ExitTriggered, message);
        AssertEqual(expectedType.ToString(), result.ExitType.ToString(), message);
        AssertClose(expectedPrice, result.ExitPrice, 0.0000001, message);
        AssertTrue(result.ExitTimestampUtc.HasValue, "Triggered intrabar exit should include a UTC timestamp.");
        DateTime exitTimestampUtc = result.ExitTimestampUtc.GetValueOrDefault();
        AssertTrue(exitTimestampUtc.Kind == DateTimeKind.Utc,
            "Triggered intrabar exit timestamp should be UTC.");
    }

    private static void AssertBrokerRuleReject(
        BacktestBrokerRuleResult result,
        string expectedCode,
        string expectedReasonFragment)
    {
        AssertFalse(result.Approved, $"Broker-rule simulation should reject with {expectedCode}.");
        AssertEqual(expectedCode, result.RejectionCode, "Broker-rule rejection should include a clear code.");
        AssertContains(expectedReasonFragment, result.RejectionReason);
    }

    private sealed class FakeTradeRepository : ITradeRepository
    {
        private readonly IReadOnlyList<TradeRecord> _records;

        public FakeTradeRepository(params TradeRecord[] records)
        {
            _records = records;
        }

        public double? LastClosedProfitUsd { get; private set; }
        public long? LastClosedTicket { get; private set; }

        public Task InsertAsync(
            TradeRequest req,
            TradeResult result,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<TradeRecord>> GetRecentAsync(
            int count = 200,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TradeRecord>>(_records.Take(count).ToList());

        public Task<IReadOnlyList<TradeRecord>> GetByDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct = default) =>
            Task.FromResult(FilterByExecutedAt(fromUtc, toUtc));

        public Task UpdateCloseAsync(
            long ticket,
            double profitUsd,
            DateTime closedAtUtc,
            CancellationToken ct = default)
        {
            LastClosedTicket = ticket;
            LastClosedProfitUsd = profitUsd;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TradeRecord>> GetRecentClosedAsync(
            int count = 50,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TradeRecord>>(_records
                .Where(r => r.ClosedAt.HasValue)
                .OrderByDescending(r => r.ClosedAt)
                .Take(count)
                .ToList());

        public Task<IReadOnlyList<TradeRecord>> GetClosedByCloseDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TradeRecord>>(_records
                .Where(r => r.ClosedAt.HasValue &&
                            r.ClosedAt.Value >= fromUtc &&
                            r.ClosedAt.Value < toUtc)
                .OrderByDescending(r => r.ClosedAt)
                .ToList());

        private IReadOnlyList<TradeRecord> FilterByExecutedAt(DateTime fromUtc, DateTime toUtc) =>
            _records
                .Where(r => r.ExecutedAt >= fromUtc && r.ExecutedAt < toUtc)
                .OrderByDescending(r => r.ExecutedAt)
                .ToList();
    }

    private sealed class FakeMt5Server : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;
        private readonly AccountInfo? _account;
        private readonly SymbolInfo? _symbol;
        private readonly IReadOnlyList<SymbolInfo?> _symbolSequence;
        private readonly IReadOnlyList<LivePosition> _positions;
        private readonly bool _positionsAvailable;
        private readonly MarginEstimate? _marginEstimate;
        private readonly bool _marginEstimateAvailable;
        private readonly OrderCheckResult? _orderCheckResult;
        private readonly bool _orderCheckAvailable;
        private readonly IReadOnlyList<IpcResponse> _openTradeResponses;
        private readonly HashSet<long> _closeFailureTickets;
        private int _openTradeCalls;
        private int _closeTradeCalls;
        private int _orderCheckCalls;
        private int _symbolInfoCalls;
        private double _lastOrderCheckLots;

        public FakeMt5Server(
            AccountInfo? account,
            SymbolInfo? symbol,
            IReadOnlyList<LivePosition>? positions = null,
            bool positionsAvailable = true,
            MarginEstimate? marginEstimate = null,
            bool marginEstimateAvailable = true,
            OrderCheckResult? orderCheckResult = null,
            bool orderCheckAvailable = true,
            IReadOnlyList<SymbolInfo?>? symbolSequence = null,
            IReadOnlyList<IpcResponse>? openTradeResponses = null,
            IReadOnlySet<long>? closeFailureTickets = null)
        {
            _account = account;
            _symbol = symbol;
            _symbolSequence = symbolSequence ?? [];
            _positions = positions ?? [];
            _positionsAvailable = positionsAvailable;
            _marginEstimate = marginEstimate;
            _marginEstimateAvailable = marginEstimateAvailable;
            _orderCheckResult = orderCheckResult;
            _orderCheckAvailable = orderCheckAvailable;
            _openTradeResponses = openTradeResponses ?? [];
            _closeFailureTickets = closeFailureTickets != null
                ? new HashSet<long>(closeFailureTickets)
                : [];
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _loop = Task.Run(AcceptLoopAsync);
        }

        public int Port { get; }
        public int OpenTradeCalls => Volatile.Read(ref _openTradeCalls);
        public int CloseTradeCalls => Volatile.Read(ref _closeTradeCalls);
        public int OrderCheckCalls => Volatile.Read(ref _orderCheckCalls);
        public int SymbolInfoCalls => Volatile.Read(ref _symbolInfoCalls);
        public double LastOrderCheckLots => _lastOrderCheckLots;

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            try { await _loop.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); }
            catch { }
            _cts.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                    _ = Task.Run(() => HandleClientAsync(client), _cts.Token);
                }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (SocketException) when (_cts.Token.IsCancellationRequested) { }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                byte[] lenBuf = new byte[4];
                await ReadExactAsync(stream, lenBuf, _cts.Token).ConfigureAwait(false);
                int len = BitConverter.ToInt32(lenBuf);
                if (len <= 0 || len > 1_048_576) return;

                byte[] payload = new byte[len];
                await ReadExactAsync(stream, payload, _cts.Token).ConfigureAwait(false);

                var msg = JObject.Parse(Encoding.UTF8.GetString(payload));
                string command = msg.Value<string>("cmd") ?? "";
                string requestId = msg.Value<string>("req_id") ?? "";
                var response = CreateResponse(command, requestId, msg);

                byte[] responsePayload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response, Formatting.None));
                await stream.WriteAsync(BitConverter.GetBytes(responsePayload.Length), _cts.Token).ConfigureAwait(false);
                await stream.WriteAsync(responsePayload, _cts.Token).ConfigureAwait(false);
            }
        }

        private IpcResponse CreateResponse(string command, string requestId, JObject msg) =>
            command switch
            {
                "PING" => Success(requestId, new { pong = true }),
                "GET_ACCOUNT" => _account == null
                    ? Error(requestId, "no account")
                    : Success(requestId, _account),
                "GET_SYMBOL_INFO" => SymbolInfo(requestId),
                "GET_POSITIONS" => _positionsAvailable
                    ? Success(requestId, _positions)
                    : Error(requestId, "positions unavailable"),
                "GET_MARGIN_ESTIMATE" => _marginEstimateAvailable && _marginEstimate != null
                    ? Success(requestId, _marginEstimate)
                    : Error(requestId, "margin estimate unavailable"),
                "CHECK_ORDER" => CheckOrder(requestId, msg),
                "OPEN_TRADE" => OpenTrade(requestId),
                "CLOSE_TRADE" => CloseTrade(requestId, msg),
                _ => Error(requestId, $"unsupported command {command}")
            };

        private IpcResponse SymbolInfo(string requestId)
        {
            int call = Interlocked.Increment(ref _symbolInfoCalls);
            SymbolInfo? symbol = _symbolSequence.Count > 0
                ? _symbolSequence[Math.Min(call - 1, _symbolSequence.Count - 1)]
                : _symbol;

            return symbol == null
                ? Error(requestId, "no symbol")
                : Success(requestId, symbol);
        }

        private IpcResponse CheckOrder(string requestId, JObject msg)
        {
            Interlocked.Increment(ref _orderCheckCalls);
            LastOrderCheckPayload(msg);

            if (!_orderCheckAvailable)
                return Error(requestId, "order check unavailable");

            return Success(requestId, _orderCheckResult ?? OrderCheckPass());
        }

        private void LastOrderCheckPayload(JObject msg)
        {
            var data = msg["data"];
            double lots =
                data?["lots"]?.Value<double?>() ??
                data?["lot_size"]?.Value<double?>() ??
                data?["LotSize"]?.Value<double?>() ??
                0;
            _lastOrderCheckLots = lots;
        }

        private IpcResponse OpenTrade(string requestId)
        {
            int call = Interlocked.Increment(ref _openTradeCalls);
            if (_openTradeResponses.Count > 0)
            {
                var response = _openTradeResponses[Math.Min(call - 1, _openTradeResponses.Count - 1)];
                response.RequestId = requestId;
                return response;
            }

            return Success(requestId, SubmittedTrade());
        }

        private TradeResult SubmittedTrade()
        {
            return new TradeResult
            {
                RequestId = "FAKE",
                Status = TradeStatus.Submitted,
                Ticket = 777001,
                ExecutedPrice = 1.1000,
                ExecutedLots = 0.10,
                ExecutedAt = DateTime.UtcNow
            };
        }

        private IpcResponse CloseTrade(string requestId, JObject msg)
        {
            long ticket = msg["data"]?["ticket"]?.Value<long>() ?? 0;
            Interlocked.Increment(ref _closeTradeCalls);

            return _closeFailureTickets.Contains(ticket)
                ? Error(requestId, "close failed")
                : Success(requestId, new { ticket, closed = true });
        }

        private static IpcResponse Success(string requestId, object data) => new()
        {
            RequestId = requestId,
            Success = true,
            Data = data
        };

        private static IpcResponse Error(string requestId, string error) => new()
        {
            RequestId = requestId,
            Success = false,
            Error = error
        };

        private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = await stream
                    .ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) throw new IOException("Fake MT5 client disconnected.");
                offset += read;
            }
        }
    }

    private sealed class UnavailableNewsCalendar : INewsCalendarService
    {
        public Task<NewsRiskSnapshot> GetRiskSnapshotAsync(
            string pair,
            ApiIntegrationConfig config,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new NewsRiskSnapshot
            {
                IsConfigured = false,
                Reason = "news provider unavailable",
                Source = config.NewsProvider
            });
    }

    private sealed class ThrowingRiskManager : IRiskManager
    {
        public Task<RiskValidationResult> ValidateAsync(
            TradeRequest request,
            AccountInfo account,
            SymbolInfo? symbolInfo,
            IReadOnlyList<LivePosition> openPositions,
            BotConfig config,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("risk data source unavailable");
    }

    private sealed class IncompleteRiskManager : IRiskManager
    {
        public Task<RiskValidationResult> ValidateAsync(
            TradeRequest request,
            AccountInfo account,
            SymbolInfo? symbolInfo,
            IReadOnlyList<LivePosition> openPositions,
            BotConfig config,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RiskValidationResult
            {
                IsApproved = true,
                Reason = "approved but incomplete"
            });
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertFalse(bool condition, string message) =>
        AssertTrue(!condition, message);

    private static void AssertEqual(string expected, string actual, string message)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
    }

    private static void AssertEqual(int expected, int actual, string message)
    {
        if (expected != actual)
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
    }

    private static void AssertClose(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
    }

    private static void AssertContains(string expectedFragment, string actual)
    {
        if (!actual.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Expected '{actual}' to contain '{expectedFragment}'.");
    }

    private static string ReportRow(string report, string item)
    {
        string prefix = $"| {item} |";
        string? row = report
            .Split(Environment.NewLine)
            .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (row == null)
            throw new InvalidOperationException($"Report row '{item}' was not found.");

        return row;
    }

    private static async Task<TException> AssertThrowsAsync<TException>(
        Func<Task> action,
        string message)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"{message} Expected {typeof(TException).Name}, got {ex.GetType().Name}: {ex.Message}");
        }

        throw new InvalidOperationException($"{message} Expected {typeof(TException).Name}, but no exception was thrown.");
    }

    private static void AssertNotNull(object? value, string message)
    {
        if (value == null) throw new InvalidOperationException(message);
    }

    private sealed record TestCase(string Name, Func<Task> Body);
}
