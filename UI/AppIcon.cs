namespace MT5TradingBot.UI
{
    internal static class AppIcon
    {
        private static readonly string IconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

        public static void ApplyTo(Form form)
        {
            if (!File.Exists(IconPath)) return;

            try
            {
                form.Icon = new Icon(IconPath);
            }
            catch
            {
                // Icon loading is cosmetic; startup should continue if the asset is unavailable.
            }
        }
    }
}
