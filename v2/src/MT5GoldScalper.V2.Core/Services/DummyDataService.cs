using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Models;

namespace MT5GoldScalper.V2.Core.Services;

public sealed class DummyDataService : IMarketDataService, IAccountDataService, INewsService, IIndicatorService
{
    public Task<MarketDataModel> GetMarketDataAsync(string symbol, CancellationToken ct = default)
    {
        symbol = NormalizeSymbol(symbol);

        var model = symbol switch
        {
            "XAUUSD-SPREAD" => new MarketDataModel
            {
                Pair = "XAUUSD-SPREAD", Bid = 3348.18m, Ask = 3349.08m, CurrentPrice = 3348.63m,
                Digits = 2, Point = 0.01m, TickSize = 0.01m, TickValue = 1m, ContractSize = 100m,
                SpreadPrice = 0.90m, SpreadPoints = 90, SpreadPips = 9.0m, LastTickAgeMs = 140,
                LastTickTimeUtc = DateTime.UtcNow.AddMilliseconds(-140), MinLot = 0.01m, MaxLot = 100m,
                LotStep = 0.01m, StopsLevelPoints = 20, MarketOpen = true, PriceFresh = true
            },
            "EURUSD" => new MarketDataModel
            {
                Pair = "EURUSD", Bid = 1.08524m, Ask = 1.08536m, CurrentPrice = 1.08530m,
                Digits = 5, Point = 0.00001m, TickSize = 0.00001m, TickValue = 1m, ContractSize = 100000m,
                SpreadPrice = 0.00012m, SpreadPoints = 12, SpreadPips = 1.2m, LastTickAgeMs = 180,
                LastTickTimeUtc = DateTime.UtcNow.AddMilliseconds(-180), MinLot = 0.01m, MaxLot = 100m,
                LotStep = 0.01m, StopsLevelPoints = 10, MarketOpen = true, PriceFresh = true
            },
            "GBPUSD" => new MarketDataModel
            {
                Pair = "GBPUSD", Bid = 1.27382m, Ask = 1.27420m, CurrentPrice = 1.27401m,
                Digits = 5, Point = 0.00001m, TickSize = 0.00001m, TickValue = 1m, ContractSize = 100000m,
                SpreadPrice = 0.00038m, SpreadPoints = 38, SpreadPips = 3.8m, LastTickAgeMs = 350,
                LastTickTimeUtc = DateTime.UtcNow.AddMilliseconds(-350), MinLot = 0.01m, MaxLot = 100m,
                LotStep = 0.01m, StopsLevelPoints = 12, MarketOpen = true, PriceFresh = true
            },
            "AUDUSD" => new MarketDataModel
            {
                Pair = "AUDUSD", Bid = 0.66218m, Ask = 0.66230m, CurrentPrice = 0.66224m,
                Digits = 5, Point = 0.00001m, TickSize = 0.00001m, TickValue = 1m, ContractSize = 100000m,
                SpreadPrice = 0.00012m, SpreadPoints = 12, SpreadPips = 1.2m, LastTickAgeMs = 220,
                LastTickTimeUtc = DateTime.UtcNow.AddMilliseconds(-220), MinLot = 0.01m, MaxLot = 100m,
                LotStep = 0.01m, StopsLevelPoints = 10, MarketOpen = true, PriceFresh = true
            },
            _ => new MarketDataModel
            {
                Pair = "XAUUSD", Bid = 3348.18m, Ask = 3348.42m, CurrentPrice = 3348.30m,
                Digits = 2, Point = 0.01m, TickSize = 0.01m, TickValue = 1m, ContractSize = 100m,
                SpreadPrice = 0.24m, SpreadPoints = 24, SpreadPips = 2.4m, LastTickAgeMs = 120,
                LastTickTimeUtc = DateTime.UtcNow.AddMilliseconds(-120), MinLot = 0.01m, MaxLot = 100m,
                LotStep = 0.01m, StopsLevelPoints = 20, MarketOpen = true, PriceFresh = true
            }
        };

        return Task.FromResult(model);
    }

    public Task<AccountRiskModel> GetAccountRiskAsync(string symbol, CancellationToken ct = default) =>
        Task.FromResult(new AccountRiskModel
        {
            Balance = 10_000m,
            Equity = 10_025m,
            FreeMargin = 9_240m,
            MarginUsed = 785m,
            MarginLevel = 1277m,
            RiskPercent = 1m,
            RiskAmount = 20m,
            LotSize = 0.10m,
            EstimatedMarginRequired = 240m,
            DailyProfitLoss = 35m,
            DailyLossLimit = 300m,
            DailyLossRemaining = 265m,
            TradesTakenToday = 1,
            MaxTradesPerDay = 5,
            OpenPositionsCount = 0,
            SamePairOpenPosition = false,
            DuplicateTradeAllowed = false
        });

    public Task<SessionNewsModel> GetSessionNewsAsync(string symbol, CancellationToken ct = default)
    {
        symbol = NormalizeSymbol(symbol);

        var model = symbol == "GBPUSD"
            ? new SessionNewsModel
            {
                CurrentSession = "London",
                IsSessionAllowed = true,
                UtcTime = DateTime.UtcNow,
                ServerTime = DateTime.UtcNow,
                NextHighImpactEvent = "BoE rate decision",
                NewsCurrency = "GBP",
                NewsImpact = "High",
                NewsTimeUtc = DateTime.UtcNow.AddMinutes(12),
                MinutesToNews = 12,
                NewsBlackoutActive = true
            }
            : new SessionNewsModel
            {
                CurrentSession = "London",
                IsSessionAllowed = true,
                UtcTime = DateTime.UtcNow,
                ServerTime = DateTime.UtcNow,
                NextHighImpactEvent = "None in blackout window",
                NewsCurrency = "USD",
                NewsImpact = "Low",
                MinutesToNews = 999,
                NewsBlackoutActive = false
            };

        return Task.FromResult(model);
    }

