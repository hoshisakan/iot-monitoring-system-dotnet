/* 即時數據流監控視圖 */
CREATE OR REPLACE VIEW prod.v_ingest_monitor AS
SELECT 
    COUNT(*) AS total_count,
    MAX(device_time) AS last_received,
    NOW() - MAX(device_time) AS data_lag
FROM prod.telemetry_records;
