using Microsoft.Data.Sqlite;
using MT5TradingBot.Models;
using Serilog;

namespace MT5TradingBot.Data
{
    public sealed class SqliteTradeRepository : ITradeRepository, IDisposable
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _initialized;

        public SqliteTradeRepository(string dbPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            _connectionString = $"Data Source={dbPath};Cache=Shared";
        }

        // -- ITradeRepository -------------------------------------

        public async Task InsertAsync(
            TradeRequest req,
            TradeResult result,
            CancellationToken ct = default)
        {
            try
            {
                await _lock.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await EnsureInitializedAsync(ct).ConfigureAwait(false);
                    using var conn = new SqliteConnection(_connectionString);
                    await conn.OpenAsync(ct).ConfigureAwait(false);

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT OR IGNORE INTO trades
                            (request_id, created_at, executed_at, pair, direction,
                             order_type, lot_size, entry_price, stop_loss, take_profit,
                             comment, magic_number, ticket, status,
                             executed_price, executed_lots, error_code, error_message)
                        VALUES
                            ($rid, $cat, $eat, $pair, $dir,
                             $ot, $ls, $ep, $sl, $tp,
                             $cmt, $mn, $tkt, $st,
                             $xp, $xl, $ec, $em)";

                    cmd.Parameters.AddWithValue("$rid", req.Id);
                    cmd.Parameters.AddWithValue("$cat", req.CreatedAt.ToString("o"));
                    cmd.Parameters.AddWithValue("$eat", result.ExecutedAt.ToString("o"));
                    cmd.Parameters.AddWithValue("$pair", req.Pair);
                    cmd.Parameters.AddWithValue("$dir",  req.TradeType.ToString());
                    cmd.Parameters.AddWithValue("$ot",   req.OrderType.ToString());
                    cmd.Parameters.AddWithValue("$ls",   req.LotSize);
                    cmd.Parameters.AddWithValue("$ep",   req.EntryPrice);
                    cmd.Parameters.AddWithValue("$sl",   req.StopLoss);
                    cmd.Parameters.AddWithValue("$tp",   req.TakeProfit);
                    cmd.Parameters.AddWithValue("$cmt",  req.Comment ?? "");
                    cmd.Parameters.AddWithValue("$mn",   req.MagicNumber);
                    cmd.Parameters.AddWithValue("$tkt",  result.Ticket);
                    cmd.Parameters.AddWithValue("$st",   result.Status.ToString());
                    cmd.Parameters.AddWithValue("$xp",   result.ExecutedPrice);
                    cmd.Parameters.AddWithValue("$xl",   result.ExecutedLots);
                    cmd.Parameters.AddWithValue("$ec",   result.ErrorCode ?? "");
                    cmd.Parameters.AddWithValue("$em",   result.ErrorMessage ?? "");

                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                finally { _lock.Release(); }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[TradeDB] Insert failed for {RequestId}", req.Id);
            }
        }

        public async Task<IReadOnlyList<TradeRecord>> GetRecentAsync(
            int count = 200,
            CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT * FROM trades
                ORDER BY executed_at DESC
                LIMIT $n";
            cmd.Parameters.AddWithValue("$n", count);

            return await ReadRowsAsync(cmd, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<TradeRecord>> GetByDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT * FROM trades
                WHERE executed_at BETWEEN $from AND $to
                ORDER BY executed_at DESC";
            cmd.Parameters.AddWithValue("$from", fromUtc.ToString("o"));
            cmd.Parameters.AddWithValue("$to",   toUtc.ToString("o"));

            return await ReadRowsAsync(cmd, ct).ConfigureAwait(false);
        }

        public async Task UpdateCloseAsync(
            long ticket,
            double profitUsd,
            DateTime closedAtUtc,
            CancellationToken ct = default)
        {
            try
            {
                await _lock.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await EnsureInitializedAsync(ct).ConfigureAwait(false);
                    using var conn = new SqliteConnection(_connectionString);
                    await conn.OpenAsync(ct).ConfigureAwait(false);

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE trades
                        SET profit_usd = $p, closed_at = $t
                        WHERE ticket   = $ticket AND closed_at IS NULL";
                    cmd.Parameters.AddWithValue("$p",      profitUsd);
                    cmd.Parameters.AddWithValue("$t",      closedAtUtc.ToString("o"));
                    cmd.Parameters.AddWithValue("$ticket", ticket);

                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                finally { _lock.Release(); }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[TradeDB] UpdateClose failed for ticket {Ticket}", ticket);
            }
        }

        public async Task<IReadOnlyList<TradeRecord>> GetRecentClosedAsync(
            int count = 50,
            CancellationToken ct = default)
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT * FROM trades
                WHERE  closed_at IS NOT NULL
                ORDER  BY closed_at DESC
                LIMIT  $n";
            cmd.Parameters.AddWithValue("$n", count);

            return await ReadRowsAsync(cmd, ct).ConfigureAwait(false);
        }

