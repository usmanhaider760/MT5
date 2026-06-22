using Atlas_Application.Services;
using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;
using Xunit;

namespace Atlas_Tests;

public class Daily_Report_Tests
{
    private static Trade_Result_BO Win(decimal r) => new()
    {
        Symbol_Name     = "EURUSD",
        Strategy        = Strategy_Type.Trend_Pullback_Continuation,
        Direction       = Trade_Direction_Type.Buy,
        Lot_Size        = 0.10m,
        Entry_Price     = 1.0900m,
        Exit_Price      = 1.0900m + (0.0010m * r),
        Stop_Loss_Price = 1.0890m,
        Gross_PnL_Currency = r * 10m,
        Commission      = 0,
        Swap            = 0,
        Closed_At_UTC   = DateTime.UtcNow
    };

    [Fact]
    public void Empty_Trade_List_Returns_No_Trades_Message()
    {
        string report = Daily_Report_Service.Generate(DateTime.UtcNow.Date, []);
        Assert.Contains("No closed trades today", report);
    }

    [Fact]
    public void Report_Contains_Date_And_Stats()
    {
        var date   = new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);
        var trades = new List<Trade_Result_BO> { Win(2.0m), Win(-1.0m) };
        string report = Daily_Report_Service.Generate(date, trades);

        Assert.Contains("2026-06-22", report);
        Assert.Contains("2 ", report);    // 2 trades
        Assert.Contains("50.0%", report); // 1W/2 = 50%
    }

    [Fact]
    public void Csv_Has_Header_And_One_Row_Per_Trade()
    {
        var trades = new List<Trade_Result_BO> { Win(2.0m), Win(-1.0m), Win(1.5m) };
        string csv  = Daily_Report_Service.To_Csv(trades);
        var lines   = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("Closed_UTC", lines[0]);
        Assert.Equal(4, lines.Length); // header + 3 data rows
    }

    [Fact]
    public void Csv_Escapes_Commas_In_Reason()
    {
        var trade = Win(1.0m);
        trade.Close_Reason = "Hit TP, partial close";
        string csv = Daily_Report_Service.To_Csv([trade]);
        Assert.Contains("\"Hit TP, partial close\"", csv);
    }

    [Fact]
    public void Report_Includes_Per_Symbol_Breakdown()
    {
        var t1 = Win(2.0m); t1.Symbol_Name = "EURUSD";
        var t2 = Win(1.5m); t2.Symbol_Name = "XAUUSD";
        string report = Daily_Report_Service.Generate(DateTime.UtcNow.Date, [t1, t2]);
        Assert.Contains("EURUSD", report);
        Assert.Contains("XAUUSD", report);
    }

    // ── EscapeCsv regression tests (via To_Csv Notes/Reason fields) ──

    [Fact]
    public void EscapeCsv_Plain_Text_Is_Unquoted()
    {
        var trade = Win(1.0m);
        trade.Close_Reason = "HitTP";
        string csv = Daily_Report_Service.To_Csv([trade]);
        Assert.Contains(",HitTP,", csv);
        Assert.DoesNotContain("\"HitTP\"", csv);
    }

    [Fact]
    public void EscapeCsv_Double_Quote_Is_Escaped_And_Wrapped()
    {
        var trade = Win(1.0m);
        trade.Notes = "she said \"buy\"";
        string csv = Daily_Report_Service.To_Csv([trade]);
        Assert.Contains("\"she said \"\"buy\"\"\"", csv);
    }

    [Fact]
    public void EscapeCsv_Empty_String_Is_Empty_Field()
    {
        var trade = Win(1.0m);
        trade.Close_Reason = string.Empty;
        trade.Notes        = string.Empty;
        string csv = Daily_Report_Service.To_Csv([trade]);
        // Last two fields are both empty — row ends with two trailing commas then newline
        Assert.Contains(",,", csv.Split('\n')[1]);
    }

    [Fact]
    public void EscapeCsv_Text_With_Both_Comma_And_Quote_Is_Fully_Escaped()
    {
        var trade = Win(1.0m);
        trade.Notes = "TP, \"partial\"";
        string csv = Daily_Report_Service.To_Csv([trade]);
        // Should be wrapped and inner quotes doubled
        Assert.Contains("\"TP, \"\"partial\"\"\"", csv);
    }
}
