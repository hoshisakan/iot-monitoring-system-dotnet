# IoT 監控系統（Production-Validated IIoT Platform）

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![MQTT](https://img.shields.io/badge/MQTT-Mosquitto%20%2F%20TLS-660066?logo=eclipsemosquitto&logoColor=white)](https://mqtt.org/)
[![React](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=black)](https://react.dev/)

本專案是一套從 **Edge Firmware** 到 **Backend API** 再到 **Frontend Dashboard** 的工業物聯網（IIoT）監控系統。  
目標不是示範型專案，而是可在高頻遙測場景中驗證可用性的 Production-oriented 實作。

---

## 專案亮點（Executive Summary）

- 已在真實資料條件下驗證 **116,230 筆**高頻時序資料。
- 單次查詢 11 萬筆以上原始資料，API 回應約 **512 ms**。
- 透過 **Dapper + PostgreSQL `date_bin`** 進行 downsampling，壓縮率達 **3,228x**（116,230 -> 36）。
- 端到端資料鏈路具備：
  - **MQTT QoS 1**
  - 韌體 **Offline EEPROM Buffer**
  - 連線恢復後的 **throttled sync-back replay**
  - 後端 idempotent ingest（unique constraint + duplicate handling）

---

## 系統架構（Architecture）

### Clean Architecture（四層解耦）

- **Domain**：Entities / Value Objects / Repository Interfaces
- **Application**：Use Cases / CQRS Handlers / Service Contracts
- **Infrastructure**：EF Core / Dapper / MQTT Ingest / Identity / External Adapters
- **API**：HTTP Boundary / Auth / Serialization / Middleware Pipeline

### CQRS（MediatR）

- Command / Query 分離，讀寫路徑可獨立優化。
- 高負載讀取（Telemetry Series / Logs / UI Events）使用可控 SQL 路徑。

### Docker Compose 一鍵部署

```bash
docker compose up -d
```

包含 Nginx、Backend API、Mosquitto、PostgreSQL、PgAdmin。

---

## 端到端資料流（End-to-End Data Pipeline）

```mermaid
flowchart LR
  Edge[Pico 2 W Firmware] -->|MQTT QoS 1| Broker[Mosquitto]
  Broker -->|Subscribe| Ingest[.NET MQTT Ingest Hosted Service]
  Ingest -->|EF Core writes| DB[(PostgreSQL 18)]
  API[ASP.NET Core API] -->|Dapper/EF queries| DB
  FE[React Dashboard] -->|REST /api/v1| API
```

### MQTT Topic 命名（與程式實作一致）

定義於 `app/firmware/include/mqtt_topics.h`：

- `iiot/<site>/<device>/telemetry`
- `iiot/<site>/<device>/status`
- `iiot/<site>/<device>/telemetry/sync-back`
- `iiot/<site>/<device>/ui-events`
- `iiot/<site>/<device>/control`

後端預設訂閱：

- `iiot/+/+/telemetry/#`
- `iiot/+/+/ui-events`
- `iiot/+/+/status`

---

## Downsampling Engine（Dapper + PostgreSQL `date_bin`）

核心實作：`app/backend/src/Pico2WH.Pi5.IIoT.Infrastructure/Queries/TelemetrySeriesDapperQuery.cs`

### 為何讀取路徑選擇 Dapper

在大時間窗時序查詢中，手寫 SQL 可明確控制 execution plan、aggregation semantics 與 payload size，降低 ORM 通用映射開銷。

### Compute Pushdown 設計

- `targetPoints = clamp(max_points, 10..5000)`
- 先 `COUNT(*)` 取得 `source_points`
- `source_points > target_points` 時啟用 `date_bin` 分桶
- 數值欄位用 `avg(...)`
- 布林欄位（`pir_active`）用 `bool_or(...)`
- 回傳 metadata：`downsampled`, `source_points`, `returned_points`, `bucket_width_ms`

這讓運算在 PostgreSQL 端完成，避免 API 先載入 O(N) 原始資料再做記憶體聚合。

---

## 📊 Performance Benchmark

- Dataset：**116,230** telemetry points（1Hz 高頻採樣條件）
- Query scope：單次查詢 11 萬筆以上原始資料
- API latency：**~512 ms**
- Downsample result：**36 trend points**
- Compression ratio：**3,228:1**

![Performance Benchmark](./docs/images/postman/performance_benchmark.png)

---

## IoT Data Resilience（資料可靠性）

韌體側（`app/firmware`）：

- 使用 **MQTT QoS 1**（at-least-once delivery）
- 發送失敗時寫入 **EEPROM queue**
- 連線恢復後以節流方式 replay 至 `telemetry/sync-back`

後端側（`app/backend`）：

- topic parsing + route dispatch
- telemetry unique constraint：`(device_id, device_time, is_sync_back)`
- duplicate key (`23505`) handling，確保 replay 不重複汙染資料

---

## Database Schema 與索引策略

核心資料表：`prod.telemetry_records`

關鍵欄位：

- `device_id`, `site_id`
- `device_time`, `server_time`
- `is_sync_back`
- `temperature_c`, `humidity_pct`, `co2_ppm`, `pir_active`, `rssi` 等時序欄位

索引理念：

- 寫入冪等：`(device_id, device_time, is_sync_back)`（unique）
- 讀取加速：`(device_id, device_time)`（時間窗查詢核心索引）

---

## 🔍 資料庫維運與可觀測性 (Database Observability)

以下內容對應 `app/backend/sql/` 的維運 SQL 視圖，重點是以資料庫視角觀察 **Data Ingestion Lag**、**Storage Efficiency**、**SLA Completeness** 與 **Capacity Forecasting**。  
目前基準數據約為 **18.4 萬筆**遙測資料、總體資料量約 **164MB**、入庫延遲實測約 **12s**。

> 注意：`pgadmin-downsampling-core-prod.sql` 屬於核心查詢引擎（Downsampling/Read Path），不屬於本節「維運視圖（Observability Views）」範圍。

### 維運圖表（Operations Charts）

`db-performance-hourly-report.png`：用於觀察系統由 **Stress Test (1Hz)** 逐步回到 **Production (30s)** 的歷程，並比對每小時 `row_count`、`avg_network_ingest_lag`、`pir_noise_ratio`。  
![DB Performance Hourly Report](docs/images/admin/db/db-performance-hourly-report.png)

`db-storage-projection.png`：用於觀察 **18 萬筆級別**數據下的容量投影與儲存效率，協助評估未來 30 天的 **Storage Projection** 與 Index 成本。  
![DB Storage Projection](docs/images/admin/db/db-storage-projection.png)

### 維運 SQL 視圖（Observability Views）

<details>
<summary><strong>高階分析與預測：v_02_advanced_analytics_and_forecasting.sql</strong></summary>

此檔整合三個維運面向：**Anomaly Detection**、**Capacity Forecasting**、**Connectivity Gap Analysis**。  
可同時回答「數據品質是否異常」、「30 天容量是否足夠」、「裝置是否有長時間靜默」三類問題。

```sql
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
```

</details>

<details>
<summary><strong>系統效能監控：v_system_performance_hourly.sql</strong></summary>

此視圖提供每小時 **System Performance** 指標，包含 `row_count`、`avg_network_ingest_lag`、`pir_noise_ratio`，並自動判定 `Stress Test (1Hz)` / `Transition / Mixed` / `Production (30s)` 模式。  
對於排查壓測切換期、觀察 12s 級別延遲是否持續、判斷 PIR 噪訊比是否異常特別有價值。

```sql
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
```

</details>

<details>
<summary><strong>數據完整性驗證：v_daily_completeness.sql</strong></summary>

此視圖按日彙總 `actual_count` 與 `vs_standard_30s_pct`，並用平均上報間隔判斷 `operation_mode`。  
在 **18.4 萬筆**資料規模下，可快速驗證每日 SLA 達成率，辨識掉點日期與異常裝置。

```sql
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
```

</details>

<details>
<summary><strong>入庫健康度監控：v_ingest_health_monitor.sql</strong></summary>

此視圖是 Data Pipeline 的即時健康探針，用 `MAX(device_time)` 與 `NOW()` 差值表示 `data_lag`。  
目前可追蹤約 **12s** 延遲，適合作為 Broker / Consumer / DB 鏈路異常的第一層告警條件。

```sql
CREATE OR REPLACE VIEW prod.v_monitor_status AS
SELECT 
    COUNT(*) as total_count,
    MAX(device_time) as last_data_received,
    NOW() - MAX(device_time) as data_lag -- 顯示延遲多久
FROM prod.telemetry_records;
```

</details>

<details>
<summary><strong>儲存效率分析：v_storage_analysis.sql</strong></summary>

此視圖拆分 `data_size` 與 `index_size`，可計算 Index/Data 比例與評估 Index 膨脹。  
在目前約 **164MB** 規模下，Index 佔比可維持約 **12%**，可作為調整索引策略與儲存成本控制依據。

```sql
/* 索引與數據分析視圖 */
CREATE OR REPLACE VIEW prod.v_storage_analysis AS
SELECT 
    pg_size_pretty(pg_relation_size('prod.telemetry_records')) AS data_size,
    pg_size_pretty(pg_total_relation_size('prod.telemetry_records') - pg_relation_size('prod.telemetry_records')) AS index_size;
```

</details>

<details>
<summary><strong>總體空間檢查：v_storage_total.sql</strong></summary>

此視圖提供單一欄位 `total_size`，用於快速盤點 `prod.telemetry_records` 的物理空間佔用。  
適合搭配 `v_storage_analysis` 與容量預估視圖一起使用，快速比對目前 **164MB** 是否持續成長。

```sql
/* 儲存成本監控視圖 */
CREATE OR REPLACE VIEW prod.v_storage_total AS
SELECT 
    pg_size_pretty(pg_total_relation_size('prod.telemetry_records')) AS total_size;
```

</details>

---

## 技術棧（Technology Stack）

> 目前程式碼 runtime 為 **.NET 8**（`*.csproj` 目標框架），並可平滑規劃至 .NET 9。

- **Backend**：ASP.NET Core, MediatR (CQRS), FluentValidation
- **Data Access**：Dapper（讀取優化）, EF Core（Schema/Write）
- **Database**：PostgreSQL 18, Npgsql
- **Messaging**：MQTTnet + Mosquitto (TLS-capable)
- **Frontend**：React 19, TypeScript, Vite
- **Firmware**：C, Raspberry Pi Pico SDK, CMake
- **Infra**：Docker Compose, Nginx

---

## 前端介面展示（docs/images/admin）

### Telemetry Dashboard

![遙測儀表（一）](docs/images/admin/telemetry/telemetry_01.png)

![遙測儀表（二）](docs/images/admin/telemetry/telemetry_02.png)

### System Status

![系統狀態（Docker 容器列表）](docs/images/admin/system-status/system_status_01.png)

### Device Logs

![裝置日誌列表](docs/images/admin/logs/logs_01.png)

---

## 快速開始（Quick Start）

### 1) 準備環境

```bash
git clone <repo-url>
cd iot-monitoring-system-dotnet
cp .env.example .env
```

### 2) 啟動完整服務

```bash
docker compose up -d
```

### 3) 後端本機啟動

```bash
cd app/backend/src/Pico2WH.Pi5.IIoT.Api
dotnet run
```

### 4) 前端本機啟動

```bash
cd app/frontend
npm ci
npm run dev
```

---

## 參考文件（Evidence & Specs）

- 規格文件：
  - `docs/specs/Pico2WH-Pi5-IIoT-專案開發規格書_v5.md`
  - `docs/specs/Pico2WH-Pi5-IIoT-專案開發規格書_v5_ASPNETCORE_4LAYER.md`
- SQL 實測腳本：
  - `app/backend/sql/pgadmin-read-queries-prod.sql`
  - `app/backend/sql/pgadmin-downsampling-core-prod.sql`
- API 測試集合：
  - `tests/postman/pico2wh-pi5-iiot-api.postman_collection.json`

---

## 技術結語

本專案的核心優勢在於三個關鍵詞：

- **運算下沉（Compute Pushdown）**：在 DB 端完成時序分桶與聚合。
- **高韌性資料鏈路（Resilient Data Pipeline）**：QoS + Buffer + Replay + Idempotency。
- **可維護架構（Maintainable Architecture）**：Clean Architecture + CQRS + 可觀測部署。

這使系統在高頻資料與長時間窗查詢下，仍能維持低延遲、可擴展與可驗證的工程品質。