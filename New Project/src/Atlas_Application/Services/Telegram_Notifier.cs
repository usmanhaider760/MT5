namespace Atlas_Application.Services;

/// <summary>
/// Sends HTML-formatted alerts to a Telegram chat via the Bot API.
/// Silently no-ops when token or chat-id are not configured.
/// </summary>
public class Telegram_Notifier
{
    private readonly HttpClient _http;
    private readonly string     _token;
    private readonly string     _chat_id;

    private bool Active => !string.IsNullOrWhiteSpace(_token) && !string.IsNullOrWhiteSpace(_chat_id);

    public Telegram_Notifier(string bot_token, string chat_id, HttpClient? http = null)
    {
        _token   = bot_token;
        _chat_id = chat_id;
        _http    = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task Send_Async(string message)
    {
        if (!Active) return;
        try
        {
            var url  = $"https://api.telegram.org/bot{_token}/sendMessage";
            var body = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("chat_id",    _chat_id),
                new KeyValuePair<string, string>("text",       message),
                new KeyValuePair<string, string>("parse_mode", "HTML")
            ]);
            await _http.PostAsync(url, body).ConfigureAwait(false);
        }
        catch
        {
            // Notification failures must never abort bot operation
        }
    }
}
