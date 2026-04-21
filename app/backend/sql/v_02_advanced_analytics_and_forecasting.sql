-- ==============================================================================
-- 1. 環境數據異常偵測視圖 (Anomaly Detection)
-- 用途：自動標記數值噴發或感測器異常（如 CO2 暴增、溫度不合理變化）
-- ==============================================================================
CREATE OR REPLACE VIEW prod.v_data_quality_anomalies AS
SELECT 
    device_id,
    device_time,
    co2_ppm,
    temperature_c_scd41 AS temp_c,
    CASE 
        WHEN co2_ppm > 2500 THEN 'Critical: High CO2'
        WHEN temperature_c_scd41 > 45 OR temperature_c_scd41 < 10 THEN 'Critical: Temp Anomaly'
        WHEN humidity_pct_scd41 > 90 OR humidity_pct_scd41 < 10 THEN 'Warning: Humi Anomaly'
        ELSE 'Healthy'
    END AS data_health_status
FROM prod.telemetry_records
WHERE co2_ppm > 2500 OR temperature_c_scd41 > 45 OR temperature_c_scd41 < 10;

-- ==============================================================================
-- 2. 未來 30 天儲存增量預估 (Capacity Forecasting)
-- 用途：基於過去 24 小時的寫入速度，預測下個月的硬碟空間需求
-- ==============================================================================
CREATE OR REPLACE VIEW prod.v_storage_projection_next_30d AS
WITH daily_stats AS (
    SELECT 
        COUNT(*) AS daily_rows,
        pg_total_relation_size('prod.telemetry_records') AS current_bytes
    FROM prod.telemetry_records
    WHERE device_time > NOW() - INTERVAL '24 hours'
)
SELECT 
    pg_size_pretty(current_bytes) AS current_size,
    (daily_rows * 30) AS projected_new_rows_30d,
    pg_size_pretty(current_bytes + (current_bytes / NULLIF((SELECT COUNT(*) FROM prod.telemetry_records), 0) * daily_rows * 30)) AS projected_total_size_30d
FROM daily_stats;

-- ==============================================================================
-- 3. 設備通訊間隔異常分析 (Connectivity Gap Analysis)
-- 用途：找出通訊中斷超過 1 分鐘的事件，分析網路穩定性
-- ==============================================================================
CREATE OR REPLACE VIEW prod.v_network_gap_analysis AS
WITH time_diffs AS (
    SELECT 
        device_time,
        LEAD(device_time) OVER (ORDER BY device_time) AS next_time
    FROM prod.telemetry_records
)
SELECT 
    device_time AS gap_start,
    next_time AS gap_end,
    (next_time - device_time) AS silent_duration
FROM time_diffs
WHERE (next_time - device_time) > INTERVAL '1 minute'
ORDER BY silent_duration DESC;