        public void Dispose() => _lock.Dispose();

        // -- internals --------------------------------------------

        private async Task EnsureInitializedAsync(CancellationToken ct)
        {
            if (_initialized) return;

            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;

                CREATE TABLE IF NOT EXISTS trades (
                    request_id     TEXT    PRIMARY KEY,
                    created_at     TEXT    NOT NULL,
                    executed_at    TEXT    NOT NULL,
                    pair           TEXT    NOT NULL,
                    direction      TEXT    NOT NULL,
                    order_type     TEXT    NOT NULL,
                    lot_size       REAL    NOT NULL,
                    entry_price    REAL    NOT NULL,
                    stop_loss      REAL    NOT NULL,
                    take_profit    REAL    NOT NULL,
                    comment        TEXT    NOT NULL DEFAULT '',
                    magic_number   INTEGER NOT NULL DEFAULT 0,
                    ticket         INTEGER NOT NULL DEFAULT 0,
                    status         TEXT    NOT NULL,
                    executed_price REAL    NOT NULL DEFAULT 0,
                    executed_lots  REAL    NOT NULL DEFAULT 0,
                    error_code     TEXT    NOT NULL DEFAULT '',
                    error_message  TEXT    NOT NULL DEFAULT '',
                    profit_usd     REAL    NOT NULL DEFAULT 0,
                    closed_at      TEXT    DEFAULT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_trades_executed_at
                    ON trades(executed_at DESC);
                CREATE INDEX IF NOT EXISTS idx_trades_pair
                    ON trades(pair);";

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            try { await RunAsync("ALTER TABLE trades ADD COLUMN profit_usd REAL DEFAULT 0", ct).ConfigureAwait(false); }
            catch { /* column already exists */ }
            try { await RunAsync("ALTER TABLE trades ADD COLUMN closed_at TEXT DEFAULT NULL", ct).ConfigureAwait(false); }
            catch { /* column already exists */ }
            await RunAsync(
                "CREATE INDEX IF NOT EXISTS idx_trades_closed_at ON trades(closed_at DESC)",
                ct).ConfigureAwait(false);
            _initialized = true;
        }

        private async Task RunAsync(string sql, CancellationToken ct = default)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        private static async Task<IReadOnlyList<TradeRecord>> ReadRowsAsync(
            SqliteCommand cmd,
            CancellationToken ct)
        {
            var list = new List<TradeRecord>();
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(new TradeRecord
                {
                    RequestId     = reader.GetString(reader.GetOrdinal("request_id")),
                    CreatedAt     = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
                    ExecutedAt    = DateTime.Parse(reader.GetString(reader.GetOrdinal("executed_at"))),
                    Pair          = reader.GetString(reader.GetOrdinal("pair")),
                    Direction     = reader.GetString(reader.GetOrdinal("direction")),
                    OrderType     = reader.GetString(reader.GetOrdinal("order_type")),
                    LotSize       = reader.GetDouble(reader.GetOrdinal("lot_size")),
                    EntryPrice    = reader.GetDouble(reader.GetOrdinal("entry_price")),
                    StopLoss      = reader.GetDouble(reader.GetOrdinal("stop_loss")),
                    TakeProfit    = reader.GetDouble(reader.GetOrdinal("take_profit")),
                    Comment       = reader.GetString(reader.GetOrdinal("comment")),
                    MagicNumber   = reader.GetInt32(reader.GetOrdinal("magic_number")),
                    Ticket        = reader.GetInt64(reader.GetOrdinal("ticket")),
                    Status        = reader.GetString(reader.GetOrdinal("status")),
                    ExecutedPrice = reader.GetDouble(reader.GetOrdinal("executed_price")),
                    ExecutedLots  = reader.GetDouble(reader.GetOrdinal("executed_lots")),
                    ErrorCode     = reader.GetString(reader.GetOrdinal("error_code")),
                    ErrorMessage  = reader.GetString(reader.GetOrdinal("error_message")),
                    ProfitUsd     = reader.IsDBNull(reader.GetOrdinal("profit_usd"))
                        ? 0.0
                        : reader.GetDouble(reader.GetOrdinal("profit_usd")),
                    ClosedAt      = reader.IsDBNull(reader.GetOrdinal("closed_at"))
                        ? null
                        : DateTime.Parse(reader.GetString(reader.GetOrdinal("closed_at"))),
                });
            }

            return list;
        }
    }
}
