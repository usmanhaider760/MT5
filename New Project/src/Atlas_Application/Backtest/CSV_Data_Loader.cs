using Atlas_Domain.BusinessObjects;

namespace Atlas_Application.Backtest;

/// <summary>
/// Loads historical OHLCV data from CSV files exported by MetaTrader 5.
/// MT5 format: DATE,TIME,OPEN,HIGH,LOW,CLOSE,TICKVOL,VOL,SPREAD
/// </summary>
public static class CSV_Data_Loader
{
    public static List<Candle_BO> Load_From_CSV(string file_path, string symbol, string timeframe)
    {
        if (!File.Exists(file_path))
            throw new FileNotFoundException($"CSV file not found: {file_path}");

        var candles = new List<Candle_BO>();

        foreach (var line in File.ReadLines(file_path).Skip(1)) // skip header
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 6) continue;

            try
            {
                // Support both MT5 export format (DATE TIME) and single-column datetime
                DateTime dt;
                int field_offset = 0;

                if (parts[0].Contains('-') && parts[1].Contains(':'))
                {
                    // MT5 format: 2024.01.02,08:00,...
                    dt = DateTime.Parse($"{parts[0].Replace('.', '-')} {parts[1]}",
                         null, System.Globalization.DateTimeStyles.AssumeUniversal);
                    field_offset = 2;
                }
                else
                {
                    // Single datetime column: 2024-01-02 08:00:00,...
                    dt = DateTime.Parse(parts[0],
                         null, System.Globalization.DateTimeStyles.AssumeUniversal);
                    field_offset = 1;
                }

                candles.Add(new Candle_BO
                {
                    Symbol_Name   = symbol,
                    Timeframe     = timeframe,
                    Open_Time_UTC = dt.ToUniversalTime(),
                    Open          = decimal.Parse(parts[field_offset],     System.Globalization.CultureInfo.InvariantCulture),
                    High          = decimal.Parse(parts[field_offset + 1], System.Globalization.CultureInfo.InvariantCulture),
                    Low           = decimal.Parse(parts[field_offset + 2], System.Globalization.CultureInfo.InvariantCulture),
                    Close         = decimal.Parse(parts[field_offset + 3], System.Globalization.CultureInfo.InvariantCulture),
                    Volume        = parts.Length > field_offset + 4
                                    ? long.Parse(parts[field_offset + 4])
                                    : 0
                });
            }
            catch
            {
                // Skip malformed rows silently
            }
        }

        return candles.OrderBy(c => c.Open_Time_UTC).ToList();
    }

    /// <summary>
    /// Resamples M1 candles up to a target timeframe (H1, H4, D1).
    /// Use when only M1 data is available.
    /// </summary>
    public static List<Candle_BO> Resample(List<Candle_BO> m1_candles, string target_tf)
    {
        int minutes = target_tf switch
        {
            "M5"  => 5,
            "M15" => 15,
            "M30" => 30,
            "H1"  => 60,
            "H4"  => 240,
            "D1"  => 1440,
            _     => throw new ArgumentException($"Unsupported timeframe: {target_tf}")
        };

        var symbol = m1_candles.FirstOrDefault()?.Symbol_Name ?? string.Empty;

        return m1_candles
            .GroupBy(c => Floor_To_Period(c.Open_Time_UTC, minutes))
            .OrderBy(g => g.Key)
            .Select(g => new Candle_BO
            {
                Symbol_Name   = symbol,
                Timeframe     = target_tf,
                Open_Time_UTC = g.Key,
                Open          = g.First().Open,
                High          = g.Max(c => c.High),
                Low           = g.Min(c => c.Low),
                Close         = g.Last().Close,
                Volume        = g.Sum(c => c.Volume)
            }).ToList();
    }

    private static DateTime Floor_To_Period(DateTime dt, int minutes)
    {
        var total = (long)dt.TimeOfDay.TotalMinutes;
        var floored = total - total % minutes;
        return dt.Date.AddMinutes(floored);
    }
}
