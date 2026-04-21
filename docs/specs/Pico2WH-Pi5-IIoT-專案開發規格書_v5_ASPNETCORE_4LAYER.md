# 🚀 Pico 2 WH & Pi 5 IIoT 專案開發規格書 v5.1（ASP.NET Core 版 · **四層架構**）

> **2026-04-22 修正**：根據 Dapper 讀寫分離實作現狀同步化文件結構。

> 本文件**只**描述後端 **Clean Architecture 四層**：**第一層 領域（Domain）**、**第二層 應用（Application）**、**第三層 基礎設施（Infrastructure）**、**第四層 表現／介面（Api）**。  
> 實作上 **每一層一個組件專案**（Domain、Application、Infrastructure、Api）；**不**定義第五、第六架構層。  
> 第三層內可依技術分子資料夾（`Persistence/`、`Identity/`、`Mqtt/` 等），僅為**同一層內**的模組切分，**不**視為額外層級。  
> 端點、WSL／Pi5、PostgreSQL DDL、驗收準則與 v5 系列其他文件一致；以下為完整可實作敘述。

> **SoT 聲明（Backend）**：凡後端分層、依賴方向、套件、API 實作約束，以本文件為準。  
> **文件分工**：`Pico2WH-Pi5-IIoT-專案開發規格書_v5.md` 為全專案總規格（硬體/韌體/部署/階段）；本文件為後端實作細則。  
> **Legacy 聲明**：舊版 `Ulfius` 敘事僅作歷史參考，現行主線為 ASP.NET Core 四層架構。  
> **補充（2026-04-18）**：讀取查詢可選 **Dapper**（與 EF Core 並存、組態切換），見 **§2.4.1a**；總規格對應 **§6.0.5 A.0**。  
> **補充（2026-04-22）**：`ITelemetryRepository` 已轉型為**純讀取介面**；遙測寫入職責轉移至 `Application/Ingest` + `Persistence/Repositories/*IngestRepository`（EF Core）。

**依賴方向（由外而內）**：第四層 `Api` → 第二層 `Application`、第三層 `Infrastructure`；第二層 → 第一層 `Domain`；第三層 → 第二層、第一層。第一層**不**依賴 EF／HTTP／MQTT／Docker SDK。

**第三層命名空間建議**：`Pico2WH.Pi5.IIoT.Infrastructure.Persistence`、`…Identity`、`…Mqtt` 等，與資料夾一致。

---

## 1. 後端技術棧（ASP.NET Core · 四層架構）

- Runtime：`.NET 8`（LTS）
- Framework：`ASP.NET Core Web API`
- 架構：`Clean Architecture`（**四層**；對應專案：`Domain` / `Application` / `Infrastructure` / `Api`）
- 核心模式：`MediatR` 驅動 `CQRS`
- DB：`PostgreSQL 18`（Npgsql + EF Core；實作於**第三層** `Persistence/`）。**讀取查詢**（日誌／遙測時序／UI 事件）可選 **Dapper** 實作，與 EF 並存、組態切換；細節見 **§2.4.1a**。
- MQTT：`MQTTnet`（實作於**第三層** `Mqtt/`）
- 驗證授權：`JWT Bearer`（簽發／雜湊實作於**第三層** `Identity/`；middleware 於**第四層** `Api`）
- 文件：`Swagger / OpenAPI`
- Logging：`Serilog`（Console + File，符合既有 log 策略；啟動於**第四層** `Api`，檔案解析可於**第三層** `Logging/`）

---

## 2. 四層架構與目錄（Solution 完整說明）

> 專案前綴命名空間：`Pico2WH.Pi5.IIoT`。

> **後端根目錄**：`app/backend`。其中 `src/` 放置 Solution 與四層專案、`sql/` 放置維運 SQL、`scripts/` 放置後端腳本。

### 2.0 六角架構（Ports & Adapters）與四層對應

| 六角概念 | 對應層／專案 | 說明 |
|----------|----------------|------|
| **核心（Domain）** | **第一層** `Domain` | 實體、值物件、領域規則、儲存庫**埠**（介面） |
| **應用／用例（Application）** | **第二層** `Application` | CQRS、MediatR、DTO、驗證、**定義**對外埠（如 `IJwtService`） |
| **主要轉接器（Driving / Primary）** | **第四層** `Api` | HTTP、Controller、Middleware、Swagger、組裝 DI |
| **次要轉接器（Driven / Secondary）** | **第三層** `Infrastructure` | 單一專案內含 EF／Repository、JWT／Hasher、MQTT、Docker、檔案日誌等**適配** |

依賴方向維持 **由外而內**：第四層 → 第二層 → 第一層；第三層實作第二／第一層所定義的介面，**不**讓第一層依賴框架。

### 2.1 總覽樹狀圖（`app/backend/`）

```text
app/backend/
├── sql/
│   ├── v_02_advanced_analytics_and_forecasting.sql
│   ├── v_system_performance_hourly.sql
│   ├── v_daily_completeness.sql
│   ├── v_ingest_health_monitor.sql
│   ├── v_storage_analysis.sql
│   └── v_storage_total.sql
├── src/
│   ├── Pico2WH.Pi5.IIoT.FourLayer.sln
│   ├── Pico2WH.Pi5.IIoT.Api/
│   ├── Pico2WH.Pi5.IIoT.Application/
│   ├── Pico2WH.Pi5.IIoT.Domain/
│   ├── Pico2WH.Pi5.IIoT.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── Context/
│   │   │   ├── Configurations/
│   │   │   ├── Repositories/
│   │   │   └── Migrations/
│   │   ├── Queries/                        # LogDapperQuery / UiEventsDapperQuery / TelemetrySeriesDapperQuery
│   │   ├── Identity/
│   │   │   ├── Jwt/
│   │   │   └── Security/
│   │   ├── Mqtt/
│   │   ├── Docker/
│   │   └── Logging/
│   └── tests/
│       ├── Pico2WH.Pi5.IIoT.Domain.Tests/
│       ├── Pico2WH.Pi5.IIoT.Application.Tests/
│       ├── Pico2WH.Pi5.IIoT.Api.IntegrationTests/
│       └── Pico2WH.Pi5.IIoT.Api.ContractTests/
└── scripts/
    └── (後端相關腳本路徑，例：wsl/pi5)
```

