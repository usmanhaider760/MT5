using Dapper;
using Microsoft.Data.Sqlite;

namespace Atlas_Data_Access;

public class Database_Schema
{
    private readonly string _connection_string;

    public Database_Schema(string db_path = "atlas_trading.db")
    {
        _connection_string = $"Data Source={db_path}";
    }

    public string Connection_String => _connection_string;

    public void Ensure_Created()
    {
        using var conn = new SqliteConnection(_connection_string);
        conn.Open();

        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS trade_signals (
                signal_id       TEXT PRIMARY KEY,
                generated_at    TEXT NOT NULL,
                symbol_name     TEXT NOT NULL,
                strategy        TEXT NOT NULL,
                direction       TEXT NOT NULL,
                entry_price     REAL NOT NULL,
                stop_loss_price REAL NOT NULL,
                take_profit_price REAL NOT NULL,
                reward_risk_ratio REAL NOT NULL,
                quality_score   INTEGER NOT NULL,
                quality_grade   TEXT NOT NULL,
                regime          TEXT NOT NULL,
                session         TEXT NOT NULL,
                spread_at_signal REAL NOT NULL,
                is_approved     INTEGER NOT NULL,
                reject_reason   TEXT,
                reject_detail   TEXT
            );");

        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS trade_results (
                signal_id           TEXT PRIMARY KEY,
                broker_ticket       INTEGER NOT NULL,
                symbol_name         TEXT NOT NULL,
                strategy            TEXT NOT NULL,
                regime_at_entry     TEXT NOT NULL,
                session_at_entry    TEXT NOT NULL,
                direction           TEXT NOT NULL,
                lot_size            REAL NOT NULL,
                entry_price         REAL NOT NULL,
                exit_price          REAL NOT NULL,
                stop_loss_price     REAL NOT NULL,
                take_profit_price   REAL NOT NULL,
                opened_at           TEXT NOT NULL,
                closed_at           TEXT NOT NULL,
                gross_pnl           REAL NOT NULL,
                commission          REAL NOT NULL,
                swap                REAL NOT NULL,
                slippage_pips       REAL NOT NULL,
                net_pnl             REAL NOT NULL,
                r_multiple          REAL NOT NULL,
                is_winner           INTEGER NOT NULL,
                close_reason        TEXT
            );");

        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS strategy_performance (
                id                      INTEGER PRIMARY KEY AUTOINCREMENT,
                strategy                TEXT NOT NULL,
                symbol_name             TEXT NOT NULL,
                snapshot_at             TEXT NOT NULL,
                total_trades            INTEGER NOT NULL,
                winning_trades          INTEGER NOT NULL,
                losing_trades           INTEGER NOT NULL,
                gross_profit_r          REAL NOT NULL,
                gross_loss_r            REAL NOT NULL,
                profit_factor           REAL NOT NULL,
                average_r               REAL NOT NULL,
                max_drawdown_percent    REAL NOT NULL,
                rolling_20_expectancy   REAL NOT NULL,
                rolling_30_profit_factor REAL NOT NULL,
                is_active               INTEGER NOT NULL,
                is_live_enabled         INTEGER NOT NULL,
                disable_reason          TEXT
            );");

        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS news_events (
                event_id            TEXT PRIMARY KEY,
                event_utc           TEXT NOT NULL,
                currency            TEXT NOT NULL,
                event_name          TEXT NOT NULL,
                impact_level        TEXT NOT NULL,
                lockout_before_min  INTEGER NOT NULL,
                lockout_after_min   INTEGER NOT NULL
            );");

        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS account_snapshots (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                snapshot_utc    TEXT NOT NULL,
                balance         REAL NOT NULL,
                equity          REAL NOT NULL,
                margin_used     REAL NOT NULL,
                free_margin     REAL NOT NULL,
                daily_pnl       REAL NOT NULL,
                weekly_pnl      REAL NOT NULL,
                drawdown_pct    REAL NOT NULL,
                open_trades     INTEGER NOT NULL
            );");

        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS open_positions (
                broker_ticket              INTEGER PRIMARY KEY,
                signal_id                  TEXT NOT NULL,
                symbol_name                TEXT NOT NULL,
                strategy                   TEXT NOT NULL,
                direction                  TEXT NOT NULL,
                lot_size                   REAL NOT NULL,
                entry_price                REAL NOT NULL,
                stop_loss_price            REAL NOT NULL,
                take_profit_price          REAL NOT NULL,
                risk_amount_currency       REAL NOT NULL,
                risk_percent               REAL NOT NULL,
                initial_stop_distance_pips REAL NOT NULL,
                opened_at                  TEXT NOT NULL,
                is_at_breakeven            INTEGER NOT NULL DEFAULT 0,
                partial_profit_taken       INTEGER NOT NULL DEFAULT 0
            );");

        // Schema migrations for existing databases
        try { conn.Execute("ALTER TABLE trade_results ADD COLUMN notes TEXT DEFAULT '';"); } catch { /* column already exists */ }

        // Indexes for query performance
        conn.Execute("CREATE INDEX IF NOT EXISTS idx_trade_results_symbol ON trade_results(symbol_name);");
        conn.Execute("CREATE INDEX IF NOT EXISTS idx_trade_results_strategy ON trade_results(strategy);");
        conn.Execute("CREATE INDEX IF NOT EXISTS idx_trade_results_closed ON trade_results(closed_at);");
        conn.Execute("CREATE INDEX IF NOT EXISTS idx_signals_generated ON trade_signals(generated_at);");
        conn.Execute("CREATE INDEX IF NOT EXISTS idx_news_event_utc ON news_events(event_utc);");
    }
}
