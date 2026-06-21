using System.Text.Json;

namespace Atlas_WinForms.Forms;

public partial class Atlas_Settings_Form : Form
{
    public Atlas_Settings_Form()
    {
        InitializeComponent();
        Load_Values();
    }

    private void Load_Values()
    {
        txt_host.Text      = Atlas_Config.MT5_Host;
        num_port.Value     = Math.Clamp(Atlas_Config.MT5_Port,               1,    65535);
        chk_demo.Checked   = Atlas_Config.Demo_Mode;
        num_interval.Value = Math.Clamp(Atlas_Config.Cycle_Interval_Seconds, 10,   3600);
    }

    private void btn_save_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txt_host.Text))
        {
            MessageBox.Show("MT5 Host cannot be empty.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txt_host.Focus();
            return;
        }

        var settings = new Dictionary<string, object>
        {
            ["MT5"] = new Dictionary<string, string>
            {
                ["Host"] = txt_host.Text.Trim(),
                ["Port"] = ((int)num_port.Value).ToString()
            },
            ["Bot"] = new Dictionary<string, string>
            {
                ["DemoMode"]             = chk_demo.Checked ? "true" : "false",
                ["CycleIntervalSeconds"] = ((int)num_interval.Value).ToString()
            },
            ["Database"] = new Dictionary<string, string>
            {
                ["Path"] = Atlas_Config.Db_Path
            }
        };

        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            File.WriteAllText(path, json);

            MessageBox.Show(
                "Settings saved successfully.\n\nRestart the application for MT5 bridge and cycle interval changes to take effect.",
                "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings:\n{ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btn_cancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
