namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence;

/// <summary>資料庫模型設定（與連線字串分開，便於 dev / prod 切換 schema）。</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>EF Core 預設 schema（PostgreSQL 常見為 <c>public</c>；開發可設 <c>dev</c>）。</summary>
    public string DefaultSchema { get; set; } = "public";

    /// <summary>啟動時是否自動執行 <c>Database.Migrate()</c>（套用待處理遷移）。若改由 CI 專責遷移可設為 <c>false</c>。</summary>
    public bool AutoMigrate { get; set; } = true;

    /// <summary>是否使用 Dapper 實作遙測時序查詢（可切回 EF 以快速回退）。</summary>
    public bool UseDapperForTelemetrySeries { get; set; } = true;

    /// <summary>是否使用 Dapper 實作結構化日誌查詢（可切回 EF 以快速回退）。</summary>
    public bool UseDapperForLogsQuery { get; set; } = true;

    /// <summary>是否使用 Dapper 實作 UI 事件查詢（可切回 EF 以快速回退）。</summary>
    public bool UseDapperForUiEventsQuery { get; set; } = true;
}
