using System.Reflection;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using MT5TradingBot.Core;
using MT5TradingBot.Data;
using MT5TradingBot.Models;
using MT5TradingBot.Modules.Backtesting;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Modules.Deployment;
using MT5TradingBot.Modules.LiveReadiness;
using MT5TradingBot.Modules.MarketData;
using MT5TradingBot.Modules.NewsFilter;
using MT5TradingBot.Modules.RiskManagement;
using MT5TradingBot.Modules.Scalping;
using MT5TradingBot.Modules.StrategyProof;
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
        new("rollout PaperOnly blocks live orders", RolloutPaperOnlyBlocksLiveOrders),
        new("rollout Demo blocks real live orders", RolloutDemoBlocksRealLiveOrders),
        new("rollout TinyLive applies capped risk", RolloutTinyLiveAppliesCappedRisk),
        new("rollout scale-up criteria can recommend advance", RolloutScaleUpCriteriaCanRecommendAdvance),
        new("rollout poor drawdown recommends rollback", RolloutPoorDrawdownRecommendsRollback),
        new("rollout high losing streak recommends rollback", RolloutHighLosingStreakRecommendsRollback),
        new("rollout high rejection rate recommends rollback", RolloutHighRejectionRateRecommendsRollback),
        new("rollout critical runtime health recommends rollback", RolloutCriticalRuntimeHealthRecommendsRollback),
        new("rollout kill switch active blocks or rolls back", RolloutKillSwitchActiveBlocksOrRollsBack),
        new("rollout does not auto-advance without explicit confirmation", RolloutDoesNotAutoAdvanceWithoutExplicitConfirmation),
        new("final go no-go all gates passing returns Go", FinalGoNoGoAllGatesPassingReturnsGo),
        new("final go no-go missing strategy proof returns Unknown", FinalGoNoGoMissingStrategyProofReturnsUnknown),
        new("final go no-go kill switch active returns No-Go", FinalGoNoGoKillSwitchActiveReturnsNoGo),
        new("final go no-go broker readiness failure returns No-Go", FinalGoNoGoBrokerReadinessFailureReturnsNoGo),
        new("final go no-go critical runtime health returns No-Go", FinalGoNoGoCriticalRuntimeHealthReturnsNoGo),
        new("final go no-go demo setup returns Conditional-Go", FinalGoNoGoDemoSetupReturnsConditionalGo),
        new("final go no-go missing evidence returns Unknown", FinalGoNoGoMissingEvidenceReturnsUnknown),
        new("final go no-go report includes required warnings", FinalGoNoGoReportIncludesRequiredWarnings),
        new("final go no-go report includes required manual actions", FinalGoNoGoReportIncludesRequiredManualActions),
        new("evidence package CSV does not silently use sample fixture", EvidencePackageCsvDoesNotSilentlyUseSampleFixture),
        new("evidence package no strategy candidates returns clear diagnostic", EvidencePackageNoStrategyCandidatesReturnsClearDiagnostic),
        new("evidence package explicit sample fixture still works", EvidencePackageExplicitSampleFixtureStillWorks),
        new("evidence package report marks data source correctly", EvidencePackageReportMarksDataSourceCorrectly),
        new("evidence package OHLC CSV generates candidates", EvidencePackageOhlcCsvGeneratesCandidates),
        new("evidence package OHLC movement omits no-candidates diagnostic", EvidencePackageOhlcMovementOmitsNoCandidatesDiagnostic),
        new("OHLC generated candidates produce backtest trades", OhlcGeneratedCandidatesProduceBacktestTrades),
        new("offline candidate generator tick data can produce candidates", OfflineCandidateGeneratorTickDataCanProduceCandidates),
        new("offline candidate generator HOLD signals are counted", OfflineCandidateGeneratorHoldSignalsAreCounted),
        new("offline candidate generator incomplete signals are counted", OfflineCandidateGeneratorIncompleteSignalsAreCounted),
        new("offline candidate generator does not reference AI or MT5 services", OfflineCandidateGeneratorDoesNotReferenceAiOrMt5Services),
        new("offline generated candidates flow into realistic runner", OfflineGeneratedCandidatesFlowIntoRealisticRunner),
        new("evidence report omits not implemented diagnostic when candidates generated", EvidenceReportOmitsNotImplementedDiagnosticWhenCandidatesGenerated),
        new("market data auto sync startup trigger is nonblocking", MarketDataAutoSyncStartupTriggerIsNonblocking),
        new("market data auto sync skips when already running", MarketDataAutoSyncSkipsWhenAlreadyRunning),
        new("market data auto sync skips during critical trading", MarketDataAutoSyncSkipsDuringCriticalTrading),
        new("market data CLI update command prints started banner", MarketDataCliUpdateCommandPrintsStartedBanner),
        new("market data CLI update command returns failure code", MarketDataCliUpdateCommandReturnsFailureCode),
        new("market data CLI update command reports MT5 unavailable", MarketDataCliUpdateCommandReportsMt5Unavailable),
        new("market data EA historical commands parse nested payload dates", MarketDataEaHistoricalCommandsParseNestedPayloadDates),
        new("market data UI disabled status text is visible", MarketDataUiDisabledStatusTextIsVisible),
        new("market data startup sync emits progress event", MarketDataStartupSyncEmitsProgressEvent),
        new("market data startup sync failure is visible", MarketDataStartupSyncFailureIsVisible),
        new("review dashboard merges rich MT5 price and account snapshot", ReviewDashboardMergesRichMt5PriceAndAccountSnapshot),
        new("market data updater creates new tick file", MarketDataUpdaterCreatesNewTickFile),
        new("market data updater does not create generic ticks csv", MarketDataUpdaterDoesNotCreateGenericTicksCsv),
        new("market data updater appends only new rows", MarketDataUpdaterAppendsOnlyNewRows),
        new("market data updater backfill ignores existing watermark", MarketDataUpdaterBackfillIgnoresExistingWatermark),
        new("market data updater backfill chunks lookback requests", MarketDataUpdaterBackfillChunksLookbackRequests),
        new("market data updater removes duplicates", MarketDataUpdaterRemovesDuplicates),
        new("market data updater accepts broker suffix symbols", MarketDataUpdaterAcceptsBrokerSuffixSymbols),
        new("market data updater treats header-only cache as empty", MarketDataUpdaterTreatsHeaderOnlyCacheAsEmpty),
        new("market data updater trims old tick rows", MarketDataUpdaterTrimsOldTickRows),
        new("market data updater trims old M1 rows", MarketDataUpdaterTrimsOldM1Rows),
        new("market data updater falls back to OHLC when ticks unavailable", MarketDataUpdaterFallsBackToOhlcWhenTicksUnavailable),
        new("market data updater zero tick rows fall back to M1", MarketDataUpdaterZeroTickRowsFallBackToM1),
        new("market data updater zero tick and M1 rows fails clearly", MarketDataUpdaterZeroTickAndM1RowsFailsClearly),
        new("market data updater invalid symbol returns clear error", MarketDataUpdaterInvalidSymbolReturnsClearError),
        new("market data updater generated CSV validates with loader", MarketDataUpdaterGeneratedCsvValidatesWithLoader),
        new("market data updater CLI output includes per-symbol path", MarketDataUpdaterCliOutputIncludesPerSymbolPath),
        new("market data updater emits progress events", MarketDataUpdaterEmitsProgressEvents),
        new("market data auto sync cancel stops safely", MarketDataAutoSyncCancelStopsSafely),
        new("market data updater CLI parses arguments", MarketDataUpdaterCliParsesArguments),
        new("market data updater CLI parses backfill", MarketDataUpdaterCliParsesBackfill),
        new("market data updater does not call live trade methods", MarketDataUpdaterDoesNotCallLiveTradeMethods),
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
        new("backtest no-trade filter blocks rollover window", BacktestNoTradeFilterBlocksRolloverWindow),
        new("backtest no-trade filter blocks additional window", BacktestNoTradeFilterBlocksAdditionalWindow),
        new("backtest no-trade filter blocks high session spread", BacktestNoTradeFilterBlocksHighSessionSpread),
        new("backtest no-trade filter allows acceptable spread", BacktestNoTradeFilterAllowsAcceptableSpread),
        new("backtest no-trade filter blocks historical high-impact news", BacktestNoTradeFilterBlocksHistoricalHighImpactNews),
        new("backtest no-trade filter allows unrelated news", BacktestNoTradeFilterAllowsUnrelatedNews),
        new("backtest no-trade filter fails clearly on missing spread data", BacktestNoTradeFilterFailsClearlyOnMissingSpreadData),
        new("backtest no-trade filter fails clearly on invalid config", BacktestNoTradeFilterFailsClearlyOnInvalidConfig),
        new("backtest no-trade filter result includes matched filter details", BacktestNoTradeFilterResultIncludesMatchedFilterDetails),
        new("backtest out-of-sample split by ratio works", BacktestOutOfSampleSplitByRatioWorks),
        new("backtest out-of-sample split by date works", BacktestOutOfSampleSplitByDateWorks),
        new("backtest out-of-sample invalid config fails clearly", BacktestOutOfSampleInvalidConfigFailsClearly),
        new("backtest walk-forward windows are generated correctly", BacktestWalkForwardWindowsAreGeneratedCorrectly),
        new("backtest walk-forward invalid config fails clearly", BacktestWalkForwardInvalidConfigFailsClearly),
        new("backtest Monte Carlo is deterministic with fixed seed", BacktestMonteCarloIsDeterministicWithFixedSeed),
        new("backtest Monte Carlo reports robustness statistics", BacktestMonteCarloReportsRobustnessStatistics),
        new("backtest Monte Carlo empty trade list fails clearly", BacktestMonteCarloEmptyTradeListFailsClearly),
        new("backtest reporting basic metrics calculate correctly", BacktestReportingBasicMetricsCalculateCorrectly),
        new("backtest reporting profit factor handles no-loss case safely", BacktestReportingProfitFactorHandlesNoLossCaseSafely),
        new("backtest reporting max drawdown calculates correctly", BacktestReportingMaxDrawdownCalculatesCorrectly),
        new("backtest reporting worst losing streak calculates correctly", BacktestReportingWorstLosingStreakCalculatesCorrectly),
        new("backtest reporting costs aggregate correctly", BacktestReportingCostsAggregateCorrectly),
        new("backtest reporting grouping by symbol works", BacktestReportingGroupingBySymbolWorks),
        new("backtest reporting grouping by session works", BacktestReportingGroupingBySessionWorks),
        new("backtest reporting grouping by spread regime works", BacktestReportingGroupingBySpreadRegimeWorks),
        new("backtest reporting empty trade list fails clearly", BacktestReportingEmptyTradeListFailsClearly),
        new("signal quality metrics calculate correctly for mixed wins and losses", SignalQualityMetricsCalculateMixedWinsAndLosses),
        new("signal quality expectancy after costs is correct", SignalQualityExpectancyAfterCostsIsCorrect),
        new("signal quality profit factor handles no-loss case safely", SignalQualityProfitFactorHandlesNoLossCaseSafely),
        new("signal quality worst losing streak calculates correctly", SignalQualityWorstLosingStreakCalculatesCorrectly),
        new("signal quality average duration calculates when timestamps exist", SignalQualityAverageDurationCalculatesWhenTimestampsExist),
        new("signal quality grouping by signal source works", SignalQualityGroupingBySignalSourceWorks),
        new("signal quality missing source groups as unknown", SignalQualityMissingSourceGroupsAsUnknown),
        new("signal quality empty input fails clearly", SignalQualityEmptyInputFailsClearly),
        new("segmented performance grouping by symbol works", SegmentedPerformanceGroupingBySymbolWorks),
        new("segmented performance grouping by session works", SegmentedPerformanceGroupingBySessionWorks),
        new("segmented performance grouping by spread regime works", SegmentedPerformanceGroupingBySpreadRegimeWorks),
        new("segmented performance missing metadata goes to Unknown", SegmentedPerformanceMissingMetadataGoesToUnknown),
        new("segmented performance AI confidence bucket grouping works", SegmentedPerformanceAiConfidenceBucketGroupingWorks),
        new("segmented performance signal source reason grouping works", SegmentedPerformanceSignalSourceReasonGroupingWorks),
        new("segmented performance metrics calculate correctly", SegmentedPerformanceMetricsCalculateCorrectly),
        new("segmented performance empty input fails clearly", SegmentedPerformanceEmptyInputFailsClearly),
        new("cost sensitivity spread increase reduces net profit correctly", CostSensitivitySpreadIncreaseReducesNetProfitCorrectly),
        new("cost sensitivity slippage increase reduces net profit correctly", CostSensitivitySlippageIncreaseReducesNetProfitCorrectly),
        new("cost sensitivity commission increase reduces net profit correctly", CostSensitivityCommissionIncreaseReducesNetProfitCorrectly),
        new("cost sensitivity combined worse-cost scenario works", CostSensitivityCombinedWorseCostScenarioWorks),
        new("cost sensitivity win-to-loss flip count works", CostSensitivityWinToLossFlipCountWorks),
        new("cost sensitivity invalid scenario config fails clearly", CostSensitivityInvalidScenarioConfigFailsClearly),
        new("cost sensitivity missing cost fields produce warnings", CostSensitivityMissingCostFieldsProduceWarnings),
        new("cost sensitivity empty input fails clearly", CostSensitivityEmptyInputFailsClearly),
        new("strategy robustness OOS degradation is calculated", StrategyRobustnessOosDegradationIsCalculated),
        new("strategy robustness small sample is inconclusive", StrategyRobustnessSmallSampleIsInconclusive),
        new("strategy robustness Monte Carlo summary is included", StrategyRobustnessMonteCarloSummaryIsIncluded),
        new("strategy robustness failing drawdown threshold returns Fail", StrategyRobustnessFailingDrawdownThresholdReturnsFail),
        new("strategy robustness passing thresholds return Pass", StrategyRobustnessPassingThresholdsReturnPass),
        new("strategy robustness invalid split config fails clearly", StrategyRobustnessInvalidSplitConfigFailsClearly),
        new("strategy robustness invalid Monte Carlo config fails clearly", StrategyRobustnessInvalidMonteCarloConfigFailsClearly),
        new("strategy robustness empty input fails clearly", StrategyRobustnessEmptyInputFailsClearly),
        new("AI filter impact outperforming non-AI returns Improves", AiFilterImpactOutperformingNonAiReturnsImproves),
        new("AI filter impact underperforming non-AI returns Hurts", AiFilterImpactUnderperformingNonAiReturnsHurts),
        new("AI filter impact missing comparison group returns Inconclusive", AiFilterImpactMissingComparisonGroupReturnsInconclusive),
        new("AI filter impact confidence bucket grouping works", AiFilterImpactConfidenceBucketGroupingWorks),
        new("AI filter impact blocked winner loser analysis works", AiFilterImpactBlockedWinnerLoserAnalysisWorks),
        new("AI filter impact missing confidence produces warning", AiFilterImpactMissingConfidenceProducesWarning),
        new("AI filter impact small sample is inconclusive", AiFilterImpactSmallSampleIsInconclusive),
        new("realistic backtest runner rejects no-trade filtered candidate", RealisticBacktestRunnerRejectsNoTradeFilteredCandidate),
        new("realistic backtest runner rejects broker-rule blocked candidate", RealisticBacktestRunnerRejectsBrokerRuleBlockedCandidate),
        new("realistic backtest runner records TP hit as winning trade", RealisticBacktestRunnerRecordsTpHitAsWinningTrade),
        new("realistic backtest runner records SL hit as losing trade", RealisticBacktestRunnerRecordsSlHitAsLosingTrade),
        new("realistic backtest runner deducts execution costs", RealisticBacktestRunnerDeductsExecutionCosts),
        new("realistic backtest runner records unresolved trade as open", RealisticBacktestRunnerRecordsUnresolvedTradeAsOpen),
        new("realistic backtest runner produces metrics report", RealisticBacktestRunnerProducesMetricsReport),
        new("realistic backtest report can be generated without MT5", RealisticBacktestReportCanBeGeneratedWithoutMt5),
        new("realistic backtest report includes outcome counts", RealisticBacktestReportIncludesOutcomeCounts),
        new("realistic backtest report includes execution costs", RealisticBacktestReportIncludesExecutionCosts),
        new("realistic backtest report includes assumptions and warnings", RealisticBacktestReportIncludesAssumptionsAndWarnings),
        new("realistic backtest report can load CSV market data", RealisticBacktestReportCanLoadCsvMarketData),
        new("strategy extraction report can be generated", StrategyExtractionReportCanBeGenerated),
        new("strategy extraction report includes deterministic logic section", StrategyExtractionReportIncludesDeterministicLogicSection),
        new("strategy extraction report includes AI boundary section", StrategyExtractionReportIncludesAiBoundarySection),
        new("strategy extraction report includes hold no-trade behavior section", StrategyExtractionReportIncludesHoldNoTradeBehaviorSection),
        new("strategy extraction report includes code evidence paths", StrategyExtractionReportIncludesCodeEvidencePaths),
        new("repaint lookahead audit report can be generated", RepaintLookaheadAuditReportCanBeGenerated),
        new("repaint lookahead audit report includes live signal-generation risk section", RepaintLookaheadAuditReportIncludesLiveSignalGenerationRiskSection),
        new("repaint lookahead audit report includes realistic backtest runner risk section", RepaintLookaheadAuditReportIncludesRealisticRunnerRiskSection),
        new("repaint lookahead audit report includes old trade-summary backtest limitation section", RepaintLookaheadAuditReportIncludesOldTradeSummaryLimitationSection),
        new("repaint lookahead audit report includes AI leakage risk section", RepaintLookaheadAuditReportIncludesAiLeakageRiskSection),
        new("repaint lookahead audit report includes severity and status fields", RepaintLookaheadAuditReportIncludesSeverityAndStatusFields),
        new("strategy edge verdict report can be generated", StrategyEdgeVerdictReportCanBeGenerated),
        new("strategy edge verdict passing metrics produce Pass", StrategyEdgeVerdictPassingMetricsProducePass),
        new("strategy edge verdict weak metrics produce Fail", StrategyEdgeVerdictWeakMetricsProduceFail),
        new("strategy edge verdict small sample produces Inconclusive", StrategyEdgeVerdictSmallSampleProducesInconclusive),
        new("strategy edge verdict critical repaint finding forces Fail", StrategyEdgeVerdictCriticalRepaintFindingForcesFail),
        new("strategy edge verdict report includes not-live-proof warning", StrategyEdgeVerdictReportIncludesNotLiveProofWarning),
        new("strategy edge verdict report includes AI caution", StrategyEdgeVerdictReportIncludesAiCaution),
        new("demo paper reconciliation matching metrics returns Matches", DemoPaperReconciliationMatchingMetricsReturnsMatches),
        new("demo paper reconciliation large expectancy degradation returns Diverges", DemoPaperReconciliationLargeExpectancyDegradationReturnsDiverges),
        new("demo paper reconciliation too small sample returns Inconclusive", DemoPaperReconciliationTooSmallSampleReturnsInconclusive),
        new("demo paper reconciliation missing cost data produces warning", DemoPaperReconciliationMissingCostDataProducesWarning),
        new("demo paper reconciliation demo outperformance is handled safely", DemoPaperReconciliationDemoOutperformanceHandledSafely),
        new("demo paper reconciliation backtest no-trade data fails clearly", DemoPaperReconciliationBacktestNoTradeDataFailsClearly),
        new("final strategy proof package can be generated", FinalStrategyProofPackageCanBeGenerated),
        new("final strategy proof package strong evidence classifies proven positive edge", FinalStrategyProofPackageStrongEvidenceClassifiesProvenPositiveEdge),
        new("final strategy proof package weak negative evidence classifies negative edge", FinalStrategyProofPackageWeakNegativeEvidenceClassifiesNegativeEdge),
        new("final strategy proof package small sample classifies inconclusive", FinalStrategyProofPackageSmallSampleClassifiesInconclusive),
        new("final strategy proof package critical repaint blocks positive classification", FinalStrategyProofPackageCriticalRepaintBlocksPositiveClassification),
        new("final strategy proof package includes required risk warnings", FinalStrategyProofPackageIncludesRequiredRiskWarnings),
        new("final strategy proof package includes AI caution", FinalStrategyProofPackageIncludesAiCaution),
        new("strategy adapter converts BUY signal to realistic candidate", StrategyAdapterConvertsBuySignalToCandidate),
        new("strategy adapter converts SELL signal to realistic candidate", StrategyAdapterConvertsSellSignalToCandidate),
        new("strategy adapter skips HOLD signal", StrategyAdapterSkipsHoldSignal),
        new("strategy adapter skips incomplete signal", StrategyAdapterSkipsIncompleteSignal),
        new("strategy adapter stays separate from live execution", StrategyAdapterStaysSeparateFromLiveExecution),
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

    private static async Task RolloutPaperOnlyBlocksLiveOrders()
    {
        var config = RolloutConfig(RolloutStage.PaperOnly);

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "PaperOnly rollout stage must block real live execution.");
        AssertEqual("ROLLOUT_STAGE_BLOCKED", result.ErrorCode, "Rollout stage should provide a dedicated block code.");
        AssertEqual(0, mt5.OpenTradeCalls, "PaperOnly rollout must block before broker execution.");
    }

    private static async Task RolloutDemoBlocksRealLiveOrders()
    {
        var config = RolloutConfig(RolloutStage.Demo);

        await using var mt5 = new FakeMt5Server(Account(), Symbol(spreadPoints: 10));
        await using var bot = new AutoBotService(
            Bridge(mt5.Port),
            config,
            apiConfig: NewsDisabled());

        var result = await bot.ExecuteTradeWithValidationAsync(BuyRequest()).ConfigureAwait(false);

        AssertFalse(result.IsSuccess, "Demo rollout stage must not send real live orders.");
        AssertEqual("ROLLOUT_STAGE_BLOCKED", result.ErrorCode, "Demo rollout should block through the rollout gate.");
        AssertEqual(0, mt5.OpenTradeCalls, "Demo rollout must block before broker execution.");
    }

    private static async Task RolloutTinyLiveAppliesCappedRisk()
    {
        var config = RolloutConfig(RolloutStage.TinyLive);
        config.MaxRiskPercent = 1.0;
        config.MaxTinyLiveRiskPercent = 0.25;
        config.MaxTinyLiveLotMultiplier = 0.50;

        var result = await NewRiskManager().ValidateAsync(
            BuyRequest(),
            Account(),
            Symbol(spreadPoints: 10),
            [],
            config).ConfigureAwait(false);

        AssertTrue(result.IsApproved, result.Reason);
        AssertClose(0.05, result.ValidatedLotSize, 0.0001, "TinyLive should reduce auto-lot size to the tiny risk cap.");
        AssertClose(0.25, result.RiskPercent, 0.01, "TinyLive risk percent should stay at the tiny-live cap.");
    }

    private static Task RolloutScaleUpCriteriaCanRecommendAdvance()
    {
        var result = new RolloutEvaluator().Evaluate(new RolloutEvaluationInput
        {
            Config = RolloutConfig(RolloutStage.TinyLive),
            TinyLiveCompletedTrades = 40,
            TinyLiveElapsedDays = 20,
            TinyLiveProfitFactor = 1.30,
            ExplicitScaleUpConfirmation = true
        });

        AssertEqual(RolloutAction.Advance.ToString(), result.Action.ToString(), "Passing scale-up criteria with confirmation should recommend advance.");
        AssertEqual(RolloutStage.ScaledLive.ToString(), result.RecommendedStage.ToString(), "Recommended stage should be ScaledLive.");
        return Task.CompletedTask;
    }

    private static Task RolloutPoorDrawdownRecommendsRollback()
    {
        var result = EvaluateRollback(CurrentDrawdownPercent: 4.0);

        AssertEqual(RolloutAction.RollBack.ToString(), result.Action.ToString(), "Excess drawdown should recommend rollback.");
        AssertEqual(RolloutStage.RolledBack.ToString(), result.RecommendedStage.ToString(), "Rollback target should be RolledBack.");
        return Task.CompletedTask;
    }

    private static Task RolloutHighLosingStreakRecommendsRollback()
    {
        var result = EvaluateRollback(CurrentLosingStreak: 5);

        AssertEqual(RolloutAction.RollBack.ToString(), result.Action.ToString(), "High losing streak should recommend rollback.");
        AssertEqual(RolloutStage.RolledBack.ToString(), result.RecommendedStage.ToString(), "Rollback target should be RolledBack.");
        return Task.CompletedTask;
    }

    private static Task RolloutHighRejectionRateRecommendsRollback()
    {
        var result = EvaluateRollback(CurrentRejectionRate: 0.20);

        AssertEqual(RolloutAction.RollBack.ToString(), result.Action.ToString(), "High rejection rate should recommend rollback.");
        AssertEqual(RolloutStage.RolledBack.ToString(), result.RecommendedStage.ToString(), "Rollback target should be RolledBack.");
        return Task.CompletedTask;
    }

    private static Task RolloutCriticalRuntimeHealthRecommendsRollback()
    {
        var result = EvaluateRollback(RuntimeHealthCritical: true);

        AssertEqual(RolloutAction.RollBack.ToString(), result.Action.ToString(), "Critical runtime health should recommend rollback.");
        AssertEqual(RolloutStage.RolledBack.ToString(), result.RecommendedStage.ToString(), "Rollback target should be RolledBack.");
        return Task.CompletedTask;
    }

    private static Task RolloutKillSwitchActiveBlocksOrRollsBack()
    {
        var result = EvaluateRollback(KillSwitchActive: true);

        AssertTrue(
            result.Action is RolloutAction.Block or RolloutAction.RollBack,
            "Active kill switch should block or roll back rollout.");
        AssertEqual(RolloutStage.RolledBack.ToString(), result.RecommendedStage.ToString(), "Kill switch target should be RolledBack.");
        return Task.CompletedTask;
    }

    private static Task RolloutDoesNotAutoAdvanceWithoutExplicitConfirmation()
    {
        var result = new RolloutEvaluator().Evaluate(new RolloutEvaluationInput
        {
            Config = RolloutConfig(RolloutStage.TinyLive),
            TinyLiveCompletedTrades = 40,
            TinyLiveElapsedDays = 20,
            TinyLiveProfitFactor = 1.30,
            ExplicitScaleUpConfirmation = false
        });

        AssertEqual(RolloutAction.Stay.ToString(), result.Action.ToString(), "Scale-up must not auto-advance without explicit confirmation.");
        AssertEqual(RolloutStage.ScaledLive.ToString(), result.RecommendedStage.ToString(), "Evaluator may recommend ScaledLive while keeping action at Stay.");
        AssertTrue(result.Warnings.Count > 0, "Missing confirmation should be surfaced as a warning.");
        return Task.CompletedTask;
    }

    private static Task FinalGoNoGoAllGatesPassingReturnsGo()
    {
        var result = new FinalGoNoGoChecklist().Evaluate(AllPassingGoNoGoInput(FinalGoNoGoTarget.FullLive) with
        {
            AllowFullLiveGo = true
        });

        AssertEqual(FinalGoNoGoDecision.Go.ToString(), result.Decision.ToString(), "All required full-live gates should return Go only with explicit full-live release allowance.");
        AssertEqual(0, result.FailedCriteria.Count, "Passing checklist should have no failed criteria.");
        return Task.CompletedTask;
    }

    private static Task FinalGoNoGoMissingStrategyProofReturnsUnknown()
    {
        var result = new FinalGoNoGoChecklist().Evaluate(AllPassingGoNoGoInput(FinalGoNoGoTarget.FullLive) with
        {
            AllowFullLiveGo = true,
            P3StrategyEdgeProofReadiness = FinalChecklistStatus.Missing
        });

        AssertEqual(FinalGoNoGoDecision.Unknown.ToString(), result.Decision.ToString(), "Missing strategy proof should leave final live decision unknown.");
        AssertTrue(result.Warnings.Any(w => w.Contains("P3 strategy edge proof", StringComparison.OrdinalIgnoreCase)),
            "Missing strategy proof should be named in warnings.");
        return Task.CompletedTask;
    }

    private static Task FinalGoNoGoKillSwitchActiveReturnsNoGo()
    {
        var result = new FinalGoNoGoChecklist().Evaluate(AllPassingGoNoGoInput(FinalGoNoGoTarget.FullLive) with
        {
            AllowFullLiveGo = true,
            KillSwitchInactive = false
        });

        AssertEqual(FinalGoNoGoDecision.NoGo.ToString(), result.Decision.ToString(), "Active kill switch must force No-Go.");
        AssertTrue(result.FailedCriteria.Any(f => f.Contains("Kill switch", StringComparison.OrdinalIgnoreCase)),
            "Kill-switch failure should be listed.");
        return Task.CompletedTask;
    }

    private static Task FinalGoNoGoBrokerReadinessFailureReturnsNoGo()
    {
        var result = new FinalGoNoGoChecklist().Evaluate(AllPassingGoNoGoInput(FinalGoNoGoTarget.FullLive) with
        {
            AllowFullLiveGo = true,
            BrokerEaDeploymentChecklist = FinalChecklistStatus.Fail
        });

        AssertEqual(FinalGoNoGoDecision.NoGo.ToString(), result.Decision.ToString(), "Broker readiness failure must force No-Go.");
        AssertTrue(result.FailedCriteria.Any(f => f.Contains("Broker", StringComparison.OrdinalIgnoreCase)),
            "Broker readiness failure should be listed.");
        return Task.CompletedTask;
    }

    private static Task FinalGoNoGoCriticalRuntimeHealthReturnsNoGo()
    {
        var result = new FinalGoNoGoChecklist().Evaluate(AllPassingGoNoGoInput(FinalGoNoGoTarget.FullLive) with
        {
            AllowFullLiveGo = true,
            RuntimeHealthStatus = FinalRuntimeHealthStatus.Critical
        });

        AssertEqual(FinalGoNoGoDecision.NoGo.ToString(), result.Decision.ToString(), "Critical runtime health must force No-Go.");
        AssertTrue(result.FailedCriteria.Any(f => f.Contains("Runtime health", StringComparison.OrdinalIgnoreCase)),
            "Runtime health failure should be listed.");
        return Task.CompletedTask;
    }

    private static Task FinalGoNoGoDemoSetupReturnsConditionalGo()
    {
        var result = new FinalGoNoGoChecklist().Evaluate(AllPassingGoNoGoInput(FinalGoNoGoTarget.PaperOrDemo));

        AssertEqual(FinalGoNoGoDecision.ConditionalGo.ToString(), result.Decision.ToString(), "Demo/paper setup should be Conditional-Go when criteria support it.");
        AssertFalse(result.FailedCriteria.Any(), "Supported demo/paper setup should not have failed criteria.");
        return Task.CompletedTask;
    }

    private static Task FinalGoNoGoMissingEvidenceReturnsUnknown()
    {
        var result = new FinalGoNoGoChecklist().Evaluate(AllPassingGoNoGoInput(FinalGoNoGoTarget.FullLive) with
        {
            AllowFullLiveGo = true,
            P2RealisticBacktestReadiness = FinalChecklistStatus.Missing
        });

        AssertEqual(FinalGoNoGoDecision.Unknown.ToString(), result.Decision.ToString(), "Missing required evidence should return Unknown.");
        AssertTrue(result.Warnings.Any(w => w.Contains("P2 realistic backtest", StringComparison.OrdinalIgnoreCase)),
            "Missing P2 evidence should be listed.");
        return Task.CompletedTask;
    }

    private static Task FinalGoNoGoReportIncludesRequiredWarnings()
    {
        string folder = TestFolder();
        var result = new FinalGoNoGoChecklist().EvaluateAndWriteReport(AllPassingGoNoGoInput(FinalGoNoGoTarget.TinyLive) with
        {
            ReportDirectory = folder
        });

        AssertEqual(Path.Combine(folder, FinalGoNoGoChecklist.ReportFileName), result.ReportPath, "Report path should use the required file name.");
        string report = File.ReadAllText(result.ReportPath);
        AssertContains("This is not financial advice", report);
        AssertContains("Backtests are not live proof", report);
        AssertContains("Real-money trading remains blocked unless all Go criteria pass", report);
        AssertContains("Tiny-live must use reduced risk caps", report);
        AssertContains("User must manually confirm live enablement", report);
        return Task.CompletedTask;
    }

    private static Task FinalGoNoGoReportIncludesRequiredManualActions()
    {
        string folder = TestFolder();
        var result = new FinalGoNoGoChecklist().EvaluateAndWriteReport(AllPassingGoNoGoInput(FinalGoNoGoTarget.FullLive) with
        {
            ReportDirectory = folder,
            AllowFullLiveGo = true,
            UserLiveEnablementConfirmed = null
        });

        AssertEqual(FinalGoNoGoDecision.Unknown.ToString(), result.Decision.ToString(), "Missing user confirmation should keep decision unknown.");
        AssertTrue(result.RequiredManualActions.Any(a => a.Contains("user live enablement", StringComparison.OrdinalIgnoreCase)),
            "User confirmation should appear as a required manual action.");
        string report = File.ReadAllText(result.ReportPath);
        AssertContains("Required Manual Actions", report);
        AssertContains("Capture explicit user live enablement confirmation", report);
        return Task.CompletedTask;
    }

    private static async Task EvidencePackageCsvDoesNotSilentlyUseSampleFixture()
    {
        string folder = TestFolder();
        string tickCsv = WriteTempCsv(
            "timestamp,symbol,bid,ask,volume",
            "2026-05-03T10:00:00Z,EURUSD,1.10000,1.10005,10",
            "2026-05-03T10:01:00Z,EURUSD,1.10010,1.10015,12");

        var result = await new EvidencePackageCommand()
            .RunAsync(new EvidencePackageCommandRequest
            {
                OutputDirectory = folder,
                TickCsvPath = tickCsv
            })
            .ConfigureAwait(false);

        AssertTrue(result.UsedRealMarketData, "Provided tick CSV should be recognized as real market data input.");
        AssertFalse(result.UsedSampleFixture, "Provided CSV must not silently fall back to the sample fixture.");
        AssertEqual(2, result.TicksLoaded, "Tick count should reflect provided CSV rows.");
        AssertTrue(result.CandidatesGenerated > 0, "Provided CSV should generate offline candidates when price movement conditions are met.");
        string report = File.ReadAllText(Path.Combine(folder, RealisticBacktestReportCommand.DefaultReportFileName));
        AssertContains("- Sample fixture used: No", report);
        AssertContains("- Market data source: Configured CSV market data", report);
    }

    private static async Task EvidencePackageNoStrategyCandidatesReturnsClearDiagnostic()
    {
        string folder = TestFolder();
        string tickCsv = WriteTempCsv(
            "timestamp,symbol,bid,ask",
            "2026-05-03T10:00:00Z,EURUSD,1.10000,1.10005",
            "2026-05-03T10:01:00Z,EURUSD,1.10000,1.10005");

        var result = await new EvidencePackageCommand()
            .RunAsync(new EvidencePackageCommandRequest
            {
                OutputDirectory = folder,
                TickCsvPath = tickCsv
            })
            .ConfigureAwait(false);

        AssertEqual(
            "REAL_MARKET_DATA_LOADED_BUT_NO_STRATEGY_CANDIDATES",
            result.CandidateGenerationDiagnostic,
            "Flat market data should return a clear no-candidates diagnostic.");
        string report = File.ReadAllText(Path.Combine(folder, RealisticBacktestReportCommand.DefaultReportFileName));
        AssertContains("REAL_MARKET_DATA_LOADED_BUT_NO_STRATEGY_CANDIDATES", report);
        AssertContains("REALISTIC_BACKTEST_NO_CANDIDATES", report);
    }

    private static async Task EvidencePackageExplicitSampleFixtureStillWorks()
    {
        string folder = TestFolder();
        string tickCsv = WriteTempCsv(
            "timestamp,symbol,bid,ask",
            "2026-05-03T10:00:00Z,EURUSD,1.10000,1.10005");

        var result = await new EvidencePackageCommand()
            .RunAsync(new EvidencePackageCommandRequest
            {
                OutputDirectory = folder,
                TickCsvPath = tickCsv,
                UseSampleFixture = true
            })
            .ConfigureAwait(false);

        AssertTrue(result.UsedSampleFixture, "Explicit sample fixture flag should allow the built-in example.");
        AssertEqual(3, result.CandidatesGenerated, "Sample fixture should retain its three example candidates.");
        string report = File.ReadAllText(Path.Combine(folder, RealisticBacktestReportCommand.DefaultReportFileName));
        AssertContains("- Sample fixture used: Yes", report);
        AssertContains("candidate_source: Built-in minimal fixture", report);
    }

    private static async Task EvidencePackageReportMarksDataSourceCorrectly()
    {
        string folder = TestFolder();
        string ohlcCsv = WriteTempCsv(
            "timestamp,symbol,timeframe,open,high,low,close,spread_pips",
            "2026-05-03T10:00:00Z,EURUSD,M1,1.10000,1.10020,1.09990,1.10010,0.8",
            "2026-05-03T10:01:00Z,EURUSD,M1,1.10010,1.10040,1.10000,1.10030,0.8");

        var result = await new EvidencePackageCommand()
            .RunAsync(new EvidencePackageCommandRequest
            {
                OutputDirectory = folder,
                OhlcCsvPath = ohlcCsv
            })
            .ConfigureAwait(false);

        AssertEqual(2, result.CandlesLoaded, "OHLC count should reflect provided CSV rows.");
        string report = File.ReadAllText(Path.Combine(folder, RealisticBacktestReportCommand.DefaultReportFileName));
        AssertContains("| Candles loaded | 2 |", report);
        AssertContains("- Real strategy candidates used: Yes", report);
        AssertContains("- Candidate generation source: offline-auto-scalping-price-movement", report);
    }

    private static async Task EvidencePackageOhlcCsvGeneratesCandidates()
    {
        string folder = TestFolder();
        string ohlcCsv = WriteOhlcMovementCsv();

        var result = await new EvidencePackageCommand()
            .RunAsync(new EvidencePackageCommandRequest
            {
                OutputDirectory = folder,
                OhlcCsvPath = ohlcCsv,
                Config = EvidenceConfig(maxSpreadPips: 50)
            })
            .ConfigureAwait(false);

        AssertEqual(0, result.TicksLoaded, "OHLC-only evidence should not require ticks.");
        AssertTrue(result.CandlesLoaded > 0, "OHLC CSV rows should be loaded.");
        AssertTrue(result.CandidatesGenerated > 0, "Moving OHLC candles should generate offline candidates.");
        AssertEqual(
            "OFFLINE_AUTO_SCALPING_PRICE_MOVEMENT_CANDIDATES_GENERATED",
            result.CandidateGenerationDiagnostic,
            "Moving OHLC candles should use the generated-candidates diagnostic.");
    }

    private static async Task EvidencePackageOhlcMovementOmitsNoCandidatesDiagnostic()
    {
        string folder = TestFolder();
        string ohlcCsv = WriteOhlcMovementCsv();

        var result = await new EvidencePackageCommand()
            .RunAsync(new EvidencePackageCommandRequest
            {
                OutputDirectory = folder,
                OhlcCsvPath = ohlcCsv,
                Config = EvidenceConfig(maxSpreadPips: 50)
            })
            .ConfigureAwait(false);

        string report = File.ReadAllText(Path.Combine(folder, RealisticBacktestReportCommand.DefaultReportFileName));
        AssertFalse(
            string.Equals(
                "REAL_MARKET_DATA_LOADED_BUT_NO_STRATEGY_CANDIDATES",
                result.CandidateGenerationDiagnostic,
                StringComparison.Ordinal),
            "Moving OHLC evidence should not return the no-candidates diagnostic.");
        AssertFalse(report.Contains("REAL_MARKET_DATA_LOADED_BUT_NO_STRATEGY_CANDIDATES", StringComparison.Ordinal),
            "Realistic report should not include the no-candidates diagnostic when OHLC movement exists.");
        AssertContains("OFFLINE_AUTO_SCALPING_PRICE_MOVEMENT_CANDIDATES_GENERATED", report);
    }

    private static Task OhlcGeneratedCandidatesProduceBacktestTrades()
    {
        var candles = OhlcMovementCandles();
        var config = EvidenceConfig(maxSpreadPips: 50);
        var generation = new EvidenceStrategyCandidateGenerator().Generate([], candles, config);

        var result = RealisticBacktestRunner.Run(new RealisticBacktestRunInput
        {
            Candidates = generation.Candidates,
            Candles = candles,
            Config = config,
            SymbolInfoBySymbol = new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["EURUSD"] = BacktestBrokerSymbol(stopLevelPoints: 0, freezeLevelPoints: 0, volumeLimit: 0)
            }
        });

        AssertTrue(generation.Candidates.Count > 0, "OHLC movement should generate at least one candidate.");
        AssertTrue(result.Success, "Realistic runner should accept OHLC-generated candidates.");
        AssertTrue(result.SuccessfulTrades.Count > 0, "OHLC-generated candidates should resolve into completed trades.");
        return Task.CompletedTask;
    }

    private static Task OfflineCandidateGeneratorTickDataCanProduceCandidates()
    {
        var result = new EvidenceStrategyCandidateGenerator().Generate(
            [
                Tick("EURUSD", 0, 1.10000, 1.10005),
                Tick("EURUSD", 60, 1.10010, 1.10015)
            ],
            []);

        AssertEqual(1, result.Candidates.Count, "Rising tick mid should produce one BUY candidate after the initial hold.");
        AssertEqual(TradeType.BUY.ToString(), result.Candidates[0].Direction.ToString(), "Rising price movement should map to BUY in auto mode.");
        AssertEqual("auto-scalping / AI-disabled", result.Candidates[0].SourceType, "Offline candidate should identify source type and disabled AI boundary.");
        AssertTrue(result.Candidates[0].StopLoss < result.Candidates[0].EntryPrice, "BUY candidate should have SL below entry.");
        AssertTrue(result.Candidates[0].TakeProfit > result.Candidates[0].EntryPrice, "BUY candidate should have TP above entry.");
        AssertEqual("OFFLINE_AUTO_SCALPING_PRICE_MOVEMENT_CANDIDATES_GENERATED", result.DiagnosticCode, "Generated candidates should not use the not-implemented diagnostic.");
        return Task.CompletedTask;
    }

    private static Task OfflineCandidateGeneratorHoldSignalsAreCounted()
    {
        var result = new EvidenceStrategyCandidateGenerator().Generate(
            [
                Tick("EURUSD", 0, 1.10000, 1.10005),
                Tick("EURUSD", 60, 1.10000, 1.10005)
            ],
            []);

        AssertEqual(0, result.Candidates.Count, "Flat price movement should not create a trade candidate.");
        AssertTrue(result.SkippedOrHoldSignals >= 2, "Initial row and flat movement should count as hold/skipped signals.");
        return Task.CompletedTask;
    }

    private static Task OfflineCandidateGeneratorIncompleteSignalsAreCounted()
    {
        var config = Config();
        config.Scalping.StopLossPips = 0;

        var result = new EvidenceStrategyCandidateGenerator().Generate(
            [
                Tick("EURUSD", 0, 1.10000, 1.10005),
                Tick("EURUSD", 60, 1.10010, 1.10015)
            ],
            [],
            config);

        AssertEqual(0, result.Candidates.Count, "Invalid SL/TP config should prevent candidate creation.");
        AssertEqual(1, result.IncompleteSignals, "Invalid SL/TP config should count the signal as incomplete.");
        return Task.CompletedTask;
    }

    private static Task OfflineCandidateGeneratorDoesNotReferenceAiOrMt5Services()
    {
        string source = File.ReadAllText(Path.Combine(FindRepoRoot(), "Application", "LiveReadiness", "EvidenceStrategyCandidateGenerator.cs"));

        AssertFalse(source.Contains("MT5Bridge", StringComparison.Ordinal), "Offline generator must not reference MT5Bridge.");
        AssertFalse(source.Contains("ITradeExecutionService", StringComparison.Ordinal), "Offline generator must not reference live execution service.");
        AssertFalse(source.Contains("AutoBotService", StringComparison.Ordinal), "Offline generator must not reference AutoBotService.");
        AssertFalse(source.Contains("Claude", StringComparison.Ordinal), "Offline generator must not reference Claude/AI services.");
        AssertFalse(source.Contains("Anthropic", StringComparison.Ordinal), "Offline generator must not reference external AI packages.");
        return Task.CompletedTask;
    }

    private static Task OfflineGeneratedCandidatesFlowIntoRealisticRunner()
    {
        var ticks = new[]
        {
            Tick("EURUSD", 0, 1.10000, 1.10005),
            Tick("EURUSD", 60, 1.10010, 1.10015),
            Tick("EURUSD", 120, 1.10200, 1.10205)
        };
        var generation = new EvidenceStrategyCandidateGenerator().Generate(ticks, []);

        var result = RealisticBacktestRunner.Run(new RealisticBacktestRunInput
        {
            Candidates = generation.Candidates,
            Ticks = ticks,
            Config = Config()
        });

        AssertTrue(result.Success, "Generated candidates should be accepted by the realistic runner.");
        AssertTrue(result.SuccessfulTrades.Count > 0, "Future tick movement should allow at least one generated candidate to resolve.");
        return Task.CompletedTask;
    }

    private static async Task EvidenceReportOmitsNotImplementedDiagnosticWhenCandidatesGenerated()
    {
        string folder = TestFolder();
        string tickCsv = WriteTempCsv(
            "timestamp,symbol,bid,ask",
            "2026-05-03T10:00:00Z,EURUSD,1.10000,1.10005",
            "2026-05-03T10:01:00Z,EURUSD,1.10010,1.10015",
            "2026-05-03T10:02:00Z,EURUSD,1.10090,1.10095");

        var result = await new EvidencePackageCommand()
            .RunAsync(new EvidencePackageCommandRequest
            {
                OutputDirectory = folder,
                TickCsvPath = tickCsv
            })
            .ConfigureAwait(false);

        AssertTrue(result.CandidatesGenerated > 0, "Rising tick data should generate candidates.");
        string report = File.ReadAllText(Path.Combine(folder, RealisticBacktestReportCommand.DefaultReportFileName));
        AssertFalse(report.Contains(EvidenceStrategyCandidateGenerator.NotImplementedCode, StringComparison.Ordinal),
            "Report should not include the not-implemented diagnostic after candidates are generated.");
        AssertContains("OFFLINE_AUTO_SCALPING_PRICE_MOVEMENT_CANDIDATES_GENERATED", report);
    }

    private static async Task MarketDataAutoSyncStartupTriggerIsNonblocking()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks = [Tick("EURUSD", 0, 1.10000, 1.10005)],
            Delay = TimeSpan.FromMilliseconds(250)
        };
        using var started = new ManualResetEventSlim(false);
        provider.OnFetchStarted = () => started.Set();

        await using var sync = CreateAutoSyncForTest(provider, folder, TimeSpan.FromMinutes(30));
        var sw = Stopwatch.StartNew();
        sync.Start(runImmediately: true);
        sw.Stop();

        AssertTrue(sw.ElapsedMilliseconds < 100, "Startup sync scheduling should not block the caller.");
        AssertTrue(started.Wait(TimeSpan.FromSeconds(2)), "Startup sync should begin in the background.");
        sync.CancelActiveSync();
    }

    private static async Task MarketDataAutoSyncSkipsWhenAlreadyRunning()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks = [Tick("EURUSD", 0, 1.10000, 1.10005)],
            Delay = TimeSpan.FromMilliseconds(300)
        };

        await using var sync = CreateAutoSyncForTest(provider, folder, TimeSpan.FromMinutes(30));
        Task first = sync.TriggerSyncAsync("manual");
        await Task.Delay(50).ConfigureAwait(false);
        var second = await sync.TriggerSyncAsync("manual").ConfigureAwait(false);
        sync.CancelActiveSync();
        await first.ConfigureAwait(false);

        AssertTrue(second.Warnings.Any(w => w.Contains("already running", StringComparison.OrdinalIgnoreCase)),
            "Concurrent sync request should be skipped.");
        AssertEqual(1, provider.TickCalls, "Only the first sync should reach the provider.");
    }

    private static async Task MarketDataAutoSyncSkipsDuringCriticalTrading()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks = [Tick("EURUSD", 0, 1.10000, 1.10005)]
        };

        await using var sync = CreateAutoSyncForTest(
            provider,
            folder,
            TimeSpan.FromMinutes(30),
            allowSyncDuringTrading: false,
            criticalTradingInProgress: () => true);

        var summary = await sync.TriggerSyncAsync("manual").ConfigureAwait(false);

        AssertTrue(summary.Warnings.Any(w => w.Contains("critical trade execution", StringComparison.OrdinalIgnoreCase)),
            "Sync should be skipped while critical trade execution is in progress.");
        AssertEqual(0, provider.TickCalls, "Provider should not be called when trading skip gate is active.");
    }

    private static async Task MarketDataCliUpdateCommandPrintsStartedBanner()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks = [Tick("EURUSD", 0, 1.10000, 1.10005)]
        };
        using var output = new StringWriter();
        var command = new HistoricalMarketDataCommand(
            () => new HistoricalMarketDataUpdater(provider),
            output);

        int exitCode = await command.RunUpdateAsync(
            new AppSettings(),
            ["--update-market-data", "--symbols", "EURUSD", "--type", "tick", "--data-dir", folder, "--lookback-days", "1"])
            .ConfigureAwait(false);

        string text = output.ToString();
        AssertEqual(0, exitCode, "Successful CLI market-data update should return 0.");
        AssertContains("MARKET_DATA_UPDATE_STARTED", text);
        AssertContains("raw args:", text);
        AssertContains("parsed symbols: EURUSD", text);
        AssertContains("parsed data type: Tick", text);
        AssertContains($"parsed data dir: {folder}", text);
        AssertContains("lookback days: 1", text);
        AssertContains("EURUSD: type=Tick", text);
        AssertContains(Path.Combine(folder, "EURUSD_ticks.csv"), text);
    }

    private static async Task MarketDataCliUpdateCommandReturnsFailureCode()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            TickError = "MT5 unavailable"
        };
        using var output = new StringWriter();
        var command = new HistoricalMarketDataCommand(
            () => new HistoricalMarketDataUpdater(provider),
            output);

        int exitCode = await command.RunUpdateAsync(
            new AppSettings(),
            ["--update-market-data", "--symbols", "EURUSD", "--type", "tick", "--data-dir", folder])
            .ConfigureAwait(false);

        string text = output.ToString();
        AssertEqual(1, exitCode, "Failed CLI market-data update should return non-zero.");
        AssertContains("MARKET_DATA_UPDATE_STARTED", text);
        AssertContains("failure reason:", text);
        AssertContains("MT5 unavailable", text);
    }

    private static async Task MarketDataCliUpdateCommandReportsMt5Unavailable()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Candles =
            [
                new BacktestOhlcCandle
                {
                    TimestampUtc = new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc),
                    Symbol = "XAUUSD",
                    Timeframe = "M1",
                    Open = 2300.0,
                    High = 2301.0,
                    Low = 2299.0,
                    Close = 2300.5
                }
            ]
        };
        using var output = new StringWriter();
        var command = new HistoricalMarketDataCommand(
            () => new HistoricalMarketDataUpdater(provider),
            output,
            () => Task.FromResult(false));

        int exitCode = await command.RunUpdateAsync(
            new AppSettings(),
            ["--update-market-data", "--symbols", "XAUUSD", "--type", "ohlc", "--data-dir", folder])
            .ConfigureAwait(false);

        string text = output.ToString();
        AssertEqual(1, exitCode, "Unavailable MT5 should return non-zero.");
        AssertContains("MARKET_DATA_UPDATE_STARTED", text);
        AssertContains(MarketDataSyncStatusText.Mt5Unavailable, text);
        AssertContains("diagnostic=MT5 bridge ping failed before historical data request.", text);
        AssertEqual(0, provider.OhlcCalls, "CLI should not call historical data provider when MT5 preflight fails.");
    }

    private static Task MarketDataEaHistoricalCommandsParseNestedPayloadDates()
    {
        string repo = FindRepoRoot();
        string eaPath = Path.Combine(repo, "MT5_EA", "TradingBotEA.mq5");
        string source = File.ReadAllText(eaPath);

        AssertContains("if(StringLen(data) == 0 || JsonLong(data, \"from_unix_ms\") <= 0 || JsonLong(data, \"to_unix_ms\") <= 0)", source);
        AssertContains("data = json;", source);
        AssertContains("CopyTicksRange", source);
        AssertContains("CopyRates", source);
        return Task.CompletedTask;
    }

    private static Task MarketDataUiDisabledStatusTextIsVisible()
    {
        string text = MarketDataSyncStatusText.Format(new HistoricalMarketDataSyncProgress
        {
            Status = HistoricalMarketDataSyncStatus.Skipped,
            Message = MarketDataSyncStatusText.Disabled
        });

        AssertEqual(MarketDataSyncStatusText.Disabled, text, "Disabled startup status should be directly visible.");
        return Task.CompletedTask;
    }

    private static async Task MarketDataStartupSyncEmitsProgressEvent()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks = [Tick("EURUSD", 0, 1.10000, 1.10005)]
        };
        var events = new List<HistoricalMarketDataSyncProgress>();

        await using var sync = CreateAutoSyncForTest(provider, folder, TimeSpan.FromMinutes(30));
        sync.ProgressChanged += events.Add;

        await sync.TriggerSyncAsync("startup").ConfigureAwait(false);

        AssertTrue(events.Any(e => e.Message == MarketDataSyncStatusText.Starting),
            "Startup sync should emit a starting progress event.");
        AssertTrue(events.Any(e => e.Status == HistoricalMarketDataSyncStatus.Completed),
            "Startup sync should emit completion when data is fetched.");
    }

    private static async Task MarketDataStartupSyncFailureIsVisible()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks = [Tick("EURUSD", 0, 1.10000, 1.10005)]
        };
        var events = new List<HistoricalMarketDataSyncProgress>();

        await using var sync = CreateAutoSyncForTest(
            provider,
            folder,
            TimeSpan.FromMinutes(30),
            mt5AvailabilityCheck: () => Task.FromResult(false));
        sync.ProgressChanged += events.Add;

        var summary = await sync.TriggerSyncAsync("startup").ConfigureAwait(false);

        AssertTrue(summary.Errors.Any(e => e == MarketDataSyncStatusText.Mt5Unavailable),
            "MT5 preflight failure should be returned in the summary.");
        AssertTrue(events.Any(e => e.Status == HistoricalMarketDataSyncStatus.Failed &&
                                   e.Message == MarketDataSyncStatusText.Mt5Unavailable),
            "MT5 preflight failure should be visible to the UI.");
    }

    private static Task ReviewDashboardMergesRichMt5PriceAndAccountSnapshot()
    {
        string source = File.ReadAllText(Path.Combine(FindRepoRoot(), "UI", "Forms", "MainForm.cs"));

        AssertContains("\"account\", \"price\", \"positions\", \"session\", \"candles\"", source);
        AssertContains("\"account\", \"symbol\", \"price\", \"positions\"", source);
        AssertContains("NormalizeReviewSnapshotForDisplay(snapshot);", source);
        AssertContains("account[\"margin_level\"] = null;", source);
        return Task.CompletedTask;
    }

    private static async Task MarketDataUpdaterCreatesNewTickFile()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            IgnoreSymbolFilter = true,
            Ticks =
            [
                Tick("EURUSD", 0, 1.10000, 1.10005),
                Tick("EURUSD", 60, 1.10010, 1.10015)
            ]
        };

        var summary = await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.Tick).ConfigureAwait(false);

        string path = Path.Combine(folder, "EURUSD_ticks.csv");
        AssertTrue(File.Exists(path), "Tick updater should create the expected output file.");
        AssertEqual(2, summary.SymbolResults.Single().RowsAfter, "Two fetched ticks should be written.");
        var loaded = await new CsvBacktestTickDataLoader().LoadAsync(path, "EURUSD").ConfigureAwait(false);
        AssertEqual(2, loaded.Count, "Generated tick CSV should load through the existing loader.");
    }

    private static async Task MarketDataUpdaterDoesNotCreateGenericTicksCsv()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks = [Tick("EURUSD", 0, 1.10000, 1.10005)]
        };

        await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.Tick).ConfigureAwait(false);

        AssertTrue(File.Exists(Path.Combine(folder, "EURUSD_ticks.csv")), "Per-symbol tick file should be created.");
        AssertFalse(File.Exists(Path.Combine(folder, "ticks.csv")), "Updater must not create or use generic ticks.csv.");
    }

    private static async Task MarketDataUpdaterAppendsOnlyNewRows()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "EURUSD_ticks.csv");
        File.WriteAllLines(path,
        [
            "timestamp,symbol,bid,ask,volume",
            "2026-05-03T10:00:00.000Z,EURUSD,1.10000,1.10005,1"
        ]);

        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks =
            [
                new BacktestTick { TimestampUtc = new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc), Symbol = "EURUSD", Bid = 1.10000, Ask = 1.10005, Volume = 1 },
                new BacktestTick { TimestampUtc = new DateTime(2026, 5, 3, 10, 1, 0, DateTimeKind.Utc), Symbol = "EURUSD", Bid = 1.10010, Ask = 1.10015, Volume = 1 }
            ]
        };

        var summary = await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.Tick).ConfigureAwait(false);
        var loaded = await new CsvBacktestTickDataLoader().LoadAsync(path, "EURUSD").ConfigureAwait(false);

        AssertTrue(provider.LastTickFromUtc > new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc),
            "Incremental update should request only data after the last existing timestamp.");
        AssertEqual(2, loaded.Count, "Existing row plus one new row should remain after deduplication.");
        AssertEqual(1, summary.SymbolResults.Single().RowsBefore, "Rows before should count existing file rows.");
    }

    private static async Task MarketDataUpdaterBackfillIgnoresExistingWatermark()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "EURUSD_M1.csv");
        File.WriteAllLines(path,
        [
            "timestamp,symbol,open,high,low,close,timeframe,spread",
            "2026-05-03T10:00:00.000Z,EURUSD,1.1000,1.1010,1.0990,1.1005,M1,1.0"
        ]);
        var provider = new FakeHistoricalMarketDataProvider
        {
            Candles =
            [
                new BacktestOhlcCandle
                {
                    TimestampUtc = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
                    Symbol = "EURUSD",
                    Timeframe = "M1",
                    Open = 1.0900,
                    High = 1.0910,
                    Low = 1.0890,
                    Close = 1.0905,
                    SpreadPips = 1.0
                },
                new BacktestOhlcCandle
                {
                    TimestampUtc = new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc),
                    Symbol = "EURUSD",
                    Timeframe = "M1",
                    Open = 1.1000,
                    High = 1.1010,
                    Low = 1.0990,
                    Close = 1.1005,
                    SpreadPips = 1.0
                }
            ]
        };
        var updater = new HistoricalMarketDataUpdater(provider);

        var summary = await updater.UpdateAsync(new HistoricalMarketDataUpdateRequest
        {
            Symbols = ["EURUSD"],
            DataDirectory = folder,
            PreferredDataType = MarketDataUpdateType.OHLC,
            LookbackDays = 30,
            MaxRowsPerUpdate = 100,
            MaxDaysPerUpdate = 7,
            Backfill = true,
            NowUtc = new DateTime(2026, 5, 4, 0, 0, 0, DateTimeKind.Utc)
        }).ConfigureAwait(false);
        var loaded = await new CsvBacktestOhlcDataLoader().LoadAsync(path, "EURUSD").ConfigureAwait(false);

        AssertTrue(provider.LastOhlcFromUtc < new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc),
            "Backfill should request from the lookback window instead of the last existing timestamp.");
        AssertEqual(2, loaded.Count, "Backfill should merge older fetched rows with existing data.");
        AssertEqual(2, summary.SymbolResults.Single().RowsAfter, "Rows after should include deduped backfill rows.");
    }

    private static async Task MarketDataUpdaterBackfillChunksLookbackRequests()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Candles =
            [
                new BacktestOhlcCandle
                {
                    TimestampUtc = new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc),
                    Symbol = "EURUSD",
                    Timeframe = "M1",
                    Open = 1.0800,
                    High = 1.0810,
                    Low = 1.0790,
                    Close = 1.0805,
                    SpreadPips = 1.0
                },
                new BacktestOhlcCandle
                {
                    TimestampUtc = new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Utc),
                    Symbol = "EURUSD",
                    Timeframe = "M1",
                    Open = 1.0900,
                    High = 1.0910,
                    Low = 1.0890,
                    Close = 1.0905,
                    SpreadPips = 1.0
                }
            ]
        };
        var updater = new HistoricalMarketDataUpdater(provider);

        var summary = await updater.UpdateAsync(new HistoricalMarketDataUpdateRequest
        {
            Symbols = ["EURUSD"],
            DataDirectory = folder,
            PreferredDataType = MarketDataUpdateType.OHLC,
            LookbackDays = 30,
            MaxRowsPerUpdate = 100,
            MaxDaysPerUpdate = 7,
            Backfill = true,
            NowUtc = new DateTime(2026, 5, 4, 0, 0, 0, DateTimeKind.Utc)
        }).ConfigureAwait(false);

        AssertTrue(provider.OhlcCalls > 1, "Backfill should request a large lookback in MaxDaysPerUpdate chunks.");
        AssertEqual(2, summary.SymbolResults.Single().RowsFetched, "Chunked backfill should aggregate rows from all windows.");
        AssertContains("Backfill requested GET_RATES", summary.SymbolResults.Single().ProviderDiagnostic);
    }

    private static async Task MarketDataUpdaterRemovesDuplicates()
    {
        string folder = TestFolder();
        var duplicateTime = new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc);
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks =
            [
                new BacktestTick { TimestampUtc = duplicateTime, Symbol = "EURUSD", Bid = 1.10000, Ask = 1.10005, Volume = 1 },
                new BacktestTick { TimestampUtc = duplicateTime, Symbol = "EURUSD", Bid = 1.10000, Ask = 1.10005, Volume = 1 }
            ]
        };

        await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.Tick).ConfigureAwait(false);

        string path = Path.Combine(folder, "EURUSD_ticks.csv");
        var loaded = await new CsvBacktestTickDataLoader().LoadAsync(path, "EURUSD").ConfigureAwait(false);
        AssertEqual(1, loaded.Count, "Duplicate ticks should be removed before validation.");
    }

    private static async Task MarketDataUpdaterAcceptsBrokerSuffixSymbols()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            IgnoreSymbolFilter = true,
            Ticks =
            [
                new BacktestTick
                {
                    TimestampUtc = new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc),
                    Symbol = "XAUUSDM",
                    Bid = 2300.10,
                    Ask = 2300.30
                }
            ]
        };

        var summary = await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.Tick, "XAUUSD")
            .ConfigureAwait(false);
        var loaded = await new CsvBacktestTickDataLoader()
            .LoadAsync(Path.Combine(folder, "XAUUSD_ticks.csv"), "XAUUSD")
            .ConfigureAwait(false);

        AssertTrue(summary.Errors.Count == 0, "Broker-suffixed provider symbols should not fail the update.");
        AssertEqual(1, loaded.Count, "Broker-suffixed provider rows should be normalized into the requested symbol file.");
        AssertEqual("XAUUSD", loaded[0].Symbol, "Persisted market data should use the requested symbol.");
    }

    private static async Task MarketDataUpdaterTreatsHeaderOnlyCacheAsEmpty()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "XAUUSD_M1.csv"), "timestamp,symbol,open,high,low,close,timeframe,spread" + Environment.NewLine);
        var provider = new FakeHistoricalMarketDataProvider
        {
            IgnoreSymbolFilter = true,
            Candles =
            [
                new BacktestOhlcCandle
                {
                    TimestampUtc = new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc),
                    Symbol = "XAUUSDM",
                    Timeframe = "M1",
                    Open = 2300.0,
                    High = 2301.0,
                    Low = 2299.0,
                    Close = 2300.5,
                    SpreadPips = 2.0
                }
            ]
        };

        var summary = await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.OHLC, "XAUUSD")
            .ConfigureAwait(false);
        var loaded = await new CsvBacktestOhlcDataLoader()
            .LoadAsync(Path.Combine(folder, "XAUUSD_M1.csv"), "XAUUSD")
            .ConfigureAwait(false);

        AssertTrue(summary.Errors.Count == 0, "Header-only cache should be treated as empty instead of crashing.");
        AssertEqual(1, loaded.Count, "Header-only cache should be replaced with fetched OHLC rows.");
        AssertEqual("XAUUSD", loaded[0].Symbol, "Persisted OHLC data should use the requested symbol.");
    }

    private static async Task MarketDataUpdaterTrimsOldTickRows()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "EURUSD_ticks.csv");
        File.WriteAllLines(path,
        [
            "timestamp,symbol,bid,ask,volume",
            "2026-02-01T10:00:00.000Z,EURUSD,1.09000,1.09005,1",
            "2026-05-03T10:00:00.000Z,EURUSD,1.10000,1.10005,1"
        ]);

        var provider = new FakeHistoricalMarketDataProvider();
        var summary = await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.Tick).ConfigureAwait(false);
        var loaded = await new CsvBacktestTickDataLoader().LoadAsync(path, "EURUSD").ConfigureAwait(false);

        AssertEqual(1, loaded.Count, "Tick retention should keep only recent rows.");
        AssertEqual(1, summary.SymbolResults.Single().RowsRemovedByRetention, "Rows removed by retention should be reported.");
    }

    private static async Task MarketDataUpdaterTrimsOldM1Rows()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "EURUSD_M1.csv");
        File.WriteAllLines(path,
        [
            "timestamp,symbol,open,high,low,close,timeframe,spread",
            "2025-01-01T10:00:00.000Z,EURUSD,1.0800,1.0810,1.0790,1.0805,M1,1.0",
            "2026-05-03T10:00:00.000Z,EURUSD,1.1000,1.1010,1.0990,1.1005,M1,1.0"
        ]);

        var provider = new FakeHistoricalMarketDataProvider();
        var summary = await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.OHLC).ConfigureAwait(false);
        var loaded = await new CsvBacktestOhlcDataLoader().LoadAsync(path, "EURUSD").ConfigureAwait(false);

        AssertEqual(1, loaded.Count, "OHLC retention should keep only rows within 365 days.");
        AssertEqual(1, summary.SymbolResults.Single().RowsRemovedByRetention, "OHLC retention removals should be reported.");
    }

    private static async Task MarketDataUpdaterFallsBackToOhlcWhenTicksUnavailable()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            TickError = "NO_TICK_HISTORY: broker returned no ticks",
            Candles =
            [
                new BacktestOhlcCandle
                {
                    TimestampUtc = new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc),
                    Symbol = "EURUSD",
                    Timeframe = "M1",
                    Open = 1.1000,
                    High = 1.1005,
                    Low = 1.0995,
                    Close = 1.1002,
                    SpreadPips = 1.0
                }
            ]
        };

        var summary = await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.TickThenOHLC).ConfigureAwait(false);
        var result = summary.SymbolResults.Single();

        AssertEqual(MarketDataUpdateType.OHLC.ToString(), result.DataTypeUsed.ToString(), "Fallback should write OHLC data.");
        AssertTrue(result.FallbackUsed, "Fallback flag should be set.");
        AssertTrue(File.Exists(Path.Combine(folder, "EURUSD_M1.csv")), "OHLC fallback file should be created.");
    }

    private static async Task MarketDataUpdaterZeroTickRowsFallBackToM1()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks = [],
            Candles =
            [
                new BacktestOhlcCandle
                {
                    TimestampUtc = new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc),
                    Symbol = "EURUSD",
                    Timeframe = "M1",
                    Open = 1.1000,
                    High = 1.1005,
                    Low = 1.0995,
                    Close = 1.1002,
                    SpreadPips = 1.0
                }
            ]
        };

        var summary = await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.TickThenOHLC).ConfigureAwait(false);
        var result = summary.SymbolResults.Single();

        AssertEqual(MarketDataUpdateType.OHLC.ToString(), result.DataTypeUsed.ToString(), "Zero tick rows should fall back to M1.");
        AssertTrue(result.FallbackUsed, "Zero tick row fallback should set fallback flag.");
        AssertTrue(summary.Warnings.Any(w => w.Contains("TICK_DATA_UNAVAILABLE_FALLING_BACK_TO_M1", StringComparison.Ordinal)),
            "Fallback warning code should be present.");
        AssertFalse(File.Exists(Path.Combine(folder, "EURUSD_ticks.csv")), "Empty tick response should not create an empty per-symbol tick file.");
        AssertTrue(File.Exists(Path.Combine(folder, "EURUSD_M1.csv")), "M1 fallback file should be written.");
    }

    private static async Task MarketDataUpdaterZeroTickAndM1RowsFailsClearly()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks = [],
            Candles = []
        };

        var summary = await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.TickThenOHLC).ConfigureAwait(false);

        AssertTrue(summary.Errors.Any(e => e.Contains("NO_MARKET_DATA_AVAILABLE", StringComparison.Ordinal)),
            "Zero tick and zero M1 rows should fail with NO_MARKET_DATA_AVAILABLE.");
        AssertFalse(File.Exists(Path.Combine(folder, "EURUSD_ticks.csv")), "Empty tick file should not be created.");
        AssertFalse(File.Exists(Path.Combine(folder, "EURUSD_M1.csv")), "Empty M1 file should not be created.");
    }

    private static async Task MarketDataUpdaterInvalidSymbolReturnsClearError()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            TickError = "INVALID_SYMBOL: Symbol not found: BAD"
        };

        var summary = await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.Tick, "BAD").ConfigureAwait(false);

        AssertTrue(summary.Errors.Any(e => e.Contains("INVALID_SYMBOL", StringComparison.OrdinalIgnoreCase)),
            "Invalid symbol should return a clear provider error.");
    }

    private static async Task MarketDataUpdaterGeneratedCsvValidatesWithLoader()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Candles =
            [
                new BacktestOhlcCandle
                {
                    TimestampUtc = new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc),
                    Symbol = "EURUSD",
                    Timeframe = "M1",
                    Open = 1.1000,
                    High = 1.1005,
                    Low = 1.0995,
                    Close = 1.1002,
                    SpreadPips = 1.2
                }
            ]
        };

        await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.OHLC).ConfigureAwait(false);

        var loaded = await new CsvBacktestOhlcDataLoader()
            .LoadAsync(Path.Combine(folder, "EURUSD_M1.csv"), "EURUSD")
            .ConfigureAwait(false);
        AssertEqual(1, loaded.Count, "Generated OHLC CSV should validate through the existing loader.");
        AssertClose(1.2, loaded[0].SpreadPips.GetValueOrDefault(), 0.0001, "Loader should accept the generated spread column alias.");
    }

    private static async Task MarketDataUpdaterCliOutputIncludesPerSymbolPath()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks = [Tick("EURUSD", 0, 1.10000, 1.10005)]
        };

        var summary = await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.Tick).ConfigureAwait(false);
        string output = string.Join(Environment.NewLine, MarketDataUpdateConsoleFormatter.Format(summary));

        AssertContains(Path.Combine(folder, "EURUSD_ticks.csv"), output);
        AssertContains("GET_TICKS", output);
        AssertContains("mt5_rows_returned=1", output);
    }

    private static async Task MarketDataUpdaterEmitsProgressEvents()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks = [Tick("EURUSD", 0, 1.10000, 1.10005)]
        };
        var events = new List<HistoricalMarketDataSyncProgress>();
        var progress = new Progress<HistoricalMarketDataSyncProgress>(events.Add);
        var updater = new HistoricalMarketDataUpdater(provider);

        await updater.UpdateAsync(new HistoricalMarketDataUpdateRequest
        {
            Symbols = ["EURUSD"],
            DataDirectory = folder,
            PreferredDataType = MarketDataUpdateType.Tick,
            NowUtc = new DateTime(2026, 5, 4, 0, 0, 0, DateTimeKind.Utc)
        }, progress).ConfigureAwait(false);

        await Task.Delay(50).ConfigureAwait(false);
        AssertTrue(events.Any(e => e.Status == HistoricalMarketDataSyncStatus.Syncing), "Updater should emit syncing progress.");
        AssertTrue(events.Any(e => e.Symbol == "EURUSD" && e.RowsFetched == 1), "Progress should include symbol and fetched rows.");
    }

    private static async Task MarketDataAutoSyncCancelStopsSafely()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks = [Tick("EURUSD", 0, 1.10000, 1.10005)],
            Delay = TimeSpan.FromSeconds(5)
        };
        var events = new List<HistoricalMarketDataSyncProgress>();

        await using var sync = CreateAutoSyncForTest(provider, folder, TimeSpan.FromMinutes(30));
        sync.ProgressChanged += events.Add;

        Task<HistoricalMarketDataUpdateSummary> running = sync.TriggerSyncAsync("manual");
        await Task.Delay(100).ConfigureAwait(false);
        sync.CancelActiveSync();
        var summary = await running.ConfigureAwait(false);

        AssertTrue(summary.Warnings.Any(w => w.Contains("cancelled", StringComparison.OrdinalIgnoreCase)),
            "Cancellation should return a safe warning summary.");
        AssertTrue(events.Any(e => e.Status == HistoricalMarketDataSyncStatus.Cancelled),
            "Cancellation should emit a cancelled progress event.");
    }

    private static Task MarketDataUpdaterCliParsesArguments()
    {
        var options = HistoricalMarketDataCliOptions.Parse(
            ["--update-market-data", "--symbols", "EURUSD,GBPUSD", "--lookback-days", "30", "--data-dir", ".\\data", "--type", "tick-then-ohlc"]);

        AssertEqual(2, options.Symbols.Count, "CLI should parse comma-separated symbols.");
        AssertEqual(30, options.LookbackDays.GetValueOrDefault(), "CLI should parse lookback days.");
        AssertEqual(".\\data", options.DataDirectory ?? "", "CLI should parse data directory.");
        AssertEqual(MarketDataUpdateType.TickThenOHLC.ToString(), options.PreferredDataType.ToString() ?? "", "CLI should parse update type.");
        return Task.CompletedTask;
    }

    private static Task MarketDataUpdaterCliParsesBackfill()
    {
        var options = HistoricalMarketDataCliOptions.Parse(
            ["--update-market-data", "--symbols", "XAUUSD", "--lookback-days", "30", "--type", "ohlc", "--backfill", "--max-days-per-update", "1"]);

        AssertTrue(options.Backfill, "CLI should parse explicit backfill mode.");
        AssertEqual(1, options.MaxDaysPerUpdate.GetValueOrDefault(), "CLI should parse max days per update.");
        return Task.CompletedTask;
    }

    private static async Task MarketDataUpdaterDoesNotCallLiveTradeMethods()
    {
        string folder = TestFolder();
        var provider = new FakeHistoricalMarketDataProvider
        {
            Ticks = [Tick("EURUSD", 0, 1.10000, 1.10005)]
        };

        await RunMarketDataUpdateForTest(provider, folder, MarketDataUpdateType.Tick).ConfigureAwait(false);

        AssertEqual(0, provider.LiveTradeMethodCalls, "Market data updater should only call historical data provider methods.");
        AssertTrue(provider.TickCalls > 0, "Test should verify the historical tick path was used.");
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

    private static Task BacktestNoTradeFilterBlocksRolloverWindow()
    {
        var result = BacktestNoTradeFilterSimulator.Evaluate(FilterInput(
            timestampUtc: TestUtc(23, 58),
            config: RolloverConfig("23:55", "00:10")));

        AssertFilterReject(result, "BACKTEST_NO_TRADE_WINDOW", "rollover", "NoTradeWindow");
        AssertContains("rollover", result.RejectionReason);
        return Task.CompletedTask;
    }

    private static Task BacktestNoTradeFilterBlocksAdditionalWindow()
    {
        var config = RolloverConfig("23:55", "00:10");
        config.AdditionalNoTradeWindows.Add(new NoTradeWindowConfig
        {
            Name = "London close",
            StartUtc = "15:00",
            EndUtc = "16:00"
        });

        var result = BacktestNoTradeFilterSimulator.Evaluate(FilterInput(
            timestampUtc: TestUtc(15, 30),
            config: config));

        AssertFilterReject(result, "BACKTEST_NO_TRADE_WINDOW", "London close", "NoTradeWindow");
        return Task.CompletedTask;
    }

    private static Task BacktestNoTradeFilterBlocksHighSessionSpread()
    {
        var result = BacktestNoTradeFilterSimulator.Evaluate(FilterInput(
            timestampUtc: TestUtc(10, 30),
            config: SessionSpreadConfig(defaultMaxSpreadPips: 10, oldMaxSpreadPips: 20,
                SpreadRule("London", "10:00", "11:00", 2.0)),
            spreadPips: 3.0));

        AssertFilterReject(result, "BACKTEST_SESSION_SPREAD_LIMIT", "London", "SessionSpread");
        AssertContains("exceeds", result.RejectionReason);
        return Task.CompletedTask;
    }

    private static Task BacktestNoTradeFilterAllowsAcceptableSpread()
    {
        var result = BacktestNoTradeFilterSimulator.Evaluate(FilterInput(
            timestampUtc: TestUtc(10, 30),
            config: SessionSpreadConfig(defaultMaxSpreadPips: 10, oldMaxSpreadPips: 20,
                SpreadRule("London", "10:00", "11:00", 2.0)),
            spreadPips: 1.5));

        AssertTrue(result.Allowed, "Acceptable historical spread should allow filter simulation to continue.");
        AssertEqual("", result.RejectionCode, "Allowed filter result should not carry a rejection code.");
        return Task.CompletedTask;
    }

    private static Task BacktestNoTradeFilterBlocksHistoricalHighImpactNews()
    {
        var result = BacktestNoTradeFilterSimulator.Evaluate(FilterInput(
            timestampUtc: TestUtc(9, 45),
            newsConfig: NewsRequired(),
            newsEvents:
            [
                NewsEvent(TestUtc(10, 0), currency: "USD", impact: "High", title: "US CPI")
            ]));

        AssertFilterReject(result, "BACKTEST_NEWS_BLACKOUT", "US CPI", "News");
        AssertContains("news blackout", result.RejectionReason);
        return Task.CompletedTask;
    }

    private static Task BacktestNoTradeFilterAllowsUnrelatedNews()
    {
        var result = BacktestNoTradeFilterSimulator.Evaluate(FilterInput(
            timestampUtc: TestUtc(9, 45),
            newsConfig: NewsRequired(),
            newsEvents:
            [
                NewsEvent(TestUtc(10, 0), currency: "AUD", impact: "High", title: "AU CPI")
            ]));

        AssertTrue(result.Allowed, "Unrelated historical news should not block EURUSD.");
        return Task.CompletedTask;
    }

    private static Task BacktestNoTradeFilterFailsClearlyOnMissingSpreadData()
    {
        var result = BacktestNoTradeFilterSimulator.Evaluate(FilterInput(
            timestampUtc: TestUtc(10, 30),
            config: SessionSpreadConfig(defaultMaxSpreadPips: 10, oldMaxSpreadPips: 20,
                SpreadRule("London", "10:00", "11:00", 2.0)),
            spreadPips: null));

        AssertFilterReject(result, "BACKTEST_SPREAD_DATA_UNAVAILABLE", "session-spread", "SessionSpread");
        AssertContains("spread data is unavailable", result.RejectionReason);
        return Task.CompletedTask;
    }

    private static Task BacktestNoTradeFilterFailsClearlyOnInvalidConfig()
    {
        var result = BacktestNoTradeFilterSimulator.Evaluate(FilterInput(
            timestampUtc: TestUtc(23, 58),
            config: RolloverConfig("not-a-time", "00:10")));

        AssertFilterReject(result, "BACKTEST_NO_TRADE_CONFIG_INVALID", "rollover", "InvalidConfig");
        AssertContains("invalid UTC start/end time", result.RejectionReason);
        return Task.CompletedTask;
    }

    private static Task BacktestNoTradeFilterResultIncludesMatchedFilterDetails()
    {
        var result = BacktestNoTradeFilterSimulator.Evaluate(FilterInput(
            timestampUtc: TestUtc(10, 30),
            config: SessionSpreadConfig(defaultMaxSpreadPips: 10, oldMaxSpreadPips: 20,
                SpreadRule("London", "10:00", "11:00", 2.0)),
            spreadPips: 3.0));

        AssertFalse(result.Allowed, "High spread should reject before checking matched filter details.");
        AssertEqual("London", result.MatchedFilterName, "Result should include the matched filter name.");
        AssertEqual("SessionSpread", result.MatchedFilterType, "Result should include the matched filter type.");
        AssertContains("EURUSD", result.RejectionReason);
        return Task.CompletedTask;
    }

    private static Task BacktestOutOfSampleSplitByRatioWorks()
    {
        var result = BacktestRobustnessTesting.SplitOutOfSample(
            RobustnessTrades(100, -25, 50, -10, 20),
            new OutOfSampleSplitConfig { InSampleRatio = 0.60 });

        AssertTrue(result.Success, "Valid ratio split should succeed.");
        AssertEqual(3, result.InSampleCount, "60% of five trades should create three in-sample trades.");
        AssertEqual(2, result.OutOfSampleCount, "Remaining trades should be out-of-sample.");
        AssertClose(125, result.InSampleProfitLossUsd, 0.0001, "In-sample P/L should sum first chronological partition.");
        AssertClose(10, result.OutOfSampleProfitLossUsd, 0.0001, "Out-of-sample P/L should sum remaining chronological partition.");
        return Task.CompletedTask;
    }

    private static Task BacktestOutOfSampleSplitByDateWorks()
    {
        var result = BacktestRobustnessTesting.SplitOutOfSample(
            RobustnessTrades(100, -25, 50, -10, 20),
            new OutOfSampleSplitConfig { SplitDateUtc = RobustnessUtc(4) });

        AssertTrue(result.Success, "Valid date split should succeed.");
        AssertEqual(3, result.InSampleCount, "Trades before split date should be in-sample.");
        AssertEqual(2, result.OutOfSampleCount, "Trades on or after split date should be out-of-sample.");
        AssertEqual("T4", result.OutOfSample[0].Id, "Date split should preserve chronological order.");
        return Task.CompletedTask;
    }

    private static Task BacktestOutOfSampleInvalidConfigFailsClearly()
    {
        var result = BacktestRobustnessTesting.SplitOutOfSample(
            RobustnessTrades(100, -25, 50),
            new OutOfSampleSplitConfig { InSampleRatio = 1.20 });

        AssertFalse(result.Success, "Invalid ratio should fail split generation.");
        AssertEqual("BACKTEST_SPLIT_CONFIG_INVALID", result.FailureCode, "Invalid split config should return a clear code.");
        AssertContains("ratio", result.FailureReason);
        return Task.CompletedTask;
    }

    private static Task BacktestWalkForwardWindowsAreGeneratedCorrectly()
    {
        var result = BacktestRobustnessTesting.GenerateWalkForwardWindows(new WalkForwardConfig
        {
            StartUtc = RobustnessUtc(1),
            EndUtc = RobustnessUtc(31),
            TrainingPeriod = TimeSpan.FromDays(10),
            TestingPeriod = TimeSpan.FromDays(5),
            StepSize = TimeSpan.FromDays(5)
        });

        AssertTrue(result.Success, "Valid walk-forward config should generate windows.");
        AssertEqual(4, result.Windows.Count, "Expected four rolling train/test windows.");
        AssertEqual(1, result.Windows[0].Index, "First walk-forward window should be indexed.");
        AssertEqual(RobustnessUtc(1).ToString("O"), result.Windows[0].TrainingStartUtc.ToString("O"),
            "First training window should start at configured start.");
        AssertEqual(RobustnessUtc(11).ToString("O"), result.Windows[0].TestingStartUtc.ToString("O"),
            "First testing window should begin after training period.");
        AssertEqual(RobustnessUtc(31).ToString("O"), result.Windows[3].TestingEndUtc.ToString("O"),
            "Last testing window should end exactly at configured end.");
        return Task.CompletedTask;
    }

    private static Task BacktestWalkForwardInvalidConfigFailsClearly()
    {
        var result = BacktestRobustnessTesting.GenerateWalkForwardWindows(new WalkForwardConfig
        {
            StartUtc = RobustnessUtc(1),
            EndUtc = RobustnessUtc(10),
            TrainingPeriod = TimeSpan.Zero,
            TestingPeriod = TimeSpan.FromDays(2),
            StepSize = TimeSpan.FromDays(1)
        });

        AssertFalse(result.Success, "Invalid walk-forward config should fail.");
        AssertEqual("BACKTEST_WALK_FORWARD_CONFIG_INVALID", result.FailureCode,
            "Invalid walk-forward config should return a clear code.");
        AssertContains("positive training", result.FailureReason);
        return Task.CompletedTask;
    }

    private static Task BacktestMonteCarloIsDeterministicWithFixedSeed()
    {
        double[] pnl = [100, -50, 25, -75, 50];
        var config = new MonteCarloConfig { StartingEquity = 10_000, Iterations = 25, Seed = 7 };

        var first = BacktestRobustnessTesting.RunMonteCarloTradeSequence(pnl, config);
        var second = BacktestRobustnessTesting.RunMonteCarloTradeSequence(pnl, config);

        AssertTrue(first.Success && second.Success, "Monte Carlo should succeed for valid inputs.");
        AssertClose(first.MaxDrawdownAmount.Average, second.MaxDrawdownAmount.Average, 0.0001,
            "Fixed seed should produce deterministic drawdown distribution.");
        AssertClose(first.SimulationResults[0].MaxDrawdownAmount, second.SimulationResults[0].MaxDrawdownAmount, 0.0001,
            "Fixed seed should produce deterministic first simulation.");
        AssertEqual(first.SimulationResults[0].WorstLosingStreak, second.SimulationResults[0].WorstLosingStreak,
            "Fixed seed should produce deterministic losing streaks.");
        return Task.CompletedTask;
    }

    private static Task BacktestMonteCarloReportsRobustnessStatistics()
    {
        var result = BacktestRobustnessTesting.RunMonteCarloTradeSequence(
            [100, -150, -50, 200, -25],
            new MonteCarloConfig { StartingEquity = 10_000, Iterations = 50, Seed = 3 });

        AssertTrue(result.Success, "Monte Carlo should succeed for non-empty trade P/L input.");
        AssertEqual(50, result.Iterations, "Monte Carlo result should report configured iterations.");
        AssertClose(10_075, result.FinalEquity.Min, 0.0001, "Final equity distribution should include final-equity min.");
        AssertClose(10_075, result.FinalEquity.Max, 0.0001, "Final equity distribution should include final-equity max.");
        AssertTrue(result.MaxDrawdownAmount.Max > 0, "Monte Carlo should report max drawdown distribution.");
        AssertTrue(result.WorstLosingStreak.Max >= 1, "Monte Carlo should report losing streak distribution.");
        AssertEqual(50, result.SimulationResults.Count, "Monte Carlo should retain per-iteration statistics.");
        return Task.CompletedTask;
    }

    private static Task BacktestMonteCarloEmptyTradeListFailsClearly()
    {
        var result = BacktestRobustnessTesting.RunMonteCarloTradeSequence(
            [],
            new MonteCarloConfig { StartingEquity = 10_000, Iterations = 10, Seed = 1 });

        AssertFalse(result.Success, "Empty Monte Carlo trade list should fail clearly.");
        AssertEqual("BACKTEST_MONTE_CARLO_NO_TRADES", result.FailureCode,
            "Empty Monte Carlo input should return a clear code.");
        AssertContains("At least one trade", result.FailureReason);
        return Task.CompletedTask;
    }

    private static Task BacktestReportingBasicMetricsCalculateCorrectly()
    {
        var report = BacktestReportingMetrics.BuildReport(ReportingTrades());

        AssertTrue(report.Success, "Valid reporting trades should produce metrics.");
        AssertEqual(5, report.Overall.TotalTrades, "Total trade count should include all trades.");
        AssertEqual(3, report.Overall.WinningTrades, "Winning trade count should include positive P/L trades.");
        AssertEqual(2, report.Overall.LosingTrades, "Losing trade count should include negative P/L trades.");
        AssertClose(60, report.Overall.WinRatePercent, 0.0001, "Win rate should be wins / total.");
        AssertClose(350, report.Overall.GrossProfitUsd, 0.0001, "Gross profit should sum positive P/L.");
        AssertClose(200, report.Overall.GrossLossUsd, 0.0001, "Gross loss should sum absolute negative P/L.");
        AssertClose(150, report.Overall.NetProfitUsd, 0.0001, "Net profit should sum all P/L.");
        AssertClose(1.75, report.Overall.ProfitFactor, 0.0001, "Profit factor should be gross profit / gross loss.");
        AssertClose(30, report.Overall.ExpectancyPerTradeUsd, 0.0001, "Expectancy should be net P/L per trade.");
        AssertClose(0.34, report.Overall.AverageRMultiple.GetValueOrDefault(), 0.0001,
            "Average R multiple should be calculated when R data exists.");
        return Task.CompletedTask;
    }

    private static Task BacktestReportingProfitFactorHandlesNoLossCaseSafely()
    {
        var report = BacktestReportingMetrics.BuildReport(
        [
            ReportTrade(100),
            ReportTrade(50, day: 2)
        ]);

        AssertTrue(report.Success, "No-loss report should still succeed.");
        AssertTrue(report.Overall.ProfitFactorUnlimited, "No-loss profitable report should mark profit factor as unlimited.");
        AssertTrue(double.IsPositiveInfinity(report.Overall.ProfitFactor),
            "No-loss profitable report should represent profit factor safely as positive infinity.");
        return Task.CompletedTask;
    }

    private static Task BacktestReportingMaxDrawdownCalculatesCorrectly()
    {
        var report = BacktestReportingMetrics.BuildReport(ReportingTrades());

        AssertClose(200, report.Overall.MaxDrawdownUsd, 0.0001,
            "Max drawdown should use cumulative P/L peak-to-trough movement.");
        return Task.CompletedTask;
    }

    private static Task BacktestReportingWorstLosingStreakCalculatesCorrectly()
    {
        var report = BacktestReportingMetrics.BuildReport(ReportingTrades());

        AssertEqual(2, report.Overall.WorstLosingStreak,
            "Worst losing streak should count consecutive negative P/L trades.");
        return Task.CompletedTask;
    }

    private static Task BacktestReportingCostsAggregateCorrectly()
    {
        var report = BacktestReportingMetrics.BuildReport(ReportingTrades());

        AssertClose(5.00, report.Overall.TotalCommissionUsd, 0.0001, "Commission should aggregate across trades.");
        AssertClose(2.50, report.Overall.TotalSlippageUsd, 0.0001, "Slippage should aggregate across trades.");
        AssertClose(7.50, report.Overall.TotalSpreadCostUsd, 0.0001, "Spread cost should aggregate across trades.");
        AssertClose(15.00, report.Overall.TotalExecutionCostUsd, 0.0001,
            "Total execution cost should sum commission, slippage, and spread cost.");
        AssertTrue(report.Warnings.Count == 0, "Complete reporting trades should not produce missing-data warnings.");
        return Task.CompletedTask;
    }

    private static Task BacktestReportingGroupingBySymbolWorks()
    {
        var report = BacktestReportingMetrics.BuildReport(ReportingTrades());
        var eurusd = report.BySymbol.Single(g => g.Key == "EURUSD").Metrics;
        var gbpusd = report.BySymbol.Single(g => g.Key == "GBPUSD").Metrics;

        AssertEqual(3, eurusd.TotalTrades, "EURUSD group should include its trades.");
        AssertClose(250, eurusd.NetProfitUsd, 0.0001, "EURUSD group should calculate net P/L.");
        AssertEqual(2, gbpusd.TotalTrades, "GBPUSD group should include its trades.");
        AssertClose(-100, gbpusd.NetProfitUsd, 0.0001, "GBPUSD group should calculate net P/L.");
        return Task.CompletedTask;
    }

    private static Task BacktestReportingGroupingBySessionWorks()
    {
        var report = BacktestReportingMetrics.BuildReport(ReportingTrades());
        var london = report.BySession.Single(g => g.Key == "London").Metrics;
        var ny = report.BySession.Single(g => g.Key == "NewYork").Metrics;

        AssertEqual(3, london.TotalTrades, "London group should include its trades.");
        AssertClose(-100, london.NetProfitUsd, 0.0001, "London group should calculate net P/L.");
        AssertEqual(2, ny.TotalTrades, "NewYork group should include its trades.");
        AssertClose(250, ny.NetProfitUsd, 0.0001, "NewYork group should calculate net P/L.");
        return Task.CompletedTask;
    }

    private static Task BacktestReportingGroupingBySpreadRegimeWorks()
    {
        var report = BacktestReportingMetrics.BuildReport(ReportingTrades());
        var tight = report.BySpreadRegime.Single(g => g.Key == "Tight").Metrics;
        var wide = report.BySpreadRegime.Single(g => g.Key == "Wide").Metrics;

        AssertEqual(3, tight.TotalTrades, "Tight-spread group should include its trades.");
        AssertClose(350, tight.NetProfitUsd, 0.0001, "Tight-spread group should calculate net P/L.");
        AssertEqual(2, wide.TotalTrades, "Wide-spread group should include its trades.");
        AssertClose(-200, wide.NetProfitUsd, 0.0001, "Wide-spread group should calculate net P/L.");
        return Task.CompletedTask;
    }

    private static Task BacktestReportingEmptyTradeListFailsClearly()
    {
        var report = BacktestReportingMetrics.BuildReport([]);

        AssertFalse(report.Success, "Empty report input should fail clearly.");
        AssertEqual("BACKTEST_REPORT_NO_TRADES", report.FailureCode,
            "Empty report input should return a clear code.");
        AssertContains("No trades", report.FailureReason);
        return Task.CompletedTask;
    }

    private static Task SignalQualityMetricsCalculateMixedWinsAndLosses()
    {
        var report = StrategySignalQualityMetrics.BuildReport(new StrategySignalQualityInput
        {
            Outcomes =
            [
                QualityCompleted("W1", 100, exitMinutes: 10),
                QualityCompleted("L1", -40, minutes: 20, exitMinutes: 30),
                QualityRejected("R1", minutes: 40),
                QualityOpen("O1", minutes: 50)
            ],
            SkippedOrHeldSignals = 2,
            SourceByCandidateId = SourceMap(("W1", "deterministic/base strategy"), ("L1", "deterministic/base strategy")),
            RMultipleByCandidateId = RMap(("W1", 1.0), ("L1", -0.4)),
            AssumptionsUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["cost_basis"] = "realistic net outcome"
            }
        });

        AssertTrue(report.Success, "Mixed signal-quality input should produce a report.");
        AssertEqual(6, report.OverallMetrics.TotalSignals, "Total signals should include executable plus skipped/held.");
        AssertEqual(4, report.OverallMetrics.ExecutableSignals, "Executable signals should include completed, rejected, and open outcomes.");
        AssertEqual(2, report.OverallMetrics.SkippedOrHeldSignals, "Skipped/held signals should be retained.");
        AssertEqual(1, report.OverallMetrics.RejectedSignals, "Rejected signal count should include rejected outcomes.");
        AssertEqual(1, report.OverallMetrics.OpenTrades, "Open trade count should include unresolved outcomes.");
        AssertEqual(2, report.OverallMetrics.CompletedTrades, "Completed trade count should include successful outcomes only.");
        AssertEqual(1, report.OverallMetrics.WinningTrades, "Winning trades should use net P/L after costs.");
        AssertEqual(1, report.OverallMetrics.LosingTrades, "Losing trades should use net P/L after costs.");
        AssertClose(50, report.OverallMetrics.WinRateAfterCostsPercent, 0.0001, "Win rate should use completed trades only.");
        AssertClose(100, report.OverallMetrics.AverageWinAfterCostsUsd, 0.0001, "Average win should use net P/L after costs.");
        AssertClose(40, report.OverallMetrics.AverageLossAfterCostsUsd, 0.0001, "Average loss should use absolute net loss after costs.");
        AssertEqual("realistic net outcome", report.AssumptionsUsed["cost_basis"], "Assumptions should be preserved.");
        return Task.CompletedTask;
    }

    private static Task SignalQualityExpectancyAfterCostsIsCorrect()
    {
        var report = StrategySignalQualityMetrics.BuildReport(new StrategySignalQualityInput
        {
            Outcomes =
            [
                QualityCompleted("W1", 120, exitMinutes: 5),
                QualityCompleted("L1", -60, minutes: 10, exitMinutes: 20),
                QualityCompleted("L2", -30, minutes: 30, exitMinutes: 40)
            ]
        });

        AssertTrue(report.Success, "Completed trades should produce expectancy.");
        AssertClose(10, report.OverallMetrics.ExpectancyAfterCostsUsd, 0.0001,
            "Expectancy should be net P/L after costs divided by completed trades.");
        return Task.CompletedTask;
    }

    private static Task SignalQualityProfitFactorHandlesNoLossCaseSafely()
    {
        var report = StrategySignalQualityMetrics.BuildReport(new StrategySignalQualityInput
        {
            Outcomes =
            [
                QualityCompleted("W1", 50, exitMinutes: 5),
                QualityCompleted("W2", 100, minutes: 10, exitMinutes: 20)
            ]
        });

        AssertTrue(report.Success, "No-loss signal-quality report should succeed.");
        AssertTrue(report.OverallMetrics.ProfitFactorAfterCostsUnlimited,
            "No-loss profitable signal-quality report should mark profit factor as unlimited.");
        AssertTrue(double.IsPositiveInfinity(report.OverallMetrics.ProfitFactorAfterCosts),
            "No-loss profitable signal-quality report should safely represent profit factor as infinity.");
        return Task.CompletedTask;
    }

    private static Task SignalQualityWorstLosingStreakCalculatesCorrectly()
    {
        var report = StrategySignalQualityMetrics.BuildReport(new StrategySignalQualityInput
        {
            Outcomes =
            [
                QualityCompleted("W1", 50, exitMinutes: 5),
                QualityCompleted("L1", -10, minutes: 10, exitMinutes: 15),
                QualityCompleted("L2", -20, minutes: 20, exitMinutes: 25),
                QualityCompleted("W2", 40, minutes: 30, exitMinutes: 35),
                QualityCompleted("L3", -30, minutes: 40, exitMinutes: 45)
            ]
        });

        AssertEqual(2, report.OverallMetrics.WorstLosingStreak,
            "Worst losing streak should count consecutive completed net losing trades.");
        return Task.CompletedTask;
    }

    private static Task SignalQualityAverageDurationCalculatesWhenTimestampsExist()
    {
        var report = StrategySignalQualityMetrics.BuildReport(new StrategySignalQualityInput
        {
            Outcomes =
            [
                QualityCompleted("T1", 50, exitMinutes: 10),
                QualityCompleted("T2", -20, minutes: 20, exitMinutes: 50)
            ]
        });

        AssertTrue(report.OverallMetrics.AverageTradeDuration.HasValue,
            "Average trade duration should be calculated when exit timestamps exist.");
        AssertClose(20, report.OverallMetrics.AverageTradeDuration!.Value.TotalMinutes, 0.0001,
            "Average duration should be calculated from signal timestamp to exit timestamp.");
        return Task.CompletedTask;
    }

    private static Task SignalQualityGroupingBySignalSourceWorks()
    {
        var report = StrategySignalQualityMetrics.BuildReport(new StrategySignalQualityInput
        {
            Outcomes =
            [
                QualityCompleted("D1", 100, exitMinutes: 10),
                QualityCompleted("A1", -50, minutes: 20, exitMinutes: 30),
                QualityRejected("M1", minutes: 40)
            ],
            SourceByCandidateId = SourceMap(
                ("D1", "deterministic/base strategy"),
                ("A1", "auto-scalping"),
                ("M1", "manual user approved"))
        });

        var deterministic = report.MetricsBySignalSource.Single(g =>
            g.SignalSource == StrategySignalSourceLabels.DeterministicBaseStrategy);
        var auto = report.MetricsBySignalSource.Single(g =>
            g.SignalSource == StrategySignalSourceLabels.AutoScalping);
        var manual = report.MetricsBySignalSource.Single(g =>
            g.SignalSource == StrategySignalSourceLabels.ManualUserApproved);

        AssertEqual(1, deterministic.Metrics.CompletedTrades, "Deterministic group should include its completed trade.");
        AssertClose(100, deterministic.Metrics.ExpectancyAfterCostsUsd, 0.0001,
            "Deterministic group should calculate net expectancy.");
        AssertEqual(1, auto.Metrics.CompletedTrades, "Auto-scalping group should include its completed trade.");
        AssertEqual(1, manual.Metrics.RejectedSignals, "Manual/user-approved group should include its rejection.");
        return Task.CompletedTask;
    }

    private static Task SignalQualityMissingSourceGroupsAsUnknown()
    {
        var report = StrategySignalQualityMetrics.BuildReport(new StrategySignalQualityInput
        {
            Outcomes = [QualityCompleted("U1", 25, exitMinutes: 5)]
        });

        var unknown = report.MetricsBySignalSource.Single(g =>
            g.SignalSource == StrategySignalSourceLabels.Unknown);
        AssertEqual(1, unknown.Metrics.CompletedTrades, "Missing source should be grouped as unknown.");
        AssertContains("missing signal source metadata", string.Join(" ", report.Warnings));
        return Task.CompletedTask;
    }

    private static Task SignalQualityEmptyInputFailsClearly()
    {
        var report = StrategySignalQualityMetrics.BuildReport(new StrategySignalQualityInput());

        AssertFalse(report.Success, "Empty signal-quality input should fail clearly.");
        AssertEqual(StrategySignalQualityMetrics.NoDataCode, report.FailureCode,
            "Empty input should return a clear no-data code.");
        AssertContains("No signals", report.FailureReason);
        return Task.CompletedTask;
    }

    private static Task SegmentedPerformanceGroupingBySymbolWorks()
    {
        var report = StrategySegmentAnalyzer.BuildReport(new StrategySegmentAnalysisInput
        {
            Outcomes =
            [
                QualityCompleted("E1", 100, exitMinutes: 5, symbol: "EURUSD"),
                QualityCompleted("G1", -40, minutes: 10, exitMinutes: 15, symbol: "GBPUSD"),
                QualityCompleted("E2", 50, minutes: 20, exitMinutes: 25, symbol: "EURUSD")
            ]
        });

        var symbolGroup = SegmentGroup(report, "Symbol");
        var eurusd = Segment(symbolGroup, "EURUSD");
        var gbpusd = Segment(symbolGroup, "GBPUSD");

        AssertEqual(2, eurusd.TotalTrades, "EURUSD symbol segment should include its completed trades.");
        AssertClose(150, eurusd.NetProfitUsd, 0.0001, "EURUSD segment should calculate net P/L.");
        AssertEqual(1, gbpusd.TotalTrades, "GBPUSD symbol segment should include its completed trade.");
        AssertClose(-40, gbpusd.NetProfitUsd, 0.0001, "GBPUSD segment should calculate net P/L.");
        return Task.CompletedTask;
    }

    private static Task SegmentedPerformanceGroupingBySessionWorks()
    {
        var report = StrategySegmentAnalyzer.BuildReport(new StrategySegmentAnalysisInput
        {
            Outcomes =
            [
                QualityCompleted("L1", 100, exitMinutes: 5, session: "London"),
                QualityCompleted("N1", -25, minutes: 10, exitMinutes: 15, session: "NewYork"),
                QualityCompleted("L2", 50, minutes: 20, exitMinutes: 25, session: "London")
            ]
        });

        var sessionGroup = SegmentGroup(report, "Session");
        AssertEqual(2, Segment(sessionGroup, "London").TotalTrades,
            "London session segment should include its completed trades.");
        AssertClose(-25, Segment(sessionGroup, "NewYork").NetProfitUsd, 0.0001,
            "NewYork session segment should calculate net P/L.");
        return Task.CompletedTask;
    }

    private static Task SegmentedPerformanceGroupingBySpreadRegimeWorks()
    {
        var report = StrategySegmentAnalyzer.BuildReport(new StrategySegmentAnalysisInput
        {
            Outcomes =
            [
                QualityCompleted("T1", 80, exitMinutes: 5, spreadRegime: "Tight"),
                QualityCompleted("W1", -30, minutes: 10, exitMinutes: 15, spreadRegime: "Wide"),
                QualityCompleted("T2", 20, minutes: 20, exitMinutes: 25, spreadRegime: "Tight")
            ]
        });

        var spreadGroup = SegmentGroup(report, "Spread Regime");
        AssertEqual(2, Segment(spreadGroup, "Tight").TotalTrades,
            "Tight-spread segment should include its completed trades.");
        AssertClose(-30, Segment(spreadGroup, "Wide").NetProfitUsd, 0.0001,
            "Wide-spread segment should calculate net P/L.");
        return Task.CompletedTask;
    }

    private static Task SegmentedPerformanceMissingMetadataGoesToUnknown()
    {
        var report = StrategySegmentAnalyzer.BuildReport(new StrategySegmentAnalysisInput
        {
            Outcomes =
            [
                QualityCompleted("U1", 40, exitMinutes: 5, session: "", spreadRegime: "")
            ]
        });

        AssertEqual(1, Segment(SegmentGroup(report, "Session"), StrategySegmentAnalyzer.UnknownSegment).TotalTrades,
            "Missing session should group as Unknown.");
        AssertEqual(1, Segment(SegmentGroup(report, "Spread Regime"), StrategySegmentAnalyzer.UnknownSegment).TotalTrades,
            "Missing spread regime should group as Unknown.");
        AssertEqual(1, Segment(SegmentGroup(report, "Volatility Regime"), StrategySegmentAnalyzer.UnknownSegment).TotalTrades,
            "Missing volatility regime should group as Unknown.");
        AssertEqual(1, Segment(SegmentGroup(report, "Trend/Range Regime"), StrategySegmentAnalyzer.UnknownSegment).TotalTrades,
            "Missing trend/range regime should group as Unknown.");
        AssertEqual(1, Segment(SegmentGroup(report, "AI Confidence Bucket"), StrategySegmentAnalyzer.UnknownSegment).TotalTrades,
            "Missing AI confidence should group as Unknown.");
        AssertEqual(1, Segment(SegmentGroup(report, "Signal Reason/Source"), StrategySegmentAnalyzer.UnknownSegment).TotalTrades,
            "Missing signal reason/source should group as Unknown.");
        AssertContains("grouped as Unknown", string.Join(" ", report.Warnings));
        return Task.CompletedTask;
    }

    private static Task SegmentedPerformanceAiConfidenceBucketGroupingWorks()
    {
        var report = StrategySegmentAnalyzer.BuildReport(new StrategySegmentAnalysisInput
        {
            Outcomes =
            [
                QualityCompleted("A1", 30, exitMinutes: 5),
                QualityCompleted("A2", 40, minutes: 10, exitMinutes: 15),
                QualityCompleted("A3", -10, minutes: 20, exitMinutes: 25)
            ],
            AiConfidenceByCandidateId = ConfidenceMap(("A1", 45), ("A2", 75), ("A3", 92))
        });

        var aiGroup = SegmentGroup(report, "AI Confidence Bucket");
        AssertEqual(1, Segment(aiGroup, "0-49").TotalTrades, "Low AI confidence bucket should group correctly.");
        AssertEqual(1, Segment(aiGroup, "70-79").TotalTrades, "70s AI confidence bucket should group correctly.");
        AssertEqual(1, Segment(aiGroup, "90-100").TotalTrades, "90+ AI confidence bucket should group correctly.");
        return Task.CompletedTask;
    }

    private static Task SegmentedPerformanceSignalSourceReasonGroupingWorks()
    {
        var report = StrategySegmentAnalyzer.BuildReport(new StrategySegmentAnalysisInput
        {
            Outcomes =
            [
                QualityCompleted("S1", 70, exitMinutes: 5),
                QualityCompleted("S2", -20, minutes: 10, exitMinutes: 15),
                QualityCompleted("S3", 50, minutes: 20, exitMinutes: 25)
            ],
            SignalReasonByCandidateId = SourceMap(("S1", "M5 trend aligned")),
            SignalSourceByCandidateId = SourceMap(("S2", "auto-scalping"), ("S3", "AI confirmed"))
        });

        var sourceGroup = SegmentGroup(report, "Signal Reason/Source");
        AssertEqual(1, Segment(sourceGroup, "M5 trend aligned").TotalTrades,
            "Signal reason should be preferred when available.");
        AssertEqual(1, Segment(sourceGroup, StrategySignalSourceLabels.AutoScalping).TotalTrades,
            "Signal source should be normalized when reason is missing.");
        AssertEqual(1, Segment(sourceGroup, StrategySignalSourceLabels.AiConfirmed).TotalTrades,
            "AI source should be normalized when reason is missing.");
        return Task.CompletedTask;
    }

    private static Task SegmentedPerformanceMetricsCalculateCorrectly()
    {
        var report = StrategySegmentAnalyzer.BuildReport(new StrategySegmentAnalysisInput
        {
            Outcomes =
            [
                QualityCompleted("M1", 100, exitMinutes: 5, symbol: "EURUSD", totalExecutionCostUsd: 3),
                QualityCompleted("M2", -40, minutes: 10, exitMinutes: 15, symbol: "EURUSD", totalExecutionCostUsd: 4),
                QualityCompleted("M3", -20, minutes: 20, exitMinutes: 25, symbol: "EURUSD", totalExecutionCostUsd: 5),
                QualityCompleted("M4", 60, minutes: 30, exitMinutes: 35, symbol: "EURUSD", totalExecutionCostUsd: 6)
            ],
            AssumptionsUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["segment_scope"] = "completed realistic outcomes"
            }
        });

        var eurusd = Segment(SegmentGroup(report, "Symbol"), "EURUSD");
        AssertTrue(report.Success, "Segmented report should succeed for completed trades.");
        AssertEqual(4, eurusd.TotalTrades, "Segment total should include completed trades only.");
        AssertClose(50, eurusd.WinRatePercent, 0.0001, "Win rate should use positive net P/L trades.");
        AssertClose(100, eurusd.NetProfitUsd, 0.0001, "Net profit should sum net P/L after costs.");
        AssertClose(2.67, eurusd.ProfitFactor, 0.0001, "Profit factor should use gross win / gross loss.");
        AssertClose(25, eurusd.ExpectancyUsd, 0.0001, "Expectancy should use net P/L after costs per trade.");
        AssertClose(60, eurusd.MaxDrawdownUsd, 0.0001, "Max drawdown should use chronological segment equity.");
        AssertEqual(2, eurusd.WorstLosingStreak, "Worst losing streak should count consecutive segment losers.");
        AssertClose(80, eurusd.AverageWinUsd, 0.0001, "Average win should use segment winners.");
        AssertClose(30, eurusd.AverageLossUsd, 0.0001, "Average loss should use absolute segment losses.");
        AssertClose(18, eurusd.TotalExecutionCostUsd, 0.0001, "Total execution cost should aggregate costs.");
        AssertEqual("completed realistic outcomes", report.AssumptionsUsed["segment_scope"],
            "Segment assumptions should be preserved.");
        return Task.CompletedTask;
    }

    private static Task SegmentedPerformanceEmptyInputFailsClearly()
    {
        var report = StrategySegmentAnalyzer.BuildReport(new StrategySegmentAnalysisInput());

        AssertFalse(report.Success, "Empty segmented analysis input should fail clearly.");
        AssertEqual(StrategySegmentAnalyzer.NoDataCode, report.FailureCode,
            "Empty segmented analysis should return a clear no-data code.");
        AssertContains("No completed", report.FailureReason);
        return Task.CompletedTask;
    }

    private static Task CostSensitivitySpreadIncreaseReducesNetProfitCorrectly()
    {
        var report = CostSensitivityRunner.Run(new CostSensitivityInput
        {
            Outcomes =
            [
                QualityCompleted("C1", 95, exitMinutes: 5, grossProfitLossUsd: 100,
                    spreadCostUsd: 2, slippageCostUsd: 1, commissionCostUsd: 2)
            ],
            Scenarios = [new CostSensitivityScenario { Name = "spread x2", SpreadMultiplier = 2 }]
        });

        var scenario = report.ScenarioMetrics.Single();
        AssertTrue(report.Success, "Spread sensitivity report should succeed.");
        AssertClose(95, report.BaseMetrics.NetProfitUsd, 0.0001, "Base metrics should preserve original net P/L.");
        AssertClose(93, scenario.Metrics.NetProfitUsd, 0.0001,
            "Doubling spread cost from 2 to 4 should reduce net profit by 2.");
        AssertClose(-2, scenario.DegradationFromBase.NetProfitChangeUsd, 0.0001,
            "Degradation should report net profit change from base.");
        return Task.CompletedTask;
    }

    private static Task CostSensitivitySlippageIncreaseReducesNetProfitCorrectly()
    {
        var report = CostSensitivityRunner.Run(new CostSensitivityInput
        {
            Outcomes =
            [
                QualityCompleted("C1", 95, exitMinutes: 5, grossProfitLossUsd: 100,
                    spreadCostUsd: 2, slippageCostUsd: 1, commissionCostUsd: 2)
            ],
            Scenarios = [new CostSensitivityScenario { Name = "slippage x3", SlippageMultiplier = 3 }]
        });

        AssertClose(93, report.ScenarioMetrics.Single().Metrics.NetProfitUsd, 0.0001,
            "Tripling slippage cost from 1 to 3 should reduce net profit by 2.");
        return Task.CompletedTask;
    }

    private static Task CostSensitivityCommissionIncreaseReducesNetProfitCorrectly()
    {
        var report = CostSensitivityRunner.Run(new CostSensitivityInput
        {
            Outcomes =
            [
                QualityCompleted("C1", 95, exitMinutes: 5, grossProfitLossUsd: 100,
                    spreadCostUsd: 2, slippageCostUsd: 1, commissionCostUsd: 2)
            ],
            Scenarios = [new CostSensitivityScenario { Name = "commission x2", CommissionMultiplier = 2 }]
        });

        AssertClose(93, report.ScenarioMetrics.Single().Metrics.NetProfitUsd, 0.0001,
            "Doubling commission cost from 2 to 4 should reduce net profit by 2.");
        return Task.CompletedTask;
    }

    private static Task CostSensitivityCombinedWorseCostScenarioWorks()
    {
        var report = CostSensitivityRunner.Run(new CostSensitivityInput
        {
            Outcomes =
            [
                QualityCompleted("C1", 95, exitMinutes: 5, grossProfitLossUsd: 100,
                    spreadCostUsd: 2, slippageCostUsd: 1, commissionCostUsd: 2)
            ],
            Scenarios =
            [
                new CostSensitivityScenario
                {
                    Name = "combined stress",
                    SpreadMultiplier = 2,
                    AdditionalSpreadPips = 1,
                    SlippageMultiplier = 2,
                    AdditionalSlippagePips = 0.5,
                    CommissionMultiplier = 2,
                    AdditionalCommissionPerLot = 4
                }
            ],
            PipCostUsdPerPipByCandidateId = DoubleMap(("C1", 10)),
            LotSizeByCandidateId = DoubleMap(("C1", 0.5)),
            AssumptionsUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["cost_stress"] = "synthetic"
            }
        });

        var scenario = report.ScenarioMetrics.Single();
        AssertTrue(report.Success, "Combined stress report should succeed.");
        AssertClose(73, scenario.Metrics.NetProfitUsd, 0.0001,
            "Combined stress should apply multipliers, additional pips, and additional commission.");
        AssertClose(27, scenario.Metrics.TotalExecutionCostUsd, 0.0001,
            "Combined stress should recalculate total execution cost.");
        AssertEqual("synthetic", report.AssumptionsUsed["cost_stress"], "Assumptions should be preserved.");
        return Task.CompletedTask;
    }

    private static Task CostSensitivityWinToLossFlipCountWorks()
    {
        var report = CostSensitivityRunner.Run(new CostSensitivityInput
        {
            Outcomes =
            [
                QualityCompleted("C1", 5, exitMinutes: 5, grossProfitLossUsd: 10,
                    spreadCostUsd: 2, slippageCostUsd: 1, commissionCostUsd: 2),
                QualityCompleted("C2", 20, minutes: 10, exitMinutes: 15, grossProfitLossUsd: 25,
                    spreadCostUsd: 2, slippageCostUsd: 1, commissionCostUsd: 2)
            ],
            Scenarios = [new CostSensitivityScenario { Name = "spread x4", SpreadMultiplier = 4 }]
        });

        var scenario = report.ScenarioMetrics.Single();
        AssertEqual(1, scenario.Metrics.WinToLossFlipCount,
            "Only the marginal winner should flip from win to loss under higher costs.");
        AssertEqual(1, scenario.DegradationFromBase.WinToLossFlipCountChange,
            "Degradation should include win-to-loss flip count change.");
        return Task.CompletedTask;
    }

    private static Task CostSensitivityInvalidScenarioConfigFailsClearly()
    {
        var report = CostSensitivityRunner.Run(new CostSensitivityInput
        {
            Outcomes = [QualityCompleted("C1", 95, exitMinutes: 5)],
            Scenarios = [new CostSensitivityScenario { Name = "bad spread", SpreadMultiplier = -1 }]
        });

        AssertFalse(report.Success, "Invalid scenario config should fail clearly.");
        AssertEqual(CostSensitivityRunner.InvalidScenarioCode, report.FailureCode,
            "Invalid scenario config should return a clear code.");
        AssertContains("spread multiplier", report.FailureReason);
        return Task.CompletedTask;
    }

    private static Task CostSensitivityMissingCostFieldsProduceWarnings()
    {
        var report = CostSensitivityRunner.Run(new CostSensitivityInput
        {
            Outcomes =
            [
                QualityCompleted("C1", 50, exitMinutes: 5, grossProfitLossUsd: 50,
                    spreadCostUsd: 0, slippageCostUsd: 0, commissionCostUsd: 0)
            ],
            Scenarios =
            [
                new CostSensitivityScenario
                {
                    Name = "missing metadata add-ons",
                    AdditionalSpreadPips = 1,
                    AdditionalSlippagePips = 1,
                    AdditionalCommissionPerLot = 2
                }
            ]
        });

        string warnings = string.Join(" ", report.Warnings);
        AssertTrue(report.Success, "Missing cost metadata should warn without connecting to broker data.");
        AssertContains("zero spread cost", warnings);
        AssertContains("zero slippage cost", warnings);
        AssertContains("zero commission cost", warnings);
        AssertContains("pip-cost metadata is missing", warnings);
        AssertContains("lot-size metadata is missing", warnings);
        return Task.CompletedTask;
    }

    private static Task CostSensitivityEmptyInputFailsClearly()
    {
        var report = CostSensitivityRunner.Run(new CostSensitivityInput());

        AssertFalse(report.Success, "Empty cost sensitivity input should fail clearly.");
        AssertEqual(CostSensitivityRunner.NoDataCode, report.FailureCode,
            "Empty cost sensitivity input should return a clear no-data code.");
        AssertContains("No completed", report.FailureReason);
        return Task.CompletedTask;
    }

    private static Task StrategyRobustnessOosDegradationIsCalculated()
    {
        var report = StrategyRobustnessRunner.Run(RobustnessInput(
            [100, 100, -50, -50],
            thresholds: LenientRobustnessThresholds(maxOosDegradation: 500)));

        AssertTrue(report.Success, "Valid robustness input should succeed.");
        AssertClose(100, report.InSampleMetrics.ExpectancyUsd, 0.0001,
            "In-sample expectancy should be calculated from first chronological partition.");
        AssertClose(-50, report.OutOfSampleMetrics.ExpectancyUsd, 0.0001,
            "Out-of-sample expectancy should be calculated from second chronological partition.");
        AssertClose(-150, report.OutOfSampleDegradation.ExpectancyChangeUsd, 0.0001,
            "OOS degradation should compare OOS expectancy to in-sample expectancy.");
        return Task.CompletedTask;
    }

    private static Task StrategyRobustnessSmallSampleIsInconclusive()
    {
        var report = StrategyRobustnessRunner.Run(RobustnessInput(
            [20, 25],
            thresholds: new StrategyRobustnessThresholds
            {
                MinimumTotalTrades = 5,
                MinimumOutOfSampleTrades = 3,
                MaximumOosExpectancyDegradationUsd = 100,
                MaximumMonteCarloDrawdownUsd = 1_000,
                MaximumMonteCarloLosingStreak = 10
            }));

        AssertTrue(report.Success, "Small but valid sample should still produce a robustness report.");
        AssertEqual(StrategyRobustnessVerdicts.Inconclusive, report.Verdict,
            "Small sample should be inconclusive rather than pass.");
        AssertContains("sample is too small", string.Join(" ", report.Warnings));
        return Task.CompletedTask;
    }

    private static Task StrategyRobustnessMonteCarloSummaryIsIncluded()
    {
        var report = StrategyRobustnessRunner.Run(RobustnessInput(
            [100, -20, 80, -10, 70, -10],
            thresholds: LenientRobustnessThresholds(),
            monteCarlo: new MonteCarloConfig { StartingEquity = 10_000, Iterations = 25, Seed = 9 },
            walkForward: RobustnessWalkForwardConfig()));

        AssertTrue(report.Success, "Valid robustness input should include Monte Carlo summary.");
        AssertEqual(25, report.MonteCarloSummary.Iterations, "Monte Carlo summary should preserve iteration count.");
        AssertClose(10_210, report.MonteCarloSummary.FinalEquity.Min, 0.0001,
            "Final equity should be deterministic for fixed total P/L.");
        AssertTrue(report.MonteCarloSummary.MaxDrawdown.Max >= 0,
            "Monte Carlo max drawdown distribution should be included.");
        AssertTrue(report.MonteCarloSummary.WorstLosingStreak.Max >= 1,
            "Monte Carlo losing streak distribution should be included.");
        AssertTrue(report.WalkForwardWindows.Count > 0, "Walk-forward window summaries should be included when configured.");
        return Task.CompletedTask;
    }

    private static Task StrategyRobustnessFailingDrawdownThresholdReturnsFail()
    {
        var report = StrategyRobustnessRunner.Run(RobustnessInput(
            [100, -200, 80, 90],
            thresholds: new StrategyRobustnessThresholds
            {
                MinimumTotalTrades = 4,
                MinimumOutOfSampleTrades = 2,
                MaximumOosExpectancyDegradationUsd = 1_000,
                MaximumMonteCarloDrawdownUsd = 50,
                MaximumMonteCarloLosingStreak = 10
            }));

        AssertTrue(report.Success, "Drawdown threshold failure should still produce a report.");
        AssertEqual(StrategyRobustnessVerdicts.Fail, report.Verdict,
            "Drawdown threshold breach should fail the robustness verdict.");
        AssertContains("Monte Carlo max drawdown", string.Join(" ", report.FailedCriteria));
        return Task.CompletedTask;
    }

    private static Task StrategyRobustnessPassingThresholdsReturnPass()
    {
        var report = StrategyRobustnessRunner.Run(RobustnessInput(
            [100, -20, 80, -10, 70, -10],
            thresholds: LenientRobustnessThresholds()));

        AssertTrue(report.Success, "Valid passing robustness input should succeed.");
        AssertEqual(StrategyRobustnessVerdicts.Pass, report.Verdict,
            "Positive OOS expectancy and lenient thresholds should pass.");
        AssertEqual(0, report.FailedCriteria.Count, "Passing report should not include failed criteria.");
        return Task.CompletedTask;
    }

    private static Task StrategyRobustnessInvalidSplitConfigFailsClearly()
    {
        var report = StrategyRobustnessRunner.Run(RobustnessInput(
            [10, 20, 30],
            split: new OutOfSampleSplitConfig { InSampleRatio = 1.2 }));

        AssertFalse(report.Success, "Invalid split config should fail clearly.");
        AssertEqual(StrategyRobustnessRunner.SplitConfigInvalidCode, report.FailureCode,
            "Invalid split config should map to strategy robustness split failure.");
        AssertContains("split", report.FailureReason);
        return Task.CompletedTask;
    }

    private static Task StrategyRobustnessInvalidMonteCarloConfigFailsClearly()
    {
        var report = StrategyRobustnessRunner.Run(RobustnessInput(
            [10, 20, 30, 40],
            monteCarlo: new MonteCarloConfig { StartingEquity = 10_000, Iterations = 0, Seed = 1 }));

        AssertFalse(report.Success, "Invalid Monte Carlo config should fail clearly.");
        AssertEqual(StrategyRobustnessRunner.MonteCarloConfigInvalidCode, report.FailureCode,
            "Invalid Monte Carlo config should map to strategy robustness Monte Carlo failure.");
        AssertContains("Monte Carlo", report.FailureReason);
        return Task.CompletedTask;
    }

    private static Task StrategyRobustnessEmptyInputFailsClearly()
    {
        var report = StrategyRobustnessRunner.Run(new StrategyRobustnessInput());

        AssertFalse(report.Success, "Empty robustness input should fail clearly.");
        AssertEqual(StrategyRobustnessRunner.NoDataCode, report.FailureCode,
            "Empty robustness input should return a clear no-data code.");
        AssertContains("No completed", report.FailureReason);
        return Task.CompletedTask;
    }

    private static Task AiFilterImpactOutperformingNonAiReturnsImproves()
    {
        var report = AiFilterImpactAnalyzer.Analyze(new AiFilterImpactInput
        {
            Outcomes =
            [
                QualityCompleted("AI1", 100, exitMinutes: 5),
                QualityCompleted("AI2", 80, minutes: 10, exitMinutes: 15),
                QualityCompleted("D1", 20, minutes: 20, exitMinutes: 25),
                QualityCompleted("A1", -10, minutes: 30, exitMinutes: 35)
            ],
            SignalSourceByCandidateId = SourceMap(
                ("AI1", "AI-confirmed"),
                ("AI2", "Claude AI"),
                ("D1", "deterministic/base strategy"),
                ("A1", "auto-scalping")),
            AiConfidenceByCandidateId = DoubleMap(("AI1", 92), ("AI2", 84)),
            Thresholds = TinyAiThresholds()
        });

        AssertTrue(report.Success, "AI filter impact report should succeed.");
        AssertEqual(AiFilterImpactVerdicts.Improves, report.Verdict,
            "Higher AI-confirmed expectancy should return Improves.");
        AssertClose(90, report.OverallComparison.AiConfirmed.ExpectancyUsd, 0.0001,
            "AI expectancy should use AI-confirmed completed outcomes.");
        AssertClose(5, report.OverallComparison.NonAi.ExpectancyUsd, 0.0001,
            "Non-AI expectancy should combine deterministic/manual/auto cohorts.");
        AssertTrue(report.OverallComparison.AiOutperformsNonAi,
            "Comparison should flag AI outperformance.");
        return Task.CompletedTask;
    }

    private static Task AiFilterImpactUnderperformingNonAiReturnsHurts()
    {
        var report = AiFilterImpactAnalyzer.Analyze(new AiFilterImpactInput
        {
            Outcomes =
            [
                QualityCompleted("AI1", -40, exitMinutes: 5),
                QualityCompleted("AI2", -20, minutes: 10, exitMinutes: 15),
                QualityCompleted("D1", 60, minutes: 20, exitMinutes: 25),
                QualityCompleted("A1", 40, minutes: 30, exitMinutes: 35)
            ],
            SignalSourceByCandidateId = SourceMap(
                ("AI1", "AI-confirmed"),
                ("AI2", "AI-confirmed"),
                ("D1", "deterministic/base strategy"),
                ("A1", "auto-scalping")),
            AiConfidenceByCandidateId = DoubleMap(("AI1", 80), ("AI2", 75)),
            Thresholds = TinyAiThresholds()
        });

        AssertEqual(AiFilterImpactVerdicts.Hurts, report.Verdict,
            "Lower AI-confirmed expectancy should return Hurts.");
        AssertClose(-80, report.OverallComparison.ExpectancyDeltaAiVsNonAiUsd, 0.0001,
            "Expectancy delta should compare AI-confirmed to non-AI.");
        return Task.CompletedTask;
    }

    private static Task AiFilterImpactMissingComparisonGroupReturnsInconclusive()
    {
        var report = AiFilterImpactAnalyzer.Analyze(new AiFilterImpactInput
        {
            Outcomes =
            [
                QualityCompleted("AI1", 100, exitMinutes: 5),
                QualityCompleted("AI2", 80, minutes: 10, exitMinutes: 15)
            ],
            SignalSourceByCandidateId = SourceMap(("AI1", "AI-confirmed"), ("AI2", "AI-confirmed")),
            AiConfidenceByCandidateId = DoubleMap(("AI1", 90), ("AI2", 85)),
            Thresholds = TinyAiThresholds()
        });

        AssertEqual(AiFilterImpactVerdicts.Inconclusive, report.Verdict,
            "Missing non-AI comparison group should be inconclusive.");
        AssertContains("No non-AI", string.Join(" ", report.Warnings));
        return Task.CompletedTask;
    }

    private static Task AiFilterImpactConfidenceBucketGroupingWorks()
    {
        var report = AiFilterImpactAnalyzer.Analyze(new AiFilterImpactInput
        {
            Outcomes =
            [
                QualityCompleted("AI1", 20, exitMinutes: 5),
                QualityCompleted("AI2", 40, minutes: 10, exitMinutes: 15),
                QualityCompleted("D1", 10, minutes: 20, exitMinutes: 25)
            ],
            SignalSourceByCandidateId = SourceMap(
                ("AI1", "AI-confirmed"),
                ("AI2", "AI-confirmed"),
                ("D1", "deterministic/base strategy")),
            AiConfidenceByCandidateId = DoubleMap(("AI1", 74), ("AI2", 91)),
            Thresholds = TinyAiThresholds()
        });

        var bucket70 = report.ConfidenceBucketComparison.Single(b => b.Bucket == "70-79");
        var bucket90 = report.ConfidenceBucketComparison.Single(b => b.Bucket == "90-100");
        AssertEqual(1, bucket70.Metrics.CompletedTrades, "70-79 AI confidence bucket should include its trade.");
        AssertClose(20, bucket70.Metrics.ExpectancyUsd, 0.0001, "70-79 bucket should calculate expectancy.");
        AssertEqual(1, bucket90.Metrics.CompletedTrades, "90-100 AI confidence bucket should include its trade.");
        AssertClose(91, bucket90.Metrics.AverageAiConfidence.GetValueOrDefault(), 0.0001,
            "Bucket metrics should include average confidence.");
        return Task.CompletedTask;
    }

    private static Task AiFilterImpactBlockedWinnerLoserAnalysisWorks()
    {
        var report = AiFilterImpactAnalyzer.Analyze(new AiFilterImpactInput
        {
            Outcomes =
            [
                QualityCompleted("AI1", 20, exitMinutes: 5),
                QualityCompleted("D1", 10, minutes: 10, exitMinutes: 15)
            ],
            BlockedSignalCounterfactualOutcomes =
            [
                QualityCompleted("B1", 50, minutes: 20, exitMinutes: 25),
                QualityCompleted("B2", -30, minutes: 30, exitMinutes: 35),
                QualityCompleted("B3", 40, minutes: 40, exitMinutes: 45)
            ],
            SignalSourceByCandidateId = SourceMap(("AI1", "AI-confirmed"), ("D1", "deterministic/base strategy")),
            AiConfidenceByCandidateId = DoubleMap(("AI1", 82)),
            Thresholds = TinyAiThresholds()
        });

        AssertEqual(3, report.BlockedSignalAnalysis.BlockedSignalsWithCounterfactuals,
            "Blocked analysis should count simulated blocked outcomes.");
        AssertEqual(2, report.BlockedSignalAnalysis.BlockedWouldHaveWon,
            "Blocked analysis should count blocked winners.");
        AssertEqual(1, report.BlockedSignalAnalysis.BlockedWouldHaveLost,
            "Blocked analysis should count blocked losers.");
        AssertTrue(report.BlockedSignalAnalysis.AiMostlyBlocksWinners,
            "Blocked analysis should flag when AI mostly blocks winners.");
        AssertClose(60, report.BlockedSignalAnalysis.BlockedCounterfactualNetProfitUsd, 0.0001,
            "Blocked counterfactual net P/L should be summed.");
        return Task.CompletedTask;
    }

    private static Task AiFilterImpactMissingConfidenceProducesWarning()
    {
        var report = AiFilterImpactAnalyzer.Analyze(new AiFilterImpactInput
        {
            Outcomes =
            [
                QualityCompleted("AI1", 30, exitMinutes: 5),
                QualityCompleted("D1", 10, minutes: 10, exitMinutes: 15)
            ],
            SignalSourceByCandidateId = SourceMap(("AI1", "AI-confirmed"), ("D1", "deterministic/base strategy")),
            Thresholds = TinyAiThresholds()
        });

        AssertContains("missing AI confidence", string.Join(" ", report.Warnings));
        AssertFalse(report.OverallComparison.AiConfirmed.AverageAiConfidence.HasValue,
            "Missing confidence should leave average confidence empty.");
        return Task.CompletedTask;
    }

    private static Task AiFilterImpactSmallSampleIsInconclusive()
    {
        var report = AiFilterImpactAnalyzer.Analyze(new AiFilterImpactInput
        {
            Outcomes =
            [
                QualityCompleted("AI1", 100, exitMinutes: 5),
                QualityCompleted("D1", 10, minutes: 10, exitMinutes: 15)
            ],
            SignalSourceByCandidateId = SourceMap(("AI1", "AI-confirmed"), ("D1", "deterministic/base strategy")),
            AiConfidenceByCandidateId = DoubleMap(("AI1", 90)),
            Thresholds = new AiFilterImpactThresholds
            {
                MinimumAiConfirmedTrades = 3,
                MinimumNonAiTrades = 3,
                MinimumExpectancyDeltaUsd = 0
            }
        });

        AssertEqual(AiFilterImpactVerdicts.Inconclusive, report.Verdict,
            "Too-small samples should be inconclusive.");
        AssertContains("sample is too small", string.Join(" ", report.Warnings));
        return Task.CompletedTask;
    }

    private static Task RealisticBacktestRunnerRejectsNoTradeFilteredCandidate()
    {
        var result = RealisticBacktestRunner.Run(RealisticInput(
            [RealisticCandidate(timestampUtc: TestUtc(23, 58))],
            config: RolloverConfig("23:55", "00:10")));

        AssertTrue(result.Success, "Runner should complete with rejected candidate outcome.");
        AssertEqual(1, result.RejectedTrades.Count, "No-trade filter should produce one rejected outcome.");
        AssertEqual("BACKTEST_NO_TRADE_WINDOW", result.RejectedTrades[0].RejectionCode,
            "Rejected no-trade candidate should preserve filter rejection code.");
        AssertEqual(0, result.SuccessfulTrades.Count, "Rejected candidate should not become a successful trade.");
        return Task.CompletedTask;
    }

    private static Task RealisticBacktestRunnerRejectsBrokerRuleBlockedCandidate()
    {
        var result = RealisticBacktestRunner.Run(RealisticInput(
            [RealisticCandidate(stopLoss: 1.09995)],
            ticks: RunnerTicks((0, 1.10000, 1.10010)),
            symbolInfo: BacktestBrokerSymbol(stopLevelPoints: 50, freezeLevelPoints: 0, volumeLimit: 0)));

        AssertTrue(result.Success, "Runner should complete with broker-rule rejected candidate outcome.");
        AssertEqual(1, result.RejectedTrades.Count, "Broker rule should produce one rejected outcome.");
        AssertEqual("BACKTEST_BROKER_STOP_LEVEL", result.RejectedTrades[0].RejectionCode,
            "Broker-rule rejection should preserve broker simulator code.");
        AssertContains("stop level", result.RejectedTrades[0].RejectionReason);
        return Task.CompletedTask;
    }

    private static Task RealisticBacktestRunnerRecordsTpHitAsWinningTrade()
    {
        var result = RealisticBacktestRunner.Run(RealisticInput(
            [RealisticCandidate()],
            ticks: RunnerTicks(
                (0, 1.10000, 1.10010),
                (1, 1.10110, 1.10120))));

        AssertEqual(1, result.SuccessfulTrades.Count, "TP hit should produce one successful outcome.");
        var trade = result.SuccessfulTrades[0];
        AssertEqual(IntrabarExitType.TakeProfit.ToString(), trade.ExitType.ToString(),
            "TP hit should record take-profit exit type.");
        AssertTrue(trade.NetProfitLossUsd > 0, "TP outcome should remain profitable after default spread cost.");
        return Task.CompletedTask;
    }

    private static Task RealisticBacktestRunnerRecordsSlHitAsLosingTrade()
    {
        var result = RealisticBacktestRunner.Run(RealisticInput(
            [RealisticCandidate()],
            ticks: RunnerTicks(
                (0, 1.10000, 1.10010),
                (1, 1.09890, 1.09900))));

        AssertEqual(1, result.SuccessfulTrades.Count, "SL hit should still produce a completed trade outcome.");
        var trade = result.SuccessfulTrades[0];
        AssertEqual(IntrabarExitType.StopLoss.ToString(), trade.ExitType.ToString(),
            "SL hit should record stop-loss exit type.");
        AssertTrue(trade.NetProfitLossUsd < 0, "SL outcome should be losing after execution costs.");
        return Task.CompletedTask;
    }

    private static Task RealisticBacktestRunnerDeductsExecutionCosts()
    {
        var config = Config();
        config.EnableCommissionModel = true;
        config.CommissionPerLotPerSide = 5.0;
        config.CommissionMode = "RoundTurn";
        config.EnableSlippageModel = true;
        config.EstimatedSlippagePips = 0.5;
        config.MaxAllowedSlippagePips = 2.0;

        var result = RealisticBacktestRunner.Run(RealisticInput(
            [RealisticCandidate()],
            ticks: RunnerTicks(
                (0, 1.10000, 1.10010),
                (1, 1.10110, 1.10120)),
            config: config));

        var trade = result.SuccessfulTrades.Single();
        AssertTrue(trade.TotalExecutionCostUsd > 0, "Runner should apply spread, commission, and slippage costs.");
        AssertClose(
            trade.GrossProfitLossUsd - trade.TotalExecutionCostUsd,
            trade.NetProfitLossUsd,
            0.0001,
            "Net outcome should deduct total execution costs from gross P/L.");
        AssertClose(
            trade.CommissionCostUsd + trade.SlippageCostUsd + trade.SpreadCostUsd,
            trade.TotalExecutionCostUsd,
            0.0001,
            "Total execution cost should equal component costs.");
        return Task.CompletedTask;
    }

    private static Task RealisticBacktestRunnerRecordsUnresolvedTradeAsOpen()
    {
        var result = RealisticBacktestRunner.Run(RealisticInput(
            [RealisticCandidate()],
            ticks: RunnerTicks((0, 1.10000, 1.10010))));

        AssertEqual(1, result.OpenTrades.Count, "Candidate with no SL/TP hit should be recorded as open.");
        AssertEqual(0, result.SuccessfulTrades.Count, "Unresolved candidate should not become successful.");
        AssertEqual(0, result.RejectedTrades.Count, "Unresolved candidate should not become rejected.");
        AssertContains("no SL/TP hit", string.Join(" ", result.OpenTrades[0].Warnings));
        return Task.CompletedTask;
    }

    private static Task RealisticBacktestRunnerProducesMetricsReport()
    {
        var result = RealisticBacktestRunner.Run(RealisticInput(
            [
                RealisticCandidate(id: "WIN"),
                RealisticCandidate(id: "LOSS", timestampUtc: TestUtc(10, 1), takeProfit: 1.1030)
            ],
            ticks:
            [
                .. RunnerTicks((0, 1.10000, 1.10010), (1, 1.10110, 1.10120)),
                new BacktestTick
                {
                    TimestampUtc = TestUtc(10, 1),
                    Symbol = "EURUSD",
                    Bid = 1.10000,
                    Ask = 1.10010
                },
                new BacktestTick
                {
                    TimestampUtc = TestUtc(10, 1).AddSeconds(1),
                    Symbol = "EURUSD",
                    Bid = 1.09890,
                    Ask = 1.09900
                }
            ]));

        AssertTrue(result.MetricsReport.Success, "Runner should produce metrics for completed trade outcomes.");
        AssertEqual(2, result.MetricsReport.Overall.TotalTrades,
            "Metrics report should include completed successful outcomes.");
        AssertEqual(1, result.MetricsReport.Overall.WinningTrades,
            "Metrics report should include winning completed outcomes.");
        AssertEqual(1, result.MetricsReport.Overall.LosingTrades,
            "Metrics report should include losing completed outcomes.");
        return Task.CompletedTask;
    }

    private static async Task RealisticBacktestReportCanBeGeneratedWithoutMt5()
    {
        string outputPath = Path.Combine(TestFolder(), RealisticBacktestReportCommand.DefaultReportFileName);
        var result = await new RealisticBacktestReportCommand()
            .RunAsync(RealisticBacktestReportCommand.CreateMinimalExample(outputPath))
            .ConfigureAwait(false);

        AssertTrue(result.Success, "Report command should run without MT5 or broker dependencies.");
        AssertTrue(File.Exists(outputPath), "Report command should write the markdown report.");
        AssertEqual(
            RealisticBacktestReportCommand.DefaultReportFileName,
            Path.GetFileName(result.OutputPath),
            "Report filename should use the required default name.");
        AssertContains("Realistic Backtest Report", result.Markdown);
        AssertContains("No MT5 or live broker connection required", result.Markdown);
    }

    private static async Task RealisticBacktestReportIncludesOutcomeCounts()
    {
        string outputPath = Path.Combine(TestFolder(), RealisticBacktestReportCommand.DefaultReportFileName);
        var result = await new RealisticBacktestReportCommand()
            .RunAsync(RealisticBacktestReportCommand.CreateMinimalExample(outputPath))
            .ConfigureAwait(false);

        AssertContains("| Total candidates | 3 |", result.Markdown);
        AssertContains("| Completed trades | 1 |", result.Markdown);
        AssertContains("| Rejected trades | 1 |", result.Markdown);
        AssertContains("| Unresolved/open trades | 1 |", result.Markdown);
        AssertContains("BACKTEST_BROKER_STOP_LEVEL", result.Markdown);
    }

    private static async Task RealisticBacktestReportIncludesExecutionCosts()
    {
        string outputPath = Path.Combine(TestFolder(), RealisticBacktestReportCommand.DefaultReportFileName);
        var result = await new RealisticBacktestReportCommand()
            .RunAsync(RealisticBacktestReportCommand.CreateMinimalExample(outputPath))
            .ConfigureAwait(false);

        AssertContains("| Total commission |", result.Markdown);
        AssertContains("| Total slippage |", result.Markdown);
        AssertContains("| Total spread cost |", result.Markdown);
        AssertTrue(result.BacktestResult.MetricsReport.Overall.TotalExecutionCostUsd > 0,
            "Fixture report should include non-zero spread/commission/slippage costs.");
    }

    private static async Task RealisticBacktestReportIncludesAssumptionsAndWarnings()
    {
        string outputPath = Path.Combine(TestFolder(), RealisticBacktestReportCommand.DefaultReportFileName);
        var result = await new RealisticBacktestReportCommand()
            .RunAsync(RealisticBacktestReportCommand.CreateMinimalExample(outputPath))
            .ConfigureAwait(false);

        AssertContains("Assumptions And Warnings", result.Markdown);
        AssertContains("simulation only, not live proof", result.Markdown);
        AssertContains("not live proof", result.Markdown);
        AssertContains("Warning:", result.Markdown);
    }

    private static async Task RealisticBacktestReportCanLoadCsvMarketData()
    {
        string folder = TestFolder();
        Directory.CreateDirectory(folder);
        string tickCsv = Path.Combine(folder, "ticks.csv");
        File.WriteAllLines(tickCsv,
        [
            "timestamp,symbol,bid,ask",
            "2026-01-02T10:00:00Z,EURUSD,1.10000,1.10010",
            "2026-01-02T10:00:10Z,EURUSD,1.10110,1.10120"
        ]);

        string outputPath = Path.Combine(folder, RealisticBacktestReportCommand.DefaultReportFileName);
        var example = RealisticBacktestReportCommand.CreateMinimalExample(outputPath);
        var request = example with
        {
            TickCsvPath = tickCsv,
            Ticks = [],
            Candidates =
            [
                RealisticCandidate(
                    id: "CSV-WIN",
                    timestampUtc: new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc))
            ],
            SymbolInfoBySymbol = new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["EURUSD"] = BacktestBrokerSymbol(stopLevelPoints: 0, freezeLevelPoints: 0, volumeLimit: 0)
            }
        };

        var result = await new RealisticBacktestReportCommand()
            .RunAsync(request)
            .ConfigureAwait(false);

        AssertTrue(result.Success, "Report command should run from CSV market data.");
        AssertEqual(1, result.BacktestResult.SuccessfulTrades.Count,
            "CSV-loaded ticks should allow the candidate to complete.");
        AssertContains("CSV/provided bid-ask ticks", result.Markdown);
    }

    private static async Task StrategyExtractionReportCanBeGenerated()
    {
        string outputPath = Path.Combine(TestFolder(), StrategyExtractionReportGenerator.DefaultReportFileName);
        var result = await new StrategyExtractionReportGenerator()
            .GenerateAsync(FindRepoRoot(), outputPath)
            .ConfigureAwait(false);

        AssertTrue(result.Success, "Strategy extraction report generation should succeed.");
        AssertTrue(File.Exists(outputPath), "Strategy extraction report file should be written.");
        AssertEqual(
            StrategyExtractionReportGenerator.DefaultReportFileName,
            Path.GetFileName(result.OutputPath),
            "Strategy extraction report should use the required filename.");
        AssertContains("Strategy Extraction Report", result.Markdown);
    }

    private static async Task StrategyExtractionReportIncludesDeterministicLogicSection()
    {
        var result = await GenerateStrategyExtractionReportForTest().ConfigureAwait(false);

        AssertContains("Deterministic Rule Logic", result.Markdown);
        AssertContains("Base strategy direction: Hold", result.Markdown);
        AssertContains("base deterministic strategy currently produces mostly HOLD", result.Markdown);
        AssertContains("StrategyEngine.CreateInitialSignalAsync", result.Markdown);
    }

    private static async Task StrategyExtractionReportIncludesAiBoundarySection()
    {
        var result = await GenerateStrategyExtractionReportForTest().ConfigureAwait(false);

        AssertContains("AI-Assisted Logic Boundary", result.Markdown);
        AssertContains("ClaudeSignalService.ParseAndExecuteAsync", result.Markdown);
        AssertContains("SignalDecisionService.CreateDecisionAsync", result.Markdown);
    }

    private static async Task StrategyExtractionReportIncludesHoldNoTradeBehaviorSection()
    {
        var result = await GenerateStrategyExtractionReportForTest().ConfigureAwait(false);

        AssertContains("Hold/No-Trade Behavior", result.Markdown);
        AssertContains("NO_TRADE", result.Markdown);
        AssertContains("WAIT", result.Markdown);
        AssertContains("Not verified", result.Markdown);
    }

    private static async Task StrategyExtractionReportIncludesCodeEvidencePaths()
    {
        var result = await GenerateStrategyExtractionReportForTest().ConfigureAwait(false);

        AssertContains("Code Evidence", result.Markdown);
        AssertContains("Trading/StrategyEngine/StrategyEngine.cs", result.Markdown);
        AssertContains("Trading/Scalping/ScalpingSessionService.cs", result.Markdown);
        AssertContains("Application/Workflows/AutoBotService.cs", result.Markdown);
        AssertContains("UI/Forms/MainForm.cs", result.Markdown);
    }

    private static async Task RepaintLookaheadAuditReportCanBeGenerated()
    {
        string outputPath = Path.Combine(TestFolder(), RepaintLookaheadAuditReportGenerator.DefaultReportFileName);
        var result = await new RepaintLookaheadAuditReportGenerator()
            .GenerateAsync(FindRepoRoot(), outputPath)
            .ConfigureAwait(false);

        AssertTrue(result.Success, "Repaint/lookahead audit report generation should succeed.");
        AssertTrue(File.Exists(outputPath), "Repaint/lookahead audit report file should be written.");
        AssertEqual(
            RepaintLookaheadAuditReportGenerator.DefaultReportFileName,
            Path.GetFileName(result.OutputPath),
            "Repaint/lookahead audit report should use the required filename.");
        AssertContains("Repainting / Future-Data Bias Audit Report", result.Markdown);
    }

    private static async Task RepaintLookaheadAuditReportIncludesLiveSignalGenerationRiskSection()
    {
        var result = await GenerateRepaintLookaheadAuditReportForTest().ConfigureAwait(false);

        AssertContains("Live Signal-Generation Risk", result.Markdown);
        AssertContains("SnapshotCandleJson", result.Markdown);
        AssertContains("SnapshotPriceJson", result.Markdown);
        AssertContains("MT5_EA/TradingBotEA.mq5", result.Markdown);
    }

    private static async Task RepaintLookaheadAuditReportIncludesRealisticRunnerRiskSection()
    {
        var result = await GenerateRepaintLookaheadAuditReportForTest().ConfigureAwait(false);

        AssertContains("Realistic Backtest Runner Risk", result.Markdown);
        AssertContains("RealisticBacktestRunner.ResolveExit", result.Markdown);
        AssertContains("StrategyToRealisticBacktestAdapter.ResolveEntryPrice", result.Markdown);
        AssertContains("IntrabarExitSimulator.SimulateOhlcExit", result.Markdown);
    }

    private static async Task RepaintLookaheadAuditReportIncludesOldTradeSummaryLimitationSection()
    {
        var result = await GenerateRepaintLookaheadAuditReportForTest().ConfigureAwait(false);

        AssertContains("Old Trade-Summary Backtest Limitation", result.Markdown);
        AssertContains("DbBacktestLoader.LoadAsync", result.Markdown);
        AssertContains("BacktestingService.CalculatePips", result.Markdown);
        AssertContains("ProfitUsd", result.Markdown);
    }

    private static async Task RepaintLookaheadAuditReportIncludesAiLeakageRiskSection()
    {
        var result = await GenerateRepaintLookaheadAuditReportForTest().ConfigureAwait(false);

        AssertContains("AI-Prompt Leakage Risk", result.Markdown);
        AssertContains("AiPrompts.AiInputPromptTemplate", result.Markdown);
        AssertContains("TRADE HISTORY", result.Markdown);
        AssertContains("Last 5 Trades", result.Markdown);
    }

    private static async Task RepaintLookaheadAuditReportIncludesSeverityAndStatusFields()
    {
        var result = await GenerateRepaintLookaheadAuditReportForTest().ConfigureAwait(false);

        AssertContains("Severity", result.Markdown);
        AssertContains("Status", result.Markdown);
        AssertContains("Critical", result.Markdown);
        AssertContains("High", result.Markdown);
        AssertContains("Medium", result.Markdown);
        AssertContains("Low", result.Markdown);
        AssertContains("Confirmed", result.Markdown);
        AssertContains("Potential", result.Markdown);
        AssertContains("Not verified", result.Markdown);
    }

    private static async Task StrategyEdgeVerdictReportCanBeGenerated()
    {
        string outputPath = Path.Combine(TestFolder(), StrategyEdgeVerdictReportBuilder.DefaultReportFileName);
        var result = await new StrategyEdgeVerdictReportBuilder()
            .GenerateAsync(BuildPassingStrategyEdgeInput(), outputPath)
            .ConfigureAwait(false);

        AssertTrue(result.Success, "Strategy edge verdict report generation should succeed.");
        AssertTrue(File.Exists(outputPath), "Strategy edge verdict report file should be written.");
        AssertEqual(
            StrategyEdgeVerdictReportBuilder.DefaultReportFileName,
            Path.GetFileName(result.ReportPath),
            "Strategy edge verdict report should use the required filename.");
        AssertContains("Strategy Edge Verdict Report", result.Markdown);
        AssertContains("Executive Verdict", result.Markdown);
        AssertContains("Live-demo readiness score", result.Markdown);
    }

    private static async Task StrategyEdgeVerdictPassingMetricsProducePass()
    {
        string outputPath = Path.Combine(TestFolder(), StrategyEdgeVerdictReportBuilder.DefaultReportFileName);
        var result = await new StrategyEdgeVerdictReportBuilder()
            .GenerateAsync(BuildPassingStrategyEdgeInput(), outputPath)
            .ConfigureAwait(false);

        AssertEqual(StrategyEdgeVerdicts.Pass, result.Verdict,
            "Passing metrics and component reports should produce Pass.");
        AssertEqual(0, result.FailedCriteria.Count,
            "Passing verdict should not include failed criteria.");
    }

    private static async Task StrategyEdgeVerdictWeakMetricsProduceFail()
    {
        var input = BuildStrategyEdgeInput(
            [
                QualityCompleted("AI1", 10, exitMinutes: 5),
                QualityCompleted("AI2", -100, minutes: 10, exitMinutes: 15),
                QualityCompleted("D1", 5, minutes: 20, exitMinutes: 25),
                QualityCompleted("D2", -80, minutes: 30, exitMinutes: 35)
            ],
            StrategyEdgePassingCriteria() with
            {
                MinimumProfitFactorAfterCosts = 1.2,
                MinimumExpectancyAfterCostsUsd = 0
            },
            repaintMarkdown: NoCriticalRepaintMarkdown());

        string outputPath = Path.Combine(TestFolder(), StrategyEdgeVerdictReportBuilder.DefaultReportFileName);
        var result = await new StrategyEdgeVerdictReportBuilder()
            .GenerateAsync(input, outputPath)
            .ConfigureAwait(false);

        AssertEqual(StrategyEdgeVerdicts.Fail, result.Verdict,
            "Weak profit factor and expectancy should fail the verdict.");
        AssertContains("Profit factor", string.Join(" ", result.FailedCriteria));
        AssertContains("Expectancy", string.Join(" ", result.FailedCriteria));
    }

    private static async Task StrategyEdgeVerdictSmallSampleProducesInconclusive()
    {
        var input = BuildStrategyEdgeInput(
            [
                QualityCompleted("AI1", 100, exitMinutes: 5),
                QualityCompleted("D1", 50, minutes: 10, exitMinutes: 15)
            ],
            StrategyEdgePassingCriteria() with { MinimumCompletedTrades = 10 },
            repaintMarkdown: NoCriticalRepaintMarkdown());

        string outputPath = Path.Combine(TestFolder(), StrategyEdgeVerdictReportBuilder.DefaultReportFileName);
        var result = await new StrategyEdgeVerdictReportBuilder()
            .GenerateAsync(input, outputPath)
            .ConfigureAwait(false);

        AssertEqual(StrategyEdgeVerdicts.Inconclusive, result.Verdict,
            "Small sample should be inconclusive rather than passed as proof.");
        AssertContains("sample is too small", string.Join(" ", result.Warnings));
    }

    private static async Task StrategyEdgeVerdictCriticalRepaintFindingForcesFail()
    {
        var input = BuildPassingStrategyEdgeInput() with
        {
            RepaintLookaheadAuditMarkdown = CriticalRepaintMarkdown()
        };

        string outputPath = Path.Combine(TestFolder(), StrategyEdgeVerdictReportBuilder.DefaultReportFileName);
        var result = await new StrategyEdgeVerdictReportBuilder()
            .GenerateAsync(input, outputPath)
            .ConfigureAwait(false);

        AssertEqual(StrategyEdgeVerdicts.Fail, result.Verdict,
            "Critical repaint/lookahead finding should force failure when configured to fail.");
        AssertContains("Critical repaint/lookahead", string.Join(" ", result.FailedCriteria));
    }

    private static async Task StrategyEdgeVerdictReportIncludesNotLiveProofWarning()
    {
        string outputPath = Path.Combine(TestFolder(), StrategyEdgeVerdictReportBuilder.DefaultReportFileName);
        var result = await new StrategyEdgeVerdictReportBuilder()
            .GenerateAsync(BuildPassingStrategyEdgeInput(), outputPath)
            .ConfigureAwait(false);

        AssertContains("Backtest edge is not live proof", result.Markdown);
        AssertContains("Live demo/paper validation is still required", result.Markdown);
    }

    private static async Task StrategyEdgeVerdictReportIncludesAiCaution()
    {
        string outputPath = Path.Combine(TestFolder(), StrategyEdgeVerdictReportBuilder.DefaultReportFileName);
        var result = await new StrategyEdgeVerdictReportBuilder()
            .GenerateAsync(BuildPassingStrategyEdgeInput(), outputPath)
            .ConfigureAwait(false);

        AssertContains("AI should not be trusted unless AI impact analysis shows improvement", result.Markdown);
        AssertContains("AI filter verdict", result.Markdown);
    }

    private static Task DemoPaperReconciliationMatchingMetricsReturnsMatches()
    {
        var report = DemoPaperReconciliationAnalyzer.Analyze(new DemoPaperReconciliationInput
        {
            BacktestOutcomes = ReconciliationBaseline("B"),
            DemoPaperOutcomes = ReconciliationBaseline("D"),
            Tolerances = ReconciliationTolerances()
        });

        AssertTrue(report.Success, "Matching demo/paper reconciliation input should succeed.");
        AssertEqual(DemoPaperReconciliationVerdicts.Matches, report.Verdict,
            "Matching demo/paper metrics should return Matches.");
        AssertEqual(4, report.BacktestMetrics.TotalTrades,
            "Backtest total trades should include completed and rejected outcomes.");
        AssertEqual(3, report.DemoPaperMetrics.CompletedTrades,
            "Demo/paper completed trades should be counted.");
        AssertClose(25, report.DemoPaperMetrics.RejectionRatePercent, 0.0001,
            "Rejection rate should include rejected demo/paper outcomes.");
        AssertClose(0, report.Deltas.ExpectancyChangeUsd, 0.0001,
            "Matching demo/paper expectancy should have zero delta.");
        AssertTrue(report.DemoPaperMetrics.AverageTradeDuration.HasValue,
            "Trade duration should calculate when timestamps exist.");
        return Task.CompletedTask;
    }

    private static Task DemoPaperReconciliationLargeExpectancyDegradationReturnsDiverges()
    {
        var report = DemoPaperReconciliationAnalyzer.Analyze(new DemoPaperReconciliationInput
        {
            BacktestOutcomes = ReconciliationBaseline("B"),
            DemoPaperOutcomes =
            [
                ReconciliationCompleted("D1", 20, exitMinutes: 5),
                ReconciliationCompleted("D2", -100, minutes: 10, exitMinutes: 15),
                ReconciliationCompleted("D3", -80, minutes: 20, exitMinutes: 25)
            ],
            Tolerances = ReconciliationTolerances(maxExpectancyDegradation: 10)
        });

        AssertTrue(report.Success, "Reconciliation should still produce a structured report when demo diverges.");
        AssertEqual(DemoPaperReconciliationVerdicts.Diverges, report.Verdict,
            "Large demo/paper expectancy degradation should diverge.");
        AssertContains("Expectancy degradation", string.Join(" ", report.FailedToleranceCriteria));
        AssertTrue(report.Deltas.ExpectancyChangeUsd < 0,
            "Demo/paper expectancy delta should show degradation.");
        return Task.CompletedTask;
    }

    private static Task DemoPaperReconciliationTooSmallSampleReturnsInconclusive()
    {
        var report = DemoPaperReconciliationAnalyzer.Analyze(new DemoPaperReconciliationInput
        {
            BacktestOutcomes = ReconciliationBaseline("B"),
            DemoPaperOutcomes = [ReconciliationCompleted("D1", 100, exitMinutes: 5)],
            Tolerances = ReconciliationTolerances(minDemoTrades: 3)
        });

        AssertTrue(report.Success, "Small demo/paper sample should still produce a report.");
        AssertEqual(DemoPaperReconciliationVerdicts.Inconclusive, report.Verdict,
            "Too small demo/paper sample should be inconclusive.");
        AssertContains("sample is too small", string.Join(" ", report.Warnings));
        return Task.CompletedTask;
    }

    private static Task DemoPaperReconciliationMissingCostDataProducesWarning()
    {
        var report = DemoPaperReconciliationAnalyzer.Analyze(new DemoPaperReconciliationInput
        {
            BacktestOutcomes = ReconciliationBaseline("B"),
            DemoPaperOutcomes =
            [
                ReconciliationCompleted("D1", 100, exitMinutes: 5, totalCost: 0, spread: 0, slippage: 0, commission: 0),
                ReconciliationCompleted("D2", -40, minutes: 10, exitMinutes: 15, totalCost: 0, spread: 0, slippage: 0, commission: 0),
                ReconciliationCompleted("D3", 60, minutes: 20, exitMinutes: 25, totalCost: 0, spread: 0, slippage: 0, commission: 0)
            ],
            Tolerances = ReconciliationTolerances()
        });

        AssertTrue(report.Success, "Missing cost data should not prevent report generation.");
        AssertContains("zero spread cost", string.Join(" ", report.Warnings));
        AssertContains("zero slippage cost", string.Join(" ", report.Warnings));
        AssertContains("zero commission cost", string.Join(" ", report.Warnings));
        return Task.CompletedTask;
    }

    private static Task DemoPaperReconciliationDemoOutperformanceHandledSafely()
    {
        var report = DemoPaperReconciliationAnalyzer.Analyze(new DemoPaperReconciliationInput
        {
            BacktestOutcomes = ReconciliationBaseline("B"),
            DemoPaperOutcomes =
            [
                ReconciliationCompleted("D1", 150, exitMinutes: 5),
                ReconciliationCompleted("D2", -10, minutes: 10, exitMinutes: 15),
                ReconciliationCompleted("D3", 100, minutes: 20, exitMinutes: 25)
            ],
            Tolerances = ReconciliationTolerances()
        });

        AssertTrue(report.Success, "Demo/paper outperformance should produce a successful report.");
        AssertEqual(DemoPaperReconciliationVerdicts.Matches, report.Verdict,
            "Demo/paper outperformance should not be treated as a divergence.");
        AssertTrue(report.Deltas.ExpectancyChangeUsd > 0,
            "Demo/paper outperformance should preserve positive expectancy delta.");
        AssertEqual(0, report.FailedToleranceCriteria.Count,
            "Outperformance should not fail degradation tolerances.");
        return Task.CompletedTask;
    }

    private static Task DemoPaperReconciliationBacktestNoTradeDataFailsClearly()
    {
        var report = DemoPaperReconciliationAnalyzer.Analyze(new DemoPaperReconciliationInput
        {
            BacktestOutcomes = [QualityRejected("B1")],
            DemoPaperOutcomes = [ReconciliationCompleted("D1", 100, exitMinutes: 5)],
            Tolerances = ReconciliationTolerances(minDemoTrades: 1)
        });

        AssertFalse(report.Success, "Backtest with no completed trades should fail clearly.");
        AssertEqual(DemoPaperReconciliationAnalyzer.NoBacktestTradesCode, report.FailureCode,
            "Backtest no-trade data should return a clear failure code.");
        AssertContains("no completed trades", report.FailureReason);
        return Task.CompletedTask;
    }

    private static async Task FinalStrategyProofPackageCanBeGenerated()
    {
        string outputPath = Path.Combine(TestFolder(), FinalStrategyProofPackageGenerator.DefaultReportFileName);
        var result = await new FinalStrategyProofPackageGenerator()
            .GenerateAsync(BuildStrongFinalStrategyProofInput(), outputPath)
            .ConfigureAwait(false);

        AssertTrue(result.Success, "Final strategy proof package generation should succeed.");
        AssertTrue(File.Exists(outputPath), "Final strategy proof package file should be written.");
        AssertEqual(FinalStrategyProofPackageGenerator.DefaultReportFileName, Path.GetFileName(result.ReportPath),
            "Final package should use the required filename.");
        AssertContains("Final Strategy Proof Package", result.Markdown);
        AssertContains("Executive Classification", result.Markdown);
        AssertContains("Go/No-Go Criteria", result.Markdown);
    }

    private static async Task FinalStrategyProofPackageStrongEvidenceClassifiesProvenPositiveEdge()
    {
        string outputPath = Path.Combine(TestFolder(), FinalStrategyProofPackageGenerator.DefaultReportFileName);
        var result = await new FinalStrategyProofPackageGenerator()
            .GenerateAsync(BuildStrongFinalStrategyProofInput(), outputPath)
            .ConfigureAwait(false);

        AssertEqual(StrategyEvidenceClassifications.ProvenPositiveEdge, result.EvidenceClassification,
            "Strong evidence should classify as proven positive edge.");
        AssertEqual(StrategyReadinessRecommendations.ProceedToTinyLiveTest, result.ReadinessRecommendation,
            "Strong evidence with matching demo/paper reconciliation should recommend tiny live test readiness.");
        AssertEqual(0, result.FailedCriteria.Count, "Strong evidence should not fail go/no-go criteria.");
    }

    private static async Task FinalStrategyProofPackageWeakNegativeEvidenceClassifiesNegativeEdge()
    {
        var input = BuildFinalStrategyProofInput(
            [
                QualityCompleted("AI1", -100, exitMinutes: 5),
                QualityCompleted("AI2", -80, minutes: 10, exitMinutes: 15),
                QualityCompleted("D1", 10, minutes: 20, exitMinutes: 25),
                QualityCompleted("A1", -40, minutes: 30, exitMinutes: 35)
            ],
            edgeVerdict: StrategyEdgeVerdicts.Fail,
            demoReport: DemoReconciliationMatches());

        string outputPath = Path.Combine(TestFolder(), FinalStrategyProofPackageGenerator.DefaultReportFileName);
        var result = await new FinalStrategyProofPackageGenerator()
            .GenerateAsync(input, outputPath)
            .ConfigureAwait(false);

        AssertEqual(StrategyEvidenceClassifications.NegativeEdge, result.EvidenceClassification,
            "Negative expectancy or sub-1 profit factor should classify as negative edge.");
        AssertEqual(StrategyReadinessRecommendations.BlockLiveTrading, result.ReadinessRecommendation,
            "Negative edge should block live trading.");
        AssertContains("Minimum expectancy", string.Join(" ", result.FailedCriteria));
    }

    private static async Task FinalStrategyProofPackageSmallSampleClassifiesInconclusive()
    {
        var input = BuildFinalStrategyProofInput(
            [
                QualityCompleted("AI1", 100, exitMinutes: 5),
                QualityCompleted("D1", 40, minutes: 10, exitMinutes: 15)
            ],
            criteria: FinalPackageCriteria() with { MinimumCompletedRealisticBacktestTrades = 10 },
            edgeVerdict: StrategyEdgeVerdicts.Inconclusive,
            demoReport: DemoReconciliationMatches());

        string outputPath = Path.Combine(TestFolder(), FinalStrategyProofPackageGenerator.DefaultReportFileName);
        var result = await new FinalStrategyProofPackageGenerator()
            .GenerateAsync(input, outputPath)
            .ConfigureAwait(false);

        AssertEqual(StrategyEvidenceClassifications.Inconclusive, result.EvidenceClassification,
            "Small samples should classify as inconclusive.");
        AssertEqual(StrategyReadinessRecommendations.CollectMoreData, result.ReadinessRecommendation,
            "Small samples should recommend collecting more data.");
        AssertContains("Minimum completed realistic backtest trades", string.Join(" ", result.FailedCriteria));
    }

    private static async Task FinalStrategyProofPackageCriticalRepaintBlocksPositiveClassification()
    {
        var input = BuildStrongFinalStrategyProofInput() with
        {
            RepaintLookaheadAuditMarkdown = CriticalRepaintMarkdown()
        };

        string outputPath = Path.Combine(TestFolder(), FinalStrategyProofPackageGenerator.DefaultReportFileName);
        var result = await new FinalStrategyProofPackageGenerator()
            .GenerateAsync(input, outputPath)
            .ConfigureAwait(false);

        AssertEqual(StrategyEvidenceClassifications.NotProven, result.EvidenceClassification,
            "Critical repaint/lookahead finding should block positive classification.");
        AssertEqual(StrategyReadinessRecommendations.BlockLiveTrading, result.ReadinessRecommendation,
            "Critical repaint/lookahead findings should block live trading.");
        AssertContains("Critical repaint/lookahead", string.Join(" ", result.FailedCriteria));
    }

    private static async Task FinalStrategyProofPackageIncludesRequiredRiskWarnings()
    {
        string outputPath = Path.Combine(TestFolder(), FinalStrategyProofPackageGenerator.DefaultReportFileName);
        var result = await new FinalStrategyProofPackageGenerator()
            .GenerateAsync(BuildStrongFinalStrategyProofInput(), outputPath)
            .ConfigureAwait(false);

        AssertContains("This is not financial advice", result.Markdown);
        AssertContains("Backtest results are not live proof", result.Markdown);
        AssertContains("Real-money trading should remain blocked unless go criteria are met", result.Markdown);
    }

    private static async Task FinalStrategyProofPackageIncludesAiCaution()
    {
        string outputPath = Path.Combine(TestFolder(), FinalStrategyProofPackageGenerator.DefaultReportFileName);
        var result = await new FinalStrategyProofPackageGenerator()
            .GenerateAsync(BuildStrongFinalStrategyProofInput(), outputPath)
            .ConfigureAwait(false);

        AssertContains("AI confirmation should not be trusted unless measured as improving expectancy", result.Markdown);
        AssertContains("AI filter impact summary", result.Markdown);
    }

    private static Task StrategyAdapterConvertsBuySignalToCandidate()
    {
        var signal = StrategySignal(SignalDirection.Buy);

        var result = StrategyToRealisticBacktestAdapter.FromMarketSignal(
            signal,
            new StrategyBacktestAdapterOptions
            {
                LotSize = 0.20,
                TimestampUtc = TestUtc(9, 30),
                Session = "London",
                SpreadRegime = "Tight",
                SpreadPips = 0.8,
                SourceSignalConfidence = 82
            });

        AssertTrue(result.CandidateCreated, "BUY strategy signal should create a candidate.");
        AssertNotNull(result.Candidate, "BUY conversion should include candidate details.");
        var candidate = result.Candidate!;
        AssertEqual(signal.Id, candidate.Id, "Candidate should preserve source signal id.");
        AssertEqual("EURUSD", candidate.Symbol, "Candidate should preserve symbol.");
        AssertEqual(TradeType.BUY.ToString(), candidate.Direction.ToString(), "BUY signal should map to BUY trade type.");
        AssertClose(1.1000, candidate.EntryPrice, 0.0000001, "Candidate should preserve entry price.");
        AssertClose(1.0990, candidate.StopLoss, 0.0000001, "Candidate should preserve stop loss.");
        AssertClose(1.1010, candidate.TakeProfit, 0.0000001, "Candidate should preserve take profit.");
        AssertClose(0.20, candidate.LotSize, 0.0000001, "Candidate should use adapter lot size.");
        AssertEqual("London", candidate.Session, "Candidate should include session metadata.");
        AssertEqual("Tight", candidate.SpreadRegime, "Candidate should include spread-regime metadata.");
        AssertEqual("momentum signal", candidate.SourceSignalReason, "Candidate should preserve source reason.");
        AssertClose(82, candidate.SourceSignalConfidence.GetValueOrDefault(), 0.0000001,
            "Candidate should preserve source confidence when available.");
        return Task.CompletedTask;
    }

    private static Task StrategyAdapterConvertsSellSignalToCandidate()
    {
        var signal = StrategySignal(
            SignalDirection.Sell,
            entryPrice: 1.1000,
            stopLoss: 1.1010,
            takeProfit: 1.0990);

        var result = StrategyToRealisticBacktestAdapter.FromMarketSignal(
            signal,
            new StrategyBacktestAdapterOptions
            {
                LotSize = 0.10,
                HistoricalMarketPrice = 1.1001
            });

        AssertTrue(result.CandidateCreated, "SELL strategy signal should create a candidate.");
        AssertNotNull(result.Candidate, "SELL conversion should include candidate details.");
        var candidate = result.Candidate!;
        AssertEqual(TradeType.SELL.ToString(), candidate.Direction.ToString(), "SELL signal should map to SELL trade type.");
        AssertClose(1.1000, candidate.EntryPrice, 0.0000001,
            "Candidate should prefer explicit strategy entry over historical placeholder price.");
        AssertClose(1.1010, candidate.StopLoss, 0.0000001, "Candidate should preserve SELL stop loss.");
        AssertClose(1.0990, candidate.TakeProfit, 0.0000001, "Candidate should preserve SELL take profit.");
        return Task.CompletedTask;
    }

    private static Task StrategyAdapterSkipsHoldSignal()
    {
        var result = StrategyToRealisticBacktestAdapter.FromMarketSignal(
            StrategySignal(SignalDirection.Hold),
            new StrategyBacktestAdapterOptions
            {
                LotSize = 0.10,
                HistoricalMarketPrice = 1.1000
            });

        AssertFalse(result.CandidateCreated, "HOLD strategy signal should not create an executable candidate.");
        AssertEqual("STRATEGY_HOLD", result.SkipCode, "HOLD skip should include clear code.");
        AssertContains("HOLD", result.SkipReason);
        return Task.CompletedTask;
    }

    private static Task StrategyAdapterSkipsIncompleteSignal()
    {
        var signal = StrategySignal(SignalDirection.Buy);
        signal.StopLoss = 0;
        signal.TakeProfit = 0;

        var result = StrategyToRealisticBacktestAdapter.FromMarketSignal(
            signal,
            new StrategyBacktestAdapterOptions());

        AssertFalse(result.CandidateCreated, "Incomplete strategy signal should not create a candidate.");
        AssertEqual("STRATEGY_SIGNAL_INCOMPLETE", result.SkipCode,
            "Incomplete signal should include clear skip code.");
        AssertContains("stop loss", result.SkipReason);
        AssertContains("take profit", result.SkipReason);
        AssertContains("lot size", result.SkipReason);
        return Task.CompletedTask;
    }

    private static Task StrategyAdapterStaysSeparateFromLiveExecution()
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "Trading",
            "Backtesting",
            "StrategyToRealisticBacktestAdapter.cs");
        string source = File.ReadAllText(path);

        AssertFalse(source.Contains("MT5Bridge", StringComparison.Ordinal),
            "Strategy adapter must not depend on MT5 bridge.");
        AssertFalse(source.Contains("OpenTradeAsync", StringComparison.Ordinal),
            "Strategy adapter must not place MT5 orders.");
        AssertFalse(source.Contains("ITradeExecutionService", StringComparison.Ordinal),
            "Strategy adapter must not depend on live trade execution services.");
        AssertFalse(source.Contains("AutoBotService", StringComparison.Ordinal),
            "Strategy adapter must not alter or depend on live workflow.");
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

    private static BotConfig EvidenceConfig(double maxSpreadPips)
    {
        var config = Config(maxSpreadPips: maxSpreadPips);
        config.Scalping.MaxSpreadPips = maxSpreadPips;
        config.Scalping.StopLossPips = 10;
        config.Scalping.TakeProfitPips = 6;
        config.Scalping.MaxTrades = 10;
        return config;
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

    private static BotConfig RolloutConfig(RolloutStage stage)
    {
        var config = Config();
        config.EnableStagedRollout = true;
        config.CurrentRolloutStage = stage;
        config.MaxTinyLiveRiskPercent = 0.25;
        config.MaxTinyLiveLotMultiplier = 0.50;
        config.MinTradesBeforeScaleUp = 30;
        config.MinDaysBeforeScaleUp = 14;
        config.MinProfitFactorBeforeScaleUp = 1.15;
        config.MaxDrawdownBeforeRollback = 3.0;
        config.MaxLosingStreakBeforeRollback = 4;
        config.MaxRejectionRateBeforeRollback = 0.10;
        config.MaxSpreadDriftBeforeRollback = 1.50;
        config.MaxSlippageDriftBeforeRollback = 1.50;
        config.AutoRollbackEnabled = true;
        return config;
    }

    private static RolloutEvaluationResult EvaluateRollback(
        double CurrentDrawdownPercent = 0,
        int CurrentLosingStreak = 0,
        double CurrentRejectionRate = 0,
        double CurrentSpreadDrift = 0,
        double CurrentSlippageDrift = 0,
        bool RuntimeHealthCritical = false,
        bool KillSwitchActive = false) =>
        new RolloutEvaluator().Evaluate(new RolloutEvaluationInput
        {
            Config = RolloutConfig(RolloutStage.TinyLive),
            CurrentDrawdownPercent = CurrentDrawdownPercent,
            CurrentLosingStreak = CurrentLosingStreak,
            CurrentRejectionRate = CurrentRejectionRate,
            CurrentSpreadDrift = CurrentSpreadDrift,
            CurrentSlippageDrift = CurrentSlippageDrift,
            RuntimeHealthCritical = RuntimeHealthCritical,
            KillSwitchActive = KillSwitchActive
        });

    private static FinalGoNoGoInput AllPassingGoNoGoInput(FinalGoNoGoTarget target) => new()
    {
        Target = target,
        AllowFullLiveGo = false,
        TinyLiveRiskCapsConfigured = target == FinalGoNoGoTarget.TinyLive,
        NewsProviderRequired = true,
        P0AccountSafetyReadiness = FinalChecklistStatus.Pass,
        P1ExecutionRealismReadiness = FinalChecklistStatus.Pass,
        P2RealisticBacktestReadiness = FinalChecklistStatus.Pass,
        P3StrategyEdgeProofReadiness = FinalChecklistStatus.Pass,
        P4LiveReadinessGate = FinalChecklistStatus.Pass,
        DemoForwardTestGate = FinalChecklistStatus.Pass,
        BrokerEaDeploymentChecklist = FinalChecklistStatus.Pass,
        RuntimeHealthStatus = FinalRuntimeHealthStatus.Healthy,
        SafetyAlertStatus = FinalChecklistStatus.Pass,
        OperationalReadinessReportStatus = FinalChecklistStatus.Pass,
        StagedRolloutStatus = FinalChecklistStatus.Pass,
        KillSwitchInactive = true,
        UserLiveEnablementConfirmed = target != FinalGoNoGoTarget.PaperOrDemo,
        EaCompiledRedeployedNote = FinalChecklistStatus.Pass,
        Mt5ConnectionHealth = FinalChecklistStatus.Pass,
        NewsProviderStatus = FinalChecklistStatus.Pass
    };

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

    private static string WriteOhlcMovementCsv() =>
        WriteTempCsv(
            "timestamp,symbol,timeframe,open,high,low,close,spread_pips",
            "2026-05-03T10:00:00Z,EURUSD,M1,1.10000,1.10010,1.09990,1.10000,0.8",
            "2026-05-03T10:01:00Z,EURUSD,M1,1.10000,1.10050,1.10000,1.10020,0.8",
            "2026-05-03T10:02:00Z,EURUSD,M1,1.10020,1.10120,1.10020,1.10040,0.8");

    private static IReadOnlyList<BacktestOhlcCandle> OhlcMovementCandles() =>
    [
        new()
        {
            TimestampUtc = new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc),
            Symbol = "EURUSD",
            Timeframe = "M1",
            Open = 1.10000,
            High = 1.10010,
            Low = 1.09990,
            Close = 1.10000,
            SpreadPips = 0.8
        },
        new()
        {
            TimestampUtc = new DateTime(2026, 5, 3, 10, 1, 0, DateTimeKind.Utc),
            Symbol = "EURUSD",
            Timeframe = "M1",
            Open = 1.10000,
            High = 1.10050,
            Low = 1.10000,
            Close = 1.10020,
            SpreadPips = 0.8
        },
        new()
        {
            TimestampUtc = new DateTime(2026, 5, 3, 10, 2, 0, DateTimeKind.Utc),
            Symbol = "EURUSD",
            Timeframe = "M1",
            Open = 1.10020,
            High = 1.10120,
            Low = 1.10020,
            Close = 1.10040,
            SpreadPips = 0.8
        }
    ];

    private static Task<StrategyExtractionReportResult> GenerateStrategyExtractionReportForTest()
    {
        string outputPath = Path.Combine(TestFolder(), StrategyExtractionReportGenerator.DefaultReportFileName);
        return new StrategyExtractionReportGenerator().GenerateAsync(FindRepoRoot(), outputPath);
    }

    private sealed class FakeHistoricalMarketDataProvider : IHistoricalMarketDataProvider
    {
        public IReadOnlyList<BacktestTick> Ticks { get; init; } = [];
        public IReadOnlyList<BacktestOhlcCandle> Candles { get; init; } = [];
        public string TickError { get; init; } = "";
        public string OhlcError { get; init; } = "";
        public bool IgnoreSymbolFilter { get; init; }
        public TimeSpan Delay { get; init; }
        public Action? OnFetchStarted { get; set; }
        public int TickCalls { get; private set; }
        public int OhlcCalls { get; private set; }
        public int LiveTradeMethodCalls { get; private set; }
        public DateTime LastTickFromUtc { get; private set; }
        public DateTime LastOhlcFromUtc { get; private set; }

        public Task<HistoricalMarketDataProviderResult<BacktestTick>> GetTicksAsync(
            string symbol,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows,
            CancellationToken cancellationToken = default)
        {
            TickCalls++;
            LastTickFromUtc = fromUtc;
            OnFetchStarted?.Invoke();
            if (Delay > TimeSpan.Zero)
                return GetTicksDelayedAsync(symbol, fromUtc, toUtc, maxRows, cancellationToken);

            if (!string.IsNullOrWhiteSpace(TickError))
                return Task.FromResult(HistoricalMarketDataProviderResult<BacktestTick>.Fail(
                    TickError,
                    "GET_TICKS",
                    0,
                    "Fake provider called GET_TICKS and returned an error."));

            return Task.FromResult(HistoricalMarketDataProviderResult<BacktestTick>.Ok(
                Ticks
                    .Where(t => IgnoreSymbolFilter || string.Equals(t.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                    .Where(t => t.TimestampUtc >= fromUtc && t.TimestampUtc <= toUtc)
                    .Take(maxRows)
                    .ToList(),
                "GET_TICKS"));
        }

        private async Task<HistoricalMarketDataProviderResult<BacktestTick>> GetTicksDelayedAsync(
            string symbol,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(TickError))
                return HistoricalMarketDataProviderResult<BacktestTick>.Fail(
                    TickError,
                    "GET_TICKS",
                    0,
                    "Fake provider called GET_TICKS and returned an error.");

            return HistoricalMarketDataProviderResult<BacktestTick>.Ok(
                Ticks
                    .Where(t => IgnoreSymbolFilter || string.Equals(t.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                    .Where(t => t.TimestampUtc >= fromUtc && t.TimestampUtc <= toUtc)
                    .Take(maxRows)
                    .ToList(),
                "GET_TICKS");
        }

        public Task<HistoricalMarketDataProviderResult<BacktestOhlcCandle>> GetOhlcM1Async(
            string symbol,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows,
            CancellationToken cancellationToken = default)
        {
            OhlcCalls++;
            LastOhlcFromUtc = fromUtc;
            OnFetchStarted?.Invoke();
            if (Delay > TimeSpan.Zero)
                return GetOhlcDelayedAsync(symbol, fromUtc, toUtc, maxRows, cancellationToken);

            if (!string.IsNullOrWhiteSpace(OhlcError))
                return Task.FromResult(HistoricalMarketDataProviderResult<BacktestOhlcCandle>.Fail(
                    OhlcError,
                    "GET_RATES",
                    0,
                    "Fake provider called GET_RATES and returned an error."));

            return Task.FromResult(HistoricalMarketDataProviderResult<BacktestOhlcCandle>.Ok(
                Candles
                    .Where(c => IgnoreSymbolFilter || string.Equals(c.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                    .Where(c => c.TimestampUtc >= fromUtc && c.TimestampUtc <= toUtc)
                    .Take(maxRows)
                    .ToList(),
                "GET_RATES"));
        }

        private async Task<HistoricalMarketDataProviderResult<BacktestOhlcCandle>> GetOhlcDelayedAsync(
            string symbol,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(OhlcError))
                return HistoricalMarketDataProviderResult<BacktestOhlcCandle>.Fail(
                    OhlcError,
                    "GET_RATES",
                    0,
                    "Fake provider called GET_RATES and returned an error.");

            return HistoricalMarketDataProviderResult<BacktestOhlcCandle>.Ok(
                Candles
                    .Where(c => IgnoreSymbolFilter || string.Equals(c.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                    .Where(c => c.TimestampUtc >= fromUtc && c.TimestampUtc <= toUtc)
                    .Take(maxRows)
                    .ToList(),
                "GET_RATES");
        }
    }

    private static Task<RepaintLookaheadAuditReportResult> GenerateRepaintLookaheadAuditReportForTest()
    {
        string outputPath = Path.Combine(TestFolder(), RepaintLookaheadAuditReportGenerator.DefaultReportFileName);
        return new RepaintLookaheadAuditReportGenerator().GenerateAsync(FindRepoRoot(), outputPath);
    }

    private static StrategyEdgeVerdictReportInput BuildPassingStrategyEdgeInput() =>
        BuildStrategyEdgeInput(
            [
                QualityCompleted("AI1", 100, exitMinutes: 5),
                QualityCompleted("AI2", 80, minutes: 10, exitMinutes: 15),
                QualityCompleted("AI3", 60, minutes: 20, exitMinutes: 25),
                QualityCompleted("D1", 30, minutes: 30, exitMinutes: 35, symbol: "GBPUSD", session: "NewYork"),
                QualityCompleted("D2", -10, minutes: 40, exitMinutes: 45, symbol: "GBPUSD", session: "NewYork"),
                QualityCompleted("A1", 20, minutes: 50, exitMinutes: 55)
            ],
            StrategyEdgePassingCriteria(),
            repaintMarkdown: NoCriticalRepaintMarkdown());

    private static StrategyEdgeVerdictReportInput BuildStrategyEdgeInput(
        IReadOnlyList<RealisticBacktestTradeOutcome> outcomes,
        StrategyEdgeVerdictCriteria criteria,
        string? repaintMarkdown = null)
    {
        var sourceByCandidateId = outcomes.ToDictionary(
            o => o.CandidateId,
            o => EdgeSourceFor(o.CandidateId),
            StringComparer.OrdinalIgnoreCase);
        var aiConfidenceByCandidateId = outcomes
            .Where(o => sourceByCandidateId[o.CandidateId] == StrategySignalSourceLabels.AiConfirmed)
            .ToDictionary(
                o => o.CandidateId,
                o => o.CandidateId == "AI1" ? 92.0 : o.CandidateId == "AI2" ? 84.0 : 78.0,
                StringComparer.OrdinalIgnoreCase);

        var signalQuality = StrategySignalQualityMetrics.BuildReport(new StrategySignalQualityInput
        {
            Outcomes = outcomes,
            SourceByCandidateId = sourceByCandidateId,
            AssumptionsUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["signal_quality_scope"] = "completed realistic outcomes"
            }
        });

        var segments = StrategySegmentAnalyzer.BuildReport(new StrategySegmentAnalysisInput
        {
            Outcomes = outcomes,
            SignalSourceByCandidateId = sourceByCandidateId,
            AiConfidenceByCandidateId = aiConfidenceByCandidateId,
            AssumptionsUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["segment_scope"] = "completed realistic outcomes"
            }
        });

        var cost = CostSensitivityRunner.Run(new CostSensitivityInput
        {
            Outcomes = outcomes,
            Scenarios = [new CostSensitivityScenario { Name = "Base/original costs" }],
            AssumptionsUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["cost_scope"] = "original execution costs"
            }
        });

        var robustness = StrategyRobustnessRunner.Run(new StrategyRobustnessInput
        {
            Outcomes = outcomes,
            SplitConfig = new OutOfSampleSplitConfig { InSampleRatio = 0.50 },
            MonteCarloConfig = new MonteCarloConfig { StartingEquity = 10_000, Iterations = 20, Seed = 7 },
            Thresholds = LenientRobustnessThresholds(),
            AssumptionsUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["robustness_scope"] = "completed realistic outcomes"
            }
        });

        var aiImpact = AiFilterImpactAnalyzer.Analyze(new AiFilterImpactInput
        {
            Outcomes = outcomes,
            SignalSourceByCandidateId = sourceByCandidateId,
            AiConfidenceByCandidateId = aiConfidenceByCandidateId,
            Thresholds = TinyAiThresholds(),
            AssumptionsUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ai_scope"] = "frozen fixture metadata"
            }
        });

        return new StrategyEdgeVerdictReportInput
        {
            SignalQualityReport = signalQuality,
            SegmentAnalysisReport = segments,
            CostSensitivityReport = cost,
            RobustnessReport = robustness,
            AiFilterImpactReport = aiImpact,
            StrategyExtractionMarkdown = StrategyExtractionMarkdownForVerdict(),
            RepaintLookaheadAuditMarkdown = repaintMarkdown ?? NoCriticalRepaintMarkdown(),
            Criteria = criteria,
            AssumptionsUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["verdict_scope"] = "P3 analytics fixture only"
            }
        };
    }

    private static StrategyEdgeVerdictCriteria StrategyEdgePassingCriteria() => new()
    {
        MinimumCompletedTrades = 4,
        MinimumProfitFactorAfterCosts = 1.2,
        MinimumExpectancyAfterCostsUsd = 0,
        MaximumDrawdownUsd = 1_000,
        MaximumLosingStreak = 5,
        MaximumCostSensitivityNetProfitDegradationUsd = 1_000,
        RobustnessMustPassOrBeInconclusive = true,
        CriticalRepaintLookaheadFindingFails = true
    };

    private static string EdgeSourceFor(string candidateId)
    {
        if (candidateId.StartsWith("AI", StringComparison.OrdinalIgnoreCase))
            return StrategySignalSourceLabels.AiConfirmed;
        if (candidateId.StartsWith("A", StringComparison.OrdinalIgnoreCase))
            return StrategySignalSourceLabels.AutoScalping;
        return StrategySignalSourceLabels.DeterministicBaseStrategy;
    }

    private static string StrategyExtractionMarkdownForVerdict() =>
        "# Strategy Extraction Report\n\nThe base deterministic strategy currently produces mostly HOLD.";

    private static string NoCriticalRepaintMarkdown() =>
        "# Repainting / Future-Data Bias Audit Report\n\n| Finding | Severity | Status |\n|---|---|---|\n| Closed-candle snapshot | Low | Confirmed |";

    private static string CriticalRepaintMarkdown() =>
        "# Repainting / Future-Data Bias Audit Report\n\n| Finding | Severity | Status |\n|---|---|---|\n| SQLite trade-history backtest reconstructs exits from realized P/L | Critical | Confirmed |";

    private static IReadOnlyList<RealisticBacktestTradeOutcome> ReconciliationBaseline(string prefix) =>
    [
        ReconciliationCompleted($"{prefix}1", 100, exitMinutes: 5),
        ReconciliationCompleted($"{prefix}2", -40, minutes: 10, exitMinutes: 15),
        ReconciliationCompleted($"{prefix}3", 60, minutes: 20, exitMinutes: 25),
        QualityRejected($"{prefix}R", minutes: 30)
    ];

    private static RealisticBacktestTradeOutcome ReconciliationCompleted(
        string id,
        double netProfitLossUsd,
        int minutes = 0,
        int? exitMinutes = null,
        double totalCost = 6,
        double spread = 2,
        double slippage = 2,
        double commission = 2) =>
        QualityCompleted(
            id,
            netProfitLossUsd,
            minutes: minutes,
            exitMinutes: exitMinutes,
            totalExecutionCostUsd: totalCost,
            spreadCostUsd: spread,
            slippageCostUsd: slippage,
            commissionCostUsd: commission,
            grossProfitLossUsd: netProfitLossUsd + totalCost);

    private static DemoPaperReconciliationTolerances ReconciliationTolerances(
        int minDemoTrades = 3,
        double maxExpectancyDegradation = 1,
        double maxProfitFactorDegradation = 0.10) => new()
    {
        MinimumDemoPaperCompletedTrades = minDemoTrades,
        MaxAllowedExpectancyDegradationUsd = maxExpectancyDegradation,
        MaxAllowedProfitFactorDegradation = maxProfitFactorDegradation,
        MaxAllowedDrawdownIncreaseUsd = 5,
        MaxAllowedAverageSpreadCostIncreaseUsd = 0.10,
        MaxAllowedAverageSlippageCostIncreaseUsd = 0.10,
        MaxAllowedAverageCommissionCostIncreaseUsd = 0.10
    };

    private static FinalStrategyProofPackageInput BuildStrongFinalStrategyProofInput() =>
        BuildFinalStrategyProofInput(
            [
                QualityCompleted("AI1", 100, exitMinutes: 5),
                QualityCompleted("AI2", 80, minutes: 10, exitMinutes: 15),
                QualityCompleted("AI3", 60, minutes: 20, exitMinutes: 25),
                QualityCompleted("D1", 30, minutes: 30, exitMinutes: 35, symbol: "GBPUSD", session: "NewYork"),
                QualityCompleted("D2", -10, minutes: 40, exitMinutes: 45, symbol: "GBPUSD", session: "NewYork"),
                QualityCompleted("A1", 20, minutes: 50, exitMinutes: 55)
            ],
            edgeVerdict: StrategyEdgeVerdicts.Pass,
            demoReport: DemoReconciliationMatches());

    private static FinalStrategyProofPackageInput BuildFinalStrategyProofInput(
        IReadOnlyList<RealisticBacktestTradeOutcome> outcomes,
        FinalStrategyProofCriteria? criteria = null,
        string edgeVerdict = StrategyEdgeVerdicts.Pass,
        DemoPaperReconciliationReport? demoReport = null,
        string? repaintMarkdown = null)
    {
        var edgeInput = BuildStrategyEdgeInput(
            outcomes,
            StrategyEdgePassingCriteria(),
            repaintMarkdown: repaintMarkdown ?? NoCriticalRepaintMarkdown());

        return new FinalStrategyProofPackageInput
        {
            StrategyExtractionMarkdown = StrategyExtractionMarkdownForVerdict(),
            RepaintLookaheadAuditMarkdown = repaintMarkdown ?? NoCriticalRepaintMarkdown(),
            RealisticBacktestMarkdown = "# Realistic Backtest Report\n\nFixture realistic simulation summary.",
            SignalQualityReport = edgeInput.SignalQualityReport,
            SegmentAnalysisReport = edgeInput.SegmentAnalysisReport,
            CostSensitivityReport = edgeInput.CostSensitivityReport,
            RobustnessReport = edgeInput.RobustnessReport,
            AiFilterImpactReport = edgeInput.AiFilterImpactReport,
            DemoPaperReconciliationReport = demoReport ?? DemoReconciliationMatches(),
            StrategyEdgeVerdictReport = new StrategyEdgeVerdictReportResult
            {
                Success = true,
                Verdict = edgeVerdict,
                Reason = "Fixture strategy edge verdict.",
                LiveDemoReadinessScore = edgeVerdict == StrategyEdgeVerdicts.Pass ? 90 : 20
            },
            Criteria = criteria ?? FinalPackageCriteria(),
            AssumptionsUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["package_scope"] = "P3 proof fixture only"
            }
        };
    }

    private static FinalStrategyProofCriteria FinalPackageCriteria() => new()
    {
        MinimumCompletedRealisticBacktestTrades = 4,
        MinimumProfitFactorAfterCosts = 1.2,
        MinimumExpectancyAfterCostsUsd = 0,
        MaximumDrawdownUsd = 1_000,
        MaximumLosingStreak = 5,
        MaximumCostSensitivityNetProfitDegradationUsd = 1_000,
        RequireAcceptableDemoPaperReconciliation = true,
        CriticalRepaintLookaheadFindingBlocksPositiveClassification = true
    };

    private static DemoPaperReconciliationReport DemoReconciliationMatches() =>
        DemoPaperReconciliationAnalyzer.Analyze(new DemoPaperReconciliationInput
        {
            BacktestOutcomes = ReconciliationBaseline("B"),
            DemoPaperOutcomes = ReconciliationBaseline("D"),
            Tolerances = ReconciliationTolerances()
        });

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

    private static BacktestNoTradeFilterInput FilterInput(
        DateTime timestampUtc,
        BotConfig? config = null,
        double? spreadPips = 1.0,
        ApiIntegrationConfig? newsConfig = null,
        IReadOnlyList<HistoricalNewsEvent>? newsEvents = null) => new()
    {
        TimestampUtc = timestampUtc,
        Symbol = "EURUSD",
        SpreadPips = spreadPips,
        Config = config ?? Config(),
        NewsConfig = newsConfig,
        HistoricalNewsEvents = newsEvents
    };

    private static HistoricalNewsEvent NewsEvent(
        DateTime timestampUtc,
        string currency,
        string impact,
        string title) => new()
    {
        TimestampUtc = timestampUtc,
        Currency = currency,
        Impact = impact,
        Title = title
    };

    private static DateTime TestUtc(int hour, int minute) =>
        new(2026, 5, 3, hour, minute, 0, DateTimeKind.Utc);

    private static IReadOnlyList<BacktestRobustnessTrade> RobustnessTrades(params double[] profitLossUsd) =>
        profitLossUsd
            .Select((pnl, index) => new BacktestRobustnessTrade
            {
                Id = $"T{index + 1}",
                TimestampUtc = RobustnessUtc(index + 1),
                ProfitLossUsd = pnl
            })
            .ToList();

    private static DateTime RobustnessUtc(int day) =>
        new(2026, 1, day, 0, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<BacktestReportTrade> ReportingTrades() =>
    [
        ReportTrade(100, symbol: "EURUSD", session: "London", spreadRegime: "Tight", rMultiple: 1.0),
        ReportTrade(-50, day: 2, symbol: "EURUSD", session: "London", spreadRegime: "Wide", rMultiple: -0.5),
        ReportTrade(-150, day: 3, symbol: "GBPUSD", session: "London", spreadRegime: "Wide", rMultiple: -1.5),
        ReportTrade(200, day: 4, symbol: "EURUSD", session: "NewYork", spreadRegime: "Tight", rMultiple: 2.0),
        ReportTrade(50, day: 5, symbol: "GBPUSD", session: "NewYork", spreadRegime: "Tight", rMultiple: 0.7)
    ];

    private static BacktestReportTrade ReportTrade(
        double profitLossUsd,
        int day = 1,
        string symbol = "EURUSD",
        string session = "London",
        string spreadRegime = "Tight",
        double? rMultiple = 1.0,
        double? commissionUsd = 1.0,
        double? slippageUsd = 0.5,
        double? spreadCostUsd = 1.5) => new()
    {
        Id = $"R{day}",
        TimestampUtc = RobustnessUtc(day),
        Symbol = symbol,
        Session = session,
        SpreadRegime = spreadRegime,
        ProfitLossUsd = profitLossUsd,
        RMultiple = rMultiple,
        CommissionUsd = commissionUsd,
        SlippageUsd = slippageUsd,
        SpreadCostUsd = spreadCostUsd
    };

    private static RealisticBacktestTradeOutcome QualityCompleted(
        string id,
        double netProfitLossUsd,
        int minutes = 0,
        int? exitMinutes = null,
        string symbol = "EURUSD",
        string session = "London",
        string spreadRegime = "Tight",
        double? totalExecutionCostUsd = null,
        double? spreadCostUsd = null,
        double? slippageCostUsd = null,
        double? commissionCostUsd = null,
        double? grossProfitLossUsd = null)
    {
        double total = totalExecutionCostUsd ?? 3;
        double spread = spreadCostUsd ?? Math.Round(total / 3.0, 2);
        double slippage = slippageCostUsd ?? Math.Round(total / 3.0, 2);
        double commission = commissionCostUsd ?? Math.Round(total / 3.0, 2);
        if (!totalExecutionCostUsd.HasValue && (spreadCostUsd.HasValue || slippageCostUsd.HasValue || commissionCostUsd.HasValue))
            total = Math.Round(spread + slippage + commission, 2);

        return new RealisticBacktestTradeOutcome
        {
            CandidateId = id,
            Status = RealisticBacktestOutcomeStatus.Successful,
            TimestampUtc = TestUtc(9, 0).AddMinutes(minutes),
            Symbol = symbol,
            EntryPrice = 1.1000,
            ExitPrice = netProfitLossUsd >= 0 ? 1.1010 : 1.0990,
            ExitTimestampUtc = exitMinutes.HasValue
                ? TestUtc(9, 0).AddMinutes(exitMinutes.Value)
                : null,
            GrossProfitLossUsd = grossProfitLossUsd ?? netProfitLossUsd + total,
            NetProfitLossUsd = netProfitLossUsd,
            CommissionCostUsd = commission,
            SlippageCostUsd = slippage,
            SpreadCostUsd = spread,
            TotalExecutionCostUsd = total,
            Session = session,
            SpreadRegime = spreadRegime
        };
    }

    private static RealisticBacktestTradeOutcome QualityRejected(
        string id,
        int minutes = 0,
        string symbol = "EURUSD") => new()
    {
        CandidateId = id,
        Status = RealisticBacktestOutcomeStatus.Rejected,
        TimestampUtc = TestUtc(9, 0).AddMinutes(minutes),
        Symbol = symbol,
        EntryPrice = 1.1000,
        RejectionCode = "TEST_REJECTION",
        RejectionReason = "Test rejection"
    };

    private static RealisticBacktestTradeOutcome QualityOpen(
        string id,
        int minutes = 0,
        string symbol = "EURUSD") => new()
    {
        CandidateId = id,
        Status = RealisticBacktestOutcomeStatus.Open,
        TimestampUtc = TestUtc(9, 0).AddMinutes(minutes),
        Symbol = symbol,
        EntryPrice = 1.1000
    };

    private static IReadOnlyDictionary<string, string> SourceMap(params (string Id, string Source)[] sources) =>
        sources.ToDictionary(s => s.Id, s => s.Source, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, double> RMap(params (string Id, double RMultiple)[] values) =>
        values.ToDictionary(v => v.Id, v => v.RMultiple, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, double> ConfidenceMap(params (string Id, double Confidence)[] values) =>
        values.ToDictionary(v => v.Id, v => v.Confidence, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, double> DoubleMap(params (string Id, double Value)[] values) =>
        values.ToDictionary(v => v.Id, v => v.Value, StringComparer.OrdinalIgnoreCase);

    private static StrategyRobustnessInput RobustnessInput(
        IReadOnlyList<double> netProfitLossUsd,
        OutOfSampleSplitConfig? split = null,
        MonteCarloConfig? monteCarlo = null,
        StrategyRobustnessThresholds? thresholds = null,
        WalkForwardConfig? walkForward = null) => new()
    {
        Outcomes = netProfitLossUsd
            .Select((pnl, index) => QualityCompleted(
                $"ROB{index + 1}",
                pnl,
                minutes: index * 10,
                exitMinutes: index * 10 + 5,
                grossProfitLossUsd: pnl + 3))
            .ToList(),
        SplitConfig = split ?? new OutOfSampleSplitConfig { InSampleRatio = 0.50 },
        MonteCarloConfig = monteCarlo ?? new MonteCarloConfig { StartingEquity = 10_000, Iterations = 20, Seed = 4 },
        Thresholds = thresholds ?? LenientRobustnessThresholds(),
        WalkForwardConfig = walkForward,
        AssumptionsUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["robustness_scope"] = "completed realistic outcomes"
        }
    };

    private static StrategyRobustnessThresholds LenientRobustnessThresholds(
        double maxOosDegradation = 1_000) => new()
    {
        MinimumTotalTrades = 1,
        MinimumOutOfSampleTrades = 1,
        MaximumOosExpectancyDegradationUsd = maxOosDegradation,
        MaximumMonteCarloDrawdownUsd = 10_000,
        MaximumMonteCarloLosingStreak = 100
    };

    private static WalkForwardConfig RobustnessWalkForwardConfig() => new()
    {
        StartUtc = TestUtc(9, 0),
        EndUtc = TestUtc(10, 20),
        TrainingPeriod = TimeSpan.FromMinutes(20),
        TestingPeriod = TimeSpan.FromMinutes(20),
        StepSize = TimeSpan.FromMinutes(10)
    };

    private static AiFilterImpactThresholds TinyAiThresholds() => new()
    {
        MinimumAiConfirmedTrades = 1,
        MinimumNonAiTrades = 1,
        MinimumExpectancyDeltaUsd = 0
    };

    private static StrategySegmentGroup SegmentGroup(StrategySegmentAnalysisReport report, string name) =>
        report.SegmentGroups.Single(g => g.Name == name);

    private static StrategySegmentMetrics Segment(StrategySegmentGroup group, string key) =>
        group.Segments.Single(s => s.Key == key);

    private static RealisticBacktestTradeCandidate RealisticCandidate(
        string id = "C1",
        DateTime? timestampUtc = null,
        string symbol = "EURUSD",
        TradeType direction = TradeType.BUY,
        double entryPrice = 1.1000,
        double stopLoss = 1.0990,
        double takeProfit = 1.1010,
        double lotSize = 0.10,
        string session = "London",
        string spreadRegime = "Tight",
        double? spreadPips = null) => new()
    {
        Id = id,
        TimestampUtc = timestampUtc ?? TestUtc(10, 0),
        Symbol = symbol,
        Direction = direction,
        EntryPrice = entryPrice,
        EntryRulePlaceholder = "external-candidate",
        StopLoss = stopLoss,
        TakeProfit = takeProfit,
        LotSize = lotSize,
        Session = session,
        SpreadRegime = spreadRegime,
        SpreadPips = spreadPips
    };

    private static MarketSignal StrategySignal(
        SignalDirection direction,
        double entryPrice = 1.1000,
        double stopLoss = 1.0990,
        double takeProfit = 1.1010) => new()
    {
        Id = "SIG1",
        Pair = "EURUSD",
        Direction = direction,
        EntryPrice = entryPrice,
        StopLoss = stopLoss,
        TakeProfit = takeProfit,
        Reason = "momentum signal",
        CreatedAt = TestUtc(9, 15)
    };

    private static RealisticBacktestRunInput RealisticInput(
        IReadOnlyList<RealisticBacktestTradeCandidate> candidates,
        IReadOnlyList<BacktestTick>? ticks = null,
        IReadOnlyList<BacktestOhlcCandle>? candles = null,
        BotConfig? config = null,
        SymbolInfo? symbolInfo = null) => new()
    {
        Candidates = candidates,
        Ticks = ticks ?? [],
        Candles = candles ?? [],
        Config = config ?? Config(),
        SymbolInfoBySymbol = new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = symbolInfo ?? BacktestBrokerSymbol(
                stopLevelPoints: 0,
                freezeLevelPoints: 0,
                volumeLimit: 0)
        },
        AssumptionsUsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["execution_model"] = "P2 realistic runner skeleton"
        }
    };

    private static IReadOnlyList<BacktestTick> RunnerTicks(params (int Seconds, double Bid, double Ask)[] ticks) =>
        ticks.Select(t => new BacktestTick
        {
            TimestampUtc = TestUtc(10, 0).AddSeconds(t.Seconds),
            Symbol = "EURUSD",
            Bid = t.Bid,
            Ask = t.Ask
        }).ToList();

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

    private static BacktestTick Tick(string symbol, int seconds, double bid, double ask) => new()
    {
        TimestampUtc = new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc).AddSeconds(seconds),
        Symbol = symbol,
        Bid = bid,
        Ask = ask
    };

    private static Task<HistoricalMarketDataUpdateSummary> RunMarketDataUpdateForTest(
        FakeHistoricalMarketDataProvider provider,
        string folder,
        MarketDataUpdateType updateType,
        params string[] symbols)
    {
        var updater = new HistoricalMarketDataUpdater(provider);
        return updater.UpdateAsync(new HistoricalMarketDataUpdateRequest
        {
            Symbols = symbols.Length > 0 ? symbols : ["EURUSD"],
            DataDirectory = folder,
            PreferredDataType = updateType,
            LookbackDays = 1,
            MaxRowsPerUpdate = 100,
            NowUtc = new DateTime(2026, 5, 4, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    private static MarketDataAutoSyncService CreateAutoSyncForTest(
        FakeHistoricalMarketDataProvider provider,
        string folder,
        TimeSpan interval,
        bool allowSyncDuringTrading = false,
        Func<bool>? criticalTradingInProgress = null,
        Func<Task<bool>>? mt5AvailabilityCheck = null) =>
        new(
            () => new HistoricalMarketDataUpdater(provider),
            () => new HistoricalMarketDataUpdateRequest
            {
                Symbols = ["EURUSD"],
                DataDirectory = folder,
                PreferredDataType = MarketDataUpdateType.Tick,
                LookbackDays = 1,
                MaxRowsPerUpdate = 100,
                NowUtc = new DateTime(2026, 5, 4, 0, 0, 0, DateTimeKind.Utc)
            },
            interval,
            allowSyncDuringTrading,
            criticalTradingInProgress,
            mt5AvailabilityCheck);

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

    private static void AssertFilterReject(
        BacktestNoTradeFilterResult result,
        string expectedCode,
        string expectedFilterName,
        string expectedFilterType)
    {
        AssertFalse(result.Allowed, $"Backtest filter should reject with {expectedCode}.");
        AssertEqual(expectedCode, result.RejectionCode, "Filter rejection should include a clear code.");
        AssertEqual(expectedFilterName, result.MatchedFilterName, "Filter rejection should include the matched filter name.");
        AssertEqual(expectedFilterType, result.MatchedFilterType, "Filter rejection should include the matched filter type.");
        AssertTrue(!string.IsNullOrWhiteSpace(result.RejectionReason), "Filter rejection should include a reason.");
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
