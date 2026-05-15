using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MT5GoldScalper.V2.Core.Abstractions;
using MT5GoldScalper.V2.Core.Models;
using MT5GoldScalper.V2.Models;

namespace MT5GoldScalper.V2.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ITradingDecisionSnapshotService _snapshotService;
    private readonly ILogger<MainViewModel> _logger;
    private bool _loading;

    public MainViewModel(ITradingDecisionSnapshotService snapshotService, ILogger<MainViewModel> logger)
    {
        _snapshotService = snapshotService;
        _logger = logger;
        Pairs = new ObservableCollection<string>(["XAUUSD", "XAUUSD-SPREAD", "GBPUSD", "EURUSD", "AUDUSD"]);
        Sections = [];
        TradeLogs = [];
    }

    [ObservableProperty]
    private TradingDecisionSnapshot snapshot = new();

    [ObservableProperty]
    private string selectedPair = "XAUUSD";

    [ObservableProperty]
    private DecisionSectionModel? selectedSection;

    [ObservableProperty]
    private string? entryOverride;

    [ObservableProperty]
    private string? slOverride;

    [ObservableProperty]
    private string? tpOverride;

    public ObservableCollection<string> Pairs { get; }
    public ObservableCollection<DecisionSectionModel> Sections { get; }
    public ObservableCollection<TradeLogItem> TradeLogs { get; }

    public string SessionName => Snapshot.SessionNews.CurrentSession;
    public string UtcTime => Snapshot.SessionNews.UtcTime.ToString("HH:mm:ss 'UTC'");
    public string TodayDate => Snapshot.AsOfUtc.ToString("dd MMM yyyy");
    public string Decision => Snapshot.FinalDecisionText;
    public string Direction => Snapshot.TradeDirection.ToString().ToUpperInvariant();
    public string SignalDecisionDisplay => Snapshot.SignalDecision.ToString().ToUpperInvariant();
    public string ExecutionReadinessDisplay => Snapshot.ExecutionReadiness.ToString().ToUpperInvariant();
    public bool CanPlaceTrade => Snapshot.CanPlaceTrade;
    public string CanPlaceTradeDisplay => Snapshot.CanPlaceTrade ? "YES" : "NO";
    public string PrimaryBlockReasonCodeDisplay => Snapshot.PrimaryBlockReason?.Code.ToString() ?? "-";
    public string PrimaryBlockReasonMessageDisplay => Snapshot.PrimaryBlockReason?.Message ?? "No hard block.";
    public string PrimaryBlockReasonDisplay => Snapshot.PrimaryBlockReason is null
        ? "No hard block."
        : $"{Snapshot.PrimaryBlockReason.Code}: {Snapshot.PrimaryBlockReason.Message}";
    public string SpreadPips => $"{Snapshot.Market.SpreadPips:0.0} pips";
    public string SpreadPoints => $"{Snapshot.Market.SpreadPoints} pts";
    public string LastTickAgeMs => $"{Snapshot.Market.LastTickAgeMs} ms";
    public bool IsMt5Connected => Snapshot.ExecutionSafety.TerminalConnected;
    public string Mt5ConnectionStatus => Snapshot.ExecutionSafety.TerminalConnected ? "MT5 Connected" : "MT5 Disconnected";
    public string NewsBlackoutActiveDisplay => Snapshot.SessionNews.NewsBlackoutActive ? "YES" : "NO";
    public string NewsStatus => Snapshot.SessionNews.NewsBlackoutActive
        ? $"{Snapshot.SessionNews.NewsImpact} impact: {Snapshot.SessionNews.NextHighImpactEvent}"
        : "Clear";
    public string Entry => Price(Snapshot.StrategySignal.EntryPrice);
    public string StopLoss => Price(Snapshot.StrategySignal.StopLossPrice);
    public string TakeProfit => Price(Snapshot.StrategySignal.Tp2Price);
    public string Tp1Price => Price(Snapshot.StrategySignal.Tp1Price);
    public string Tp2Price => Price(Snapshot.StrategySignal.Tp2Price);
    public string SlPips => Snapshot.StrategySignal.StopLossPips == 0 ? string.Empty : $"{Snapshot.StrategySignal.StopLossPips:0.0} p";
    public string TpPips => Snapshot.StrategySignal.Tp2Pips == 0 ? string.Empty : $"{Snapshot.StrategySignal.Tp2Pips:0.0} p";
    public string RiskReward => Snapshot.StrategySignal.RiskRewardTp2 == 0 ? "-" : $"1:{Snapshot.StrategySignal.RiskRewardTp2:0.0}";
    public string RiskRewardTp1 => Snapshot.StrategySignal.RiskRewardTp1 == 0 ? "-" : $"1:{Snapshot.StrategySignal.RiskRewardTp1:0.0}";
    public string RiskRewardTp2 => Snapshot.StrategySignal.RiskRewardTp2 == 0 ? "-" : $"1:{Snapshot.StrategySignal.RiskRewardTp2:0.0}";
    public string LotSize => Snapshot.AccountRisk.LotSize == 0 ? "-" : Snapshot.AccountRisk.LotSize.ToString("0.00");
    public string RiskDollar => Snapshot.AccountRisk.RiskAmount == 0 ? "-" : Snapshot.AccountRisk.RiskAmount.ToString("0.00");
    public string MaxProfit => Snapshot.StrategySignal.EstimatedTp2Profit == 0 ? "-" : Snapshot.StrategySignal.EstimatedTp2Profit.ToString("0.00");
    public decimal Confidence => Snapshot.ConfidenceScore;
    public string ConfidencePct => $"{Snapshot.ConfidenceScore:0}%";
    public string TradeLogSummary => $"{TradeLogs.Count} prototype trade log entries";

    public async Task InitializeAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        try
        {
            _logger.LogInformation("Loading decision snapshot for {Pair}", SelectedPair);
            Snapshot = await _snapshotService.CreateAsync(SelectedPair);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load decision snapshot for {Pair}", SelectedPair);
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnSelectedPairChanged(string value)
    {
        _ = InitializeAsync();
    }

    partial void OnSnapshotChanged(TradingDecisionSnapshot value)
    {
        Sections.Clear();
        foreach (var section in value.Sections)
        {
            Sections.Add(section);
        }

        SelectedSection = Sections.FirstOrDefault();
        RaiseSnapshotBindings();
        TradeCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectSection(DecisionSectionModel? section)
    {
        SelectedSection = ReferenceEquals(SelectedSection, section) ? null : section;
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedSection = null;
        EntryOverride = null;
        SlOverride = null;
        TpOverride = null;
    }

    private bool CanTrade(string? action)
    {
        var tradeAction = action?.ToUpperInvariant();
        return tradeAction switch
        {
            "BUY" => Snapshot.CanPlaceTrade && Snapshot.TradeDirection == TradeDirection.Buy,
            "SELL" => Snapshot.CanPlaceTrade && Snapshot.TradeDirection == TradeDirection.Sell,
            _ => true
        };
    }

    [RelayCommand(CanExecute = nameof(CanTrade))]
    private void Trade(string? action)
    {
        var tradeAction = string.IsNullOrWhiteSpace(action) ? "SKIP" : action.ToUpperInvariant();
        _logger.LogInformation("Prototype trade action {Action} for {Pair}. Decision: {Decision}", tradeAction, SelectedPair, Snapshot.FinalDecisionText);

        TradeLogs.Insert(0, new TradeLogItem
        {
            Time = DateTime.Now.ToString("HH:mm"),
            Pair = Snapshot.Pair,
            Direction = tradeAction,
            Entry = string.IsNullOrWhiteSpace(EntryOverride) ? Entry : EntryOverride,
            StopLoss = string.IsNullOrWhiteSpace(SlOverride) ? StopLoss : SlOverride,
            TakeProfit = string.IsNullOrWhiteSpace(TpOverride) ? TakeProfit : TpOverride,
            RiskReward = RiskReward,
            Lot = LotSize,
            Result = Snapshot.FinalDecisionText,
            Pips = TpPips,
            Notes = Snapshot.BlockReasons.Count == 0
                ? $"Prototype action: {tradeAction}"
                : $"Blocked: {string.Join("; ", Snapshot.BlockReasons.Select(reason => reason.Message))}"
        });

        OnPropertyChanged(nameof(TradeLogSummary));
    }

    private string Price(decimal value)
    {
        if (value == 0)
        {
            return "-";
        }

        return Snapshot.Market.Digits <= 2 ? value.ToString("F2") : value.ToString("F5");
    }

    private void RaiseSnapshotBindings()
    {
        OnPropertyChanged(nameof(SessionName));
        OnPropertyChanged(nameof(UtcTime));
        OnPropertyChanged(nameof(TodayDate));
        OnPropertyChanged(nameof(Decision));
        OnPropertyChanged(nameof(Direction));
        OnPropertyChanged(nameof(SignalDecisionDisplay));
        OnPropertyChanged(nameof(ExecutionReadinessDisplay));
        OnPropertyChanged(nameof(CanPlaceTrade));
        OnPropertyChanged(nameof(CanPlaceTradeDisplay));
        OnPropertyChanged(nameof(PrimaryBlockReasonCodeDisplay));
        OnPropertyChanged(nameof(PrimaryBlockReasonMessageDisplay));
        OnPropertyChanged(nameof(PrimaryBlockReasonDisplay));
        OnPropertyChanged(nameof(SpreadPips));
        OnPropertyChanged(nameof(SpreadPoints));
        OnPropertyChanged(nameof(LastTickAgeMs));
        OnPropertyChanged(nameof(IsMt5Connected));
        OnPropertyChanged(nameof(Mt5ConnectionStatus));
        OnPropertyChanged(nameof(NewsBlackoutActiveDisplay));
        OnPropertyChanged(nameof(NewsStatus));
        OnPropertyChanged(nameof(Entry));
        OnPropertyChanged(nameof(StopLoss));
        OnPropertyChanged(nameof(TakeProfit));
        OnPropertyChanged(nameof(Tp1Price));
        OnPropertyChanged(nameof(Tp2Price));
        OnPropertyChanged(nameof(SlPips));
        OnPropertyChanged(nameof(TpPips));
        OnPropertyChanged(nameof(RiskReward));
        OnPropertyChanged(nameof(RiskRewardTp1));
        OnPropertyChanged(nameof(RiskRewardTp2));
        OnPropertyChanged(nameof(LotSize));
        OnPropertyChanged(nameof(RiskDollar));
        OnPropertyChanged(nameof(MaxProfit));
        OnPropertyChanged(nameof(Confidence));
        OnPropertyChanged(nameof(ConfidencePct));
    }
}
