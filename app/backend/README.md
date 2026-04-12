# 後端（ASP.NET Core）

本專案為 **.NET 8** **Web API**，採四層式結構：**Domain**、**Application**（**MediatR**、驗證管線）、**Infrastructure**（**EF Core**、**MQTT**、**Docker** 用戶端）、**Api**（控制器、**Middleware**、**Swagger**）。

## 方案檔與執行

- 方案：`src/Pico2WH.Pi5.IIoT.FourLayer.sln`
- 啟動專案：`src/Pico2WH.Pi5.IIoT.Api/Pico2WH.Pi5.IIoT.Api.csproj`

```bash
cd src/Pico2WH.Pi5.IIoT.Api
dotnet run
```

## 設定

- `Pico2WH.Pi5.IIoT.Api/appsettings.json`：**ConnectionStrings**、**Jwt**、**Mqtt**（**Broker**、**TLS**、**SubscribeTopicFilters**）、**Docker**（容器狀態 API，需能存取 **Docker socket** 或對應 **URI**）。
- **`Database:DefaultSchema`**：可由環境變數 **`Database__DefaultSchema`** 覆寫（**ASP.NET Core** 慣例：`__` 表示階層）。**Docker Compose** 自專案根目錄 **`.env`** 讀取 **`BACKEND_API_DATABASE_DEFAULT_SCHEMA`** 並注入（見儲存庫根目錄 **`docker-compose.yml`**）。本機 **`dotnet run`** 時若未經 **Compose**，請在 shell 設定 **`export Database__DefaultSchema=dev`**，或使用 **`dotnet user-secrets`**／**啟動設定** 帶入同一鍵名（**`.env` 不會被 `dotnet` 自動載入**，除非另行使用工具載入）。
- 背景服務 **MQTT ingest** 需 `Mqtt:Enabled` 與 `Mqtt:IngestEnabled` 為 **true**，方可訂閱 `iiot/+/+/telemetry/#`、`ui-events`、`status` 等並寫入資料庫。

## 資料庫與 EF Core 遷移

- **PostgreSQL**；遷移檔在 **`Pico2WH.Pi5.IIoT.Infrastructure/Migrations/`**，以子資料夾區隔：**`Dev/`**（現有遷移與快照）、**`Prod/`**（若要讓產生的 **SQL** 對 **`prod`** schema）。
- **Schema** 名稱由 **`Database:DefaultSchema`** 決定（預設 **`dev`**）。**`dotnet ef`** 設計階段會讀 **Api** 的設定；覆寫方式：**`export Database__DefaultSchema=prod`**（或 **`dev`**）。更細指令見同目錄 **`db-migration-commands.txt`**。
- 手動插入 **admin**／**customer** 種子帳號（**SQL**）見 **`db/seed_users_manual.sql`**（內含部署用初始密碼與 **`password_hash`**；上線後請變更密碼或改走既有帳號流程）。

### 啟動時自動套用遷移（預設開啟）

- 應用程式啟動時會讀取 **`Database:AutoMigrate`**（預設 **`true`**），並對目前連線之資料庫執行 **`Database.Migrate()`**，將尚未套用之 **EF Core** 遷移寫入資料庫（本機 **`dotnet run`** 與 **Docker** 容器皆同）。
- 若改由 **CI**／手動 **`dotnet ef database update`** 專責遷移，請設 **`Database:AutoMigrate`** 為 **`false`**，或環境變數 **`Database__AutoMigrate=false`**。
- 連線失敗或遷移錯誤時，啟動會記錄 **Serilog** **`Fatal`** 並中止，請檢查 **PostgreSQL** 可達性與帳密。

### 前置

- 已安裝 **.NET 8 SDK**。
- 安裝 **EF Core** 命令列工具（僅需一次）：

```bash
dotnet tool install --global dotnet-ef
```

- 可連線至目標 **PostgreSQL**（本機或 **Docker** 對外埠）；連線字串須與實際 **`Database`**、帳密一致。

### 工作目錄

以下指令均在 **`app/backend/src`** 下執行（若自儲存庫根目錄操作，請先 **`cd app/backend/src`**）。

### 套用遷移（建立／更新資料表）

依 **`appsettings.json`**（或環境變數）中的 **`ConnectionStrings:Default`** 連線並套用全部待執行遷移：