以下各節列出**各目錄**內建議檔名與職責（實作時可依需求增刪，但分層與命名應維持一致）。**路徑**中 `Infrastructure/` 以下子資料夾相對於 `Pico2WH.Pi5.IIoT.Infrastructure` 專案根。

### 2.2 第一層：領域（Domain，`Pico2WH.Pi5.IIoT.Domain`）

| 路徑（相對於專案根） | 檔案名稱 | 作用 |
|----------------------|----------|------|
| `./` | `Pico2WH.Pi5.IIoT.Domain.csproj` | 類別庫專案檔；**不**參考 EF／HTTP／MQTT 等。 |
| `Common/` | `EntityBase.cs` | 共用主鍵、建立／修改時間等基底欄位。 |
| `Common/` | `DomainException.cs` | 領域層可預期錯誤例外。 |
| `Entities/` | `Device.cs` | 裝置聚合根或實體。 |
| `Entities/` | `TelemetryReading.cs` | 遙測資料實體（對齊 `co2_ppm`、`temperature_c_scd41` 等欄位語意）。 |
| `Entities/` | `User.cs` | 使用者帳號實體。 |
| `Entities/` | `RefreshToken.cs` | Refresh Token 儲存實體。 |
| `Entities/` | `DeviceControlAudit.cs` | 裝置控制審計紀錄。 |
| `ValueObjects/` | `DeviceId.cs` | 裝置識別值物件（避免裸 `string`/`Guid` 散落）。 |
| `Repositories/` | `ITelemetryRepository.cs` | 遙測查詢之儲存庫**介面**（讀取埠）。 |
| `Repositories/` | `IUserRepository.cs` | 使用者／憑證相關持久化介面。 |
| `Repositories/` | `ILogQueryRepository.cs` | 日誌／KV 查詢抽象（實作於第三層 `Persistence/` 或 `Logging/`，擇一）。 |
| `Repositories/` | `IRefreshTokenRepository.cs` | Refresh Token 儲存介面。 |

### 2.3 第二層：應用（Application，`Pico2WH.Pi5.IIoT.Application`）

| 路徑（相對於專案根） | 檔案名稱 | 作用 |
|----------------------|----------|------|
| `./` | `Pico2WH.Pi5.IIoT.Application.csproj` | 參考 `Domain`；含 MediatR、FluentValidation 等。 |
| `./` | `DependencyInjection.cs`（或 `ServiceCollectionExtensions.cs`） | 註冊 Application 服務、MediatR、Validators、Pipeline Behaviors。 |
| `Common/Interfaces/` | `IJwtService.cs` | JWT 簽發／驗證之**應用埠**（由第三層 `Identity/` 實作）。 |
| `Common/Interfaces/` | `IPasswordHasher.cs` | 密碼雜湊埠（由第三層 `Identity/` 實作）。 |
| `Common/Interfaces/` | `IMqttPublisher.cs` | MQTT 發布命令之埠（由第三層 `Mqtt/` 實作）。 |
| `Common/Interfaces/` | `IDockerSystemClient.cs` | 讀取容器狀態之埠（由第三層 `Docker/` 實作）。 |
| `Common/Models/` | `PagedResult.cs` | 分頁查詢共用模型。 |
| `Features/Auth/Commands/Login/` | `LoginCommand.cs` | MediatR Command：登入。 |
| `Features/Auth/Commands/Login/` | `LoginCommandHandler.cs` | 處理登入、產生 Token 呼叫埠。 |
| `Features/Auth/Commands/Login/` | `LoginCommandValidator.cs` | FluentValidation。 |
| `Features/Auth/Commands/RefreshToken/` | `RefreshTokenCommand.cs` / `...Handler.cs` / `...Validator.cs` | Refresh 流程。 |
| `Features/Auth/Commands/Logout/` | `LogoutCommand.cs` / `...Handler.cs` / `...Validator.cs` | 撤銷 Refresh。 |
| `Features/Telemetry/Queries/ListTelemetry/` | `ListTelemetryQuery.cs` / `...Handler.cs` / `...Validator.cs` | `GET /telemetry` 查詢。 |
| `Features/Telemetry/Queries/SeriesTelemetry/` | `SeriesTelemetryQuery.cs` / `...Handler.cs` / `...Validator.cs` | 時序與降採樣。 |
| `Features/Logs/Queries/ListLogs/` | `ListLogsQuery.cs` / `...Handler.cs` | 日誌列表。 |
| `Features/UiEvents/Queries/ListUiEvents/` | `ListUiEventsQuery.cs` / `...Handler.cs` | UI 事件。 |
| `Features/System/Queries/SystemStatus/` | `SystemStatusQuery.cs` / `...Handler.cs` | Docker 系統狀態。 |
| `Features/Device/Commands/DeviceControl/` | `DeviceControlCommand.cs` / `...Handler.cs` | PWM 控制與審計。 |
| `Behaviors/` | `ValidationBehavior.cs` | MediatR 管線：統一 FluentValidation。 |
| `Behaviors/` | `LoggingBehavior.cs` | 請求／回應追蹤。 |
| `Behaviors/` | `AuthorizationBehavior.cs` | 可選：角色前置檢查。 |
| `Mappings/` | `MappingProfile.cs` | AutoMapper 設定（若採用）。 |

### 2.4 第三層：基礎設施（Infrastructure，`Pico2WH.Pi5.IIoT.Infrastructure`）

> 本層為 **Driven（次要）轉接器**：資料庫、身分（JWT／密碼雜湊）、MQTT、Docker、檔案日誌等**所有對外技術**，**集中於此單一專案**；子資料夾僅為模組化，**不**構成額外架構層。  
> 專案檔：`Pico2WH.Pi5.IIoT.Infrastructure.csproj`，參考第二層、第一層；套件見 §3.3。  
> 建議以 `DependencyInjection.cs`（或 `AddPersistence()`、`AddIdentityServices()`、`AddExternalInfrastructure()` 等擴充）集中註冊，再由第四層 `Api/Program.cs` 一次呼叫。

#### 2.4.1 第三層內 · `Persistence/`（EF Core／儲存庫實作）

