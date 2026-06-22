using Atlas_Domain.BusinessObjects;

namespace Atlas_Application.Services;

/// <summary>
/// Builds a plain-text daily P&L summary from a list of closed trades.
/// Can be sent via Telegram, Email, or written to file.
/// </summary>
public static class Daily_Report_Service
{
    public static string Generate(DateTime report_date_utc, List<Trade_Result_BO> trades)
    {
        string date_str = report_date_utc.ToString("yyyy-MM-dd");

        if (trades.Count == 0)
            return $"ATLAS Daily Report — {date_str}\nNo closed trades today.";

        int    total   = trades.Count;
        int    wins    = trades.Count(t => t.Is_Winner);
        int    losses  = total - wins;
        decimal win_rt = Math.Round((decimal)wins / total * 100, 1);
        decimal total_r = trades.Sum(t => t.R_Multiple);
        decimal total_pnl = trades.Sum(t => t.Net_PnL_Currency);
        decimal avg_r   = Math.Round(total_r / total, 2);

        decimal gross_p = trades.Where(t => t.Is_Winner).Sum(t => t.Net_PnL_Currency);
        decimal gross_l = Math.Abs(trades.Where(t => !t.Is_Winner).Sum(t => t.Net_PnL_Currency));
        decimal pf      = gross_l > 0 ? Math.Round(gross_p / gross_l, 2) : 0;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"━━━  ATLAS Daily Report  ━━━");
        sb.AppendLine($"Date    : {date_str} UTC");
        sb.AppendLine($"Trades  : {total}  ({wins}W / {losses}L)");
        sb.AppendLine($"Win Rate: {win_rt}%");
        sb.AppendLine($"Total R : {total_r:+0.00;-0.00}R   Avg: {avg_r:+0.00;-0.00}R");
        sb.AppendLine($"Net P&L : {total_pnl:+$#,##0.00;-$#,##0.00}");
        sb.AppendLine($"Prof.F  : {pf:F2}");
        sb.AppendLine();

        // Per-symbol breakdown
        var by_symbol = trades.GroupBy(t => t.Symbol_Name)
                              .OrderByDescending(g => g.Sum(t => t.R_Multiple));
        sb.AppendLine("── By Symbol ──");
        foreach (var g in by_symbol)
        {
            decimal sym_r = g.Sum(t => t.R_Multiple);
            sb.AppendLine($"  {g.Key,-10} {g.Count(),2} trade(s)  {sym_r:+0.00;-0.00}R");
        }

        sb.AppendLine();
        sb.AppendLine("── Trades ──");
        foreach (var t in trades.OrderBy(t => t.Closed_At_UTC))
            sb.AppendLine($"  {t.Closed_At_UTC:HH:mm}  {t.Symbol_Name,-10} {t.Direction,-5}" +
                          $"  {t.R_Multiple:+0.00;-0.00}R  {t.Net_PnL_Currency:+$#,##0.00;-$#,##0.00}");

        return sb.ToString();
    }

    public static string To_Csv(List<Trade_Result_BO> trades)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Closed_UTC,Symbol,Strategy,Direction,Lot_Size,Entry,Exit," +
                      "SL,TP,R_Multiple,Gross_PnL,Commission,Swap,Net_PnL,Is_Winner,Reason,Notes");

        foreach (var t in trades)
            sb.AppendLine(
                $"{t.Closed_At_UTC:yyyy-MM-dd HH:mm:ss}," +
                $"{t.Symbol_Name},{t.Strategy},{t.Direction}," +
                $"{t.Lot_Size},{t.Entry_Price},{t.Exit_Price}," +
                $"{t.Stop_Loss_Price},{t.Take_Profit_Price}," +
                $"{t.R_Multiple:F4}," +
                $"{t.Gross_PnL_Currency:F2},{t.Commission:F2},{t.Swap:F2},{t.Net_PnL_Currency:F2}," +
                $"{(t.Is_Winner ? 1 : 0)},{EscapeCsv(t.Close_Reason)},{EscapeCsv(t.Notes)}");

        return sb.ToString();
    }

    private static string EscapeCsv(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }
}
