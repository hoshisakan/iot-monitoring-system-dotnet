namespace Pico2WH.Pi5.IIoT.Infrastructure.Mqtt;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public string Host { get; set; } = "127.0.0.1";

    /// <summary>預設 1883 為明文；8883 為 TLS（搭配 <see cref="UseTls"/>）。</summary>
    public int Port { get; set; } = 1883;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string ClientId { get; set; } = "pico2wh-pi5-api";

    public bool Enabled { get; set; } = true;

    /// <summary>是否對連線使用 TLS（MQTT over TLS，常見埠 8883）。</summary>
    public bool UseTls { get; set; }

    /// <summary>
    /// 相對於應用程式 ContentRoot（Api 專案目錄）的 CA 憑證路徑，用於驗證 Broker 伺服器憑證。
    /// 例：<c>../../../../conf/mqtt_broker/certs/local/ca.crt</c>
    /// </summary>
    public string? TlsCaCertificatePath { get; set; }

    /// <summary>選用：客戶端憑證（PEM），Broker 要求 mTLS 時使用。</summary>
    public string? TlsClientCertificatePath { get; set; }

    /// <summary>選用：客戶端私鑰（PEM），須與 <see cref="TlsClientCertificatePath"/> 併用。</summary>
    public string? TlsClientPrivateKeyPath { get; set; }

    /// <summary>開發用：接受自簽或未受信任憑證（略過嚴格驗證）。正式環境應為 false 並正確設定 CA。</summary>
    public bool TlsAllowUntrustedCertificate { get; set; }

    /// <summary>是否啟動背景訂閱並 ingest（<c>telemetry</c>／<c>ui-events</c>／<c>status</c>）。需 <see cref="Enabled"/> 同為 true。</summary>
    public bool IngestEnabled { get; set; } = true;

    /// <summary>
    /// 訂閱篩選（MQTT 萬用字元）。請勿在屬性初始式預先填入項目：設定檔陣列繫結至清單時會附加至既有項目，易與預設值重疊成重複訂閱。
    /// 未設定或空清單時改以 <see cref="EffectiveSubscribeTopicFilters"/> 套用 <see cref="DefaultSubscribeTopicFilters"/>。
    /// </summary>
    public List<string> SubscribeTopicFilters { get; set; } = new();

    /// <summary>未在設定檔指定任何篩選時使用的預設主題（遙測含 sync-back 子路徑、ui-events、status）。</summary>
    public static IReadOnlyList<string> DefaultSubscribeTopicFilters { get; } = new[]
    {
        "iiot/+/+/telemetry/#",
        "iiot/+/+/ui-events",
        "iiot/+/+/status"
    };

    /// <summary>實際要訂閱的清單：<see cref="SubscribeTopicFilters"/> 有項目時用之，否則用 <see cref="DefaultSubscribeTopicFilters"/>。</summary>
    public IReadOnlyList<string> EffectiveSubscribeTopicFilters =>
        SubscribeTopicFilters.Count > 0 ? SubscribeTopicFilters : DefaultSubscribeTopicFilters;
}
