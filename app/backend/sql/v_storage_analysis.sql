/* 索引與數據分析視圖 */
CREATE OR REPLACE VIEW prod.v_storage_analysis AS
SELECT 
    pg_size_pretty(pg_relation_size('prod.telemetry_records')) AS data_size,
    pg_size_pretty(pg_total_relation_size('prod.telemetry_records') - pg_relation_size('prod.telemetry_records')) AS index_size;
