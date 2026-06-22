using Atlas_Application.Backtest;
using Atlas_Domain.BusinessObjects;
using Xunit;

namespace Atlas_Tests;

/// <summary>
/// Tests for CSV_Data_Loader: MT5 two-column date format, single-column datetime,
/// malformed row tolerance, resampling accuracy, and file-not-found guard.
/// </summary>
public class CSV_Data_Loader_Tests
{
    // ── Helpers ───────────────────────────────────────────────────────

    private static string Write_Temp_Csv(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"atlas_test_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        return path;
    }

    private static void Delete(string path) { try { File.Delete(path); } catch { } }

    // ── MT5 two-column date format ─────────────────────────────────

    [Fact]
    public void Loads_MT5_Format_Date_And_Time_Columns()
    {
        string csv = """
            DATE,TIME,OPEN,HIGH,LOW,CLOSE,TICKVOL,VOL,SPREAD
            2024.01.02,08:00,1.08000,1.08500,1.07800,1.08300,1200,0,1
            2024.01.02,08:15,1.08300,1.08600,1.08100,1.08450,900,0,1
            """;

        string path = Write_Temp_Csv(csv);
        try
        {
            var candles = CSV_Data_Loader.Load_From_CSV(path, "EURUSD", "M15");

            Assert.Equal(2, candles.Count);
            Assert.Equal(1.08000m, candles[0].Open);
            Assert.Equal(1.08500m, candles[0].High);
            Assert.Equal(1.07800m, candles[0].Low);
            Assert.Equal(1.08300m, candles[0].Close);
            Assert.Equal(1200, candles[0].Volume);
            Assert.Equal("EURUSD", candles[0].Symbol_Name);
        }
        finally { Delete(path); }
    }

    // ── Single-column datetime format ─────────────────────────────

    [Fact]
    public void Loads_Single_Column_Datetime_Format()
    {
        string csv = """
            Datetime,Open,High,Low,Close,Volume
            2024-01-02 09:00:00,1.09000,1.09500,1.08800,1.09200,800
            2024-01-02 10:00:00,1.09200,1.09700,1.09000,1.09400,650
            """;

        string path = Write_Temp_Csv(csv);
        try
        {
            var candles = CSV_Data_Loader.Load_From_CSV(path, "GBPUSD", "H1");

            Assert.Equal(2, candles.Count);
            Assert.Equal(1.09000m, candles[0].Open);
            Assert.Equal(1.09200m, candles[0].Close);
            Assert.Equal("GBPUSD", candles[0].Symbol_Name);
        }
        finally { Delete(path); }
    }

    // ── Sorted output ─────────────────────────────────────────────

    [Fact]
    public void Output_Is_Sorted_By_Open_Time_Ascending()
    {
        string csv = """
            DATE,TIME,OPEN,HIGH,LOW,CLOSE,TICKVOL,VOL,SPREAD
            2024.01.02,09:00,1.08000,1.08100,1.07900,1.08050,100,0,1
            2024.01.02,08:00,1.07800,1.08000,1.07700,1.07950,100,0,1
            """;

        string path = Write_Temp_Csv(csv);
        try
        {
            var candles = CSV_Data_Loader.Load_From_CSV(path, "EURUSD", "M15");

            Assert.Equal(2, candles.Count);
            Assert.True(candles[0].Open_Time_UTC < candles[1].Open_Time_UTC);
        }
        finally { Delete(path); }
    }

    // ── Malformed row tolerance ────────────────────────────────────

    [Fact]
    public void Skips_Malformed_Rows_And_Loads_Valid_Ones()
    {
        string csv = """
            DATE,TIME,OPEN,HIGH,LOW,CLOSE,TICKVOL,VOL,SPREAD
            2024.01.02,08:00,1.08000,1.08500,1.07800,1.08300,1200,0,1
            BADROW,notadate,X,Y,Z,W
            2024.01.02,08:15,1.08300,1.08600,1.08100,1.08450,900,0,1
            """;

        string path = Write_Temp_Csv(csv);
        try
        {
            var candles = CSV_Data_Loader.Load_From_CSV(path, "EURUSD", "M15");
            Assert.Equal(2, candles.Count);
        }
        finally { Delete(path); }
    }

    [Fact]
    public void Skips_Short_Rows_With_Fewer_Than_Six_Columns()
    {
        string csv = """
            DATE,TIME,OPEN,HIGH,LOW,CLOSE,TICKVOL
            2024.01.02,08:00,1.08000
            2024.01.02,08:15,1.08300,1.08600,1.08100,1.08450,900,0,1
            """;

        string path = Write_Temp_Csv(csv);
        try
        {
            var candles = CSV_Data_Loader.Load_From_CSV(path, "EURUSD", "M15");
            Assert.Single(candles);
        }
        finally { Delete(path); }
    }

    [Fact]
    public void File_Not_Found_Throws_FileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
            CSV_Data_Loader.Load_From_CSV("nonexistent_atlas_test.csv", "EURUSD", "M15"));
    }

    // ── Resample ──────────────────────────────────────────────────

    [Fact]
    public void Resample_M1_To_M15_Groups_Every_15_Bars()
    {
        var start = new DateTime(2024, 1, 2, 8, 0, 0, DateTimeKind.Utc);
        var m1 = Enumerable.Range(0, 30).Select(i => new Candle_BO
        {
            Symbol_Name   = "EURUSD",
            Open_Time_UTC = start.AddMinutes(i),
            Open          = 1.0800m + i * 0.0001m,
            High          = 1.0810m + i * 0.0001m,
            Low           = 1.0790m + i * 0.0001m,
            Close         = 1.0805m + i * 0.0001m,
            Volume        = 100
        }).ToList();

        var m15 = CSV_Data_Loader.Resample(m1, "M15");

        Assert.Equal(2, m15.Count);
        // First M15: 08:00–08:14 → 15 bars
        Assert.Equal(start, m15[0].Open_Time_UTC);
        Assert.Equal(m1[0].Open,  m15[0].Open);
        Assert.Equal(m1[14].Close, m15[0].Close);
        Assert.Equal(m1.Take(15).Max(c => c.High), m15[0].High);
        Assert.Equal(m1.Take(15).Min(c => c.Low),  m15[0].Low);
        Assert.Equal(1500, m15[0].Volume);
    }

    [Fact]
    public void Resample_M1_To_H1_Groups_Every_60_Bars()
    {
        var start = new DateTime(2024, 1, 2, 8, 0, 0, DateTimeKind.Utc);
        var m1 = Enumerable.Range(0, 120).Select(i => new Candle_BO
        {
            Symbol_Name   = "EURUSD",
            Open_Time_UTC = start.AddMinutes(i),
            Open  = 1.0800m,
            High  = 1.0850m,
            Low   = 1.0750m,
            Close = 1.0820m,
            Volume = 10
        }).ToList();

        var h1 = CSV_Data_Loader.Resample(m1, "H1");

        Assert.Equal(2, h1.Count);
        Assert.Equal(start, h1[0].Open_Time_UTC);
        Assert.Equal(600, h1[0].Volume);
    }

    [Fact]
    public void Resample_Unsupported_Timeframe_Throws()
    {
        var m1 = new List<Candle_BO> { new() { Open_Time_UTC = DateTime.UtcNow } };
        Assert.Throws<ArgumentException>(() => CSV_Data_Loader.Resample(m1, "W1"));
    }
}
