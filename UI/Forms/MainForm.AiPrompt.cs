namespace MT5TradingBot.UI
{
    public sealed partial class MainForm
    {
        private static string BuildFilledAiInputPrompt(
            string snapshotJson,
            double? lotSizeOverride = null,
            int? leverageOverride = null)
            => MT5TradingBot.Services.AiPrompts.BuildFilledAiInputPrompt(snapshotJson, lotSizeOverride, leverageOverride);
    }
}
