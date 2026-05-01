using System.Drawing;
using System.Drawing.Drawing2D;
using MT5TradingBot.Models;

namespace MT5TradingBot.UI
{
    internal sealed class EquityCurvePanel : Panel
    {
        private IReadOnlyList<EquityPoint> _points = [];

        private static readonly Color ColBg       = Color.FromArgb(18,  18,  28);
        private static readonly Color ColGrid     = Color.FromArgb(40,  40,  60);
        private static readonly Color ColAxisText = Color.FromArgb(140, 140, 170);
        private static readonly Color ColLinePos  = Color.FromArgb(0,   200, 120);
        private static readonly Color ColLineNeg  = Color.FromArgb(220, 60,  60);
        private static readonly Color ColZero     = Color.FromArgb(80,  80,  110);

        public EquityCurvePanel()
        {
            DoubleBuffered = true;
            ResizeRedraw   = true;
        }

        public void SetData(IReadOnlyList<EquityPoint> points)
        {
            _points = points;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(ColBg);

            const int padL = 72, padR = 20, padT = 20, padB = 36;
            var plot = new Rectangle(padL, padT, Width - padL - padR, Height - padT - padB);
            if (plot.Width < 10 || plot.Height < 10) return;

            if (_points.Count < 2)
            {
                using var noFont  = new Font("Segoe UI", 11f);
                using var noBrush = new SolidBrush(ColAxisText);
                string msg = _points.Count == 0
                    ? "No closed trades yet." : "Need at least 2 data points.";
                var sz = g.MeasureString(msg, noFont);
                g.DrawString(msg, noFont, noBrush, (Width - sz.Width) / 2f, (Height - sz.Height) / 2f);
                return;
            }

            double minPnl  = _points.Min(p => p.CumulativePnl);
            double maxPnl  = _points.Max(p => p.CumulativePnl);
            if (Math.Abs(maxPnl - minPnl) < 0.01) { minPnl -= 1; maxPnl += 1; }
            double pnlRange = maxPnl - minPnl;

            DateTime minAt  = _points[0].At;
            DateTime maxAt  = _points[^1].At;
            double   tRange = Math.Max((maxAt - minAt).TotalSeconds, 1);

            PointF ToScreen(EquityPoint p) => new(
                plot.Left + (float)((p.At - minAt).TotalSeconds / tRange * plot.Width),
                plot.Bottom - (float)((p.CumulativePnl - minPnl) / pnlRange * plot.Height));

            // Grid
            using var gridPen  = new Pen(ColGrid, 1f) { DashStyle = DashStyle.Dot };
            using var axisFnt  = new Font("Consolas", 8f);
            using var axisBrush = new SolidBrush(ColAxisText);
            for (int i = 0; i <= 5; i++)
            {
                double val = minPnl + pnlRange * i / 5;
                float  y   = plot.Bottom - (float)(i * plot.Height / 5);
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                string lbl = val >= 0 ? $"+{val:F0}" : $"{val:F0}";
                g.DrawString(lbl, axisFnt, axisBrush, 2, y - 8);
            }

            // Zero line
            if (minPnl < 0 && maxPnl > 0)
            {
                float zy = plot.Bottom - (float)(-minPnl / pnlRange * plot.Height);
                using var zp = new Pen(ColZero, 1.5f);
                g.DrawLine(zp, plot.Left, zy, plot.Right, zy);
            }

            // Fill under curve
            var screenPts = _points.Select(ToScreen).ToArray();
            float baseY   = Math.Clamp(
                plot.Bottom - (float)(-minPnl / pnlRange * plot.Height),
                plot.Top, plot.Bottom);

            if (screenPts.Length >= 2)
            {
                var poly = screenPts.ToList();
                poly.Add(new PointF(screenPts[^1].X, baseY));
                poly.Add(new PointF(screenPts[0].X,  baseY));
                double fin = _points[^1].CumulativePnl;
                using var fb = new SolidBrush(fin >= 0
                    ? Color.FromArgb(35, 0, 200, 120)
                    : Color.FromArgb(35, 220, 60, 60));
                g.FillPolygon(fb, [.. poly]);
            }

            // Equity line
            for (int i = 1; i < screenPts.Length; i++)
            {
                double prev = _points[i - 1].CumulativePnl;
                double cur  = _points[i].CumulativePnl;
                Color col   = (cur >= 0 || prev >= 0 && cur >= prev) ? ColLinePos : ColLineNeg;
                using var lp = new Pen(col, 2f);
                g.DrawLine(lp, screenPts[i - 1], screenPts[i]);
            }

            // Dots (suppressed when dense)
            if (_points.Count <= 100)
            {
                foreach (var (pt, sp) in _points.Zip(screenPts))
                {
                    using var db = new SolidBrush(pt.IsWin ? ColLinePos : ColLineNeg);
                    g.FillEllipse(db, sp.X - 3, sp.Y - 3, 6, 6);
                }
            }

            // X-axis date labels
            string fmt = tRange > 86400 * 30 ? "MMM yy"
                       : tRange > 86400      ? "dd MMM"
                       : "HH:mm";
            for (int i = 0; i <= 4; i++)
            {
                double frac = i / 4.0;
                float  x    = plot.Left + (float)(frac * plot.Width);
                var    dt   = minAt + TimeSpan.FromSeconds(frac * tRange);
                string lbl  = dt.ToString(fmt);
                var    sz   = g.MeasureString(lbl, axisFnt);
                g.DrawString(lbl, axisFnt, axisBrush, x - sz.Width / 2, plot.Bottom + 4);
            }
        }
    }
}
