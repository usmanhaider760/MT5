namespace MT5TradingBot.UI
{
    public sealed class PairSettingsJsonForm : Form
    {
        private readonly RichTextBox _txtJson = new();
        private readonly Func<string, bool>? _acceptValidator;

        public string JsonText => _txtJson.Text;

        public PairSettingsJsonForm(
            string sampleJson,
            string title = "Import Pair Settings JSON",
            string acceptButtonText = "Import",
            Func<string, bool>? acceptValidator = null)
        {
            _acceptValidator = acceptValidator;

            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(620, 480);
            ClientSize = new Size(720, 560);
            BackColor = Color.FromArgb(22, 22, 32);
            ForeColor = Color.FromArgb(218, 218, 230);
            Font = new Font("Segoe UI", 9F);

            _txtJson.Dock = DockStyle.Fill;
            _txtJson.Text = sampleJson;
            _txtJson.Font = new Font("Consolas", 9F);
            _txtJson.BackColor = Color.FromArgb(13, 13, 19);
            _txtJson.ForeColor = Color.White;
            _txtJson.BorderStyle = BorderStyle.None;
            _txtJson.AcceptsTab = true;

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            var btnImport = Button(acceptButtonText, Color.FromArgb(72, 199, 142));
            var btnCancel = Button("Cancel", Color.FromArgb(110, 110, 130));
            btnImport.Click += BtnAccept_Click;
            btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(btnImport);
            buttons.Controls.Add(btnCancel);

            Controls.Add(_txtJson);
            Controls.Add(buttons);
        }

        private void BtnAccept_Click(object? sender, EventArgs e)
        {
            if (_acceptValidator != null && !_acceptValidator(_txtJson.Text))
                return;

            DialogResult = DialogResult.OK;
        }

        private static Button Button(string text, Color color) => new()
        {
            Text = text,
            Width = Math.Max(96, text.Length * 9 + 24),
            Height = 32,
            BackColor = color,
            ForeColor = Color.FromArgb(10, 10, 20),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0)
        };
    }
}
