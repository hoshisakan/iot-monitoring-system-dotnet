/* 儲存成本監控視圖 */
CREATE OR REPLACE VIEW prod.v_storage_total AS
SELECT 
    pg_size_pretty(pg_total_relation_size('prod.telemetry_records')) AS total_size;
