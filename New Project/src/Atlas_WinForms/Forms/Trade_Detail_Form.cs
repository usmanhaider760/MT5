using Atlas_Domain.BusinessObjects;
using Atlas_Domain.Enums;

namespace Atlas_WinForms.Forms;

public class Trade_Detail_Form : Form
{
    private readonly Trade_Result_BO _trade;

    private static readonly Color BG       = Color.FromArgb(14, 14, 21);
    private static readonly Color BG_CARD  = Color.FromArgb(25, 25, 42);
    private static readonly Color TEXT_PRI = Color.FromArgb(221, 224, 240);
    private static readonly Color TEXT_MUT = Color.FromArgb(80, 88, 140);
    private static readonly Color COL_GRN  = Color.FromArgb(61, 221, 128);
    private static readonly Color COL_RED  = Color.FromArgb(240, 85, 85);

    public Trade_Detail_Form(Trade_Result_BO trade)
    {
        _trade = trade;
        InitializeLayout();
    }

    private void InitializeLayout()
    {
        Text            = $"Trade Detail — {_trade.Symbol_Name}";
        ClientSize      = new Size(480, 390);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        BackColor       = BG;
        ForeColor       = TEXT_PRI;
        Font            = new Font("Segoe UI", 9);

        bool   is_win  = _trade.Is_Winner;
        string dir_str = _trade.Direction.ToString().ToUpper();
        string r_str   = $"{_trade.R_Multiple:+0.00;-0.00}R";
        string pnl_str = $"{_trade.Net_PnL_Currency:+$#,##0.00;-$#,##0.00}";

        // ── Title ──────────────────────────────────────────────────────
        var lbl_title = new Label
        {
            Text      = $"{_trade.Symbol_Name}  {dir_str}  {r_str}  {pnl_str}",
            Location  = new Point(12, 10),
            Size      = new Size(456, 26),
            Font      = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = is_win ? COL_GRN : COL_RED,
            BackColor = Color.Transparent
        };
        Controls.Add(lbl_title);

        // ── Price visualization ─────────────────────────────────────────
        var pnl_price = new Panel
        {
            Location  = new Point(12, 42),
            Size      = new Size(456, 120),
            BackColor = BG_CARD
        };
        pnl_price.Paint += Pnl_Price_Paint;
        Controls.Add(pnl_price);

        // ── Trade info ─────────────────────────────────────────────────
        var pnl_info = new Panel
        {
            Location  = new Point(12, 170),
            Size      = new Size(456, 170),
            BackColor = BG_CARD
        };

        int dp = _trade.Symbol_Name.Contains("JPY") ? 3
               : _trade.Symbol_Name.Contains("XAU") ? 2 : 5;
        string pf = $"F{dp}";
        var dur = _trade.Closed_At_UTC - _trade.Opened_At_UTC;
        string dur_str = dur.TotalHours >= 1
            ? $"{(int)dur.TotalHours}h {dur.Minutes}m"
            : $"{(int)dur.TotalMinutes}m";

        var info = new[]
        {
            ("Strategy",   _trade.Strategy.ToString()),
            ("Direction",  dir_str),
            ("Opened UTC", _trade.Opened_At_UTC.ToString("yyyy-MM-dd HH:mm")),
            ("Closed UTC", _trade.Closed_At_UTC.ToString("yyyy-MM-dd HH:mm")),
            ("Duration",   dur_str),
            ("Entry",      _trade.Entry_Price.ToString(pf)),
            ("Exit",       _trade.Exit_Price.ToString(pf)),
            ("Stop Loss",  _trade.Stop_Loss_Price.ToString(pf)),
            ("Take Profit",_trade.Take_Profit_Price.ToString(pf)),
            ("Net P&L",    pnl_str),
            ("R-Multiple", r_str),
            ("Lot Size",   $"{_trade.Lot_Size:F2}"),
        };

        int col2_x = 240;
        for (int i = 0; i < info.Length; i++)
        {
            int col = i < 6 ? 0 : 1;
            int idx = i < 6 ? i : i - 6;
            int bx  = col == 0 ? 10 : col2_x;
            int by  = 12 + idx * 24;

            pnl_info.Controls.Add(new Label
            {
                Text = info[i].Item1 + ":", Location = new Point(bx, by),
                Size = new Size(100, 20), ForeColor = TEXT_MUT, BackColor = Color.Transparent
            });
            pnl_info.Controls.Add(new Label
            {
                Text = info[i].Item2, Location = new Point(bx + 104, by),
                Size = new Size(130, 20), ForeColor = TEXT_PRI, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            });
        }
        Controls.Add(pnl_info);

        // ── Notes (read-only) ───────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(_trade.Notes))
        {
            Controls.Add(new Label
            {
                Text = $"Notes: {_trade.Notes}", Location = new Point(12, 348),
                Size = new Size(456, 18), ForeColor = TEXT_MUT, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8)
            });
        }