| 路徑（相對於 Infrastructure 專案根） | 檔案名稱 | 作用 |
|--------------------------------------|----------|------|
| `./` | （同上，與 Identity/Mqtt 共用一個 `.csproj`） | 含 EF Core、Npgsql 套件參考。 |
| `Persistence/` | `PersistenceServiceCollectionExtensions.cs`（或併入總 `DependencyInjection.cs`） | 註冊 `DbContext`、Repository 實作。 |
| `Persistence/Context/` | `ApplicationDbContext.cs` | `DbSet<>`、OnModelCreating。 |
| `Persistence/Configurations/` | `DeviceConfiguration.cs` | Fluent API 對應 `Device`。 |
| `Persistence/Configurations/` | `TelemetryReadingConfiguration.cs` | 遙測表索引、欄位型別。 |
| `Persistence/Configurations/` | `UserConfiguration.cs` | 使用者表設定。 |
| `Persistence/Configurations/` | `RefreshTokenConfiguration.cs` | Refresh Token 表。 |
| `Persistence/Repositories/` | `TelemetryRepository.cs` | `ITelemetryRepository` 實作。 |
| `Persistence/Repositories/` | `UserRepository.cs` | `IUserRepository` 實作。 |
| `Persistence/Repositories/` | `RefreshTokenRepository.cs` | `IRefreshTokenRepository` 實作。 |
| `Persistence/Migrations/` | `<Timestamp>_InitialCreate.cs` 等 | EF 遷移產生檔（實際檔名含時間戳）。 |

#### 2.4.1a 第三層內 · Dapper 讀取路徑（HTTP 讀取預設，讀寫分離）

> **範圍**：僅**讀取查詢**；**寫入**（含 MQTT ingest 落庫）、**EF Migration**、**DbContext** 仍以 **EF Core** 為主。  
> **套件**：`Dapper`（與 `Npgsql` 經 `IDbConnectionFactory` 建立連線）。  
> **組態**：`DatabaseOptions`（`appsettings` 節點 `Database`）以 **`DefaultSchema`**、**`AutoMigrate`** 為主；**無**「EF／Dapper 切換」旗標。

> **架構演進同步（2026-04-22）**：`ITelemetryRepository` 已轉型為純讀取介面；高效讀取路徑由 Dapper Query 類別承擔（如 `TelemetrySeriesDapperQuery`、`LogDapperQuery`、`UiEventsDapperQuery`）。  
> 遙測寫入責任轉移至第二層 `Application/Ingest` 用例，並由第三層 `Persistence/Repositories/*IngestRepository` 以 EF Core 實作落庫。

| 設定鍵 | 預設 | 說明 |
|--------|------|------|
| `DefaultSchema` | `public` | PostgreSQL schema；開發常為 `dev`。 |
| `AutoMigrate` | `true` | 啟動時是否執行 `Migrate()`。 |

- **結構化日誌**（`GET /api/v1/logs`）：`ILogQueryRepository` → **`LogDapperQuery`**。
- **UI 事件列表**（`GET /api/v1/ui-events`）：`IUiEventsQuery` → **`UiEventsDapperQuery`**。
- **遙測時序**（`GET /api/v1/telemetry/series`）：`ITelemetrySeriesQuery` → **`TelemetrySeriesDapperQuery`**（PostgreSQL `date_bin` 等聚合於 SQL 完成）。
- **DI 註冊**（`Infrastructure/DependencyInjection.cs`）：上述介面皆**直接**綁定 Dapper 實作，無執行期切換。
- **連線**：`IDbConnectionFactory` → `NpgsqlConnectionFactory`（與 `ConnectionStrings:Default` 同源），Dapper 查詢使用參數化 SQL；schema 取自 `Database:DefaultSchema`，實作會**拒絕**不安全之 schema 字元（與 EF 命名慣例一致時通常為 `public` 或 `dev`）。
- **介面歸屬**：第二層仍只依賴 `ILogQueryRepository`、`ITelemetrySeriesQuery`、`IUiEventsQuery` 等**抽象**；第三層對 HTTP 讀路徑提供 **Dapper／SQL** 實作。
- **寫入歸屬**：`ITelemetryIngestRepository` / `IUiEventIngestRepository` / `IStatusLogIngestRepository` 由 `TelemetryIngestRepository` / `UiEventIngestRepository` / `StatusLogIngestRepository` 以 EF Core 實作。

| 路徑（相對於 Infrastructure 專案根） | 檔案名稱 | 作用 |
|--------------------------------------|----------|------|
| `Persistence/` | `DatabaseOptions.cs` | `Database` 組態（`DefaultSchema`、`AutoMigrate` 等）。 |
| `Persistence/` | `NpgsqlConnectionFactory.cs`（或同等 `IDbConnectionFactory` 實作） | 提供開啟之 `NpgsqlConnection` 供 Dapper 使用。 |
| `Queries/` | `LogDapperQuery.cs` | `ILogQueryRepository` 之 Dapper 實作（結構化日誌列表）。 |
| `Queries/` | `TelemetrySeriesDapperQuery.cs` | `ITelemetrySeriesQuery` 之 Dapper 實作（遙測時序／降採樣讀取）。 |
| `Queries/` | `UiEventsDapperQuery.cs` | `IUiEventsQuery` 之 Dapper 實作。 |

**測試**：`tests/Pico2WH.Pi5.IIoT.Api.IntegrationTests` 內 `DapperReadQueryTests` 以 Testcontainers PostgreSQL 驗證 Dapper 讀路徑與參數化（正確性）；效能基準非本文件範圍。

#### 2.4.2 第三層內 · `Identity/`（驗證身分）

| 路徑（相對於 Infrastructure 專案根） | 檔案名稱 | 作用 |
|--------------------------------------|----------|------|
| `Identity/` | `IdentityServiceCollectionExtensions.cs`（或併入總 DI） | 註冊 JWT、PasswordHasher。 |
| `Identity/Jwt/` | `JwtOptions.cs` | 綁定 `Jwt__*` 設定。 |
| `Identity/Jwt/` | `JwtTokenService.cs` | `IJwtService` 實作（簽發 Access／Refresh 聲明）。 |
| `Identity/Security/` | `PasswordHasherService.cs` | `IPasswordHasher` 實作（例如 PBKDF2／ASP.NET Identity 相容演算法）。 |

