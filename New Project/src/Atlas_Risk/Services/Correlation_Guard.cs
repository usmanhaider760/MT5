using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;

namespace Atlas_Risk.Services;

/// <summary>
/// Prevents stacking positions across highly correlated currency pairs.
/// Score drops when open positions share significant currency exposure.
/// Direction-specific check fully blocks same-direction pile-ons.
/// </summary>
public class Correlation_Guard
{
    // Pairs that share significant currency exposure in the same direction
    private static readonly Dictionary<string, HashSet<string>> _correlated =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = ["GBPUSD", "AUDUSD", "NZDUSD"],
            ["GBPUSD"] = ["EURUSD", "AUDUSD", "GBPJPY"],
            ["AUDUSD"] = ["EURUSD", "GBPUSD", "NZDUSD"],
            ["NZDUSD"] = ["EURUSD", "AUDUSD"],
            ["USDJPY"] = ["USDCHF", "GBPJPY"],
            ["USDCHF"] = ["USDJPY"],
            ["GBPJPY"] = ["USDJPY", "GBPUSD"],
            ["XAUUSD"] = [],
        };

    // Returns 0–10 score based on correlated open-position count (10 = no conflict)
    public int Get_Correlation_Score(string symbol, IReadOnlyList<Position_BO> open_positions)
    {
        if (!_correlated.TryGetValue(symbol, out var peers) || peers.Count == 0)
            return 10;

        int count = open_positions.Count(p => peers.Contains(p.Symbol_Name));
        return count switch
        {
            0 => 10,
            1 => 5,
            _ => 0
        };
    }

    // Hard block: returns true when an existing position is in the same direction on a correlated pair
    public bool Is_Direction_Correlated(
        string symbol,
        Trade_Direction_Type direction,
        IReadOnlyList<Position_BO> open_positions)
    {
        if (!_correlated.TryGetValue(symbol, out var peers) || peers.Count == 0)
            return false;

        return open_positions.Any(p =>
            peers.Contains(p.Symbol_Name) && p.Direction == direction);
    }
}
