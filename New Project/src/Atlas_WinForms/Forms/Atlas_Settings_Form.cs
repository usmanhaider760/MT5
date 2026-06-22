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
        txt_host.Text              = Atlas_Config.MT5_Host;
        num_port.Value             = Math.Clamp(Atlas_Config.MT5_Port,               1,    65535);
        chk_demo.Checked           = Atlas_Config.Demo_Mode;
        num_interval.Value         = Math.Clamp(Atlas_Config.Cycle_Interval_Seconds, 10,   3600);
        txt_telegram_token.Text    = Atlas_Config.Telegram_Bot_Token;
        txt_telegram_chatid.Text   = Atlas_Config.Telegram_Chat_Id;
        txt_email_smtp_host.Text   = Atlas_Config.Email_Smtp_Host;
        num_email_smtp_port.Value  = Math.Clamp(Atlas_Config.Email_Smtp_Port, 1, 65535);
        txt_email_username.Text    = Atlas_Config.Email_Username;
        txt_email_password.Text    = Atlas_Config.Email_Password;
        txt_email_to.Text          = Atlas_Config.Email_To;
        num_forex_risk.Value       = Math.Clamp(Atlas_Config.Risk_Forex_Per_Trade,  0.01m, 5.00m);
        num_daily_loss.Value       = Math.Clamp(Atlas_Config.Risk_Max_Daily_Loss,   0.10m, 10.0m);
        num_dd_breaker.Value       = Math.Clamp(Atlas_Config.Risk_Max_Drawdown,     0.50m, 30.0m);
        num_spread_mult.Value      = Math.Clamp(Atlas_Config.Spread_Limit_Multiplier, 0.50m, 3.00m);
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
            },
            ["Telegram"] = new Dictionary<string, string>
            {
                ["BotToken"] = txt_telegram_token.Text.Trim(),
                ["ChatId"]   = txt_telegram_chatid.Text.Trim()
            },
            ["Email"] = new Dictionary<string, string>
            {
                ["SmtpHost"] = txt_email_smtp_host.Text.Trim(),
                ["SmtpPort"] = ((int)num_email_smtp_port.Value).ToString(),
                ["Username"] = txt_email_username.Text.Trim(),
                ["Password"] = txt_email_password.Text,
                ["To"]       = txt_email_to.Text.Trim()
            },
            ["NewsFeed"] = new Dictionary<string, string>
            {
                ["Url"] = Atlas_Config.News_Feed_Url
            },
            ["Risk"] = new Dictionary<string, string>
            {
                ["ForexRiskPct"]    = num_forex_risk.Value.ToString("F2"),
                ["MaxDailyLossPct"] = num_daily_loss.Value.ToString("F2"),
                ["MaxDrawdownPct"]  = num_dd_breaker.Value.ToString("F2")
            },
            ["SpreadLimits"] = new Dictionary<string, string>
            {
                ["GlobalMultiplier"] = num_spread_mult.Value.ToString("F2")
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
