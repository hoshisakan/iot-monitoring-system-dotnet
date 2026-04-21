CREATE OR REPLACE VIEW prod.v_system_performance_hourly AS
WITH hourly_stats AS (
    SELECT 
        date_trunc('hour', device_time) AS hour_bucket,
        COUNT(*) AS row_count,
        ROUND(AVG(EXTRACT(EPOCH FROM (server_time - device_time)))::numeric, 2) AS avg_lag_sec,
        COUNT(CASE WHEN pir_active = true THEN 1 END) AS pir_true_count
    FROM prod.telemetry_records
    GROUP BY 1
)
SELECT 
    hour_bucket,
    row_count,
    CASE 
        WHEN row_count > 3000 THEN 'Stress Test (1Hz)'
        WHEN row_count BETWEEN 150 AND 3000 THEN 'Transition / Mixed'
        ELSE 'Production (30s)'
    END AS data_density_mode,
    avg_lag_sec || ' s' AS avg_network_ingest_lag,
    pir_true_count AS pir_spikes,
    ROUND((pir_true_count::numeric / NULLIF(row_count, 0)) * 100, 3) || ' %' AS pir_noise_ratio
FROM hourly_stats
ORDER BY hour_bucket DESC;
