using Atlas_Execution.MT5;
using Atlas_Execution.Protocol;

namespace Atlas_WinForms.Forms;

/// <summary>
/// Manages the MT5 bridge connection indicator shown in the header.
/// Pings the bridge every 10 seconds and updates the UI label.
/// </summary>
public class Connection_Status_Panel : IDisposable
{
    private readonly MT5_Bridge_Client _bridge;
    private readonly Label             _lbl_connection;
    private readonly System.Windows.Forms.Timer _ping_timer;

    private static readonly Color COL_GREEN  = Color.FromArgb(61, 221, 128);
    private static readonly Color COL_RED    = Color.FromArgb(240, 85, 85);
    private static readonly Color COL_YELLOW = Color.FromArgb(240, 176, 48);

    public bool Is_Connected { get; private set; }

    public Connection_Status_Panel(MT5_Bridge_Client bridge, Label lbl_connection)
    {
        _bridge         = bridge;
        _lbl_connection = lbl_connection;

        _ping_timer = new System.Windows.Forms.Timer { Interval = 10_000 };
        _ping_timer.Tick += async (_, _) => await Ping_Async();
        _ping_timer.Start();
    }

    public async Task Ping_Async()
    {
        Set_Status("● CONNECTING...", COL_YELLOW);

        var response = await _bridge.Send_Async<MT5_Response>(new MT5_Request
        {
            Command = MT5_Command.PING
        });

        if (response?.Is_Ok == true)
        {
            Is_Connected = true;
            Set_Status("● MT5 CONNECTED", COL_GREEN);
        }
        else
        {
            Is_Connected = false;
            Set_Status("● MT5 DISCONNECTED", COL_RED);
        }
    }

    private void Set_Status(string text, Color color)
    {
        if (_lbl_connection.IsDisposed) return;

        if (_lbl_connection.InvokeRequired)
            _lbl_connection.BeginInvoke(() => Update(text, color));
        else
            Update(text, color);
    }

    private void Update(string text, Color color)
    {
        _lbl_connection.Text      = text;
        _lbl_connection.ForeColor = color;
    }

    public void Dispose()
    {
        _ping_timer.Stop();
        _ping_timer.Dispose();
    }
}
