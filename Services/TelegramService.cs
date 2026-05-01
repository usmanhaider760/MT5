using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using MT5TradingBot.Models;
using Newtonsoft.Json;
using Serilog;

namespace MT5TradingBot.Services
{
    public interface ITelegramService
    {
        Task SendSignalAsync(string symbol, string direction, string reason);
        Task SendApprovalNeededAsync(string symbol, string direction, double lotSize);
        Task SendTradeOpenedAsync(string symbol, string direction, double lotSize,
                                  double entry, double sl, double tp, long ticket);
        Task SendTradeClosedAsync(string symbol, double profit, long ticket);
        Task SendRiskBlockedAsync(string symbol, string reason);
        Task SendTestMessageAsync();
        Task SendAsync(string message);
        Task NotifyTradeOpenedAsync(TradeResult result, TradeRequest req);
        Task NotifyRiskBlockedAsync(TradeRequest req, string reason);
    }

    internal sealed class TelegramService : ITelegramService
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

        private readonly string _token;
        private readonly string _chatId;
        private readonly ApiIntegrationConfig _cfg;

        public TelegramService(ApiIntegrationConfig cfg)
        {
            _cfg    = cfg ?? new ApiIntegrationConfig();
            _token  = _cfg.TelegramBotToken ?? "";
            _chatId = _cfg.TelegramChatId   ?? "";
        }

        // ── public interface ────────────────────────────────────────────

        public Task SendSignalAsync(string symbol, string direction, string reason)
        {
            if (!_cfg.NotifySignals) return Task.CompletedTask;
            string emoji = direction == "BUY" ? "📈" : direction == "SELL" ? "📉" : "⏸";
            return SendFireAndForget(
                $"{emoji} *Signal* | {Esc(symbol)} {Esc(direction)}\n_{Esc(reason)}_");
        }

        public Task SendApprovalNeededAsync(string symbol, string direction, double lotSize)
        {
            if (!_cfg.NotifyApprovalNeeded) return Task.CompletedTask;
            return SendFireAndForget(
                $"⏳ *Approval Needed*\n{Esc(symbol)} {Esc(direction)} {lotSize:F2} lots — waiting for manual approval");
        }

        public Task SendTradeOpenedAsync(string symbol, string direction, double lotSize,
                                         double entry, double sl, double tp, long ticket)
        {
            if (!_cfg.NotifyTradeOpened) return Task.CompletedTask;
            return SendFireAndForget(
                $"✅ *Trade Opened* #{ticket}\n" +
                $"{Esc(symbol)} {Esc(direction)} {lotSize:F2} lots\n" +
                $"Entry: `{entry:F5}` | SL: `{sl:F5}` | TP: `{tp:F5}`");
        }

        public Task SendTradeClosedAsync(string symbol, double profit, long ticket)
        {
            if (!_cfg.NotifyTradeClosed) return Task.CompletedTask;
            string sign = profit >= 0 ? "+" : "";
            return SendFireAndForget(
                $"🔒 *Trade Closed* #{ticket}\n{Esc(symbol)} | P&L: `{sign}{profit:F2}` USD");
        }

        public Task SendRiskBlockedAsync(string symbol, string reason)
        {
            if (!_cfg.NotifyRiskBlocked) return Task.CompletedTask;
            return SendFireAndForget(
                $"🚫 *Risk Blocked*\n{Esc(symbol)}: {Esc(reason)}");
        }

        public Task SendTestMessageAsync() =>
            SendAsync("✅ *Telegram test OK* — MT5 Bot notifications are active.");

        // ── internals ───────────────────────────────────────────────────

        // Fire-and-forget: schedules the send but does not await it in the caller.
        // Any exception is caught inside SendAsync and only logged.
        public Task NotifyTradeOpenedAsync(TradeResult result, TradeRequest req) =>
            SendTradeOpenedAsync(
                req.Pair,
                req.TradeType.ToString(),
                req.LotSize,
                result.ExecutedPrice,
                req.StopLoss,
                req.TakeProfit,
                result.Ticket);

        public Task NotifyRiskBlockedAsync(TradeRequest req, string reason) =>
            SendRiskBlockedAsync(req.Pair, reason);

        private Task SendFireAndForget(string markdownText)
        {
            _ = Task.Run(() => SendAsync(markdownText));
            return Task.CompletedTask;
        }

        public async Task SendAsync(string markdownText)
        {
            if (string.IsNullOrWhiteSpace(_token) || string.IsNullOrWhiteSpace(_chatId))
                return;

            try
            {
                string url  = $"https://api.telegram.org/bot{_token}/sendMessage";
                var payload = new
                {
                    chat_id    = _chatId,
                    text       = markdownText,
                    parse_mode = "Markdown"
                };
                string json = JsonConvert.SerializeObject(payload);
                using var content  = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync(url, content).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Log.Warning("[Telegram] Send failed {Status}: {Body}",
                                (int)response.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Telegram] Send exception — notification dropped");
            }
        }

        // Escape Markdown special characters that would break Telegram formatting.
        private static string Esc(string? s) =>
            (s ?? "").Replace("_", "\\_").Replace("*", "\\*").Replace("`", "\\`");
    }

    // Null-object: used when Telegram is not configured so callers need no null checks.
    internal sealed class NullTelegramService : ITelegramService
    {
        public static readonly NullTelegramService Instance = new();
        public Task SendSignalAsync(string s, string d, string r)            => Task.CompletedTask;
        public Task SendApprovalNeededAsync(string s, string d, double l)    => Task.CompletedTask;
        public Task SendTradeOpenedAsync(string s, string d, double l,
                                         double e, double sl, double tp, long t) => Task.CompletedTask;
        public Task SendTradeClosedAsync(string s, double p, long t)         => Task.CompletedTask;
        public Task SendRiskBlockedAsync(string s, string r)                 => Task.CompletedTask;
        public Task SendTestMessageAsync()                                   => Task.CompletedTask;
        public Task SendAsync(string message)                                => Task.CompletedTask;
        public Task NotifyTradeOpenedAsync(TradeResult result, TradeRequest req) => Task.CompletedTask;
        public Task NotifyRiskBlockedAsync(TradeRequest req, string reason)      => Task.CompletedTask;
    }
}
