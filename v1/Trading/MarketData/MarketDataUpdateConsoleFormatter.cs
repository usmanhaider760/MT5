namespace MT5TradingBot.Modules.MarketData
{
    public static class MarketDataUpdateConsoleFormatter
    {
        public static IReadOnlyList<string> Format(HistoricalMarketDataUpdateSummary summary)
        {
            var lines = new List<string> { "Market data update completed." };
            foreach (var result in summary.SymbolResults)
            {
                lines.Add($"{result.Symbol}: type={result.DataTypeUsed}, rows before={result.RowsBefore}, fetched={result.RowsFetched}, after={result.RowsAfter}, removed by retention={result.RowsRemovedByRetention}, fallback={(result.FallbackUsed ? "yes" : "no")}");
                lines.Add($"  range={result.FromUtc:O} to {result.ToUtc:O}");
                lines.Add($"  output={result.OutputFilePath}");
                if (!string.IsNullOrWhiteSpace(result.ProviderCommand))
                    lines.Add($"  mt5_command={result.ProviderCommand}");
                lines.Add($"  mt5_rows_returned={result.ProviderRowsReturned}");
                if (!string.IsNullOrWhiteSpace(result.ProviderDiagnostic))
                    lines.Add($"  diagnostic={result.ProviderDiagnostic}");
                foreach (string warning in result.Warnings)
                    lines.Add($"  warning={warning}");
                foreach (string error in result.Errors)
                {
                    lines.Add($"  error={error}");
                    lines.Add($"  failure reason: {error}");
                }
            }

            foreach (string warning in summary.Warnings.Except(summary.SymbolResults.SelectMany(r => r.Warnings)))
                lines.Add($"Warning: {warning}");
            foreach (string error in summary.Errors.Except(summary.SymbolResults.SelectMany(r => r.Errors)))
            {
                lines.Add($"Error: {error}");
                lines.Add($"failure reason: {error}");
            }

            return lines;
        }
    }
}
