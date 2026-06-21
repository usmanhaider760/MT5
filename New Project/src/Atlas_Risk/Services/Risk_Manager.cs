using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Atlas_Domain.Interfaces;
using Atlas_Shared;

namespace Atlas_Risk.Services;

public class Risk_Manager : I_Risk_Manager
{
    private readonly Risk_Setting_BO _settings;
    private Account_State_BO _account = new();

    public Risk_Manager(Risk_Setting_BO settings)
    {
        _settings = settings;
    }

    public async Task<(bool Approved, string Reason)> Validate_Trade_Risk_Async(
        Trade_Signal_BO signal,
        Account_State_BO account,
        List<Position_BO> open_positions)
    {
        _account = account;

        if (account.Is_Daily_Loss_Limit_Hit)
            return (false, "Daily loss limit already reached. No new trades today.");

        if (account.Is_Weekly_Loss_Limit_Hit)
            return (false, "Weekly loss limit reached. No new trades this week.");

        if (account.Is_Drawdown_Limit_Hit)
            return (false, "Account drawdown circuit breaker active.");

        var daily_loss_pct = account.Daily_PnL < 0
            ? Math.Abs(account.Daily_PnL) / account.Day_Open_Balance * 100
            : 0;
        if (daily_loss_pct >= _settings.Max_Daily_Loss_Percent)
            return (false, $"Daily loss {daily_loss_pct:F2}% reached max {_settings.Max_Daily_Loss_Percent}%.");

        var weekly_loss_pct = account.Weekly_PnL < 0
            ? Math.Abs(account.Weekly_PnL) / account.Week_Open_Balance * 100
            : 0;
        if (weekly_loss_pct >= _settings.Max_Weekly_Loss_Percent)
            return (false, $"Weekly loss {weekly_loss_pct:F2}% reached max {_settings.Max_Weekly_Loss_Percent}%.");

        if (account.Drawdown_From_Peak >= _settings.Account_Drawdown_Circuit_Breaker_Percent)
            return (false, $"Drawdown {account.Drawdown_From_Peak:F2}% hit circuit breaker {_settings.Account_Drawdown_Circuit_Breaker_Percent}%.");

        if (account.Consecutive_Losses >= _settings.Max_Consecutive_Losses_Before_Pause)
            return (false, $"Consecutive losses ({account.Consecutive_Losses}) hit pause threshold.");

        if (open_positions.Count >= _settings.Max_Open_Trades)
            return (false, $"Max open trades ({_settings.Max_Open_Trades}) already reached.");

        // Gold-specific: max 1 Gold trade
        bool is_gold = signal.Symbol_Name.Contains("XAU", StringComparison.OrdinalIgnoreCase);
        if (is_gold)
        {
            int gold_count = open_positions.Count(p => p.Symbol_Name.Contains("XAU", StringComparison.OrdinalIgnoreCase));
            if (gold_count >= _settings.Max_Gold_Trades)
                return (false, "Max Gold trades already open.");
        }

        // Correlation check: count same-USD-direction exposure
        var correlation_result = Check_Correlation(signal, open_positions);
        if (!correlation_result.ok)
            return (false, correlation_result.reason);

        // R:R minimum
        bool is_swing = signal.Strategy == Strategy_Type.Trend_Pullback_Continuation
                     || signal.Strategy == Strategy_Type.Breakout_Retest_Expansion;
        decimal min_rr = is_gold && is_swing ? _settings.Min_Reward_Risk_Gold_Swing
                       : is_swing             ? _settings.Min_Reward_Risk_Forex
                       :                        _settings.Min_Reward_Risk_Intraday;

        if (signal.Reward_Risk_Ratio < min_rr)
            return (false, $"R:R {signal.Reward_Risk_Ratio:F2} is below minimum {min_rr:F2} for {signal.Strategy}.");

        await Task.CompletedTask;
        return (true, string.Empty);
    }

    public decimal Calculate_Lot_Size(decimal account_equity, decimal risk_percent, decimal stop_loss_pips, decimal pip_value_per_lot)
    {
        if (stop_loss_pips <= 0 || pip_value_per_lot <= 0) return 0;
        decimal risk_amount = account_equity * (risk_percent / 100m);
        decimal lot_size = risk_amount / (stop_loss_pips * pip_value_per_lot);
        return Math.Round(lot_size, 2);
    }

    public Task<Account_State_BO> Get_Account_State_Async() => Task.FromResult(_account);

    public void Update_Account_State(Account_State_BO state) => _account = state;

    private static (bool ok, string reason) Check_Correlation(Trade_Signal_BO signal, List<Position_BO> open_positions)
    {
        // Check if same currency pair is already open
        var same_symbol = open_positions.Any(p => p.Symbol_Name == signal.Symbol_Name);
        if (same_symbol)
            return (false, $"{signal.Symbol_Name} already has an open position.");

        // USD-direction correlation check
        bool is_usd_long_signal  = signal.Direction == Trade_Direction_Type.Buy
                                 && signal.Symbol_Name.StartsWith("USD", StringComparison.OrdinalIgnoreCase);
        bool is_usd_short_signal = signal.Direction == Trade_Direction_Type.Sell
                                 && signal.Symbol_Name.StartsWith("USD", StringComparison.OrdinalIgnoreCase);

        // Count correlated USD-direction positions
        // Simplified: treat as correlated if both have USD as quote/base moving same way
        int correlated_count = open_positions.Count(p =>
            (p.Symbol_Name.Contains("USD", StringComparison.OrdinalIgnoreCase)));

        if (correlated_count >= Trading_Constants.Max_Active_Forex_Symbols)
            return (false, $"USD correlation limit reached ({correlated_count} correlated positions open).");

        return (true, string.Empty);
    }
}
