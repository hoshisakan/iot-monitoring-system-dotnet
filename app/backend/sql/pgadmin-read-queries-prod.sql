-- ==========================================================
-- pgAdmin Query Tool script (PostgreSQL)
-- Source mapping:
--   - Infrastructure/Queries/TelemetrySeriesDapperQuery.cs
--   - Infrastructure/Queries/LogDapperQuery.cs
--   - Infrastructure/Queries/UiEventsDapperQuery.cs
--   - Infrastructure/Persistence/Repositories/TelemetryRepository.cs
-- Schema: prod
-- ==========================================================

-- ----------------------------------------------------------
-- [Parameters] editable test parameters (single-row temp table)
-- ----------------------------------------------------------
DO $$
BEGIN
    CREATE TEMP TABLE IF NOT EXISTS _query_params (
        _device_id      text,
        _site_id        text,
        _from_utc       timestamptz,
        _to_utc         timestamptz,
        _page           integer,
        _page_size      integer,
        _target_points  integer
    ) ON COMMIT PRESERVE ROWS;

    DELETE FROM _query_params;

    INSERT INTO _query_params (
        _device_id, _site_id, _from_utc, _to_utc, _page, _page_size, _target_points
    )
    VALUES (
        'pico2_wh_1',
        'default',
        '2026-04-01 00:00:00+00',
        '2026-04-20 00:00:00+00',
        1,
        50,
        28
    );
END $$;

-- 技術註解：
-- 此段提供可重複使用的測試參數，模擬後端 QueryAsync 接收到的 device/time/page/max_points。


-- ----------------------------------------------------------
-- [A1] Telemetry source count (SeriesTelemetryQuery metadata)
-- 對應功能：TelemetrySeriesDapperQuery.QueryAsync -> countSql
-- ----------------------------------------------------------
-- EXPLAIN ANALYZE
SELECT COUNT(*)::bigint AS source_points
FROM prod.telemetry_records t
CROSS JOIN _query_params p
WHERE t.device_id = p._device_id
  AND t.device_time >= p._from_utc
  AND t.device_time <= p._to_utc;

-- 技術註解：
-- 這是 downsampling 前的原始資料量（source_points），後端會用它判斷是否需要進入分桶聚合。


-- ----------------------------------------------------------
-- [A2] Telemetry raw series (no downsampling path)
-- 對應功能：TelemetrySeriesDapperQuery.LoadRawSeriesAsync
-- ----------------------------------------------------------
-- EXPLAIN ANALYZE
SELECT
    t.device_time                         AS "DeviceTimeUtc",
    t.temperature_c                       AS "TemperatureC",
    t.humidity_pct                        AS "HumidityPct",
    t.lux                                 AS "Lux",
    t.co2_ppm                             AS "Co2Ppm",
    t.temperature_c_scd41                 AS "TemperatureCScd41",
    t.humidity_pct_scd41                  AS "HumidityPctScd41",
    t.pir_active                          AS "PirActive",
    t.pressure_hpa                        AS "PressureHpa",
    t.gas_resistance_ohm                  AS "GasResistanceOhm",
    t.accel_x                             AS "AccelX",
    t.accel_y                             AS "AccelY",
    t.accel_z                             AS "AccelZ",
    t.gyro_x                              AS "GyroX",
    t.gyro_y                              AS "GyroY",
    t.gyro_z                              AS "GyroZ",
    t.rssi                                AS "Rssi"
FROM prod.telemetry_records t
CROSS JOIN _query_params p
WHERE t.device_id = p._device_id
  AND t.device_time >= p._from_utc
  AND t.device_time <= p._to_utc
ORDER BY t.device_time ASC;

-- 技術註解：
-- 當 source_points <= target_points 時，後端走 raw 路徑，直接輸出時序點，不做聚合。


-- ----------------------------------------------------------
-- [A3] Telemetry date_bin downsampling (bucketed path)
-- 對應功能：TelemetrySeriesDapperQuery.LoadBucketedSeriesAsync
-- ----------------------------------------------------------
-- EXPLAIN ANALYZE
SELECT
    date_bin(
        ((p._to_utc - p._from_utc) / p._target_points::double precision),
        t.device_time::timestamptz,
        p._from_utc
    )                                     AS "DeviceTimeUtc",
    avg(t.temperature_c)                  AS "TemperatureC",
    avg(t.humidity_pct)                   AS "HumidityPct",
    avg(t.lux)                            AS "Lux",
    avg(t.co2_ppm)                        AS "Co2Ppm",
    avg(t.temperature_c_scd41)            AS "TemperatureCScd41",
    avg(t.humidity_pct_scd41)             AS "HumidityPctScd41",
    bool_or(t.pir_active)                 AS "PirActive",     -- bool metric 使用 bool_or
    avg(t.pressure_hpa)                   AS "PressureHpa",
    avg(t.gas_resistance_ohm)             AS "GasResistanceOhm",
    avg(t.accel_x)                        AS "AccelX",
    avg(t.accel_y)                        AS "AccelY",
    avg(t.accel_z)                        AS "AccelZ",
    avg(t.gyro_x)                         AS "GyroX",
    avg(t.gyro_y)                         AS "GyroY",
    avg(t.gyro_z)                         AS "GyroZ",
    avg(t.rssi)                           AS "Rssi"
