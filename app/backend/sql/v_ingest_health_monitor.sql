CREATE OR REPLACE VIEW prod.v_monitor_status AS
SELECT 
    COUNT(*) as total_count,
    MAX(device_time) as last_data_received,
    NOW() - MAX(device_time) as data_lag -- 顯示延遲多久
FROM prod.telemetry_records;