```bash
dotnet ef database update \
  --project Pico2WH.Pi5.IIoT.Infrastructure/Pico2WH.Pi5.IIoT.Infrastructure.csproj \
  --startup-project Pico2WH.Pi5.IIoT.Api/Pico2WH.Pi5.IIoT.Api.csproj
```

若需指向與 **`appsettings`** 不同的資料庫（例如 **Docker Compose** 對外映射的 **PostgreSQL**），可在單次指令前覆寫連線字串（**bash**）：

```bash
export ConnectionStrings__Default="Host=127.0.0.1;Port=<對外埠>;Database=<資料庫名>;Username=<帳號>;Password=<密碼>"
dotnet ef database update \
  --project Pico2WH.Pi5.IIoT.Infrastructure/Pico2WH.Pi5.IIoT.Infrastructure.csproj \
  --startup-project Pico2WH.Pi5.IIoT.Api/Pico2WH.Pi5.IIoT.Api.csproj
```

若已關閉自動遷移，**Compose** 部署時請自行對目標資料庫執行 **`database update`**，否則執行期可能出現資料表不存在之錯誤。

### 新增遷移（`dev` schema，檔案寫入 `Migrations/Dev`）

在 **`app/backend/src`** 執行；將 **`MigrationName`** 換成實際名稱（例如 **`AddSomeTable`**）：

```bash
export Database__DefaultSchema=dev
dotnet ef migrations add MigrationName \
  --project Pico2WH.Pi5.IIoT.Infrastructure/Pico2WH.Pi5.IIoT.Infrastructure.csproj \
  --startup-project Pico2WH.Pi5.IIoT.Api/Pico2WH.Pi5.IIoT.Api.csproj \
  --output-dir Migrations/Dev
```

（若 **`ASPNETCORE_ENVIRONMENT=Development`** 且 **appsettings** 已設 **`dev`**，可省略 **`export`**。）

### 新增遷移（`prod` schema，檔案寫入 `Migrations/Prod`）

目標是讓 **EF** 依 **`prod`** schema 產生 **`Up`/`Down`**（表名前會帶 **`prod.`**）。同樣在 **`app/backend/src`**：

```bash
export Database__DefaultSchema=prod
dotnet ef migrations add MigrationName \
  --project Pico2WH.Pi5.IIoT.Infrastructure/Pico2WH.Pi5.IIoT.Infrastructure.csproj \
  --startup-project Pico2WH.Pi5.IIoT.Api/Pico2WH.Pi5.IIoT.Api.csproj \
  --output-dir Migrations/Prod
```

**請留意：** 同一 **`ApplicationDbContext`** 只有**一份** **`ApplicationDbContextModelSnapshot`**。使用 **`--output-dir Migrations/Prod`** 時，**新遷移與更新後的快照會寫入 `Migrations/Prod`**，勿與 **`Dev/`** 混成兩條互不相容的歷史。若一直以 **`dev`** 遷移為主，正式環境多半是**同一套**遷移檔 + 不同 **連線字串**，而不是另建 **`prod`** schema 遷移。

產生檔案後，視需要執行上一節 **`database update`**（執行期 **`Database__DefaultSchema`** 須與目標資料庫 schema 一致）。

### 選用指令

- **列出遷移**：`dotnet ef migrations list`（同上 **`--project`** / **`--startup-project`**）。
- **還原至指定遷移**：`dotnet ef database update <MigrationId>`（**`0`** 表示卸載全部遷移，慎用）。

完整一行範例與 **Development** 環境下之補充說明見 **`db-migration-commands.txt`**。

## Docker 映像

- **Dockerfile** 位於本目錄根層；**build context** 為 `app/backend`（與儲存庫根目錄 **docker-compose.yml** 之 `backend_api` 一致）。
- 執行階段監聽位址由環境變數 **`ASPNETCORE_URLS`** 決定；**Compose** 設為 **`http://+:${BACKEND_API_INSIDE_PORT}`**，與 **`ports`** 映射一致。映像預設為 **`http://+:8080`**（僅在未覆寫時生效）。
- 正式環境之連線字串、**MQTT** 帳密與 **Broker** **TLS** **CA** 路徑由 **Compose** 以環境變數注入；映像內預設關閉 **Docker** 引擎查詢（`Docker__Enabled=false`），若需容器內查宿主容器狀態，須掛載 **Docker socket** 並另行設定。

## 測試

- `src/tests/` 下含 **Domain**／**Application**／**Api** 之單元與整合／契約測試；以 **dotnet test** 執行方案或測試專案。

---