#### 2.4.3 第三層內 · `Mqtt/`、`Docker/`、`Logging/`（外部系統與檔案）

| 路徑（相對於 Infrastructure 專案根） | 檔案名稱 | 作用 |
|--------------------------------------|----------|------|
| `Mqtt/` | `MqttOptions.cs` | `Mqtt__*` 設定。 |
| `Mqtt/` | `MqttPublisher.cs` | `IMqttPublisher` 實作（MQTTnet）。 |
| `Mqtt/` | `MqttTelemetrySubscriber.cs` | 可選：背景訂閱遙測並寫入（若由後端訂閱）。 |
| `Docker/` | `DockerSystemClient.cs` | `IDockerSystemClient` 實作（Docker.DotNet），供 `system/status`。 |
| `Logging/` | `KvLogParser.cs` | 解析 `log_file_path` 之 KV 結構日誌。 |
| `Logging/` | `FileLogQueryRepository.cs` | 若 `ILogQueryRepository` 改由檔案解析時之實作（與 `Queries/LogDapperQuery` 擇一註冊）。 |

**Ingest 分層備註（2026-04）**

- MQTT `topic` 路由與連線生命週期維持在第三層 `Infrastructure/Mqtt`（HostedService/Adapter）。
- `telemetry` / `ui-events` / `status` 三條 ingest 用例統一放在第二層 `Application/Ingest`。
- 三條 ingest 用例僅依賴第二層介面（如 `I*IngestRepository`），不直接依賴 `ApplicationDbContext`。
- 第三層 `Persistence/Repositories/*IngestRepository` 負責實際落庫（EF Core / DbContext）。
- 因此，第三層舊式 `UiEventMqttIngestService`、`StatusLogMqttIngestService` 路徑視為淘汰設計，不再新增同型實作。

### 2.5 第四層：表現／介面（Api，`Pico2WH.Pi5.IIoT.Api`）

| 路徑（相對於專案根） | 檔案名稱 | 作用 |
|----------------------|----------|------|
| `./` | `Pico2WH.Pi5.IIoT.Api.csproj` | 啟動專案；參考 **第二層 `Application`、第三層 `Infrastructure`**（Persistence／Identity 為第三層內部模組，非獨立專案）。 |
| `./` | `Program.cs` | 建置 WebApplication、Serilog、JWT、Swagger、MediatR、DI 組裝。 |
| `./` | `appsettings.json` | 預設組態（連線字串、JWT、MQTT 等占位）。 |
| `./` | `appsettings.Development.json` | 開發覆寫。 |
| `Properties/` | `launchSettings.json` | 本機 Kestrel／IIS Express 啟動設定（.NET 範本預設位置）。 |
| `Controllers/` | `AuthController.cs` | `/api/v1/auth/*`。 |
| `Controllers/` | `TelemetryController.cs` | `/api/v1/telemetry`、`/series`。 |
| `Controllers/` | `LogsController.cs` | `/api/v1/logs`。 |
| `Controllers/` | `UiEventsController.cs` | `/api/v1/ui-events`。 |
| `Controllers/` | `SystemController.cs` | `/api/v1/system/status`。 |
| `Controllers/` | `DeviceController.cs` | `/api/v1/device/control`。 |
| `Middleware/` | `ExceptionHandlingMiddleware.cs` | 全域例外與問題詳情格式。 |
| `Middleware/` | `RequestLoggingMiddleware.cs` | 可選：HTTP 層追蹤。 |

### 2.6 `tests`（統一於 `app/backend/src/tests`）

| 路徑 | 檔案／結構 | 作用 |
|------|------------|------|
| `Pico2WH.Pi5.IIoT.Domain.Tests/` | `*.csproj`、`Entities/*Tests.cs` | 領域規則單元測試。 |
| `Pico2WH.Pi5.IIoT.Application.Tests/` | `Handlers/*Tests.cs`、`Validators/*Tests.cs` | Command／Query 與驗證測試（Moq 介面）。 |
| `Pico2WH.Pi5.IIoT.Api.IntegrationTests/` | `WebApplicationFactory`、`*EndpointTests.cs` | 端到端／整合測試（`Microsoft.AspNetCore.Mvc.Testing`、Testcontainers）。 |
| `Pico2WH.Pi5.IIoT.Api.ContractTests/` | 可選 | OpenAPI 契約或消費者驅動測試。 |

### 2.7 `scripts`（同層於 `app/backend`）

| 路徑 | 作用 |
|------|------|
| `app/backend/scripts/wsl/` | WSL 開發環境輔助腳本（還原、測試、遷移包裝）。 |
| `app/backend/scripts/pi5/` | Pi 5 部署、systemd、compose 輔助腳本。 |

### 2.8 四層依賴與專案參考（摘要）

| 層 | 專案 | 職責 | 參考（depends on） |
|----|------|------|---------------------|
| 一 | `Pico2WH.Pi5.IIoT.Domain` | 實體與儲存庫介面 | （無） |
| 二 | `Pico2WH.Pi5.IIoT.Application` | MediatR、用例、DTO、管線 | `Domain` |
| 三 | `Pico2WH.Pi5.IIoT.Infrastructure` | EF、Repository、Migrations、JWT、Hasher、MQTT、Docker、檔案日誌 | `Application`、`Domain` |
| 四 | `Pico2WH.Pi5.IIoT.Api` | 啟動、Controller、Middleware、Swagger | `Application`、`Infrastructure` |

> **注意**：`ILogQueryRepository` 的實作可落在第三層 **`Persistence/`**（DB）或 **`Logging/`**（檔案）；實作時擇一並在 DI 註冊單一實作即可。

### 2.9 各層檔案建立順序（建議）

> 由內而外：先完成 **第一、二層**，再 **第三層**（建議先 `Persistence/`，再 `Identity/`，再 `Mqtt`／`Docker`／`Logging`），最後 **第四層** 與測試。