        // ── Close button ───────────────────────────────────────────────
        var btn_close = new Button
        {
            Text      = "CLOSE",
            Location  = new Point(336, 358),
            Size      = new Size(132, 26),
            ForeColor = TEXT_MUT,
            BackColor = Color.FromArgb(30, 30, 55),
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor    = Cursors.Hand
        };
        btn_close.FlatAppearance.BorderColor = TEXT_MUT;
        btn_close.Click += (_, _) => Close();
        AcceptButton = btn_close;
        Controls.Add(btn_close);
    }

    private void Pnl_Price_Paint(object? sender, PaintEventArgs e)
    {
        if (_trade.Entry_Price == 0 || _trade.Stop_Loss_Price == 0 || _trade.Take_Profit_Price == 0) return;

        var panel = (Panel)sender!;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int W = panel.Width - 1, H = panel.Height - 1;
        decimal sl = _trade.Stop_Loss_Price, entry = _trade.Entry_Price, tp = _trade.Take_Profit_Price;
        decimal lo = Math.Min(sl, tp), hi = Math.Max(sl, tp);
        decimal padding = (hi - lo) * 0.08m;
        lo -= padding; hi += padding;
        decimal range = hi - lo;
        if (range <= 0) return;

        float PriceToY(decimal p) => (float)((hi - p) / range * (H - 30)) + 10;
        float y_sl    = PriceToY(sl);
        float y_entry = PriceToY(entry);
        float y_tp    = PriceToY(tp);

        // Fill zones
        float zt = Math.Min(y_entry, y_tp), zb = Math.Max(y_entry, y_tp);
        float rt = Math.Min(y_entry, y_sl), rb = Math.Max(y_entry, y_sl);
        using (var br = new SolidBrush(Color.FromArgb(35, 61, 221, 128)))
            g.FillRectangle(br, 60, zt, W - 120, zb - zt);
        using (var br = new SolidBrush(Color.FromArgb(35, 240, 85, 85)))
            g.FillRectangle(br, 60, rt, W - 120, rb - rt);

        // Price lines
        using var pen_sl    = new Pen(COL_RED,   1.5f)  { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
        using var pen_entry = new Pen(TEXT_PRI,  2f);
        using var pen_tp    = new Pen(COL_GRN,   1.5f)  { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };

        g.DrawLine(pen_sl,    60, y_sl,    W - 60, y_sl);
        g.DrawLine(pen_entry, 60, y_entry, W - 60, y_entry);
        g.DrawLine(pen_tp,    60, y_tp,    W - 60, y_tp);

        // Labels
        int dp = _trade.Symbol_Name.Contains("JPY") ? 3
               : _trade.Symbol_Name.Contains("XAU") ? 2 : 5;
        string fmt = $"F{dp}";
        using var font = new Font("Segoe UI", 7.5f);
        using var br_sl    = new SolidBrush(COL_RED);
        using var br_entry = new SolidBrush(TEXT_PRI);
        using var br_tp    = new SolidBrush(COL_GRN);
        using var br_mut   = new SolidBrush(Color.FromArgb(80, 88, 140));

        g.DrawString($"TP  {tp.ToString(fmt)}", font, br_tp,    2, y_tp - 10);
        g.DrawString($"▶   {entry.ToString(fmt)}", font, br_entry, 2, y_entry - 10);
        g.DrawString($"SL  {sl.ToString(fmt)}", font, br_sl,    2, y_sl - 10);

        // R:R label on right
        decimal sl_dist = Math.Abs(entry - sl);
        decimal tp_dist = Math.Abs(tp - entry);
        decimal rr = sl_dist > 0 ? tp_dist / sl_dist : 0;
        g.DrawString($"R:R  {rr:F2}", font, br_mut, W - 55, 4);
    }
}