FROM prod.telemetry_records t
CROSS JOIN _query_params p
WHERE t.device_id = p._device_id
  AND t.device_time >= p._from_utc
  AND t.device_time <= p._to_utc
GROUP BY 1
ORDER BY 1 ASC;

-- 技術註解：
-- 這是核心降採樣邏輯：用 date_bin 將 9 萬筆壓縮到 target_points 附近，
-- 數值欄位取 avg，布林欄位 pir_active 取 bool_or，對應後端 metadata 的 downsampled=true。


-- ----------------------------------------------------------
-- [A4] Latest telemetry records (operational check)
-- 對應功能：TelemetryRepository.ListForDeviceAsync 的「最新資料」觀測用途
-- ----------------------------------------------------------
-- EXPLAIN ANALYZE
SELECT
    t.device_id,
    t.device_time,
    t.temperature_c,
    t.humidity_pct,
    t.lux,
    t.co2_ppm,
    t.pir_active,
    t.rssi,
    t.is_sync_back
FROM prod.telemetry_records t
CROSS JOIN _query_params p
WHERE t.device_id = p._device_id
ORDER BY t.device_time DESC
LIMIT 100;

-- 技術註解：
-- 用於快速驗證裝置是否持續上報、PIR 是否異常常駐、sync-back 是否啟用等。


-- ----------------------------------------------------------
-- [B1] Logs count query (paged logs metadata)
-- 對應功能：LogDapperQuery -> countSql
-- ----------------------------------------------------------
-- EXPLAIN ANALYZE
SELECT COUNT(1) AS total_count
FROM prod.app_logs l
CROSS JOIN _query_params p
WHERE 1 = 1
  AND l.device_id = p._device_id
  AND l.created_at_utc >= p._from_utc
  AND l.created_at_utc <= p._to_utc;

-- 技術註解：
-- 對應 logs API 分頁總筆數查詢，通常搭配 device/time/channel/level 條件。


-- ----------------------------------------------------------
-- [B2] Logs paged list query
-- 對應功能：LogDapperQuery -> listSql
-- ----------------------------------------------------------
-- EXPLAIN ANALYZE
SELECT
    l.id,
    l.device_id,
    l.channel,
    l.level,
    l.message,
    l.payload_json,
    l.source_ip,
    l.device_time_utc,
    l.created_at_utc
FROM prod.app_logs l
CROSS JOIN _query_params p
WHERE 1 = 1
  AND l.device_id = p._device_id
  AND l.created_at_utc >= p._from_utc
  AND l.created_at_utc <= p._to_utc
ORDER BY l.created_at_utc DESC
OFFSET ((p._page - 1) * p._page_size)
LIMIT p._page_size;

-- 技術註解：
-- 對應後端 logs 列表主查詢（倒序 + OFFSET/LIMIT）。


-- ----------------------------------------------------------
-- [C1] UI events count query
-- 對應功能：UiEventsDapperQuery -> countSql
-- ----------------------------------------------------------
-- EXPLAIN ANALYZE
SELECT COUNT(1) AS total_count
FROM prod.device_ui_events e
CROSS JOIN _query_params p
WHERE 1 = 1
  AND e.device_id = p._device_id
  AND e.site_id = p._site_id
  AND e.device_time_utc >= p._from_utc
  AND e.device_time_utc <= p._to_utc;

-- 技術註解：
-- 對應 ui-events API 分頁總數查詢。


-- ----------------------------------------------------------
-- [C2] UI events paged list query
-- 對應功能：UiEventsDapperQuery -> listSql
-- ----------------------------------------------------------
-- EXPLAIN ANALYZE
SELECT
    e.event_id,
    e.device_id,
    e.device_time_utc,
    e.event_type,
    e.event_value,
    e.channel,
    e.site_id
FROM prod.device_ui_events e
CROSS JOIN _query_params p
WHERE 1 = 1
  AND e.device_id = p._device_id
  AND e.site_id = p._site_id
  AND e.device_time_utc >= p._from_utc
  AND e.device_time_utc <= p._to_utc
ORDER BY e.device_time_utc DESC
OFFSET ((p._page - 1) * p._page_size)
LIMIT p._page_size;

-- 技術註解：
-- 對應後端 ui-events 列表主查詢（倒序 + OFFSET/LIMIT）。


-- ----------------------------------------------------------
-- [D1] DB-level system status (SQL view of system health)
-- 備註：API `/api/v1/system/status` 來自 Docker client，不是 SQL。
-- ----------------------------------------------------------
-- EXPLAIN ANALYZE
SELECT
    now() AT TIME ZONE 'UTC'             AS host_time_utc,
    current_database()                   AS database_name,
    current_schema()                     AS current_schema_name,
    version()                            AS postgres_version;

-- 技術註解：
-- 這段提供資料庫層面的 system status，補足 API Docker 狀態之外的 DB 基本健康資訊。


-- ----------------------------------------------------------
-- [D2] DB activity status (connections and commit stats)
-- ----------------------------------------------------------
-- EXPLAIN ANALYZE
SELECT
    datname,
    numbackends,
    xact_commit,
    xact_rollback,
    blks_read,
    blks_hit,
    tup_returned,
    tup_fetched,
    tup_inserted,
    tup_updated,
    tup_deleted
FROM pg_stat_database
WHERE datname = current_database();

-- 技術註解：
-- 用於觀察資料庫活動量與 cache 命中趨勢（blks_hit/blks_read）。
