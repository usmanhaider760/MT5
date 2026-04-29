using MT5TradingBot.Core;
using MT5TradingBot.Modules.BrokerIntegration;
using MT5TradingBot.Services;

namespace MT5TradingBot.UI
{
    public partial class SplashScreen : Form
    {
        public bool ShouldProceed { get; private set; }

        private readonly List<CheckRowControl> _rows = new();
        private int _totalModules;
        private int _passedModules;

        public SplashScreen()
        {
            InitializeComponent();
            AppIcon.ApplyTo(this);
            this.Load  += SplashScreen_Load;
            _btnProceed.Click += BtnProceed_Click;
        }

        private async void SplashScreen_Load(object? sender, EventArgs e)
        {
            await RunChecksAsync();
        }

        private async Task RunChecksAsync()
        {
            // Load settings first, then build module list
            var sm = new SettingsManager();
            await sm.LoadAsync().ConfigureAwait(false);

            IModule[] modules =
            [
                new BrokerModule(sm.Current.Mt5)
            ];

            _totalModules = modules.Length;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            foreach (var module in modules)
            {
                var row = AddCheckRow(module.Icon, module.Name, module.Description);
                SetStatus($"Checking {module.Name}...");
                await Task.Delay(150, cts.Token).ConfigureAwait(false);

                ModuleStatus result;
                try   { result = await module.CheckAsync(cts.Token).ConfigureAwait(false); }
                catch { result = new ModuleStatus(false, "Check timed out or threw an exception"); }

                row.SetResult(result.IsOk, result.Message);
                if (result.IsOk) _passedModules++;

                AdvanceProgress(_passedModules + (_totalModules - _passedModules));
            }

            AdvanceProgress(_totalModules, final: true);

            bool allPassed = _passedModules == _totalModules;

            if (allPassed)
            {
                SetStatus("All checks passed — ready to launch.");
                EnableProceed("Continue", Color.FromArgb(39, 174, 96));

                await Task.Delay(1500).ConfigureAwait(false);
                this.Invoke(LaunchMainForm);
            }
            else
            {
                int failed = _totalModules - _passedModules;
                SetStatus($"{failed} check{(failed == 1 ? "" : "s")} failed — review before continuing.");
                EnableProceed("Proceed Anyway", Color.FromArgb(180, 130, 30));
            }
        }

        private CheckRowControl AddCheckRow(string icon, string name, string description)
        {
            if (_pnlCheckArea.InvokeRequired)
                return (CheckRowControl)_pnlCheckArea.Invoke(() => AddCheckRow(icon, name, description))!;

            var row = new CheckRowControl(icon, name, description)
            {
                Width  = _pnlCheckArea.ClientSize.Width - 2,
                Top    = _rows.Count * CheckRowControl.RowHeight,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            _rows.Add(row);
            _pnlCheckArea.Controls.Add(row);
            return row;
        }

        private void SetStatus(string text)
        {
            if (_lblStatus.InvokeRequired)
                _lblStatus.Invoke(() => _lblStatus.Text = text);
            else
                _lblStatus.Text = text;
        }

        private void EnableProceed(string text, Color accent)
        {
            Action act = () =>
            {
                _btnProceed.Text      = text;
                _btnProceed.BackColor = accent;
                _btnProceed.ForeColor = Color.White;
                _btnProceed.FlatAppearance.BorderColor = accent;
                _btnProceed.Cursor    = Cursors.Hand;
                _btnProceed.Enabled   = true;
            };
            if (_btnProceed.InvokeRequired) _btnProceed.Invoke(act);
            else act();
        }

        private void AdvanceProgress(int done, bool final = false)
        {
            Action act = () =>
            {
                int trackWidth = _pnlProgressTrack.Width;
                int fillWidth  = final
                    ? trackWidth
                    : (int)Math.Round((double)done / _totalModules * trackWidth);
                _pnlProgressFill.Width = fillWidth;
            };
            if (_pnlProgressFill.InvokeRequired) _pnlProgressFill.Invoke(act);
            else act();
        }

        private void LaunchMainForm()
        {
            ShouldProceed = true;
            this.Close();
        }

        private void BtnProceed_Click(object? sender, EventArgs e) => LaunchMainForm();

        // ── Inner control — one row per module ────────────────────

        private sealed class CheckRowControl : Panel
        {
            public const int RowHeight = 54;

            private readonly Label _lblIcon;
            private readonly Label _lblMsg;

            public CheckRowControl(string icon, string name, string description)
            {
                Height    = RowHeight;
                BackColor = Color.Transparent;

                _lblIcon = new Label
                {
                    Text      = "⏳",
                    Location  = new Point(0, 13),
                    AutoSize  = true,
                    Font      = new Font("Segoe UI", 14f),
                    ForeColor = Color.FromArgb(110, 110, 130)
                };

                var lblName = new Label
                {
                    Text      = $"{icon}  {name}",
                    Location  = new Point(34, 7),
                    AutoSize  = true,
                    Font      = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(218, 218, 230)
                };

                _lblMsg = new Label
                {
                    Text      = description,
                    Location  = new Point(36, 29),
                    AutoSize  = true,
                    Font      = new Font("Segoe UI", 8.5f),
                    ForeColor = Color.FromArgb(110, 110, 130)
                };

                Controls.AddRange(new Control[] { _lblIcon, lblName, _lblMsg });
            }

            public void SetResult(bool ok, string message)
            {
                if (InvokeRequired) { Invoke(() => SetResult(ok, message)); return; }

                _lblIcon.Text      = ok ? "✓" : "✗";
                _lblIcon.ForeColor = ok ? Color.FromArgb(39, 174, 96) : Color.FromArgb(220, 80, 80);
                _lblMsg.Text       = message;
                _lblMsg.ForeColor  = ok ? Color.FromArgb(90, 160, 90) : Color.FromArgb(200, 90, 90);
            }
        }
    }
}
