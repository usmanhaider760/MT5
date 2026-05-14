using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MT5GoldScalper.V2.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private DashboardSection? _selectedSection;
    private bool _isDarkTheme = true;
    private int _tileColumns = 8;
    private string _selectedPair = "EURUSDm";

    public DashboardViewModel()
    {
        Pairs = ["EURUSDm", "GBPUSDm", "USDJPYm", "XAUUSDm", "AUDUSDm", "USDCADm"];
        Sections = BuildSections();
        TradeLogs = BuildTradeLogs();

        SelectSectionCommand = new RelayCommand<DashboardSection>(SelectSection);
        SetThemeCommand = new RelayCommand<string>(value => IsDarkTheme = value != "light");
        SetLayoutCommand = new RelayCommand<string>(SetLayout);
        AddLogCommand = new RelayCommand<string>(AddLog);
        ResetCommand = new RelayCommand(() => SelectedSection = null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Pairs { get; }
    public ObservableCollection<DashboardSection> Sections { get; }
    public ObservableCollection<TradeLogEntry> TradeLogs { get; }

    public ICommand SelectSectionCommand { get; }
    public ICommand SetThemeCommand { get; }
    public ICommand SetLayoutCommand { get; }
    public ICommand AddLogCommand { get; }
    public ICommand ResetCommand { get; }

    public string SelectedPair
    {
        get => _selectedPair;
        set => SetField(ref _selectedPair, value);
    }

    public DashboardSection? SelectedSection
    {
        get => _selectedSection;
        private set
        {
            if (SetField(ref _selectedSection, value))
            {
                OnPropertyChanged(nameof(HasSelectedSection));
            }
        }
    }

    public bool HasSelectedSection => SelectedSection is not null;

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set => SetField(ref _isDarkTheme, value);
    }

    public int TileColumns
    {
        get => _tileColumns;
        set => SetField(ref _tileColumns, value);
    }

    public string ThemeLabel => IsDarkTheme ? "Dark" : "Light";

    private void SelectSection(DashboardSection? section)
    {
        SelectedSection = ReferenceEquals(SelectedSection, section) ? null : section;
    }

    private void AddLog(string? direction)
    {
        direction ??= "SKIP";
        var isTrade = direction is "BUY" or "SELL";

        TradeLogs.Insert(0, new TradeLogEntry(
            DateTime.Now.ToString("HH:mm"),
            SelectedPair,
            direction,
            isTrade ? "1.08530" : "-",
            isTrade ? "15p" : "-",
            isTrade ? "30p" : "-",
            isTrade ? "1:2" : "-",
            isTrade ? "0.02" : "-",
            isTrade ? "OPEN" : "SKIP",
            isTrade ? "+0p" : "-",
            "Manual log"));
    }

    private void SetLayout(string? value)
    {
        if (int.TryParse(value, out var columns) && columns is 2 or 4 or 8)
        {
            TileColumns = columns;
            SelectedSection = null;
        }
    }

    private static ObservableCollection<DashboardSection> BuildSections() =>
    [
        new("Account &\nTrade Safety", "SAFE", "Good", [
            new("Balance", "$500", "AUTO"), new("Equity", "$498", "AUTO"), new("Free Margin", "$450", "AUTO"),
            new("Margin Lvl", "842%", "AUTO"), new("Daily P/L", "+$6", "AUTO"), new("Open Trades", "1", "AUTO"),
            new("Risk/Trade", "1%", "CFG"), new("Max Trades", "1/3", "CFG")
        ], [
            new("Account Balance", "$500.00", "AUTO", "Defines max lot size", "100%"),
            new("Equity", "$498.20", "AUTO", "Balance + floating P/L", "99.6%"),
            new("Free Margin", "$450.00", "AUTO", "Available margin safe", "90%"),
            new("Margin Level", "842%", "AUTO", "Above 200% safe", "100%"),
            new("Max Daily Loss", "$10 (2%)", "CFG", "Not hit today", "0%"),
            new("Consecutive Losses", "0", "AUTO", "No losing streak", "100%")
        ]),
        new("Pair &\nSession", "OPTIMAL", "Good", [
            new("Pair", "EURUSDm", "SEL"), new("Spread", "0.9p", "AUTO"), new("Session", "LN-NY", "AUTO"),
            new("UTC Time", "14:32", "AUTO"), new("To Close", "3h 28m", "AUTO"), new("Volatility", "Active", "AUTO"),
            new("Mkt Type", "Trending", "AUTO"), new("Friday Risk", "No", "AUTO")
        ], [
            new("Selected Pair", "EURUSDm", "SEL", "User selected", "-"),
            new("Current Spread", "0.9 pips", "AUTO", "Acceptable", "82%"),
            new("Liquidity", "High", "AUTO", "Tick volume strong", "88%"),
            new("Session Now", "London-NY Overlap", "AUTO", "Best session", "95%"),
            new("Rollover", "7h 28m away", "AUTO", "Safe to enter", "100%")
        ]),
        new("News &\nSentiment", "CAUTION", "Watch", [
            new("Next Event", "CPI 2h", "API"), new("Blackout", "No", "AUTO"), new("DXY", "Rising", "API"),
            new("VIX", "14.2", "API"), new("Risk Mood", "Neutral", "MAN"), new("ECB Tone", "Hawkish", "MAN"),
            new("Fed Tone", "Hawkish", "MAN"), new("Geo Risk", "Low", "MAN")
        ], [
            new("Next USD Event", "CPI - 2h 10m", "API", "High impact", "55%"),
            new("Blackout Active", "No", "AUTO", ">30m away", "100%"),
            new("DXY Direction", "Rising", "API", "USD strong", "70%"),
            new("Risk Mood", "Neutral", "MAN", "Manual input", "50%"),
            new("Double USD Bet", "No", "AUTO", "No duplicate risk", "100%")
        ]),
        new("Spread &\nExecution", "ACCEPTABLE", "Good", [
            new("Spread Now", "0.9p", "AUTO"), new("Avg Spread", "1.1p", "AUTO"), new("Spike?", "No", "AUTO"),
            new("Commission", "$0.07", "AUTO"), new("Slippage", "0.3p", "AUTO"), new("Round Trip", "2.3p", "AUTO"),
            new("Cost/TP", "7.7%", "AUTO"), new("Cost/Range", "14%", "AUTO")
        ], [
            new("Current Spread", "0.9 pips", "AUTO", "Entry cost now", "85%"),
            new("Average Spread", "1.1 pips", "AUTO", "Normal for pair", "78%"),
            new("Round-Trip Cost", "2.3 pips", "AUTO", "Total cost", "74%"),
            new("Filter Result", "PASS", "AUTO", "Ratio acceptable", "100%")
        ]),
        new("HTF Trend\n& Indicators", "BULLISH", "Good", [
            new("Weekly", "Up Bull", "AUTO"), new("Daily", "Up Bull", "AUTO"), new("H4", "Up Bull", "AUTO"),
            new("H1", "Up Bull", "AUTO"), new("M15", "Pullback", "AUTO"), new("EMA 200", "Above", "AUTO"),
            new("ADX", "24 Str", "AUTO"), new("RSI H1", "58", "AUTO")
        ], [
            new("Weekly Trend", "Bullish", "AUTO", "EMA alignment W", "82%"),
            new("EMA Alignment", "8>21>50>200", "AUTO", "Full bull stack", "90%"),
            new("ADX", "24 - Strong", "AUTO", "Trending market", "72%"),
            new("Volatility State", "Normal", "AUTO", "No spike", "80%")
        ]),
        new("Structure\n& Levels", "WATCH", "Watch", [
            new("Structure", "HH/HL", "AUTO"), new("BOS", "Bull", "AUTO"), new("CHoCH", "No", "AUTO"),
            new("FVG", "Below", "AUTO"), new("Order Block", "Bull 78%", "SEMI"), new("Liq Sweep", "Partial", "AUTO"),
            new("Liq Above", "Eq Hi", "AUTO"), new("Location", "Demand", "SEMI")
        ], [
            new("Market Structure", "HH / HL", "AUTO", "Swing detection", "76%"),
            new("Break of Structure", "Yes - Bull", "AUTO", "Close above swing hi", "80%"),
            new("Order Block", "Bull - 78%", "SEMI", "Rule confidence", "78%"),
            new("Price Location", "At Demand", "SEMI", "Zone proximity", "74%")
        ]),
        new("Setup &\nCandle", "READY", "Good", [
            new("Pattern", "Pin Bar", "AUTO"), new("At Level", "Yes", "AUTO"), new("Pullback", "Clean", "SEMI"),
            new("Confluence", "3/3", "AUTO"), new("MACD", "Bull", "AUTO"), new("Stoch", "42", "AUTO"),
            new("BB", "Mid Exp", "AUTO"), new("Vol Confirm", "High", "AUTO")
        ], [
            new("Candle Pattern", "Pin Bar", "AUTO", "OHLC formula", "78%"),
            new("At Key Level", "Yes", "AUTO", "Zone proximity", "85%"),
            new("Confluence Count", "3 of 3", "AUTO", "Signal stack", "100%"),
            new("Setup Quality", "Strong 88%", "SEMI", "Aggregated score", "88%")
        ]),
        new("Trade Plan\n& Risk", "VALID", "Good", [
            new("Entry", "1.08530", "AUTO"), new("SL", "1.08380", "AUTO"), new("TP", "1.08830", "AUTO"),
            new("R:R", "1:2.0", "AUTO"), new("Lot", "0.02", "AUTO"), new("Risk $", "$5 (1%)", "AUTO"),
            new("SL Pips", "15p", "AUTO"), new("TP Pips", "30p", "AUTO")
        ], [
            new("Entry Price", "1.08530", "AUTO", "Structure + FVG", "78%"),
            new("Stop Loss", "1.08380", "AUTO", "Below swing + buffer", "82%"),
            new("Take Profit", "1.08830", "AUTO", "Next liquidity / 2R", "76%"),
            new("Confidence", "78%", "AUTO", "Weighted score", "78%")
        ])
    ];

    private static ObservableCollection<TradeLogEntry> BuildTradeLogs() =>
    [
        new("11:30", "EURUSDm", "SELL", "1.0901", "12p", "24p", "1:2", "0.02", "WIN", "+24p", "Clean S/R break"),
        new("09:15", "EURUSDm", "BUY", "1.0820", "18p", "36p", "1:2", "0.02", "LOSS", "-18p", "News spike"),
        new("08:45", "EURUSDm", "SKIP", "-", "-", "-", "-", "-", "SKIP", "-", "Spread high")
    ];

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsDarkTheme))
        {
            OnPropertyChanged(nameof(ThemeLabel));
        }
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record DashboardSection(
    string Title,
    string Status,
    string Severity,
    ObservableCollection<MetricItem> Summary,
    ObservableCollection<DetailItem> Details);

public sealed record MetricItem(string Label, string Value, string Source);

public sealed record DetailItem(string Label, string Value, string Source, string Note, string Percentage)
{
    public DetailItem(string label, string value, string source, string note)
        : this(label, value, source, note, "-")
    {
    }
}

public sealed record TradeLogEntry(
    string Time,
    string Pair,
    string Direction,
    string Entry,
    string StopLoss,
    string TakeProfit,
    string RiskReward,
    string Lot,
    string Result,
    string Pips,
    string Notes);

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;

    public RelayCommand(Action<T?> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        if (parameter is T typed)
        {
            _execute(typed);
            return;
        }

        _execute(default);
    }
}
