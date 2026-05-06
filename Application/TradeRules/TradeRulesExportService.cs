using System.Text;
using MT5TradingBot.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MT5TradingBot.Modules.TradeRules
{
    public sealed class TradeRulesExportService
    {
        private static readonly string[] SecretMarkers =
        [
            "api_key", "apikey", "token", "password", "secret", "telegram_bot_token", "chat_id"
        ];

        public string ExportJson(
            TradeRulesRuntimeSnapshotResult snapshot,
            IReadOnlyList<string> history)
        {
            var payload = JObject.FromObject(new
            {
                context = snapshot.Context,
                account = snapshot.Account,
                symbol = snapshot.Symbol,
                position = snapshot.Position,
                summary = snapshot.Summary,
                rules = snapshot.Rules,
                history,
                captured_at_utc = snapshot.CapturedAtUtc
            });

            RedactSecrets(payload);
            return payload.ToString(Formatting.Indented);
        }

        public string ExportText(
            TradeRulesRuntimeSnapshotResult snapshot,
            IReadOnlyList<string> history)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Live Trade Rules Monitor & Control");
            sb.AppendLine($"Captured UTC: {snapshot.CapturedAtUtc:O}");
            sb.AppendLine();
            sb.AppendLine("Context");
            sb.AppendLine($"Pair: {snapshot.Context.Pair}");
            sb.AppendLine($"Strategy: {snapshot.Context.Strategy}");
            sb.AppendLine($"Ticket: {snapshot.Context.Ticket?.ToString() ?? "-"}");
            sb.AppendLine($"Trade Type: {snapshot.Context.TradeType?.ToString() ?? "-"}");
            sb.AppendLine($"RequestId: {snapshot.Context.RequestId ?? "-"}");
            sb.AppendLine($"Opened From: {snapshot.Context.OpenedFrom}");
            sb.AppendLine();

            sb.AppendLine("Account");
            if (snapshot.Account == null)
            {
                sb.AppendLine("Unavailable");
            }
            else
            {
                sb.AppendLine($"Account: {snapshot.Account.AccountNumber}");
                sb.AppendLine($"Server: {snapshot.Account.Server}");
                sb.AppendLine($"Broker/Name: {snapshot.Account.Name}");
                sb.AppendLine($"Balance: {snapshot.Account.Balance:F2}");
                sb.AppendLine($"Equity: {snapshot.Account.Equity:F2}");
                sb.AppendLine($"Free Margin: {snapshot.Account.FreeMargin:F2}");
                sb.AppendLine($"Margin Level: {snapshot.Account.MarginLevel:F2}");
                sb.AppendLine($"Floating P/L: {snapshot.Account.Profit:F2}");
            }
            sb.AppendLine();

            sb.AppendLine("Decision Summary");
            sb.AppendLine($"Current Decision: {snapshot.Summary.CurrentDecision}");
            sb.AppendLine($"Main Blocking Rule: {snapshot.Summary.MainBlockingRule}");
            sb.AppendLine($"Risk Level: {snapshot.Summary.RiskLevel}");
            sb.AppendLine($"Passed: {snapshot.Summary.Passed}");
            sb.AppendLine($"Warning: {snapshot.Summary.Warning}");
            sb.AppendLine($"Blocked: {snapshot.Summary.Blocked}");
            sb.AppendLine($"Disabled: {snapshot.Summary.Disabled}");
            sb.AppendLine($"Disabled But Would Block: {snapshot.Summary.DisabledButWouldBlock}");
            sb.AppendLine();

            sb.AppendLine("Rules");
            foreach (var rule in snapshot.Rules)
            {
                sb.AppendLine($"{rule.RuleCode} - {rule.RuleName}");
                sb.AppendLine($"  Category: {rule.Category}");
                sb.AppendLine($"  Group: {rule.GroupName}");
                sb.AppendLine($"  Enabled: {rule.IsEnabled}");
                sb.AppendLine($"  RuleCode: {rule.RuleCode}");
                sb.AppendLine($"  RuleName: {rule.RuleName}");
                sb.AppendLine($"  StandardValue: {FormatRedactedValue(rule.StandardValue)}");
                sb.AppendLine($"  ConfiguredValue: {FormatRedactedValue(rule.ConfiguredValue)}");
                sb.AppendLine($"  PreviewValue: {FormatRedactedValue(rule.PreviewValue)}");
                sb.AppendLine($"  LiveValue: {FormatRedactedValue(rule.LiveValue)}");
                sb.AppendLine($"  Status: {rule.Result}");
                sb.AppendLine($"  Would Have Result: {rule.WouldHaveResult ?? "-"}");
                sb.AppendLine($"  ActualEffect: {rule.ActualEffect}");
                sb.AppendLine($"  RuntimeMode: {rule.RuntimeMode}");
                sb.AppendLine($"  Reason: {rule.Reason}");
                sb.AppendLine($"  LastCheckedAtUtc: {rule.LastCheckedAtUtc:O}");
                sb.AppendLine($"  Source: {rule.SourceFile}");
            }
            sb.AppendLine();

            sb.AppendLine("Snapshot History");
            foreach (string row in history)
                sb.AppendLine(row);

            return RedactSecretText(sb.ToString());
        }

        public void WriteJson(string path, TradeRulesRuntimeSnapshotResult snapshot, IReadOnlyList<string> history) =>
            File.WriteAllText(path, ExportJson(snapshot, history));

        public void WriteText(string path, TradeRulesRuntimeSnapshotResult snapshot, IReadOnlyList<string> history) =>
            File.WriteAllText(path, ExportText(snapshot, history));

        private static void RedactSecrets(JToken token)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties().ToList())
                {
                    if (IsSecretKey(property.Name))
                        property.Value = "[REDACTED]";
                    else
                        RedactSecrets(property.Value);
                }
            }
            else if (token is JArray array)
            {
                foreach (var child in array)
                    RedactSecrets(child);
            }
        }

        private static bool IsSecretKey(string key) =>
            SecretMarkers.Any(marker => key.Contains(marker, StringComparison.OrdinalIgnoreCase));

        private static string FormatValue(object? value) =>
            value switch
            {
                null => "-",
                double d => d.ToString("0.#####"),
                float f => f.ToString("0.#####"),
                decimal d => d.ToString("0.#####"),
                IEnumerable<string> strings => string.Join(", ", strings),
                _ => value.ToString() ?? "-"
            };

        private static string FormatRedactedValue(object? value)
        {
            if (value == null)
                return "-";

            if (value is string text)
                return IsSecretValue(text) ? "[REDACTED]" : text;

            if (value.GetType().IsPrimitive || value is decimal)
                return FormatValue(value);

            try
            {
                var token = JToken.FromObject(value);
                RedactSecrets(token);
                return token.Type == JTokenType.String
                    ? token.ToString()
                    : token.ToString(Formatting.None);
            }
            catch
            {
                string formatted = FormatValue(value);
                return IsSecretValue(formatted) ? "[REDACTED]" : formatted;
            }
        }

        private static string RedactSecretText(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (SecretMarkers.Any(marker => lines[i].Contains(marker, StringComparison.OrdinalIgnoreCase)) ||
                    IsSecretValue(lines[i]))
                {
                    int colon = lines[i].IndexOf(':');
                    lines[i] = colon >= 0
                        ? lines[i][..(colon + 1)] + " [REDACTED]"
                        : "[REDACTED]";
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static bool IsSecretValue(string value) =>
            value.Contains("sk-", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("xoxb-", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("xoxp-", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Bearer ", StringComparison.OrdinalIgnoreCase);
    }
}
