-- 進階版：計算平均間隔來判斷模式
CREATE OR REPLACE VIEW prod.v_daily_data_completeness AS
SELECT 
    device_id,
    date_trunc('day', device_time) AS report_date,
    COUNT(*) AS actual_count,
    CASE 
        WHEN (86400.0 / COUNT(*)) < 5 THEN 'Stress Test (1Hz)' 
        ELSE 'Production (30s)' 
    END AS operation_mode,
    ROUND((COUNT(*) / 2880.0) * 100, 2) AS vs_standard_30s_pct
FROM prod.telemetry_records
GROUP BY 1, 2
ORDER BY 2 DESC;