| 順序 | 層級/區域 | 先建立檔案 | 作用（為何先做） |
|------|-----------|------------|------------------|
| 1 | `Domain` | `Common/EntityBase.cs`、`Common/DomainException.cs` | 建立領域共用基底與錯誤語意。 |
| 2 | `Domain` | `Entities/*`、`ValueObjects/DeviceId.cs` | 核心模型與不變條件。 |
| 3 | `Domain` | `Repositories/*.cs`（介面） | 宣告埠；Application 可不依賴實作。 |
| 4 | `Application` | `Common/Interfaces/*`、`PagedResult.cs` | 定義 JWT/MQTT/Docker 等契約。 |
| 5 | `Application` | `Features/*/*Command|Query`、`Handlers`、`Validators`、`Behaviors`、`DependencyInjection.cs` | 用例與管線。 |
| 6 | `Infrastructure` → **Persistence** | `Context/ApplicationDbContext.cs`、`Configurations/*` | EF 對映與 DbSet。 |
| 7 | `Infrastructure` → **Persistence** | `Repositories/*Repository.cs`、`Migrations/*` | 實作儲存埠、遷移。 |
| 8 | `Infrastructure` → **Identity** | `Jwt/*`、`Security/*`、DI 註冊 | 實作 `IJwtService` / `IPasswordHasher`。 |
| 9 | `Infrastructure` → **Mqtt/Docker/Logging** | `MqttPublisher`、`DockerSystemClient`、`KvLogParser`、`FileLogQueryRepository` | 外部適配。 |
| 10 | `Infrastructure` | 總覽 `DependencyInjection.cs`（或分區擴充方法） | 對外單一註冊入口。 |
| 11 | `Api` | `Program.cs`、組態、Middleware | 組裝主機。 |
| 12 | `Api` | `Controllers/*` | HTTP 入口。 |
| 13 | `tests` | 對應各層之測試專案 | 可與功能開發並行。 |

#### 2.9.1 建立順序檢查點（Checkpoint）

- **Checkpoint A（完成步驟 1～3）**：`Domain` 可獨立編譯，且不含 EF/HTTP/MQTT 依賴。  
- **Checkpoint B（完成步驟 4～5）**：`Application` 可用 Mock 介面執行 Handler 測試。  
- **Checkpoint C（完成步驟 6～10）**：`Infrastructure` 可註冊 DbContext、Repositories、JWT、MQTT 等。  
- **Checkpoint D（完成步驟 11～13）**：API 啟動成功，Swagger 可見，關鍵端點可由整合測試驗證。

---

## 3. 實作階段必須使用的套件清單

> 下列按 **層**標註套件歸屬；第二層（Application）僅保留用例相關套件，勿將資料存取／MQTT 等套件誤加在 Application。

### 3.1 第二層（`Application` 專案）

- `MediatR`
- `MediatR.Extensions.Microsoft.DependencyInjection`
- `FluentValidation`
- `FluentValidation.DependencyInjectionExtensions`
- `AutoMapper`（可選，若 DTO 映射複雜）
- `AutoMapper.Extensions.Microsoft.DependencyInjection`（可選）

### 3.2 第四層（`Api` 專案）

- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `Microsoft.AspNetCore.Authorization`
- `Swashbuckle.AspNetCore`
- `Microsoft.AspNetCore.OpenApi`
- `Serilog.AspNetCore`
- `Serilog.Sinks.Console`
- `Serilog.Sinks.File`
- `Serilog.Enrichers.Environment`
- `Serilog.Enrichers.Thread`

### 3.3 第三層（`Infrastructure` 專案）

- **資料庫**：`Npgsql.EntityFrameworkCore.PostgreSQL`、`Microsoft.EntityFrameworkCore.Design`、`Dapper`（讀取查詢可選實作，見 §2.4.1a）
- **身分（JWT 實作常用）**：`System.IdentityModel.Tokens.Jwt`、`Microsoft.IdentityModel.Tokens`、`Microsoft.Extensions.Options.ConfigurationExtensions`
- **MQTT / Docker**：`MQTTnet`、`Docker.DotNet`

### 3.4 全域工具（開發機）

- `Microsoft.EntityFrameworkCore.Tools`（`dotnet ef` CLI）

### 3.5 測試專案

- `xunit`
- `xunit.runner.visualstudio`
- `FluentAssertions`
- `Moq`
- `Microsoft.AspNetCore.Mvc.Testing`
- `Testcontainers`（PostgreSQL/Mosquitto 整合測試）

---

## 4. WSL 與 Pi5 安裝套件與步驟

## 4.1 WSL（開發環境）

### 目標
- 可開發、測試、遷移資料庫、啟動本地 API 與整合測試

### 必裝項目
- .NET SDK 8
- Docker Engine + Compose Plugin
- PostgreSQL client tools
- OpenSSL / curl / jq

### 安裝步驟（Ubuntu 22.04+，可直接執行）