    public Task<StrategySignalModel> GetStrategySignalAsync(string symbol, CancellationToken ct = default)
    {
        symbol = NormalizeSymbol(symbol);

        var model = symbol switch
        {
            "EURUSD" => new StrategySignalModel
            {
                SetupDirection = "SELL", HigherTimeframeTrend = "Bearish", M15Trend = "Bearish", M5Trend = "Mixed",
                TrendAligned = true, Alma34 = 1.08542m, Alma99 = 1.08563m, AlmaPass = true,
                RsiValue = 47m, RsiPass = false, AtrValue = 0.00082m, AtrPass = true,
                BollingerUpper = 1.08610m, BollingerMiddle = 1.08554m, BollingerLower = 1.08498m,
                BollingerPass = false, LiquiditySweepFound = true, LiquiditySweepSide = "High",
                MomentumCandleFound = false, ConfirmationCandleClosed = false, EntryPrice = 1.08530m,
                StopLossPrice = 1.08620m, Tp1Price = 1.08440m, Tp2Price = 1.08350m,
                StopLossPips = 9m, Tp1Pips = 9m, Tp2Pips = 18m, RiskRewardTp1 = 1m,
                RiskRewardTp2 = 2m, EstimatedTp1Profit = 9m, EstimatedTp2Profit = 18m
            },
            "AUDUSD" => new StrategySignalModel
            {
                SetupDirection = "BUY", HigherTimeframeTrend = "Range", M15Trend = "Range", M5Trend = "Mixed",
                TrendAligned = false, Alma34 = 0.66220m, Alma99 = 0.66235m, AlmaPass = false,
                RsiValue = 50m, RsiPass = false, AtrValue = 0.00060m, AtrPass = true,
                BollingerUpper = 0.66300m, BollingerMiddle = 0.66230m, BollingerLower = 0.66160m,
                BollingerPass = false, LiquiditySweepFound = false, LiquiditySweepSide = "None",
                MomentumCandleFound = false, ConfirmationCandleClosed = false, EntryPrice = 0.66224m,
                StopLossPrice = 0.66140m, Tp1Price = 0.66308m, Tp2Price = 0.66392m,
                StopLossPips = 8.4m, Tp1Pips = 8.4m, Tp2Pips = 16.8m, RiskRewardTp1 = 1m,
                RiskRewardTp2 = 2m, EstimatedTp1Profit = 8.4m, EstimatedTp2Profit = 16.8m
            },
            "GBPUSD" => new StrategySignalModel
            {
                SetupDirection = "BUY", HigherTimeframeTrend = "Bullish", M15Trend = "Bullish", M5Trend = "Bullish",
                TrendAligned = true, Alma34 = 1.27390m, Alma99 = 1.27310m, AlmaPass = true,
                RsiValue = 59m, RsiPass = true, AtrValue = 0.00125m, AtrPass = true,
                BollingerUpper = 1.27480m, BollingerMiddle = 1.27395m, BollingerLower = 1.27310m,
                BollingerPass = true, LiquiditySweepFound = true, LiquiditySweepSide = "Low",
                MomentumCandleFound = true, ConfirmationCandleClosed = true, EntryPrice = 1.27400m,
                StopLossPrice = 1.27310m, Tp1Price = 1.27490m, Tp2Price = 1.27580m,
                StopLossPips = 9m, Tp1Pips = 9m, Tp2Pips = 18m, RiskRewardTp1 = 1m,
                RiskRewardTp2 = 2m, EstimatedTp1Profit = 9m, EstimatedTp2Profit = 18m
            },
            _ => new StrategySignalModel
            {
                SetupDirection = "BUY", HigherTimeframeTrend = "Bullish", M15Trend = "Bullish", M5Trend = "Bullish",
                TrendAligned = true, Alma34 = 3348.65m, Alma99 = 3346.90m, AlmaPass = true,
                RsiValue = 61m, RsiPass = true, AtrValue = 2.10m, AtrPass = true,
                BollingerUpper = 3352.40m, BollingerMiddle = 3348.90m, BollingerLower = 3345.80m,
                BollingerPass = true, LiquiditySweepFound = true, LiquiditySweepSide = "Low",
                MomentumCandleFound = true, ConfirmationCandleClosed = true, EntryPrice = 3348.00m,
                StopLossPrice = 3346.00m, Tp1Price = 3351.00m, Tp2Price = 3354.00m,
                StopLossPips = 20m, Tp1Pips = 30m, Tp2Pips = 60m, RiskRewardTp1 = 1.5m,
                RiskRewardTp2 = 3m, EstimatedTp1Profit = 30m, EstimatedTp2Profit = 60m
            }
        };

        return Task.FromResult(model);
    }

    private static string NormalizeSymbol(string symbol) =>
        symbol.Trim().ToUpperInvariant().Replace("M", string.Empty);
}
