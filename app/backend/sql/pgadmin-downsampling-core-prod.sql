-- Core downsampling test script (prod schema)
-- Purpose: minimal, editable, pgAdmin-friendly script

DO $$
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS _p(
    _device_id text,
    _from_utc timestamptz,
    _to_utc timestamptz,
    _target_points int
  ) ON COMMIT PRESERVE ROWS;

  DELETE FROM _p;
  INSERT INTO _p VALUES (
    'pico2_wh_1',
    '2026-04-01 00:00:00+00',
    '2026-04-20 00:00:00+00',
    36
  );
END $$;

-- EXPLAIN ANALYZE
SELECT
  date_bin(
    ((p._to_utc - p._from_utc) / p._target_points::double precision),
    t.device_time::timestamptz,
    p._from_utc
  ) AS bucket_time,
  avg(t.temperature_c) AS temperature_c,
  avg(t.humidity_pct) AS humidity_pct,
  avg(t.co2_ppm) AS co2_ppm,
  bool_or(t.pir_active) AS pir_active
FROM prod.telemetry_records t
CROSS JOIN _p p
WHERE t.device_id = p._device_id
  AND t.device_time >= p._from_utc
  AND t.device_time <= p._to_utc
GROUP BY 1
ORDER BY 1;