1) 更新系統與基礎工具

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y ca-certificates curl gnupg lsb-release apt-transport-https software-properties-common
```

2) 安裝 Microsoft 套件源（.NET 8）

```bash
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt update
```

3) 安裝 .NET SDK 8 與常用工具

```bash
sudo apt install -y dotnet-sdk-8.0
sudo apt install -y postgresql-client jq openssl
dotnet --info
```

4) 安裝 Docker Engine + Compose Plugin

```bash
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
rm get-docker.sh
sudo apt install -y docker-compose-plugin
sudo usermod -aG docker "$USER"
newgrp docker
docker --version
docker compose version
```

5) 專案還原、測試與 EF 工具

```bash
dotnet restore
dotnet test
dotnet tool install --global dotnet-ef
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef --version
```

6) Migration（於 **`Pico2WH.Pi5.IIoT.Infrastructure`** 執行，啟動專案為 `Pico2WH.Pi5.IIoT.Api`）

```bash
# 於 app/backend/src 目錄執行（專案資料夾直接在該層，無須再加 src/ 前綴）
dotnet ef migrations add InitialCreate --project Pico2WH.Pi5.IIoT.Infrastructure --startup-project Pico2WH.Pi5.IIoT.Api
dotnet ef database update --project Pico2WH.Pi5.IIoT.Infrastructure --startup-project Pico2WH.Pi5.IIoT.Api
```

### 4.1.1 DB Migration 使用範例（**第三層**為遷移目標專案）

> 根目錄固定為 `app/backend/src`。以下指令可直接套用於 **Windows cmd**、**WSL**、**Pi 5（Linux）**；`dotnet ef` 參數格式一致。

#### A. Solution 內各專案與 Migration 角色

| 專案 | 是否直接執行 Migration | 角色 | 說明 / 指令定位 |
|------|------------------------|------|------------------|
| `Pico2WH.Pi5.IIoT.Domain` | 否（間接） | Entity Source | 提供實體模型；由 `Infrastructure` 內 EF 對映反映到遷移。 |
| `Pico2WH.Pi5.IIoT.Application` | 否 | Use Case Layer | 不應含 `DbContext`；不作為 `--project`。 |
| **`Pico2WH.Pi5.IIoT.Infrastructure`** | **是** | **Migration Target Project** | `DbContext`、`Persistence/Migrations/*` 所在專案；**`dotnet ef --project` 指向此專案**。 |
| `Pico2WH.Pi5.IIoT.Api` | 是（啟動） | **Startup Project** | 主要 `--startup-project`；載入連線字串與 DI。 |
| `tests/*` | 否 | Test Projects | 驗證行為，不產生 migration。 |
| `scripts/*` | 否 | Automation | 可封裝指令，但本身不是 migration project。 |

#### B. 指令範本（cmd / WSL / Pi5 共用）

```bash
# 1) 切到統一根目錄（app/backend/src）
cd app/backend/src

# 2) 建立新遷移（名稱自行替換）
dotnet ef migrations add <MigrationName> --project Pico2WH.Pi5.IIoT.Infrastructure --startup-project Pico2WH.Pi5.IIoT.Api

# 3) 套用到資料庫
dotnet ef database update --project Pico2WH.Pi5.IIoT.Infrastructure --startup-project Pico2WH.Pi5.IIoT.Api

# 4) （可選）查看目前遷移清單
dotnet ef migrations list --project Pico2WH.Pi5.IIoT.Infrastructure --startup-project Pico2WH.Pi5.IIoT.Api
```

#### C. 常見操作範本

```bash
# 產生 SQL 腳本（部署用）
dotnet ef migrations script --project Pico2WH.Pi5.IIoT.Infrastructure --startup-project Pico2WH.Pi5.IIoT.Api --output migration.sql

# 回滾到指定遷移（例如 InitialCreate）
dotnet ef database update InitialCreate --project Pico2WH.Pi5.IIoT.Infrastructure --startup-project Pico2WH.Pi5.IIoT.Api

# 移除尚未套用的最後一個遷移
dotnet ef migrations remove --project Pico2WH.Pi5.IIoT.Infrastructure --startup-project Pico2WH.Pi5.IIoT.Api
```

#### C.1 若未來新增「第二個 DbContext」時的完整範本

> 目前規格於 **Infrastructure** 內通常僅一個 `ApplicationDbContext`。若新增第二個 `DbContext`（例如獨立身分資料庫），可使用 `--context`：

```bash
dotnet ef migrations add <MigrationName> --context <DbContextName> --project Pico2WH.Pi5.IIoT.Infrastructure --startup-project Pico2WH.Pi5.IIoT.Api

dotnet ef database update --context <DbContextName> --project Pico2WH.Pi5.IIoT.Infrastructure --startup-project Pico2WH.Pi5.IIoT.Api
```

#### D. 跨環境注意事項

- `cmd`：若尚未安裝 EF 工具，可先執行 `dotnet tool install --global dotnet-ef`，重新開啟終端機後再執行。  
- `WSL/Pi5`：若 `dotnet ef` 找不到，加入 `export PATH="$PATH:$HOME/.dotnet/tools"`。  
- Pi 5 若僅安裝 Runtime 而無 SDK，無法在機上產生 Migration；需改由 WSL/CI 產生後部署。  
- Migration 名稱建議使用 `PascalCase`（例：`AddDeviceControlAudit`），便於版本追蹤。

### 4.1.2 資料表 DDL（dev / prod 雙 schema）

> 依據 `Pico2WH-Pi5-IIoT-專案開發規格書_v5.md` 與 ASP.NET Core 版整併。  
> 採單一 PostgreSQL DB、雙 schema（`dev` / `prod`）隔離環境。  
> 下列 SQL 可於 `cmd`（`psql`）、`WSL`、`Pi 5` 直接執行（語法相同）。

#### A. 建立 schema

```sql
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE SCHEMA IF NOT EXISTS dev;
CREATE SCHEMA IF NOT EXISTS prod;
```

#### B. 建表（必要資料表）

```sql
-- =========================================
-- Apply this block twice: once for dev, once for prod
-- 1) 將 __SCHEMA__ 替換為 dev 或 prod
-- =========================================

-- 裝置主檔
CREATE TABLE IF NOT EXISTS __SCHEMA__.devices (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id               VARCHAR(64) NOT NULL UNIQUE,
    name                    VARCHAR(128) NOT NULL,
    is_active               BOOLEAN NOT NULL DEFAULT TRUE,
    last_seen_at_utc        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at_utc          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc          TIMESTAMPTZ
);

-- 使用者帳號（JWT login）
CREATE TABLE IF NOT EXISTS __SCHEMA__.users (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username                VARCHAR(64) NOT NULL UNIQUE,
    password_hash           TEXT NOT NULL,
    role                    VARCHAR(32) NOT NULL,
    is_enabled              BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc          TIMESTAMPTZ,
    CONSTRAINT ck_users_role CHECK (role IN ('admin', 'viewer'))
);

-- Refresh Token（login/refresh/logout）
CREATE TABLE IF NOT EXISTS __SCHEMA__.refresh_tokens (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                 UUID NOT NULL REFERENCES __SCHEMA__.users(id) ON DELETE CASCADE,
    token                   TEXT NOT NULL UNIQUE,
    expires_at_utc          TIMESTAMPTZ NOT NULL,
    revoked                 BOOLEAN NOT NULL DEFAULT FALSE,
    revoked_at_utc          TIMESTAMPTZ,
    created_at_utc          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc          TIMESTAMPTZ
);

-- 遙測資料（/api/v1/telemetry, /series）
CREATE TABLE IF NOT EXISTS __SCHEMA__.telemetry_readings (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id               VARCHAR(64) NOT NULL REFERENCES __SCHEMA__.devices(device_id),
    device_time_utc         TIMESTAMPTZ NOT NULL,
    co2_ppm                 INTEGER,
    temperature_c_scd41     NUMERIC(6,2),
    pir_active              BOOLEAN,
    created_at_utc          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_telemetry_co2_nonnegative CHECK (co2_ppm IS NULL OR co2_ppm >= 0)
);

-- UI 事件（/api/v1/ui-events）
CREATE TABLE IF NOT EXISTS __SCHEMA__.ui_events (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id               VARCHAR(64) NOT NULL REFERENCES __SCHEMA__.devices(device_id),
    event_type              VARCHAR(64) NOT NULL,
    event_value             VARCHAR(256),
    channel                 VARCHAR(32) NOT NULL DEFAULT 'ui-events',
    device_time_utc         TIMESTAMPTZ NOT NULL,
    created_at_utc          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 系統狀態快照（/api/v1/system/status）
CREATE TABLE IF NOT EXISTS __SCHEMA__.system_status_snapshots (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    container_id            VARCHAR(128) NOT NULL,
    name                    VARCHAR(128) NOT NULL,
    status                  VARCHAR(64) NOT NULL,
    uptime_sec              BIGINT,
    ip                      INET,
    health_status           VARCHAR(64),
    observed_at_utc         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 裝置控制審計（/api/v1/device/control）
CREATE TABLE IF NOT EXISTS __SCHEMA__.device_control_audits (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id               VARCHAR(64) NOT NULL REFERENCES __SCHEMA__.devices(device_id),
    command                 VARCHAR(64) NOT NULL,
    value_percent           INTEGER NOT NULL,
    value_16bit             INTEGER NOT NULL,
    requested_by_user_id    UUID REFERENCES __SCHEMA__.users(id),
    request_id              VARCHAR(128) NOT NULL UNIQUE,
    accepted                BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_device_control_value_percent CHECK (value_percent BETWEEN 0 AND 100),
    CONSTRAINT ck_device_control_value_16bit CHECK (value_16bit BETWEEN 0 AND 65535)
);

-- 結構化日誌（對應 /api/v1/logs，若採 DB 來源）
CREATE TABLE IF NOT EXISTS __SCHEMA__.app_logs (
    id                      BIGSERIAL PRIMARY KEY,
    device_id               VARCHAR(64),
    channel                 VARCHAR(32) NOT NULL,
    level                   VARCHAR(16) NOT NULL,
    message                 TEXT NOT NULL,
    payload_json            JSONB,
    source_ip               INET,
    device_time_utc         TIMESTAMPTZ,
    created_at_utc          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_app_logs_channel CHECK (channel IN ('telemetry', 'ui-events', 'status')),
    CONSTRAINT ck_app_logs_level CHECK (level IN ('debug', 'info', 'warn', 'error'))
);
```

#### C. 索引（必要查詢優化）

```sql
-- =========================================
-- Replace __SCHEMA__ with dev or prod
-- =========================================

CREATE INDEX IF NOT EXISTS ix_devices_last_seen_at
    ON __SCHEMA__.devices (last_seen_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user_id
    ON __SCHEMA__.refresh_tokens (user_id);

CREATE INDEX IF NOT EXISTS ix_refresh_tokens_expires_at
    ON __SCHEMA__.refresh_tokens (expires_at_utc);

CREATE INDEX IF NOT EXISTS ix_telemetry_device_time
    ON __SCHEMA__.telemetry_readings (device_id, device_time_utc DESC);

CREATE INDEX IF NOT EXISTS ix_ui_events_device_time
    ON __SCHEMA__.ui_events (device_id, device_time_utc DESC);

CREATE INDEX IF NOT EXISTS ix_system_status_observed_at
    ON __SCHEMA__.system_status_snapshots (observed_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_device_control_audits_device_created
    ON __SCHEMA__.device_control_audits (device_id, created_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_app_logs_filter
    ON __SCHEMA__.app_logs (channel, level, created_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_app_logs_device_time
    ON __SCHEMA__.app_logs (device_id, device_time_utc DESC);
```

#### D. 一次建立 dev / prod 的指令範本（cmd / WSL / Pi5）

```bash
# 1) 將 DDL 存成檔案，例如 app/backend/scripts/sql/init_schema.sql

# 2) 以 psql 執行（cmd / WSL / Pi5 相同）
psql "<PostgresConnectionString>" -v ON_ERROR_STOP=1 -f app/backend/scripts/sql/init_schema.sql
```

#### E. 與 EF Core Migration 的關係

- 若採 EF Core 為唯一 schema 管理來源，建議將上述 DDL 對應成 **`Infrastructure` 專案內** `Persistence/Migrations`（`Up/Down`）。  
- 若採 DBA 先行建庫策略，可先跑 DDL，再用 EF Core 對齊模型（避免重複建表）。  
- `dev` / `prod` 可透過不同 `ConnectionStrings__Postgres` 與 `search_path` 控制目標 schema。

### 開發端環境變數（最小集合）
- `ASPNETCORE_ENVIRONMENT=Development`
- `ConnectionStrings__Postgres=...`
- `Jwt__Issuer=...`
- `Jwt__Audience=...`
- `Jwt__SigningKey=...`
- `Mqtt__Host=...`
- `Logging__log_file_path=app/backend/logs/backend.log`

## 4.2 Pi 5（部署環境）

### 目標
- 以 `dotnet runtime` 或容器方式穩定執行 Web API，對接 Nginx

### 必裝項目
- `.NET 8 ASP.NET Core Runtime`（非 SDK，若僅執行）
- Docker Engine + Compose plugin（若容器部署）
- systemd（服務託管）
- Nginx（反向代理）

### 安裝步驟（Raspberry Pi OS 64-bit，可直接執行）

1) 更新系統與基礎工具

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y ca-certificates curl gnupg apt-transport-https
```

2) 安裝 Microsoft 套件源（ARM64）

```bash
wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt update
```

3) 安裝 .NET Runtime（原生部署）或 SDK（需本機建置）

```bash
# 原生部署（推薦最小安裝）
sudo apt install -y aspnetcore-runtime-8.0

# 若需要在 Pi5 本機 build，改裝 SDK
# sudo apt install -y dotnet-sdk-8.0

dotnet --list-runtimes
```

4) 安裝 Docker + Compose（容器部署時）

```bash
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
rm get-docker.sh
sudo apt install -y docker-compose-plugin
sudo systemctl enable docker
sudo systemctl start docker
docker --version
docker compose version
```

5) 安裝並設定 Nginx（反向代理）

```bash
sudo apt install -y nginx
sudo systemctl enable nginx
sudo systemctl start nginx
```

6) 開放防火牆（若使用 ufw）

```bash
sudo apt install -y ufw
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw --force enable
sudo ufw status
```

7) 啟動服務（二選一）

```bash
# A. 原生部署：systemd
sudo systemctl daemon-reload
sudo systemctl enable iiot-webapi
sudo systemctl start iiot-webapi
sudo systemctl status iiot-webapi --no-pager

# B. 容器部署：Compose
docker compose up -d
docker compose ps
```

8) 部署後驗證

```bash
curl -k https://<pi-domain-or-ip>/health
curl -k -H "Authorization: Bearer <access_token>" https://<pi-domain-or-ip>/api/v1/telemetry?device_id=<id>
```

### Pi5 部署模式建議
- **推薦**：Docker Compose（與 PostgreSQL/Mosquitto/Nginx 一致化）
- **備選**：systemd + Kestrel（資源占用更輕，需自行管理更新）

---

## 5. API Endpoint 詳細規格（ASP.NET Core 實作版）

> 端點語意對齊 v5 §6.0.3；此處補充 ASP.NET Core 實作約束。

### 5.1 Auth

#### POST `/api/v1/auth/login`
- 目的：登入取得 Access/Refresh Token
- Request：`username`, `password`
- Response：`access_token`, `refresh_token`, `expires_in`, `token_type`
- 錯誤：`401`, `429`

#### POST `/api/v1/auth/refresh`
- 目的：以 Refresh Token 換發新 Access Token（支援輪替）
- Request：`refresh_token`
- Response：`access_token`, `refresh_token`, `expires_in`
- 錯誤：`401`, `403`, `429`

#### POST `/api/v1/auth/logout`
- 目的：撤銷 Refresh Token
- Request：`refresh_token`
- Response：`status=ok`
- 錯誤：`400`, `401`

### 5.2 Telemetry

#### GET `/api/v1/telemetry`
- 篩選：`device_id`, `from`, `to`, `page`, `page_size`
- 欄位：對齊第一層遙測實體語意（§2.2，`co2_ppm`, `temperature_c_scd41`, `pir_active` 等）
- 錯誤：`400`, `401`, `403`

#### GET `/api/v1/telemetry/series`
- 參數：`device_id`, `from`, `to`, `metrics`, `max_points`
- 必須支援 metrics：`co2_ppm`, `temperature_c_scd41`, `pir_active`
- 降採樣：當點數超過 `max_points`（預設 500）啟動桶化平均
- 回傳 metadata：`downsampled`, `source_points`, `returned_points`
- 錯誤：`400`, `401`, `429`

### 5.3 Logs / UI Events

#### GET `/api/v1/logs`
- 篩選：`device_id`, `channel`, `level`, `from`, `to`, `page`, `page_size`
- `channel`：`telemetry` / `ui-events` / `status`
- `level`：`debug` / `info` / `warn` / `error`
- 來源：`log_file_path` 對應的 KV 日誌

#### GET `/api/v1/ui-events`
- 篩選：`device_id`, `from`, `to`, `page`, `page_size`
- 回傳：`event_type`, `event_value`, `channel`, `device_time`

### 5.4 Operations

#### GET `/api/v1/system/status`
- 來源：Docker Engine API
- 回傳：`container_id`, `name`, `status`, `uptime_sec`, `ip`, `health_status`
- 用途：儀表板 + KY-016 狀態映射

#### POST `/api/v1/device/control`
- Request：`device_id`, `command=set_pwm`, `value(0~100)`
- 語意：映射到 `0..65535` 後透過 MQTT 發送
- 回傳：`accepted`, `request_id`, `value_16bit`
- 授權：僅 `admin`

---

## 6. Clean Architecture + MediatR（CQRS）落地規範

### 6.1 命名與分層規則
- Command：狀態改變（Login / Refresh / Logout / Ingest / DeviceControl）
- Query：讀取（TelemetryList / TelemetrySeries / Logs / UiEvents / SystemStatus）
- Controller/Endpoint 不含業務邏輯，只做輸入驗證與 dispatch

### 6.2 MediatR Pipeline Behaviors（必須）
- ValidationBehavior：統一參數驗證（FluentValidation）
- LoggingBehavior：請求/回應追蹤（對齊 §6.0A）
- AuthorizationBehavior（可選）：角色/範圍前置檢查

### 6.3 不可違反項
- Domain 不可依賴 EF / HTTP / MQTT / Docker SDK
- Application 不得出現 SQL 字串
- **第三層（Infrastructure）**不得包含**商業規則判斷**（僅技術適配、資料對映、外部呼叫）；商業規則留在第一／二層（Domain／Application）

---

## 7. 交付驗收清單（ASP.NET Core · 四層架構）

- 所有 `§6.0.3` 端點在 Swagger UI 可見並可測
- Auth（login/refresh/logout）端到端可用
- Series 端點降採樣 metadata 正確
- Logs 篩選（channel/level）可用
- System status 可讀 Docker 容器健康
- Device control 可發布 MQTT 指令並落審計
- **四層**邊界與 DI 註冊正確（第四層 `Api` 僅參考第二層 `Application` 與第三層 `Infrastructure`；第一層無框架依賴）

---

## 8. 文件關係

| 文件 | 說明 |
|------|------|
| **本文件** | **ASP.NET Core 後端四層 SoT**（Domain / Application / Infrastructure / Api）。後端實作與 Code Review 以此為準。 |
| `Pico2WH-Pi5-IIoT-專案開發規格書_v5.md` | 全專案總規格（硬體、韌體、部署、階段計畫）；後端細節請回指本文件。 |
| Legacy（Ulfius 路徑） | 僅供歷史參考，不作為現行開發基準。 |
