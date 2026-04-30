using MT5TradingBot.Models;

namespace MT5TradingBot.Modules.PairSettings
{
    public interface IPairSettingsService
    {
        IReadOnlyList<PairTradingSettings> GetAll();
        PairTradingSettings? GetForPair(string pair);
        void Upsert(PairTradingSettings settings);
        bool Delete(string pair);
        int ImportJson(string json);
    }
}
