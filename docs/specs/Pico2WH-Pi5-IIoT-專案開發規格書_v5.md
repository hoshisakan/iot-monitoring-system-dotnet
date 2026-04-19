# 🚀 Pico 2 WH & Pi 5 IIoT 專案開發規格書 v5

> 更新日期：**2026-04-05**  
> 補充（2026-04-18）：**§6.0.5 A.0** 後端讀取路徑 **EF Core／Dapper** 與 `Database` 組態；細節見 `Pico2WH-Pi5-IIoT-專案開發規格書_v5_ASPNETCORE_4LAYER.md` **§2.4.1a**。  
> 修訂：新增 **§6.0.5 技術策略深度擬定**（Database Strategy、Auth Policy、Fault Tolerance）；新增 **§6.0.4 技術規格增補（SoT）**：REST API 深度規範、Clean Architecture 分層契約、MQTT payload 與錯誤處理、Pico I2C 衝突掃描與 AT24C256 Ring Buffer 策略；更新 **§6.0.3** API 實作細節（`GET /api/v1/logs` 結構化檔案解析與 `channel/level` 篩選、`GET /api/v1/telemetry/series` 降採樣與 metrics 驗證、新增 `GET /api/v1/system/status`、`POST /api/v1/device/control`）；新增 **§6.0.3**（Markdown 模擬 Swagger UI API 端點說明，對齊 **§2.4.2** MQTT 鍵與 **§6.0** 功能）；**§6.0A** 後端 Logging 架構補強（**Key-Value 格式**、**10MB 輪替/覆蓋**、**JWT 驗證失敗審計：來源 IP + 錯誤類型**）；`config.json` 可控 `log_file_path`、啟動自動建目錄、Console+檔案雙寫、MQTT `on_message_received` 記錄 `device_id/topic/payload`；**§2.4.2** **SCD41／PIR** 與 **§6.0** 之 **MQTT Topic／JSON 鍵／DB** 單一契約；**§6.1** **`iot/2026-04-05 012821.png`** 新硬體之**階段目標、驗收、接線對照**；**§6.0**／**§6.2** 階段清單 **SCD41／Mini PIR 韌體與前後端**（與 **§2.4.1**、硬體計畫 **§4.0／🟢階段一** 對齊）；**§2.3～§2.4.1** **`iot/2026-04-05 012821.png`**：**SCD41**、**Grove Mini PIR**、**Grove→DuPont 公頭線 20 cm**；**§2.3** **ProsKit BX-4112N×2、8PK-AS07-1（已購買）**；**§5.1～§5.2** 單板／雙麵包板並接接線圖；**§1** 補充並行節奏與 **Clean Architecture**；**§2.1B** 前後端層級；**§2.1A** **Nginx**；**§2.6** **LCD1602×2（Pico／Pi 5）**、**KY-016（已購買）** 接線；**§6.0** **Nginx／JWT** 與 **後端／前端（Clean Architecture）** 標註；**§6.0.1** **遙測圖表（前後端設計步驟）**；**§6.0.2** 第四階段為生產調優；MQTT 細節見 `Stage1-Stage3_Implementation_Roadmap_with_MQTT_Topic_Standard.md`。  
> 基礎版本：`Pico2WH-Pi5-IIoT-專案開發規格書_v4.md`  
> 環境：**開發端（WSL2 Ubuntu 22.04+）/ 生產端（Pi 5 64-bit）**

---

## 文件定位與 SoT（必讀）

- 本文件（`v5.md`）是**全專案總規格**（硬體、韌體、前後端、部署與階段計畫）。
- 後端實作細節（分層、專案結構、套件、API 實作約束）以 `Pico2WH-Pi5-IIoT-專案開發規格書_v5_ASPNETCORE_4LAYER.md` 為**後端 SoT**。
- 若兩份文件對後端技術描述不一致，以 **ASP.NET Core 四層文件**為準。
- `Ulfius` 在本文件中視為 **Legacy 歷史方案**註記；現行後端主線為 **ASP.NET Core (.NET 8) 四層架構**。
- MQTT ingest 分層以後端 SoT 為準：`topic` 路由在 `Infrastructure`，三條 ingest 用例（telemetry/ui-events/status）在 `Application`，落庫由 `Infrastructure/Persistence` repository 實作。

---

## 1. 專案架構

- **生產環境**：Pi 5 16GB（Raspberry Pi OS 64-bit），運行 Docker 容器化服務。  
- **開發環境**：Windows WSL2（Ubuntu 22.04+）。  
- **後端服務**：ASP.NET Core Web API（.NET 8，四層架構）、PostgreSQL 18、Mosquitto（MQTT Broker）、PGAdmin。  
- **端點設備**：Pico 2 WH（C + Pico SDK）。  
- **除錯工具**：Raspberry Pi Debug Probe（CMSIS-DAP / SWD）。  
- **責任分工（固定）**：**Pi 5** 負責全端系統（Frontend + Backend + MQTT Broker + PostgreSQL + Nginx）；**Pico 2 WH** 負責感測/告警 I/O（含 `Grove-MOSFET` PWM 輸出）。  
- **實作節奏**：韌體 **階段一至三**（見 §6 與 `Pico2WH_Hardware_Plan_v3.md` §4）與 **Pi 5 前後端**為 **並行軌道**；詳見 **§6.0**。超出韌體交付之儀表與主機監控為 **第四階段**（§6.0.2）。MQTT topic 與驗收清單見 `Stage1-Stage3_Implementation_Roadmap_with_MQTT_Topic_Standard.md`。
- **對外服務**：**客戶遠端檢視**（目標規模 **約 500 名使用者帳號**）經 **Nginx 反向代理**（TLS、upstream）對外提供 HTTPS；**Mosquitto／PGAdmin／PostgreSQL 埠不對公網開放**。細節見 **§2.1A**。
- **應用架構**：**前端**與**後端**程式碼組織採 **Clean Architecture**（依賴向內、領域與框架分離）；層級約定見 **§2.1B**。階段並行實作見 **§6.0**（標題 **後端（Clean Architecture）**／**前端（Clean Architecture）**）。

## 2. 軟硬體架構（採購整合 + Dual-Bus）

### 2.1 軟體清單

| 類別 | 技術 |
|---|---|
| 架構風格 | **Clean Architecture**（前後端；見 **§2.1B**） |
| 前端 | React（儀表含 **遙測圖表** 時，圖表元件實作見 **§6.0.1**） |
| 後端 | ASP.NET Core Web API (.NET 8, 四層架構) |
| 資料庫 | PostgreSQL 18 |
| DB 管理 | PGAdmin |
| 快取層（第四階段） | Redis（查詢快取、限流、短期狀態） |
| 訊息傳輸 | MQTT (Mosquitto, TLS 1.2) |
| 可觀測性（第四階段） | Prometheus + Grafana（Metrics） |
| 日誌匯流（第四階段） | Loki + Promtail（集中式日誌） |
| 部署 | Docker / Docker Compose |
| 反向代理（對外 HTTPS） | **Nginx**（TLS 終止、`upstream` 至 backend API；靜態資源與 API 分流） |
| 韌體開發 | Pico SDK + CMake + arm-none-eabi-gcc |
| 燒錄除錯 | OpenOCD + Debug Probe |

### 2.1A Nginx 反向代理與客戶遠端檢視（規模：約 500 人）

**目標**：讓**客戶**透過網際網路以瀏覽器**安全檢視**儀表與歷史資料（**約 500 名使用者帳號**規模），**不**將 backend API、PostgreSQL、Mosquitto 管理埠直接暴露於公網。

**分階段實作**：**Nginx** 與 **JWT** 依韌體 **階段一至三** 拆入並行工作項，見 **§6.0**（對齊階段一～三）；**第四階段** 做憑證自動更新、全鏈路壓測與監控調優，見 **§6.0.2**。

#### 架構角色

| 層級 | 職責 |
|------|------|
| **Nginx** | 對外 **443/TLS**；`server_name` 綁定正式網域；憑證（Let’s Encrypt 或企業 CA）更新流程；**`proxy_pass`** 至 backend API（REST）；React 建置產物可 **靜態根目錄** 或 `try_files` + SPA fallback，**gzip**／**gzip_static**；**HTTP/2**（`http2` on TLS）。 |
| **Backend API（ASP.NET Core）** | 僅監聽 **內網／loopback**（Docker 網路），由 Nginx 反向；**不可**單獨對外開放。 |
| **PostgreSQL / Mosquitto / PGAdmin** | **僅內網**；PGAdmin 僅管理 VPN 或 SSH 隧道；**客戶端不連 MQTT**。 |

#### 身分與授權（與對外服務配套）

- 客戶帳號／角色（例如僅能看所屬站臺／裝置）須由後端實作；建議 **JWT（Access + Refresh）** 或 **Session + HTTPS Cookie**，由 **Backend API** 簽發、驗證；Nginx **不**代為驗證 JWT，僅可選擇傳遞 `Authorization` 標頭。
- 管理員與客戶分流：管理 API 與客戶唯讀 API 可 **路徑前綴**分離（例如 `/api/v1/customer/...`），並於後端強制授權檢查。

#### 約 500 人規模之容量假設與計畫

- **名詞**：**約 500 人**指**註冊／可登入之客戶帳號數**；**同時在線**（並發連線、儀表輪詢）通常遠小於 500，需以 **壓力測試**訂出設計目標（例如峰值 **50～100 concurrent** 瀏覽會話，依輪詢間隔與查詢頻率調整）。
- **Nginx**：`worker_processes`／`worker_connections` 依 Pi 5 與壓測調整；對 API 設 **合理連線**與 **timeout**；可對 `/api/` 設 **limit_req**（防刷）與 **client_max_body_size**。
- **Backend API**：執行緒池與 Kestrel 併發設定需依實作驗證；**500 帳號**下 PostgreSQL **`max_connections`**、連線池、慢查詢索引（`(device_id, device_time)`）須一併檢核。
- **靜態資源**：React 打包檔 **長快取**（`Cache-Control` + 檔名 hash）；減少 Nginx→API 之不必要流量。
- **若壓測未達標**：優先 **橫向擴充**（多臺後端 + Nginx `upstream` 多節點）、或 **靜態與 API 分離**（CDN／物件儲存託管前端）、或 **雲端託管** API／DB；單一 Pi 5 作為**唯一**入口時，**必須**以壓測報告驗收。

#### 部署與交付

- Nginx 以 **Docker Compose** 與既有服務同棧，或 **主機 systemd + nginx**（與容器內 backend API 橋接網路）；正式環境需 **防火牆** 僅放行 **80/443**（HTTP 可重定向至 HTTPS）。
- **驗收**：由外網僅能連 **HTTPS 儀表**；`curl` 內網服務埠無法從公網直連；以工具模擬 **目標並發** 之 API 回應時間與錯誤率達標。

### 2.1B Clean Architecture（前後端）

**原則**：依賴僅指向內層；**領域／用例**不依賴 ASP.NET Core、HTTP、PostgreSQL、React 等細節；外層透過介面與內層銜接。

#### 後端（ASP.NET Core／.NET 8）

| 層級 | 職責（示例） |
|------|----------------|
| **Entity／Domain** | 領域模型、遙測／事件／授權不變式；**不**依賴框架或驅動。 |
| **Use Cases** | 應用服務：ingest 寫入、查詢、JWT 簽發與驗證流程、授權（角色／站臺範圍）；僅依賴 Domain 與 **Repository／Clock 等介面**。 |
| **Interface Adapters** | HTTP 端點（Controller/Endpoint 轉呼叫用例）、MQTT ingest 轉呼叫用例、Repository 實作（PostgreSQL）、JWT 適配器。 |
| **Frameworks & Drivers** | ASP.NET Core、Npgsql、MQTTnet、Serilog、密碼雜湊／JWT 函式庫；僅在**最外層**組裝與注入。 |

**要點**：MQTT ingest 與 REST **共用同一套用例**與儲存抽象，避免業務邏輯重複在 callback 中。

#### 前端（React）

| 層級 | 職責（示例） |
|------|----------------|
| **Domain** | 型別、遙測／事件／使用者範圍等業務語意；**不**依賴 React Router 或 fetch。 |
| **Application** | 用例／hooks（例如載入遙測、登入、刷新 Token）、認證狀態；依賴 **Repository 介面**（由外層實作）。 |
| **Infrastructure** | HTTP client、API 基底 URL、Bearer 注入、錯誤對應。 |
| **Presentation** | 頁面、元件、路由；僅呼叫 Application 層。 |

**要點**：元件不直接拼 URL；**儀表與登入** 經同一用例邊界，便於測試與替換。

### 2.2 硬體清單（彙整 `@pi/`、`@iot/` 全部圖片）

#### 核心設備

| 項目 | 數量 | 備註 |
|---|---:|---|
| Raspberry Pi 5 16GB | 1 | 生產主機 |
| Raspberry Pi Pico 2 WH | 1 | 韌體端裝置 |
| Raspberry Pi Debug Probe | 1 | SWD 燒錄與除錯 |

#### Pi 平台配件

| 項目 | 數量 |
|---|---:|
| Pi 5 PCIe to M.2 轉接板 | 1 |
| M.2 2230 SSD 512GB | 1 |
| 官方 27W USB-C 電源 | 1 |
| 官方 45W USB-C 電源 | 1 |
| Pi 5 Active Cooler | 1 |
| Pi 5 Bumper | 1 |
| 官方 15.6 吋顯示器 | 1 |
| 官方 Micro HDMI to HDMI 線 | 1 |
| 官方 USB 3 Hub | 1 |
| Pi 5 RTC 電池 | 1 |
| Pi 5 PCIe 16pin/0.5mm/70mm 排線 | 5 |

#### 感測 / 顯示 / 控制模組（重掃補齊）

| 項目 | 數量 | 介面 | 備註 |
|---|---:|---|---|
| 1.3" OLED (SH1106, 128x64) | 1 | I2C | 主顯示建議，Column Offset +2 |
| LCD1602 I2C（白底黑字、3.3V 版） | **2** | I2C | **一片**：Pico **I2C1** 與 DS3231／AT24C256 並聯；**另一片**：Pi 5 **I2C1**（經 T 型擴充板，見 **§2.6**）。位址多為 `0x27/0x3F`（以掃描為準） |
| KY-016 共陰 RGB LED 模組（3.3V） | 1 | GPIO | **已購買**；僅接 **Pi 5**（BCM `GPIO17/27/22` → R/G/B），見 **§2.6** |
| BME680 | 1 | I2C | 溫濕度/氣壓/VOC |
| TSL2561 光照感測器 | 1 | I2C | 數位光照 |
| Grove Gesture（PAJ7620U2） | 1 | I2C | 手勢切頁 |
| Grove IMU 9DOF（MPU-9250） | 1 | I2C / SPI | 預設 0x68 |
| AT24C256 EEPROM | 1 | I2C | 斷線暫存 |
| DS3231 + AT24C32 RTC 模組 | 2 | I2C | RTC 位址 0x68 |
| INA219 數位功率計（Gravity） | 1 | I2C | 電源電流/電壓監測 |
| **Grove Mini PIR** 人體運動 | **1** | GPIO | **已購買**（`iot/2026-04-05 012821.png`）；**Pico `GPIO6`** 數位輸入，見 **§2.4.1** |
| Sensirion **SCD41**（CO₂／溫／溼） | **1** | I2C | **已購買**（同上）；**`0x62`**，**Pico `I2C1`**（Hub 已滿），見 **§2.4.1** |
| Grove-MOSFET 模組 | 1 | GPIO/PWM | 風扇 PWM 控速 |

### 2.2A 去重後推薦實作清單（主選）

| 功能類別 | 主選（建議先實作） | 選型理由 |
|---|---|---|
| 環境感測 | **BME680** + **SCD41（CO₂／溫溼，已採購）** | BME680 保留氣壓/VOC；**SCD41** 補 **CO₂** 與參考溫溼（**I2C1 `0x62`**），韌體須 **分欄位**避免混用。 |
| 光照感測 | **TSL2561（I2C）** | I2C 便於與既有 Dual-Bus 架構整合。 |
| 主顯示 | **1.3" OLED（SH1106）** | 1.3 吋可讀性較佳；已在文件內有 SH1106 修正。I2C0 Hub 原 Port2（0.96 OLED）已改接 MPU。 |
| 字元顯示 | **LCD1602 I2C（白底黑字、3.3V）×2**（Pico 一片、Pi 5 一片） | 兩片皆為 3.3V 背包；Pi 端字元顯示見 **§2.6**。 |
| 狀態燈 | **KY-016 共陰 RGB（已購買，接 Pi 5）** | 以 3.3V GPIO 指示主機／容器狀態；接線見 **§2.6**。 |
| 互動輸入 | **PAJ7620U2 手勢** | 先用非接觸切頁。 |
| 時間基準 | **DS3231 RTC** | 斷網場景仍可提供穩定時間戳。 |
| 非揮發儲存 | **AT24C256 EEPROM** | AT24C256 容量較大，適合斷線快取。 |
| 振動監測 | **MPU-9250** | 高階閉迴路控制核心來源。 |
| 告警輸出 | **Grove-MOSFET（PWM）** | MOSFET 可做比例控制（風扇/散熱）。 |
| I2C 佈線 | **6-Port I2C Hub + Dual-Bus** | 先用 Dual-Bus；同位址衝突再上 MUX。 |

> 建議最小可行組合（MVP）：`BME680 + TSL2561 + SH1106 + MPU-9250(I2C0) + DS3231 + AT24C256 + PAJ7620U2 + MOSFET`，先跑通 I2C0/I2C1、顯示、IMU、快取、告警主線（字元顯示以 LCD1602 為輔）。

> **KY-016（已購買）** 作為 Pi 5 端容器／系統狀態燈；接線與 GPIO 見 **§2.6**。

### 2.2B 重複項目與虧損估算（移至獨立檔）

- 重複清單、非重複清單與虧損估算已移至：`Hardware_Inventory_去重與虧損評估.md`
- 本版自 **§2.2** 主清單移除之重複/備選項目：`BME280（CJMCU-280E）`、`Grove BME280`、`Grove Light Sensor v1.2`、`Grove LCD RGB Backlight`、`PIR 人體移動感測器（一般模組）`、`Grove LED Bar v2.0`、`旋轉編碼器模組`、`Grove 蜂鳴器（壓電）`、`主動式蜂鳴器驅動模組`、`單路 5V 繼電器模組`
- 目前可視虧損估算：`NT$1,300`（以重複採購且未使用為前提）

### 2.3 連接與原型材料

| 項目 | 數量 | 用途 |
|---|---:|---|
| Grove I2C Hub (6-port) | 1 | I2C0 匯流分接（**1 孔常為電源，僅 5 孔可掛 I2C**；LCD1602 改接 I2C1） |
| TCA9548A 8 通道 I2C 多工器 | 1 | 同位址設備隔離（擴充方案） |
| 四通道邏輯電平轉換 | 1 | 3.3V/5V 相容 |
| Pi 5 PCIe 排線（16pin/0.5mm/70mm） | 5 | Pi 5 PCIe 轉接 |
| Grove 4pin 通用線（無扣/母母，20cm） | 2 包（10 條） | Grove 佈線 |
| Grove→DuPont 4x1p 母頭線（30cm） | 1 包（5 條） | Grove 對接一般模組 |
| Grove→DuPont 4x1p 公頭線（20cm） | 2 包（10 條） | Grove 對接一般模組 |
| **Grove→DuPont 4×1p 公頭線（20 cm，5 條／包，Seeed）** | **1 包** | **已購買**（`iot/2026-04-05 012821.png`）；供 **SCD41／Mini PIR** 接麵包板 |
| Grove ↔ DuPont 線材（多種） | 多包 | 快速布線 |
| T 型 GPIO 擴充板 + 排線 + 麵包板 | 1 組 | 原型開發 |
| ProsKit BX-4112N 免焊麵包板（840 圓孔，可拼接） | **2** | **已購買**（PChome `DEDG2H-A900B35AV`，2026-04-05）；與硬體計畫 **§2.5.3** 雙板並接圖對齊 |
| ProsKit 8PK-AS07-1 防靜電工作布 | **1** | **已購買**（PChome `DQBBFN-A900HX9DY`，2026-04-05）；ESD 工作面，須 **接地＋手環** |
| 麵包板電源模組 | 2 | 分軌供電 |
| Micro SD 讀寫模組（SPI） | 2 | 本地儲存/記錄 |
| Mini USB 資料線（30cm） | 2 | 模組連接 |
| 40P 杜邦線（母對公） | 2 組 | 通用連接 |
| 40P 杜邦線（公對公） | 2 組 | 通用連接 |
| 金屬膜電阻組（1%，600pcs） | 1 組 | 分壓/限流 |
| 12x12x5mm 按鍵（5 顆/包） | 2 包 | 人機輸入 |

### 2.4 Dual-Bus 最優規劃（Pico 2 WH）

- **I2C0（高速互動）**：1.3" OLED(SH1106)、Grove MPU-9250、BME680、TSL2561、PAJ7620U2（經 6-Port Hub；**5 孔 I2C + 1 孔電源**）— **已滿**，**SCD41 不可接 Hub**。  
- **I2C1（控制/儲存/字元顯示/CO₂）**：DS3231、AT24C256、**LCD1602(3.3V，Pico 用)**、**SCD41（`0x62`）**  
- **數位輸入**：**Grove Mini PIR** → **`GPIO6`**  
- **Pi 5 第二片 LCD1602(3.3V)**：經 **T 型擴充板**接 **I2C1**（與 Pico 端為**不同實體模組**），見 **§2.6**  
- **PWM 控制腳**：Grove-MOSFET 採 `GPIO16`
- **相容優先原則**：Hub 埠數不足同時掛 LCD 時，**Pico 端 LCD1602 固定改接 I2C1**（與 RTC／EEPROM 並聯，位址通常不衝突）；**SCD41 同接 I2C1**（見 **§2.4.1**）。

#### 建議腳位分配

| 功能 | Pico 2 WH 腳位 | 用途 |
|---|---|---|
| I2C0 SDA | GPIO4 | 連 6-Port I2C Hub SDA |
| I2C0 SCL | GPIO5 | 連 6-Port I2C Hub SCL |
| I2C1 SDA | GPIO2 | 連 DS3231 / AT24C256 / LCD1602 |
| I2C1 SCL | GPIO3 | 同上 |
| MOSFET PWM | GPIO16 | PWM 輸出 |
| **Grove Mini PIR** | **GPIO6** | 數位輸入（運動偵測） |
| 3V3 OUT | Pin 36 | 3.3V 主電源匯流 |
| GND | Pin 38（建議） | 系統共地 |

#### I2C 位址分配表（修正版）

| Bus | 裝置 | 典型位址 | 設計說明 |
|---|---|---|---|
| I2C0 | SH1106 OLED | `0x3C` | 高頻更新顯示 |
| I2C0 | MPU-9250（Grove） | `0x68`（典型；與 DS3231 分 bus 即可共存） | IMU / 振動監測，經 Hub |
| I2C1 | LCD1602 I2C（白底黑字, 3.3V） | `0x27`（或 `0x3F`，請掃描確認） | 除錯文字/告警；**不經 Hub** |
| I2C0 | BME680 | `0x76`（或 `0x77`） | 環境感測 |
| I2C0 | TSL2561 | `0x39` | 光照 |
| I2C0 | PAJ7620U2 | `0x73` | 手勢事件 |
| I2C1 | DS3231 | `0x68` | RTC（僅 I2C1） |
| I2C1 | AT24C256 | `0x50~0x57` | 快取儲存 |
| I2C1 | **SCD41** | **`0x62`** | CO₂／溫／溼；**已採購**；與 I2C0 裝置分離 |
| I2C1 + TCA9548A | （預留）同位址擴充 | 視模組而定 | 若未來同 bus 需掛多顆同位址裝置再啟用 |

#### I2C0 Hub 用途與 Port 分配（5 孔 I2C + 1 孔電源）

- **Hub 僅 5 個埠可接 I2C 裝置**（另 1 埠依板卡標示接電源）；**LCD1602 改接 I2C1**，與 PAJ7620 可同時使用。
- I2C 埠（實際孔位依線材插入為準，下為建議對應）：
  - 1.3" OLED (SH1106) - 主 UI
  - Grove MPU-9250 - IMU（典型 `0x68`，**不可**與 I2C1 的 DS3231 併接同一 bus）
  - BME680 - 環境感測
  - TSL2561 - 光照 Lux
  - PAJ7620U2 - 手勢事件
- 第 6 孔：**電源**（非 I2C 資料）

#### I2C1 並聯（與 Hub 無關）

- DS3231、AT24C256、**LCD1602**（gesture／last_update 等除錯字串）、**SCD41（`0x62`）**

> 關鍵檢查點：`DS3231` 與 `MPU-9250` 預設皆可為 `0x68`，須靠 **I2C0 / I2C1 分離**；LCD 典型 `0x27/0x3F` 與 RTC／EEPROM 通常不衝突，以 i2c scan 為準；**SCD41 固定 `0x62`**，與上述 **不衝突**。

### 2.4.1 `iot/2026-04-05 012821.png`：採購、接線位置與實作步驟

**採購內容**：**SCD41**×1、**Grove Mini PIR**×1、**Grove→DuPont 公頭線 20 cm**×1 包（5 條）。

**接線位置（Pico 2 WH）**

| 訊號 | Pico 接點 | 說明 |
|------|-----------|------|
| SCD41 `SDA`／`SCL` | `GPIO2`／`GPIO3`（**I2C1**） | 與 **§2.4** I2C1 匯流排並聯 |
| SCD41 `VCC`／`GND` | `Pin36` 3.3V 軌／`Pin38` GND | 依模組僅能 3.3 V 時 **禁止誤接 5 V** |
| Mini PIR `SIG` | **`GPIO6`** | 數位輸入；其餘 `VCC`／`GND` 接 3.3V 與 GND（依模組標示） |

**Grove→DuPont 公頭線（本採購 1 包 5 條）**：將 **Grove 母座**轉成 **公針**插入麵包板，再與 **GPIO2／3／6** 及電源軌對接；**SCD41** 建議用 **一條 4 線**（VCC/GND/SDA/SCL），**Mini PIR** 建議用 **一條 3 線**（VCC/GND/SIG），色線以包裝對照為準。

#### 接線確認清單（上電前／除錯時勾選）

> 目的：確認 **`iot/2026-04-05 012821.png`** 三項採購已依本專案 **Dual-Bus** 規劃接入，**未**誤占 I2C0 Hub 或錯接電壓。

- [ ] **SCD41** 之 **SDA／SCL** 僅併入 **I2C1（GPIO2／GPIO3）**，與 **§2.4「I2C1 並聯」** 同一匯流排（DS3231／AT24C256／LCD1602），**未**接至 Hub 或 I2C0。
- [ ] **SCD41** **VCC** 為 **3.3 V**（麵包板紅軌自 `Pin36`），絲印若標 **僅 3.3 V** 則 **禁止**誤接 5 V。
- [ ] **Grove Mini PIR** 之 **SIG** 接 **GPIO6**；**VCC／GND** 與 Pico **共地**（`Pin38` 負軌），供電電壓依模組標示（**優先 3.3 V** 與 I2C 同參考）。
- [ ] **I2C0 Hub** 仍僅掛 **五孔 I2C + 一孔電源**；**SCD41 未**插入 Hub 任一孔。
- [ ] 上電後 **I2C1** 掃描可見 **`0x62`**（模組已接線且韌體／scan 模式允許時）；若無：先查 **SDA/SCL 是否誤接 GPIO4/5**、**共地**、**3.3 V**。
- [ ] **PIR**：靜止與觸發時 **GPIO6** 位準／事件可區分（依模組為高有效或低有效；必要時對照模組說明與上拉設定）。

**實作步驟（摘要）**：與 `Pico2WH_Hardware_Plan_v3.md` **§2.9** 一致——（1）ESD 與共地；（2）Grove 線對接麵包板；（3）上電前短路／極性檢查；（4）`i2c` 掃描確認 **`0x62`**；（5）Sensirion **SCD4x** 初始化與週期讀值；（6）**GPIO6** 讀取 PIR 或 IRQ；（7）**同一 `telemetry` topic** 之 JSON 依 **§2.4.2** 填入 **`co2_ppm`**／**`temperature_c_scd41`**／**`humidity_pct_scd41`**／**`pir_active`**，ingest 與 **§6.0.1** 圖表 API 對齊。  
**與 BME680**：溫溼度可能重覆，韌體與儀表應 **標註感測器來源**（分鍵見 **§2.4.2**）；BME680 仍保留 **氣壓／VOC** 差異化。

> **遙測／MQTT 契約**（不因匯流排上新增 SCD41 而另開 topic）：見 **§2.4.2**，並與 **§6.0**、**§6.0.1** 對齊。

### 2.4.2 遙測 MQTT Topic、JSON 鍵與資料庫（與 §6.0 對齊；SCD41／PIR）

本節銜接 **§2.4** 之**硬體分工**與 **§6.0** 之**後端 ingest／REST**，避免「只在硬體章見 I2C、只在 §6 見欄位」造成實作分歧。

#### MQTT Topic（加入 SCD41／PIR 後**不變**）

- **SCD41（I2C1 `0x62`）** 與 **Grove Mini PIR（GPIO6）** **不新增** MQTT topic、**不改** topic 層級模板。  
- 韌體仍將感測值置於既有 **`telemetry`** 類訊息，後端訂閱樣式為 **`iiot/+/+/telemetry`**（**`site_id`／`device_id`** 由路徑中 `+` 匹配）；過渡期 **舊 topic 雙讀** 依 `Stage1-Stage3_Implementation_Roadmap_with_MQTT_Topic_Standard.md`。  
- **語意**：Dual-Bus 僅影響 **Pico 上哪條線讀到資料**；**MQTT 路由** 仍由 **站臺／裝置** 決定，**不由**「是否掛 SCD41」分支。

#### `telemetry` JSON：與 BME680 並存時之**分鍵**（v5 約定）

| 來源（§2.4 硬體） | JSON 鍵（建議欄位名） | 型別 | 說明 |
|---|---|---:|---|
| **SCD41** | **`co2_ppm`** | number | CO₂（ppm）；啟用 SCD41 時**應**送出 |
| **SCD41** | **`temperature_c_scd41`** | number | 與 BME 並存時**必**分鍵，避免與下方混淆 |
| **SCD41** | **`humidity_pct_scd41`** | number | 同上 |
| **BME680**（I2C0） | **`temperature_c`**、**`humidity_pct`** | number | 沿用既有環境主線鍵名（若專案早期已用 `temperature_c_bme` 等，**全專案只保留一組 BME 鍵**，並寫入對照表） |
| **Grove Mini PIR** | **`pir_active`** | boolean | **建議主鍵**（是否偵測到運動／高有效依模組，韌體統一為 bool 語意） |
| **相容** | **`motion`** | boolean | **可選別名**；若與 `pir_active` 並存，**須同語意**（建議新韌體只送 `pir_active`） |

**ingest 規則**：上述鍵皆為 **可選**；舊韌體無 SCD41／PIR 時，對應鍵缺省。後端 **不得**因多出未知鍵而丟棄整筆遙測（可記錄未知鍵供除錯）；**`GET /api/v1/telemetry/series`** 之 **`metrics`** 查詢鍵名須與上表**一致**（**§6.0.1**）。

#### 資料庫（PostgreSQL）

- **建議**：遙測以 **`device_id` + `device_time` + 承載欄位** 儲存；承載可為 **JSONB**（彈性合併 payload），或 **JSONB + 可空生成欄位**（便於 `co2_ppm` 等索引）。  
- **SCD41 啟用前後**：同一表結構應相容 **缺欄**（NULL 或 JSON 無該鍵）；遷移時補 **可空** 欄位或 JSONB path 索引，**無須**為 SCD41 **新增資料表**。  
- **與 Pi 5 周邊**：Pi 端周邊資料屬主機本地路徑，**不**寫入 Pico **`telemetry`** topic；與 **§2.4** Pico **SCD41** 無 DB 層級衝突。

### 2.5 方案 A 電力管理（3.3V 統一 + 共地）

- 由 `Pin 36 (3V3 OUT)` 輸入麵包板 3.3V 正電軌，再分配到兩條 I2C bus 所有模組。  
- 由 `Pin 38 (GND)` 連到麵包板藍色負電軌，全部設備共地（`GND Common`）。  
- 5V 負載（繼電器/外部風扇）可獨立供電，但控制地必須與 Pico 共地。  
- 任一 I2C 模組與 OLED 建議固定使用 3.3V，避免電位混雜。
- LCD1602 I2C 已採用你描述的 3.3V 版；仍請在上電前核對背面 `VCC/GND`，並以 i2c scan 確認 SDA/SCL 不會被拉死。

### 2.6 Pi 5 + T 型擴充板：LCD1602（3.3V）+ KY-016（已購買）+ Grove LCD RGB（可選）

> **分工**：`LCD1602（白底黑字、3.3V）` **共兩片**——**Pico** 端一片接 **I2C1**（與 DS3231／AT24C256 並聯，見 **§2.4**）；**Pi 5** 端一片經本節 **T 型擴充板**接 **I2C1**，供主機／服務狀態字元顯示。**KY-016 共陰 RGB（3.3V，已購買）** 僅接 **Pi 5 GPIO**，不占用 Pico 腳位。  
> **Grove LCD RGB Backlight（5V）** 仍為**可選**，需電平轉換；若與 Pi 端 **LCD1602** 同時使用 **I2C1**，須 **擇一**或加 **TCA9548A** 等多工器。

#### 2.6.1 麵包板連接表（主路徑：Pi 5 + LCD1602 + KY-016）

| 元件 | Pi 5 / T 型擴充板接點 | 模組接點 | 說明 |
|---|---|---|---|
| Pi 5 | T 型擴充板 40-pin 母座 | — | 排線將 Pi 5 GPIO 導至麵包板中線 |
| LCD1602 I2C（3.3V，**Pi 用**） | GPIO2 (SDA1)、GPIO3 (SCL1) | SDA、SCL | **直連** 3.3V 背包；與 Pico 端 LCD **不同實體模組** |
| | 3V3 | VCC | 依背包標示接 3.3V |
| | GND | GND | 與 Pi / 負電軌共地 |
| KY-016 共陰 RGB（**已購買**） | **GPIO17**、**GPIO27**、**GPIO22**（BCM） | R、G、B | 共陰；依模組標示確認腳序；必要時串限流電阻 |
| | GND | GND（共陰 / K） | 共地 |
| | 3V3 | VCC | 依模組標示（3.3V 版） |

#### 2.6.2 ASCII 接線圖（Pi 5 + T 型擴充板 + LCD1602 + KY-016）

```text
Pi 5 (40-pin header)
      ||
      ||  ribbon / 排線
      \/
+---------------------------+
| T-type GPIO expansion     |
| (on breadboard center)    |
+-----+-----+-----+----+----+
      |     |     |    |
     3V3   GND   SDA1 SCL1   GPIO17/27/22 (BCM)
      |     |     |    |         |
      |     |     |    |         +---> KY-016 R / G / B
      |     +-----+----+------------- KY-016 GND (common cathode)
      +-----------+------------------ KY-016 VCC (3.3V, 依模組)
      |
      +---> LCD1602 (3.3V backpack) VCC / GND / SDA / SCL
           (I2C1: GPIO2=SDA1, GPIO3=SCL1；與 Pico 端 LCD1602 為第二片、獨立配線)
```

#### 2.6.2A 可選：Grove LCD RGB Backlight（5V，與 Pi 端 LCD1602 擇一或加 MUX）

| 元件 | Pi 5 / T 型擴充板接點 | Grove LCD RGB 接點 | 說明 |
|---|---|---|---|
| 電源 | 5V | VCC | Grove RGB LCD 依規格採 5V 供電 |
| 地線 | GND | GND | 共地 |
| I2C SDA | GPIO2 (SDA1) | SDA | **與 Pi 端 LCD1602 同占 I2C1** → 擇一或 MUX |
| I2C SCL | GPIO3 (SCL1) | SCL | 經邏輯電平轉換器（3.3V↔5V） |

```text
Pi 5 GPIO Header
      ||
      || (40-pin ribbon)
      \/
+--------------------------+
| T-type GPIO expansion    |
| on breadboard center     |
+---+-----+-----+----------+
    |     |     |
   3V3   GND   I2C1(SDA/SCL)
    |     |       |
    |     |       +------------->[Level Shifter]------> Grove LCD RGB SDA/SCL
    |     +------------------------------> Grove LCD RGB GND
    +------------------------------------> Grove LCD RGB VCC (5V)
```

#### 2.6.3 顯示邏輯規格

- **主路徑（Pi 5 LCD1602 + KY-016）**：`LCD1602` 兩行文字建議與 **§2.6.2 ASCII** 之 Docker 摘要一致（第 1 行容器名稱、第 2 行 `IP:...`）；**KY-016** 以 **R/G/B** 表示狀態（綠／黃／紅），與下列規則對齊。
- **可選（Grove LCD RGB）**：需 `5V` 供電，且 I2C 必須經電平轉換器；**與 Pi 端 LCD1602 同占 I2C1 時須擇一或加 MUX**。  
- **RGB 顏色規則**（KY-016 與 Grove RGB 背光可擇一或並用，但 Grove 與 LCD1602 擇一時再啟用）：
  - 綠色：有 running 容器（Docker API 正常）
  - 黃色：Docker 正常但無 running 容器
  - 紅色：Docker Socket 失敗或解析失敗
- **LCD 兩行文字**（Pico 與 Pi 各一片時，驗收分開；字串格式可一致）：
  - 第 1 行：容器名稱（16 字元裁切）
  - 第 2 行：`IP:<container_ip>`（無 IP 顯示 `IP:N/A`）

#### 2.6.4 C 範例程式（可在 WSL dry-run / Pi 5 實機）

> 下列程式為 **Grove LCD RGB Backlight**（`0x3e`／`0x62`）示範。若改為 **Pi 5 端標準 LCD1602 背包（PCF8574，典型 `0x27/0x3F`）**，需改用對應初始化與寫入；**KY-016** 以 **R/G/B GPIO**（`GPIO17/27/22`）另行驅動。

```c
// file: docker_lcd_status.c
#include <errno.h>
#include <fcntl.h>
#include <linux/i2c-dev.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/ioctl.h>
#include <unistd.h>
#include <curl/curl.h>
#include <cjson/cJSON.h>

#define LCD_TEXT_ADDR 0x3e
#define LCD_RGB_ADDR  0x62

typedef struct { char *buf; size_t len; } mem_t;
static size_t on_write(void *p, size_t s, size_t n, void *u) {
  size_t bytes = s * n; mem_t *m = (mem_t *)u;
  char *np = realloc(m->buf, m->len + bytes + 1); if (!np) return 0;
  m->buf = np; memcpy(m->buf + m->len, p, bytes); m->len += bytes; m->buf[m->len] = 0;
  return bytes;
}
static int i2c_wr(int fd, int addr, unsigned char reg, unsigned char val) {
  if (ioctl(fd, I2C_SLAVE, addr) < 0) return -1;
  unsigned char b[2] = {reg, val}; return (write(fd, b, 2) == 2) ? 0 : -1;
}
static int lcd_cmd(int fd, unsigned char c) { return i2c_wr(fd, LCD_TEXT_ADDR, 0x80, c); }
static int lcd_dat(int fd, unsigned char c) { return i2c_wr(fd, LCD_TEXT_ADDR, 0x40, c); }
static void lcd_line(int fd, int line, const char *s) {
  lcd_cmd(fd, line ? 0xC0 : 0x80);
  for (int i = 0; i < 16; i++) lcd_dat(fd, (i < (int)strlen(s)) ? s[i] : ' ');
}
static void lcd_init(int fd) {
  usleep(50000); lcd_cmd(fd,0x38); lcd_cmd(fd,0x39); lcd_cmd(fd,0x14);
  lcd_cmd(fd,0x70); lcd_cmd(fd,0x56); lcd_cmd(fd,0x6C); usleep(200000);
  lcd_cmd(fd,0x38); lcd_cmd(fd,0x0C); lcd_cmd(fd,0x01); usleep(2000);
}
static void rgb_set(int fd, unsigned char r, unsigned char g, unsigned char b) {
  i2c_wr(fd, LCD_RGB_ADDR, 0x00, 0x00); i2c_wr(fd, LCD_RGB_ADDR, 0x01, 0x00);
  i2c_wr(fd, LCD_RGB_ADDR, 0x08, 0xAA); i2c_wr(fd, LCD_RGB_ADDR, 0x04, r);
  i2c_wr(fd, LCD_RGB_ADDR, 0x03, g);    i2c_wr(fd, LCD_RGB_ADDR, 0x02, b);
}
static int docker_json(char **out) {
  CURL *c = curl_easy_init(); if (!c) return -1; mem_t m = {0};
  curl_easy_setopt(c, CURLOPT_UNIX_SOCKET_PATH, "/var/run/docker.sock");
  curl_easy_setopt(c, CURLOPT_URL, "http://localhost/containers/json?all=0");
  curl_easy_setopt(c, CURLOPT_WRITEFUNCTION, on_write); curl_easy_setopt(c, CURLOPT_WRITEDATA, &m);
  CURLcode rc = curl_easy_perform(c); curl_easy_cleanup(c);
  if (rc != CURLE_OK) { free(m.buf); return -2; } *out = m.buf; return 0;
}
int main(int argc, char **argv) {
  bool dry = (argc > 1 && strcmp(argv[1], "--dry-run") == 0);
  unsigned char r=255,g=0,b=0; char l1[17]="docker error", l2[17]="IP:N/A";
  char *json = NULL; int st = docker_json(&json);
  if (st == 0) {
    cJSON *arr = cJSON_Parse(json);
    if (arr && cJSON_IsArray(arr) && cJSON_GetArraySize(arr) > 0) {
      cJSON *o = cJSON_GetArrayItem(arr, 0), *names = cJSON_GetObjectItem(o, "Names");
      cJSON *n0 = (names && cJSON_IsArray(names)) ? cJSON_GetArrayItem(names, 0) : NULL;
      const char *nm = (n0 && cJSON_IsString(n0)) ? n0->valuestring : "unknown"; if (nm[0]=='/') nm++;
      snprintf(l1, sizeof(l1), "%-16.16s", nm);
      const char *ip = "N/A";
      cJSON *ns = cJSON_GetObjectItem(o, "NetworkSettings");
      cJSON *nets = ns ? cJSON_GetObjectItem(ns, "Networks") : NULL;
      if (nets && cJSON_IsObject(nets) && nets->child) {
        cJSON *ipj = cJSON_GetObjectItem(nets->child, "IPAddress");
        if (ipj && cJSON_IsString(ipj) && ipj->valuestring[0]) ip = ipj->valuestring;
      }
      snprintf(l2, sizeof(l2), "IP:%-13.13s", ip); r=0; g=255; b=0;
    } else { snprintf(l1,sizeof(l1),"%-16.16s","no container"); r=255; g=180; b=0; }
    cJSON_Delete(arr);
  }
  free(json);
  if (dry) { printf("RGB=(%u,%u,%u)\nLCD1=%s\nLCD2=%s\n", r,g,b,l1,l2); return 0; }
  int fd = open("/dev/i2c-1", O_RDWR); if (fd < 0) return 2;
  lcd_init(fd); rgb_set(fd, r,g,b); lcd_line(fd, 0, l1); lcd_line(fd, 1, l2); close(fd); return 0;
}
```

#### 2.6.5 WSL / Pi 5 測試方式

**WSL（dry-run）**

```bash
sudo apt update
sudo apt install -y build-essential libcurl4-openssl-dev libcjson-dev
gcc -O2 -Wall docker_lcd_status.c -o docker_lcd_status -lcurl -lcjson
./docker_lcd_status --dry-run
```

**Pi 5（實機 I2C）**

```bash
sudo apt update
sudo apt install -y build-essential libcurl4-openssl-dev libcjson-dev i2c-tools
sudo raspi-config nonint do_i2c 0
sudo usermod -aG i2c,docker "$USER"
# 重新登入後
i2cdetect -y 1
gcc -O2 -Wall docker_lcd_status.c -o docker_lcd_status -lcurl -lcjson
./docker_lcd_status
```

- `i2cdetect -y 1` 應可看到 `0x3e`（LCD 控制）與 `0x62`（RGB 背光）。
- 啟停容器後重跑，驗證 RGB 顏色與 LCD 內容是否依規格切換。

#### 2.6.6 systemd 開機自動刷新（建議）

**安裝執行檔**

```bash
sudo install -m 755 ./docker_lcd_status /usr/local/bin/docker_lcd_status
```

**建立 service：`/etc/systemd/system/docker-lcd-status.service`**

```ini
[Unit]
Description=Update Grove LCD RGB by Docker container status
After=network-online.target docker.service
Wants=network-online.target

[Service]
Type=oneshot
ExecStart=/usr/local/bin/docker_lcd_status
```

**建立 timer：`/etc/systemd/system/docker-lcd-status.timer`**

```ini
[Unit]
Description=Run docker-lcd-status every 10 seconds

[Timer]
OnBootSec=15s
OnUnitActiveSec=10s
Unit=docker-lcd-status.service

[Install]
WantedBy=timers.target
```

**啟用與驗證**

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now docker-lcd-status.timer
sudo systemctl start docker-lcd-status.service
systemctl status docker-lcd-status.timer
journalctl -u docker-lcd-status.service -n 50 --no-pager
```

- 若無法讀 Docker 或 I2C，請確認使用者已加入 `docker` 與 `i2c` 群組，並重新登入。

---

## 3. 系統基礎環境與開發套件

> 本章為 **WSL2 / Pi 5 共通基礎層**。為避免重複，`3.1 OpenOCD` 與 `3.4 Pico SDK` 只保留專屬步驟。

### 3.0 基礎環境安裝

#### 3.0.1 核心編譯鏈

**WSL2 (Ubuntu 22.04+)**

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y \
  build-essential cmake ninja-build git pkg-config \
  curl wget ca-certificates gnupg lsb-release \
  libusb-1.0-0-dev libftdi-dev \
  automake autoconf texinfo libtool make
```

**Pi 5 (Raspberry Pi OS 64-bit)**

```bash
sudo apt update && sudo apt full-upgrade -y
sudo apt install -y \
  build-essential cmake ninja-build git pkg-config \
  curl wget ca-certificates gnupg lsb-release \
  libusb-1.0-0-dev libftdi-dev \
  automake autoconf texinfo libtool make
```

#### 3.0.2 系統與硬體驅動開發套件

> 對應硬體：Debug Probe、I2C 裝置（DS3231/BME680/SH1106）、USB 診斷。

**WSL2 (Ubuntu 22.04+)**

```bash
sudo apt install -y \
  libi2c-dev i2c-tools libbsd-dev eeprog usbutils
```

**Pi 5 (Raspberry Pi OS 64-bit)**

```bash
sudo apt install -y \
  libi2c-dev i2c-tools libbsd-dev eeprog usbutils
sudo usermod -aG i2c "$USER"
# 重新登入後生效（或先 newgrp i2c）

sudo raspi-config
# Interface Options -> I2C -> Enable
sudo reboot
```

**Pi 5 重開後檢查**

```bash
i2cdetect -l
sudo i2cdetect -y 1
```

#### 3.0.3 後端、安全與資料庫開發庫

> 對應需求：MQTT TLS 1.2、PostgreSQL 18、ASP.NET Core 後端、JSON Telemetry。  
> `libulfius` 等 C 套件為 Legacy 路徑，現行 ASP.NET Core 主線非必要。

**WSL2 (Ubuntu 22.04+)**

```bash
sudo apt install -y \
  libpq-dev libssl-dev mosquitto-clients libmosquitto-dev \
  libcjson-dev libjansson-dev libgnutls28-dev
```

**Pi 5 (Raspberry Pi OS 64-bit)**

```bash
sudo apt install -y \
  libpq-dev libssl-dev mosquitto-clients libmosquitto-dev \
  libcjson-dev libjansson-dev libgnutls28-dev

# PostgreSQL 18（PGDG 套件庫）安裝步驟（確保 `libpq-dev` 對齊 v18）
sudo install -d /etc/apt/keyrings
curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc | \
  sudo gpg --dearmor -o /etc/apt/keyrings/postgresql.gpg

echo "deb [signed-by=/etc/apt/keyrings/postgresql.gpg] http://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" | \
  sudo tee /etc/apt/sources.list.d/pgdg.list > /dev/null

sudo apt update
sudo apt install -y postgresql-18 postgresql-client-18 libpq-dev postgresql-server-dev-18
psql --version
```

#### 3.0.4 Docker 引擎部署

**WSL2 (Ubuntu 22.04+)**

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker "$USER"
newgrp docker

docker --version
docker compose version
```

**Pi 5 (Raspberry Pi OS 64-bit)**

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker "$USER"
newgrp docker

docker --version
docker compose version
```

#### 3.0.5 React 前端開發環境（Node.js 20 LTS）

**WSL2 (Ubuntu 22.04+) / Pi 5 (Raspberry Pi OS 64-bit)**

```bash
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt install -y nodejs
node -v
npm -v
```

---

### 3.1 OpenOCD 燒錄工具編譯（WSL / Pi 5）

> 已將共通編譯套件移到 §3.0；本節僅保留 OpenOCD 專屬步驟。

```bash
cd ~
git clone -b master --recursive --depth=1 https://github.com/openocd-org/openocd.git
cd openocd
./bootstrap
./configure --enable-cmsis-dap
make -j"$(nproc)"
sudo make install
openocd --version
```

### 3.2 自動燒錄設定（UDEV 規則，健壯化寫法）

```bash
sudo tee /etc/udev/rules.d/99-pico-debug.rules > /dev/null <<'EOF'
# Raspberry Pi Debug Probe (CMSIS-DAP)
ATTRS{idVendor}=="2e8a", ATTRS{idProduct}=="000c", MODE="0666", GROUP="plugdev"
# RP2 BOOTSEL (UF2)
ATTRS{idVendor}=="2e8a", ATTRS{idProduct}=="0003", MODE="0666", GROUP="plugdev"
EOF

sudo udevadm control --reload-rules
sudo udevadm trigger
sudo usermod -aG plugdev "$USER"
```

### 3.3 OpenOCD 燒錄語法（WSL / Pi 5）

```bash
openocd -f interface/cmsis-dap.cfg -f target/rp2350.cfg \
  -c "adapter speed 5000" \
  -c "init" \
  -c "program build/iiot_firmware.elf verify reset" \
  -c "shutdown"
```

### 3.4 Pico SDK 開發環境安裝（WSL / Pi 5）

> 已將 `git/cmake/build-essential` 移至 §3.0；本節僅保留 Pico 專屬工具鏈與 SDK。

```bash
sudo apt install -y gcc-arm-none-eabi libnewlib-arm-none-eabi libstdc++-arm-none-eabi-newlib

cd ~
git clone -b master --recursive https://github.com/raspberrypi/pico-sdk.git

echo 'export PICO_SDK_PATH=$HOME/pico-sdk' >> ~/.bashrc
source ~/.bashrc
```

### 3.5 WSL USB 穿透（Windows PowerShell 系統管理員）

```powershell
usbipd list
usbipd bind --busid <BUSID>
usbipd attach --wsl --busid <BUSID>
# 解除
usbipd detach --busid <BUSID>
```

---

## 4. CMake 與韌體架構更新

### 4.1 Pico 2 WH 韌體端（Dual-Bus + MQTT）

```cmake
cmake_minimum_required(VERSION 3.13)

if (NOT PICO_SDK_PATH)
    set(PICO_SDK_PATH $ENV{PICO_SDK_PATH})
endif()

set(PICO_BOARD pico2_w)
include(${PICO_SDK_PATH}/external/pico_sdk_import.cmake)

project(iiot_firmware C CXX ASM)
pico_sdk_init()

add_executable(iiot_firmware
    main.c
    lib/sh1106_driver.c
    lib/bme680_driver.c
    lib/tsl2561_driver.c
    lib/paj7620u2_driver.c
    lib/ds3231_driver.c
    lib/mpu9250_driver.c
    lib/at24c256_driver.c
    lib/mosfet_pwm.c
)

target_link_libraries(iiot_firmware
    pico_stdlib
    pico_lwip_mqtt
    pico_cyw43_arch_lwip_poll
    hardware_i2c
    hardware_gpio
    hardware_pwm
)

target_compile_definitions(iiot_firmware PRIVATE
    CYW43_LWIP=1
    I2C_DUAL_BUS=1
)

pico_enable_stdio_usb(iiot_firmware 1)
pico_enable_stdio_uart(iiot_firmware 0)
pico_add_extra_outputs(iiot_firmware)
```

### 4.2 SH1106 驅動修正說明（必要）

- 1.3" OLED 使用 `SH1106`，不可直接套 `SSD1306` 初始化。  
- 設定位址時需加 **Column Offset +2**，否則畫面左右位移/裁切。

```c
static inline void sh1106_set_pos(uint8_t page, uint8_t col) {
    uint8_t c = col + 2; // SH1106 offset correction
    oled_cmd(0xB0 | (page & 0x0F));
    oled_cmd(0x10 | ((c >> 4) & 0x0F));
    oled_cmd(0x00 | (c & 0x0F));
}
```

### 4.3 安全性警語（必讀）

- 1.3" OLED 雖標示 3.3V~5V，但本案統一接 3.3V。  
- 上電前**必須手動核對模組背面 `VCC/GND` 順序**；不同批次可能反序，接反會燒毀。

---

## 5. 完整接線 ASCII 拓樸圖（Dual-Bus + Power Rail）

**採購對齊**：Pico 端原型已採購 **ProsKit BX-4112N ×2**（可拼接）、**8PK-AS07-1**（防靜電工作布）、**`iot/2026-04-05 012821.png`**：**SCD41**、**Grove Mini PIR**、**Grove→DuPont 公頭線**。下列 **§5.1** 為**單一麵包板等效**拓樸（邏輯主軸，保留）；**§5.2** 為**兩片 BX-4112N 並接**後之同一電氣關係（實體分區）。細部佈線、跨片跳線與 **SCD41／PIR** 步驟見 `Pico2WH_Hardware_Plan_v3.md` **§2.5.3**、**§2.9**；規格書 **§2.4.1**。

### 5.1 單一麵包板等效拓樸（保留）

```text
                    +-----------------------+
                    | Raspberry Pi Debug    |
                    | Probe (SWD)           |
                    +-----------+-----------+
                                |
                          SWDIO/SWCLK/GND
                                |
+-------------------------------v--------------------------------+
|                       Pico 2 WH (RP2350)                       |
| I2C0: GPIO4(SDA0), GPIO5(SCL0)                                 |
| I2C1: GPIO2(SDA1), GPIO3(SCL1)                                 |
| PWM : GPIO16 -> Grove-MOSFET                                   |
| DIN : GPIO6  <- Grove Mini PIR                                 |
| PWR : Pin36(3V3 OUT) -> Breadboard 3.3V rail                   |
+-------------------+----------------------+----------------------+
                    |                      |
                I2C0 trunk             I2C1 trunk
                    |                      |
            +-------v-------+       +--------------------------------+
            | 6-Port Hub    |       | DS3231 (0x68)                  |
            | Port* SH1106  |       | AT24C256 (0x50~0x57)           |
            | Port* MPU     |       | LCD1602 (0x27/0x3F)            |
            | Port* BME680  |       | SCD41 CO2/T/RH (0x62)          |
            | Port* TSL2561 |       +--------------------------------+
            | Port* PAJ7620 |
            | +1 孔：電源   |
            +---------------+

3.3V rail: Pin36 -> all I2C modules / Hub / sensors
GND rail : Pin38 -> all modules + MOSFET control ground (GND Common)
GPIO16 PWM -----------------------------------------> Grove-MOSFET Gate
GPIO6  DIN <----------------------------------------- Grove Mini PIR SIG
```

### 5.2 雙 BX-4112N 並接後拓樸（電氣等效 §5.1）

> **BB-A／BB-B**：兩片 **BX-4112N** 側邊卡扣拼接；**紅／藍電軌**若於接縫處不導通，須 **手動跳線** 連通兩片之 **3.3V** 與 **GND**。**8PK-AS07-1** 鋪設於工作區後先完成 **接地**，再操作模組。

```text
     +3.3V 紅軌 --[跨縫跳線若需]--+3.3V 紅軌          Debug Probe (SWD)
           :                      :                       |
    +-------------+        +-------------+               |
    | BB-A        | 並扣   | BB-B        |          SWDIO/SWCLK/GND
    | Pico 跨溝    |        | 模組可分流  |               |
    | Hub / I2C0  |        | 或 I2C1 延伸 |               v
    | 主佈線區     |        | (依走線調整) |    +------------------------v--+
    +-------------+        +-------------+    | Pico 2 WH (RP2350)       |
           :                      :            | I2C0: GPIO4/5  I2C1: 2/3 |
     GND 藍軌 --[跨縫跳線若需]-- GND 藍軌      | PWM: GPIO16  DIN: GPIO6  |
                                              | PWR: Pin36/38            |
                                              +------------+-------------+
                                                           |
                    同 §5.1 邏輯匯流 ----------------------+
            +-------v-------+       +--------------------------------+
            | 6-Port Hub    |       | DS3231 / AT24C256 / LCD1602    |
            | + SH1106 等   |       | / SCD41 (0x62)                 |
            +---------------+       +--------------------------------+

3.3V / GND：Pin36／Pin38 分別接到 **兩片** 紅／藍軌（並確認接縫導通）。
I2C0／I2C1：SDA／SCL 以杜邦線連至各模組；跨片時沿同一匯流延伸即可。
GPIO16 PWM -> Grove-MOSFET；**GPIO6 -> Grove Mini PIR**（接線同 §5.3 補充說明於 **§2.4.1**）。
```

### 5.3 Grove-MOSFET（Pico 麵包板）接線圖（固定版）

> 說明：`Grove-MOSFET` 之 PWM 控制腳固定由 **Pico 2 WH GPIO16** 輸出；**不**使用 Pi 5 GPIO 直接驅動 MOSFET。

```text
Pico 2 WH on breadboard
  GPIO16 (PWM) -------------------------------> Grove-MOSFET SIG/Gate
  Pin38 (GND) --------------------------------> Grove-MOSFET GND
  External Load + Power ----------------------> Grove-MOSFET Load terminals
  Load GND -----------------------------------> Common GND (with Pico Pin38)
```

---

## 6. 階段式實作更新（Dual-Bus）

下列編號 **1～3** 為 **Pico 2 WH 韌體／硬體**主線。**Pi 5 後端／前端**請與之 **並行**開發，見 **§6.0**；**第四階段**見 **§6.0.2**。

### 6.0 階段並行：Pi 5 後端／前端（對齊階段一至三）

> **技術棧**：後端 ASP.NET Core Web API（.NET 8，四層架構）、PostgreSQL 18、Mosquitto（MQTT，TLS 1.2）；前端 React；部署 Docker／Compose。硬體計畫 §2.7 之 **C 監控程式**（Docker Unix Socket）與 **Redis / Prometheus+Grafana / Loki+Promtail** 屬 **第四階段**深化，見 **§6.0.2**。

> **架構**：前後端均採 **Clean Architecture**（**§2.1B**）。下列小節標題 **後端（Clean Architecture）**／**前端（Clean Architecture）** 表示該階段交付須依 **§2.1B** 分層實作（用例／轉接器／框架分離）。  
> **強制要求**：後端實作（MQTT ingest、REST、JWT、Logging）**必須**遵循 Clean Architecture；Domain / Use Cases **不得**直接依賴 Controller/HTTP 細節、MQTT client 或檔案 I/O 細節（以 Adapter 注入）。

下列為與 **韌體階段一至三**對齊之 **最小可行** 前後端步驟；可與 Pico bring-up **並行**，不必待韌體某階段全數完成才啟動 Pi 5。

**與 §2.4 之銜接（避免硬體／後端各說各話）**：**Dual-Bus** 決定 **SCD41 在 I2C1、PIR 在 GPIO6**；**MQTT topic 模板不因該二模組而改變**（見 **§2.4.2**）。**`telemetry` JSON 鍵名、DB 儲存與 `metrics` 查詢** 以 **§2.4.2** 為準，下列步驟僅列工作項。

### 6.0A 後端 Logging 架構（C 後端；不寫入 `/var/log/`）

> 本節為**架構規範**，供 `app/backend` 實作；本次僅更新文件，不新增程式碼。

#### 目標與路徑規範

- **禁止路徑**：不寫入 `/var/log/`。  
- **預設目錄**：`app/backend/logs/`。  
- **預設檔名**：`backend.log`。  
- **預設完整路徑**：`app/backend/logs/backend.log`（若部署腳本以專案根目錄為工作路徑，建議解析為相對專案根的路徑）。  

#### `config.json` 可控參數（由單一變數控管）

| 鍵名 | 型別 | 預設值 | 說明 |
|---|---|---|---|
| `log_file_path` | string | `app/backend/logs/backend.log` | 控管 log 目錄與檔名的單一變數；可改為其他相對或絕對路徑，但不得指向 `/var/log/`。 |
| `log_to_console` | boolean | `true` | 是否輸出到 Console。 |
| `log_to_file` | boolean | `true` | 是否輸出到檔案。 |
| `log_level` | string | `info` | 建議 `debug/info/warn/error`。 |
| `log_rotate_max_size_mb` | number | `10` | 單一 log 檔案上限（MB）。 |
| `log_rotate_mode` | string | `backup` | `backup`（超過上限轉存 `backend.log.1`）或 `overwrite`（覆蓋原檔）。 |

#### 啟動流程（目錄自動建立）

1. 後端啟動時先讀取 `config.json`。  
2. 解析 `log_file_path` 的父目錄（例如 `app/backend/logs/`）。  
3. 若目錄不存在則自動建立（含必要的中間層）。  
4. 建立／開啟 `backend.log` 後，再初始化 logger。  
5. 若檔案 logger 初始化失敗，仍保留 Console logger，並在 Console 印出錯誤原因。  

#### 輸出策略（Console + File）

- 可使用 **Yder**（建議）或 **`fopen`**（簡化）實作。  
- 要求同時輸出至 **Console** 與 **File**（由 `log_to_console` / `log_to_file` 控制）。  
- 每筆 log 至少含：`timestamp`、`level`、`module`、`message`。  
- **格式規範**：採 **Key-Value**（例如 `ts=... lvl=... mod=... msg="..."`），便於 `rg` / `grep` 篩選。  
- **容量防護**：單一檔案上限 **10MB**（`log_rotate_max_size_mb`）；超過時依 `log_rotate_mode` 執行 **`backend.log.1` 備份** 或 **覆蓋**。

#### MQTT `on_message_received` 必記錄欄位（對齊 §6.0 階段一）

- 在 `on_message_received` 進入點寫入一筆 **ingest log**，內容至少包含：  
  - `device_id`（由 topic 解析或 payload 取得）  
  - `topic`（完整 topic）  
  - `payload`（原始字串或安全截斷字串）  
- 建議格式（示意）：`[mqtt_ingest] device_id=<...> topic=<...> payload=<...>`。  
- `payload` 過長時可截斷並註記長度（避免單筆 log 過大），但需保留可追溯性。  
- 建議採 Key-Value 形式（示例）：`ts=... lvl=info mod=mqtt_ingest device_id=... topic="..." payload="..."`。

#### JWT 安全審計（對齊 §6.0 驗證路徑）

- JWT 驗證失敗時必須記錄：  
  - `src_ip`（來源 IP）  
  - `jwt_err_type`（`Expired` / `Invalid` / `Missing`）  
  - `endpoint`（例如 `/api/v1/telemetry`）  
- 建議格式（示例）：`ts=... lvl=warn mod=auth event=jwt_verify_failed src_ip=... jwt_err_type=Expired endpoint=/api/v1/...`。

#### 對齊階段一（雙總線／雙顯／遙測主線）

**後端（Clean Architecture）**
1. **執行環境**：Pi 5（或開發機）以 Docker Compose 啟動 Mosquitto、PostgreSQL、backend API 容器；網路與 volume 命名與本章部署一致。
2. **MQTT 訂閱與 ingest**：訂閱 `iiot/+/+/telemetry`（及過渡期舊 topic 雙讀，策略見 `Stage1-Stage3_Implementation_Roadmap_with_MQTT_Topic_Standard.md` §2.3）；將 payload 寫入遙測表，鍵含 `device_id`、`device_time`、`is_sync_back`。**韌體若已啟用 SCD41／PIR**，ingest 與 schema 須能保存 **§2.4.2** 所列之 **`co2_ppm`**、**`temperature_c_scd41`**、**`humidity_pct_scd41`**、**`pir_active`**（及可選 **`motion`** 別名）；舊韌體無該等鍵時以 **可空／缺鍵** 處理。  
2.1 **MQTT Logging（`on_message_received`）**：每次接收訊息都必須依 **§6.0A** 記錄 `device_id`、`topic`、`payload`，且輸出至 Console + `log_file_path` 指定檔案（預設 `app/backend/logs/backend.log`，非 `/var/log/`）。
2.2 **Logging 實作分層（必做）**：Logging 介面置於 Application/Use Cases 邊界，檔案/Console 寫入由 Infrastructure Adapter 實作；避免在 Domain 直接呼叫檔案 API。
3. **REST API**：提供 `/api/v1/telemetry`、`/api/v1/logs` 之分頁／篩選查詢；回傳欄位與韌體 JSON 約定一致。
3.1. **遙測歷程（圖表）**：實作 **`GET /api/v1/telemetry/series`**（查詢參數、JSON 形狀、授權、限流與 DB 索引要求見 **§6.0.1**），供前端折線圖使用；**`metrics`** 須支援 **§2.4.2** 之 **`co2_ppm`**、**`temperature_c_scd41`**、**`humidity_pct_scd41`**、**`pir_active`**（與既有 **`temperature_c`**／**`humidity_pct`** 等並列時，鍵名不重疊）。
4. **裝置與站臺**：若 topic 含 `site`、`device_id`，後端路由與日誌須可解析並記錄。
5. **TLS／憑證（可漸進）**：開發期可內網明文 MQTT；上線前收斂為 **TLS 1.2**（broker 與後端設定同步）。
6. **Nginx（本階段）**：Compose 納入 **Nginx**；`upstream` 指向 **backend API**（僅容器內網／loopback，不對公網直連 API 容器）；對外若需 HTTPS，於 Nginx 做 **TLS 終止**（自簽或 Let’s Encrypt，見 **§2.1A**）；`proxy_set_header Host / X-Forwarded-*` 正確傳遞。
7. **JWT（本階段）**：實作 **簽發／驗證** 基礎（建議 **Access Token** 為 JWT）；提供 **登入**端點（例如 `POST /api/v1/auth/login`）產生 Token；**backend API** 對 `/api/v1/telemetry`、`/api/v1/logs` 加上 **可選**驗證：開發環境可豁免，**預生產起**一律要求 `Authorization: Bearer <access>`（行為須在部署說明中明載）。**JWT 與 ingest 用例** 實作於 **Use Cases** 與 **Adapters**（**§2.1B**），避免邏輯寫死在 Controller callback 內。

**前端（Clean Architecture）**
1. **專案骨架**：React（Node.js 20 LTS）、環境變數區分 `dev`／生產 API base URL、MQTT **不**直接由瀏覽器連 broker（經後端 API 或 WebSocket 閘道，依專案安全策略）。
2. **儀表最小頁**：裝置／站臺選擇、最近遙測列表；並依 **§6.0.1** 納入 **至少一條時間序列圖表（簡圖）**（例如溫度或光照對時間，預設時間窗可為最近 24 小時）；錯誤與載入狀態可辨識。**加選**：勾選 **`co2_ppm`**（或 **§2.4.2** 其他已存鍵）之折線，或列表顯示 **CO₂／`pir_active`**；與 BME 溫溼度並列時 **標籤區分感測器**（**`temperature_c`** vs **`temperature_c_scd41`**）。
3. **JWT 串接**：登入後儲存 Access Token（建議 **memory** 或 **httpOnly Cookie** 策略二選一）；API 請求帶 **`Authorization: Bearer`**；401 時導向登入（Refresh 流程可延至階段二）。
4. **對齊階段一驗收**：Pico 上線後，經 **Nginx → HTTPS → backend API** 可查遙測；與 OLED／序列輸出時間序一致（允許延遲秒級）；**JWT** 驗證路徑可通（依環境是否強制 Bearer）；**圖表** 與列表資料來源一致（同一後端 series API，見 **§6.0.1**）；若韌體已送 SCD41／PIR，**列表／series** 可端到端核對。

#### 對齊階段二（手勢、`ui-events`、雙顯同步）

**後端（Clean Architecture）**
1. **訂閱**：`iiot/+/+/ui-events`（與路線圖 §2.2 後端訂閱策略一致）。
2. **持久化**：實作 `device_ui_events` 之 **ingest → repository → 查詢 API**；payload 含 `device_id`、`device_time`、手勢或頁面代碼等。
3. **去重與順序**：與遙測相同，以 `(device_id, device_time, channel)` 或專案約定去重。
4. **Nginx（本階段）**：確認 **`Authorization` 標頭**轉發至 backend API（`proxy_set_header Authorization $http_authorization`）；若啟用 **SSE／WebSocket** 訂閱摘要，補 **Upgrade／Connection** 相關設定。
5. **JWT（本階段）**：**`device_ui_events` 查詢 API** 與 **客戶唯讀路徑**（例如 `/api/v1/customer/...`）**必須**帶有效 **Access Token**；JWT **claims** 含 **角色／站臺或裝置範圍**（`scope`／`tenant`），後端強制 **授權檢查**（客戶僅能讀所屬資料）；實作 **Refresh Token**（輪替建議）與 **登出**（token 撤銷或黑名單可選）。**授權** 實作於 **Use Cases**（**§2.1B**），Repository 查詢帶 **tenant／device** 篩選。
6. **事件記錄（本階段）**：`ui-events` ingest 與查詢關鍵路徑需輸出結構化 log（至少含 `device_id`、`event_type`/`channel`、`result`），並延用 **§6.0A** 的 Console + File 策略。

**前端（Clean Architecture）**
1. **事件檢視**：依裝置篩選之「介面事件」列表或時間軸（對應手勢切頁／LCD 狀態字串）。
2. **可選**：與「目前分頁」狀態連動之單欄位顯示（資料來自後端聚合或最後一筆 `ui-events`）。
3. **JWT**：登入頁、Token 刷新流程、**403** 與 **401** 區分；事件 API 與遙測 API 使用同一 Bearer 策略。

#### 對齊階段三（IMU 告警、補傳、MOSFET）

**後端（Clean Architecture）**
1. **裝置日誌與補傳**：訂閱 `status`、`telemetry/sync-back`；ingest 標記 `is_sync_back`；查詢 API 可區分即時與補傳列。
2. **與資料庫**：PostgreSQL 索引建議包含 `(device_id, device_time)`；大量補傳時注意寫入批次與連線池（容器內 `postgresql.conf`／連線參數可參考 §8.3）。
3. **Nginx（本階段）**：對 **`/api/`** 啟用 **`limit_req`**、合理 **`client_max_body_size`** 與 **timeout**；靜態資源 **Cache-Control**（hash 檔名）；與 **§2.1A**（約 500 帳號）進行 **壓測**並調整 `worker_connections`／`worker_processes`；**80→443** 重定向。
4. **JWT（本階段）**：告警／sync-back／歷史查詢 **一律**需有效 Token；**Access 短效**、**Refresh 輪替**；**管理員 API** 與 **客戶 API** 路徑與 **角色**分離；**簽名密鑰**之產生、儲存與 **輪替**程序（環境變數／密鑰管理）。**密鑰輪替** 與 **告警查詢用例** 分屬 **Infrastructure**／**Use Cases**（**§2.1B**）。
5. **運維記錄（本階段）**：補傳、告警、授權失敗與關鍵錯誤需有後端 log（含 `device_id`、`is_sync_back`、`endpoint` 或 `topic`、`error_code`），並寫入 `log_file_path`。

**前端（Clean Architecture）**
1. **告警與補傳可視化**：告警列表、嚴重度或觸發原因摘要；補傳資料在時間軸上以樣式或圖例區分「離線回填」。
2. **階段三 C（MOSFET）**：若後端記錄 PWM／告警輸出事件，前端可顯示「最後告警輸出狀態」（需韌體／日誌欄位約定一致）。
3. **JWT**：敏感操作（若未來開放）須符合後端角色；一般客戶維持唯讀。

**驗收（§6.0 整體）**
- 階段一並行：Pi 5 可穩定收 Pico 遙測（含 **§2.4.2** 之 **`co2_ppm`**／**`pir_active`** 等，若韌體已啟用；**`motion`** 僅為可選別名）；**Nginx → backend API** 路徑可通；**JWT** 簽發／驗證與儀表查詢可通（依環境是否強制 Bearer）；**`GET /api/v1/telemetry/series`** 與儀表 **折線圖** 可通（**§6.0.1**）；`on_message_received` 可在 Console 與 `log_file_path` 查得 `device_id/topic/payload` 記錄；程式碼複核：**後端／前端** 目錄與依賴方向符合 **§2.1B**（無 Domain 依賴 ASP.NET Core／fetch 細節）。
- 階段二並行：`ui-events` 端到端可寫入並可查；**查詢 API** 具 **授權**（角色／範圍）；**Refresh** 流程可用；授權邏輯位於 **Use Cases**，非散落在 handler；`ui-events` 相關後端 log 可追溯成功/失敗路徑（Console + 檔案）。
- 階段三並行：告警與 sync-back 可查詢；儀表能反映補傳與即時差異；**Nginx 限流**與 **JWT 生產策略**（短效、輪替）就緒；**壓測**達 **§2.1A** 約定之目標或已記錄瓶頸與擴充方案；密鑰與告警用例分層符合 **§2.1B**；補傳/告警/錯誤 log 可於 `log_file_path` 抽樣核對。

### 6.0.1 遙測圖表與歷程查詢（前後端設計步驟）

> **目的**：在 **§6.0** 並行軌道內，將「客戶遠端檢視 **歷史資料**」落到可驗收之前後端行為；圖表為 **時間序列** 呈現，**不**以瀏覽器直連 MQTT。  
> **與第四階段關係**：**§6.0.1** 定義 **可上線之圖表與 API 契約**；**§6.0.2** 在相同契約上擴充 **多裝置／多指標／告警疊圖／壓測調優**。

#### 後端（Clean Architecture）

1. **領域（Domain）**  
   - 定義 **時間序列點** 與 **指標鍵** 語意；**與 §2.4.2 對齊之鍵**包含：`temperature_c`、`humidity_pct`（BME680）、`lux`、`co2_ppm`、`temperature_c_scd41`、`humidity_pct_scd41`（SCD41）、`pir_active`（PIR）等，與 payload／DB 對應表置於**單一處**（避免 magic string 散落）。  
   - **不**在 Domain 依賴 HTTP 或 SQL。

2. **用例（Use Cases）**  
   - **`GetTelemetrySeries`**：輸入 `device_id`（或站臺＋裝置範圍）、`metrics[]`、`time_from`、`time_to`、可選 `limit`／`max_points`；輸出依指標分組之有序點列。  
   - **授權**：與 **§6.0** 既有 **JWT／角色／站臺範圍** 一致；客戶僅能查所屬裝置。  
   - **邊界**：拒絕過大查詢（例如單次時間窗 **> 90 天** 或 **點數上限** 可配置），回傳 400 與建議縮小範圍或分頁策略。

3. **介面轉接（Interface Adapters）**  
   - **Repository**：以 PostgreSQL 對 **遙測表** 做 **`WHERE device_id = ? AND device_time BETWEEN ? AND ?`**，**`ORDER BY device_time`**；必要欄位已具 **`(device_id, device_time)`** 索引（見 **§6.0** 階段三）。  
   - **可選優化**：長區間可 **降採樣**（例如按小時 `avg`）由用例或 SQL 視圖實作，避免一次回傳十萬點。

4. **HTTP（ASP.NET Core）**  
   - 建議端點：**`GET /api/v1/telemetry/series`**，查詢參數例如：`device_id`、`from`（ISO8601 或 Unix ms）、`to`、`metrics`（逗號分隔）。  
   - 回應 JSON 建議形狀：`{ "device_id": "...", "from": "...", "to": "...", "series": [ { "metric": "temperature_c", "unit": "C", "points": [ { "t": "...", "v": 23.1 } ] } ] }`。  
   - **JWT**：與 **`/api/v1/telemetry`** 列表 API **相同**驗證策略；**Nginx** 轉發 **`Authorization`**（**§2.1A**）。  
   - **限流**：對 **`/api/v1/telemetry/series`** 可設較嚴 **`limit_req`**（防拖庫），與 **§2.1A** 壓測一併驗收。

5. **與既有 ingest 關係**  
   - **MQTT ingest** 寫入邏輯不變；**series** 僅為 **讀路徑** 聚合，避免在 ingest callback 內做圖表邏輯。

#### 前端（Clean Architecture）

1. **領域（Domain）**  
   - 定義 **`TelemetrySeries`**、`SeriesPoint` 型別，與後端 JSON **對齊**；時區顯示策略（建議 **UTC 存、瀏覽器本地化顯示**）在型別或常數層註明。

2. **應用（Application）**  
   - **用例**：`loadTelemetrySeries(deviceId, range, metrics)`、`setDateRange`、`retry`；處理 **401／403／429** 與空資料。  
   - **Repository 介面**：`getSeries(params)`，由 **Infrastructure** 實作。

3. **基礎設施（Infrastructure）**  
   - **HTTP client**：`GET /api/v1/telemetry/series`，附 **`Authorization: Bearer`**；錯誤 body 對應至使用者可讀訊息。

4. **展示（Presentation）**  
   - **圖表元件**：選用 **一種**圖表庫（例如 **Recharts**、**Chart.js**＋**react-chartjs-2** 等）實作 **折線圖**；**軸**：X 為時間、Y 為數值與單位。  
   - **儀表頁**：與 **§6.0**「裝置／站臺選擇」共用；提供 **時間範圍**（預設最近 24h，可選 7d）與 **指標勾選**（至少一項）。  
   - **狀態**：載入 skeleton、錯誤提示、無資料提示；**響應式**寬度（**§6.0.2** 再強化斷點）。

5. **驗收（§6.0.1）**  
   - 登入後於同一裝置可見 **列表** 與 **至少一張折線圖**，資料與 DB 中該時段 **抽樣核對**一致；重新整理頁面後圖表可重現。

#### 與階段二、三之銜接

- **階段二**：若需對 **手勢／`ui-events`** 做時間軸，可 **沿用同一圖表框架**，資料來源改為 **`/api/v1/.../ui-events`** 之時間序列或 **條狀事件軸**（實作細節可獨立小節，契約仍遵循 JWT 與授權）。  
- **階段三**：**補傳** 點可在圖上以 **不同線型／標記** 區分（需後端在 payload 或查詢結果帶 **`is_sync_back`**）；與 **§6.0** 前端「補傳視覺區分」對齊。

### 6.0.3 API 端點說明（Markdown 模擬 Swagger UI）

> **Base URL（示意）**：`https://<your-domain>`  
> **Auth**：`Authorization: Bearer <access_token>`（依 §6.0 階段要求）  
> **資料契約來源**：遙測鍵名以 **§2.4.2** 為準（`co2_ppm`、`temperature_c_scd41`、`humidity_pct_scd41`、`pir_active` 等）。

---

#### POST `/api/v1/auth/login`

**Summary**：登入並取得 Access Token（JWT）  
**Tags**：`Auth`

**Request Body**

```json
{
  "username": "operator01",
  "password": "********"
}
```

**Responses**

- `200 OK`

```json
{
  "access_token": "<jwt>",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

- `401 Unauthorized`：帳密錯誤  
- `429 Too Many Requests`：登入嘗試過多

---

#### POST `/api/v1/auth/refresh`

**Summary**：使用 Refresh Token 換發新 Access Token（可選輪替 Refresh Token）  
**Tags**：`Auth`

**Request Body**

```json
{
  "refresh_token": "<refresh-token>"
}
```

**Responses**

- `200 OK`

```json
{
  "access_token": "<new-jwt>",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "<new-refresh-token>"
}
```

- `401 Unauthorized`：Refresh Token 過期/無效  
- `403 Forbidden`：Refresh Token 已撤銷  
- `429 Too Many Requests`：重試過於頻繁

---

#### POST `/api/v1/auth/logout`

**Summary**：登出並使目前 Refresh Token 失效  
**Tags**：`Auth`  
**Security**：Bearer JWT（建議）

**Request Body**

```json
{
  "refresh_token": "<refresh-token>"
}
```

**Responses**

- `200 OK`

```json
{
  "status": "ok",
  "message": "logout success"
}
```

- `401 Unauthorized`：缺少或無效 Access Token  
- `400 Bad Request`：缺少 refresh_token

---

#### GET `/api/v1/telemetry`

**Summary**：查詢遙測列表（分頁 / 篩選）  
**Tags**：`Telemetry`  
**Security**：Bearer JWT（預生產起強制）

**Query Parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `device_id` | string | yes | 裝置識別碼 |
| `from` | string (ISO8601) | no | 起始時間 |
| `to` | string (ISO8601) | no | 結束時間 |
| `page` | integer | no | 頁碼，預設 1 |
| `page_size` | integer | no | 每頁筆數，預設 50 |

**Responses**

- `200 OK`

```json
{
  "page": 1,
  "page_size": 50,
  "total": 2,
  "items": [
    {
      "device_id": "pico2wh-001",
      "device_time": "2026-04-06T10:15:00Z",
      "is_sync_back": false,
      "temperature_c": 25.1,
      "humidity_pct": 54.2,
      "co2_ppm": 672,
      "temperature_c_scd41": 25.4,
      "humidity_pct_scd41": 53.6,
      "pir_active": true
    }
  ]
}
```

- `400 Bad Request`：時間窗或參數格式錯誤  
- `401 Unauthorized`：缺少或無效 Token  
- `403 Forbidden`：超出租戶/角色可見範圍

---

#### GET `/api/v1/telemetry/series`

**Summary**：查詢時間序列資料（圖表）  
**Tags**：`Telemetry`  
**Security**：Bearer JWT

**Query Parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `device_id` | string | yes | 裝置識別碼 |
| `from` | string (ISO8601 / Unix ms) | yes | 起始時間 |
| `to` | string (ISO8601 / Unix ms) | yes | 結束時間 |
| `metrics` | string (comma-separated) | yes | 例如 `co2_ppm,temperature_c_scd41,pir_active` |
| `max_points` | integer | no | 單次最大點數（避免超量查詢） |

**Supported metrics（對齊 §2.4.2）**

- `temperature_c`
- `humidity_pct`
- `lux`
- `co2_ppm`
- `temperature_c_scd41`
- `humidity_pct_scd41`
- `pir_active`

**Implementation Rules（補全 §6.0.1）**

- `metrics` 需為受支援鍵之子集合，且本系統必須至少支援：`co2_ppm`、`temperature_c_scd41`、`pir_active`。  
- 若 `metrics` 包含未知鍵，回傳 `400 Bad Request`（附 `invalid_metrics` 清單）。  
- 若時間窗過大導致點數超過 `max_points`（或伺服器上限），必須啟用 **Downsampling**。  
- Downsampling 建議流程：  
  1. 先依 `device_time` 升序取原始點列。  
  2. 計算 `bucket_size = ceil(total_points / target_points)`。  
  3. 以時間桶分組，每桶輸出代表點（建議 `avg`，可保留 `min/max` 作擴充）。  
  4. 回傳中附 `downsampled=true`、`source_points`、`returned_points`。  

**Responses**

- `200 OK`

```json
{
  "device_id": "pico2wh-001",
  "from": "2026-04-06T00:00:00Z",
  "to": "2026-04-06T12:00:00Z",
  "series": [
    {
      "metric": "co2_ppm",
      "unit": "ppm",
      "points": [
        { "t": "2026-04-06T10:00:00Z", "v": 645 },
        { "t": "2026-04-06T10:05:00Z", "v": 672 }
      ]
    }
  ]
}
```

- `400 Bad Request`：查詢範圍過大或 metrics 不合法  
- `401 Unauthorized`  
- `429 Too Many Requests`

---

#### GET `/api/v1/logs`

**Summary**：查詢裝置事件 / 系統日誌（REST 層）  
**Tags**：`Logs`  
**Security**：Bearer JWT

**Query Parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `device_id` | string | no | 裝置識別碼 |
| `channel` | string | no | 如 `telemetry` / `ui-events` / `status` |
| `from` | string (ISO8601) | no | 起始時間 |
| `to` | string (ISO8601) | no | 結束時間 |
| `level` | string | no | `debug/info/warn/error` |

**Implementation Rules（補全）**

- 日誌資料來源為 `log_file_path` 指向之結構化日誌檔案（見 §6.0A）。  
- 需先解析 Key-Value 格式（例如 `ts=... lvl=... mod=...`）再套用篩選。  
- `channel` 僅允許：`telemetry` / `ui-events` / `status`；`level` 僅允許：`debug` / `info` / `warn` / `error`。  
- 不合法參數回傳 `400 Bad Request`，並標示 `allowed_values`。  
- 若同時提供 `from/to`，應先做時間窗篩選，再做 `channel/level/device_id` 篩選。  
- 建議回傳分頁欄位：`page`、`page_size`、`total`，避免單次回傳整檔。

**Responses**

- `200 OK`

```json
{
  "page": 1,
  "page_size": 50,
  "total": 1,
  "items": [
    {
      "ts": "2026-04-06T10:20:31Z",
      "level": "info",
      "channel": "telemetry",
      "module": "mqtt_ingest",
      "device_id": "pico2wh-001",
      "topic": "iiot/siteA/pico2wh-001/telemetry",
      "message": "payload accepted"
    }
  ]
}
```

---

#### GET `/api/v1/system/status`（對齊 §6.0.2）

**Summary**：回報容器運行與健康狀態（供儀表與 KY-016 狀態燈聯動）  
**Tags**：`System`  
**Security**：Bearer JWT（管理者角色建議）

**Query Parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `include_stopped` | boolean | no | 是否包含停止中的容器，預設 `true` |

**Implementation Rules**

- 後端透過 Docker Engine API 讀取容器狀態（`Running/Stopped`）與健康資訊。  
- 每個容器回傳至少：`container_name`、`status`、`ip`、`health`。  
- 若無健康檢查設定，`health` 可回傳 `unknown`。  
- 回傳資料可直接供 KY-016 狀態對應（綠=healthy、黃=degraded、紅=unhealthy/stopped）。

**Responses**

- `200 OK`

```json
{
  "items": [
    {
      "container_name": "backend",
      "status": "running",
      "ip": "172.20.0.5",
      "health": "healthy"
    },
    {
      "container_name": "postgres",
      "status": "running",
      "ip": "172.20.0.3",
      "health": "healthy"
    }
  ]
}
```

- `500 Internal Server Error`：Docker API 不可用或權限不足

---

#### POST `/api/v1/device/control`（對齊 §5.3 MOSFET）

**Summary**：下發裝置控制命令（PWM）至 Pico  
**Tags**：`DeviceControl`  
**Security**：Bearer JWT（建議管理者角色）

**Request Body**

```json
{
  "device_id": "pico2wh-001",
  "command": "set_pwm",
  "value": 65
}
```

**Validation Rules**

- `command` 目前僅接受 `set_pwm`。  
- `value` 範圍必須為 `0~100`（百分比 duty）。  
- 不合法 payload 回傳 `400 Bad Request`。

**Implementation Rules**

- 後端將控制命令轉為 MQTT 訊息並發布給對應裝置 topic。  
- 建議 topic：`iiot/<site_id>/<device_id>/control`。  
- 建議 payload 包含：`command`、`value`、`request_id`、`server_time`。  
- 成功發布後回傳 `accepted`，並於 log 記錄 `device_id/command/value/topic`（見 §6.0A）。

**Responses**

- `202 Accepted`

```json
{
  "status": "accepted",
  "device_id": "pico2wh-001",
  "command": "set_pwm",
  "value": 65
}
```

- `400 Bad Request`：參數錯誤（如 `value` 超出範圍）  
- `401 Unauthorized` / `403 Forbidden`  
- `502 Bad Gateway`：MQTT broker 發布失敗

---

#### GET `/api/v1/ui-events`（階段二）

**Summary**：查詢手勢 / UI 事件時間軸  
**Tags**：`UIEvents`  
**Security**：Bearer JWT

**Query Parameters**

| Name | Type | Required | Description |
|---|---|---|---|
| `device_id` | string | yes | 裝置識別碼 |
| `from` | string (ISO8601) | no | 起始時間 |
| `to` | string (ISO8601) | no | 結束時間 |
| `page` | integer | no | 頁碼 |
| `page_size` | integer | no | 每頁筆數 |

**Responses**

- `200 OK`

```json
{
  "items": [
    {
      "device_id": "pico2wh-001",
      "device_time": "2026-04-06T10:30:00Z",
      "event_type": "gesture",
      "event_value": "LEFT"
    }
  ]
}
```

---

#### 共用錯誤模型（Error Model）

```json
{
  "error": {
    "code": "UNAUTHORIZED",
    "message": "Missing Bearer token",
    "request_id": "req-20260406-abc123"
  }
}
```

---

#### 與 MQTT / ingest 對應（Implementation Notes）

- MQTT 訂閱樣式：`iiot/+/+/telemetry`（見 §6.0）  
- `on_message_received` 寫入 log（見 §6.0A）：至少記錄 `device_id`、`topic`、`payload`  
- 時序查詢 `metrics` 與鍵名以 §2.4.2 為準，避免與舊鍵混用

### 6.0.4 技術規格增補（Source of Truth）

> 本節為後續開發對話之**單一真相來源**（SoT）。若與其他章節描述有歧義，以本節與 **§2.4.2**、**§2.1B** 一致之條文為準。

#### 1) REST API 深度規範（補完計畫）

##### 1.1 共用規範

- **Content-Type**：`application/json; charset=utf-8`
- **時間格式**：輸入接受 ISO8601（UTC）；回傳統一 ISO8601 UTC（尾碼 `Z`）
- **授權**：除 `POST /api/v1/auth/login`、`POST /api/v1/auth/refresh` 外，預設需 Bearer JWT
- **錯誤模型**：沿用 §6.0.3 `Error Model`，並在 `error.code` 使用穩定機器碼（例如 `INVALID_METRICS`、`INVALID_FILTER`、`DOCKER_API_UNAVAILABLE`）

##### 1.2 `GET /api/v1/telemetry/series`（Downsampling 規格）

**Request Schema（Query）**

| 欄位 | 型別 | 必填 | 規格 |
|---|---|---|---|
| `device_id` | string | yes | 1..64 字元 |
| `from` | string | yes | ISO8601 或 Unix ms |
| `to` | string | yes | ISO8601 或 Unix ms，且 `to > from` |
| `metrics` | string | yes | 逗號分隔，元素需在允許清單內 |
| `max_points` | integer | no | 10..5000；預設 500 |

**Metrics 驗證規則**

- `metrics` 僅允許：`temperature_c`、`humidity_pct`、`lux`、`co2_ppm`、`temperature_c_scd41`、`humidity_pct_scd41`、`pir_active`
- 本端點實作必須完整支持：`co2_ppm`、`temperature_c_scd41`、`pir_active`
- 非法鍵回 `400`，`error.details.invalid_metrics` 回傳非法鍵陣列

**Downsampling 計算規則（強制）**

1. 先查原始點，依 `device_time ASC` 排序。  
2. 若 `source_points <= target_points`（`target_points = max_points` 或預設 500），則不降採樣。  
3. 若 `source_points > target_points`，以固定時間桶做降採樣：  
   - `bucket_count = target_points`  
   - `bucket_width_ms = ceil((to - from) / bucket_count)`  
   - 每桶取該桶內點的 `avg(value)` 作為代表點；`pir_active` 以 `max(bool)`（桶內任一 true 即 true）  
4. 回傳 metadata：`downsampled`、`source_points`、`returned_points`、`bucket_width_ms`

**Response Schema（200）**

```json
{
  "device_id": "string",
  "from": "2026-04-06T00:00:00Z",
  "to": "2026-04-06T12:00:00Z",
  "downsampled": true,
  "source_points": 14230,
  "returned_points": 500,
  "bucket_width_ms": 86400,
  "series": [
    {
      "metric": "co2_ppm",
      "unit": "ppm",
      "points": [{ "t": "2026-04-06T10:00:00Z", "v": 645.0 }]
    }
  ]
}
```

##### 1.3 `GET /api/v1/system/status`（Docker 容器狀態）

**Request Schema（Query）**

| 欄位 | 型別 | 必填 | 規格 |
|---|---|---|---|
| `include_stopped` | boolean | no | 預設 `true` |

**Response Schema（200）**

```json
{
  "host_time": "2026-04-06T12:00:00Z",
  "items": [
    {
      "container_id": "3f2d8a9c1b7e",
      "name": "backend",
      "status": "running",
      "uptime_sec": 86420,
      "ip": "172.20.0.5",
      "health_status": "healthy"
    }
  ]
}
```

**欄位語意**

- `status`: `running` / `stopped`
- `health_status`: `healthy` / `unhealthy` / `starting` / `unknown`
- `uptime_sec`: 若容器停止可為 `0`

##### 1.4 `POST /api/v1/device/control`（PWM 封裝語意）

**Request Schema**

```json
{
  "device_id": "pico2wh-001",
  "command": "set_pwm",
  "value": 65
}
```

**驗證規則**

- `command` 僅允許 `set_pwm`
- `value` 僅允許 `0..100`（整數或可接受小數，建議最終四捨五入為整數百分比）

**PWM 映射規範（16-bit）**

- `pwm_16bit = round(clamp(value, 0, 100) / 100 * 65535)`
- 對照：
  - `0% -> 0`
  - `50% -> 32768`（四捨五入）
  - `100% -> 65535`
- MQTT 控制 payload 建議包含：`device_id`、`command`、`value_pct`、`value_16bit`、`request_id`、`server_time`

**Response Schema（202）**

```json
{
  "status": "accepted",
  "device_id": "pico2wh-001",
  "command": "set_pwm",
  "value_pct": 65,
  "value_16bit": 42598
}
```

#### 2) Clean Architecture 分層契約（ASP.NET Core/.NET）

##### 2.1 Domain 層（不變領域實體）

- `TelemetryData`
  - `device_id`, `device_time`, `is_sync_back`
  - `temperature_c`, `humidity_pct`, `lux`
  - `co2_ppm`, `temperature_c_scd41`, `humidity_pct_scd41`
  - `pir_active`
- `DeviceEvent`
  - `device_id`, `device_time`, `event_type`, `event_value`, `channel`
- `AuthSession`
  - `user_id`, `role`, `tenant_scope`, `token_exp`
- `SystemContainerStatus`
  - `container_id`, `name`, `status`, `uptime_sec`, `ip`, `health_status`

##### 2.2 Use Case 層（核心應用邏輯）

- `IngestNewData`
- `GetTelemetryList`
- `GetTelemetrySeries`
- `GetLogsByFilter`
- `GetSystemStatus`
- `PublishDeviceControl`
- `AuthenticateUser`
- `RefreshAccessToken`
- `LogoutSession`
- `GetUiEvents`

##### 2.3 Adapter 邊界（介面契約，無 SQL）

**TelemetryRepository**

- `save_telemetry(data)`
- `find_telemetry(device_id, from, to, page, page_size, filters)`
- `find_series(device_id, from, to, metrics, max_points)`

**LogRepository**

- `read_logs(range, page, page_size)`
- `find_logs_by_device(device_id, range, page, page_size)`
- `find_logs_by_filter(channel, level, range, page, page_size)`

**UiEventRepository**

- `save_ui_event(event)`
- `find_ui_events(device_id, from, to, page, page_size)`

**AuthRepository**

- `verify_credentials(username, password_hash)`
- `store_refresh_token(user_id, token_meta)`
- `revoke_refresh_token(token_id)`
- `find_refresh_token(token_id)`

**SystemStatusPort**

- `list_containers(include_stopped)`

**DeviceControlPort**

- `publish_control(device_id, command, payload)`

#### 3) MQTT 數據格式與錯誤處理規格

##### 3.1 Payload 契約（SCD41 + BME680 並存）

- **優先權與語意**
  - `temperature_c` / `humidity_pct`：保留給 BME680 主鍵
  - `temperature_c_scd41` / `humidity_pct_scd41`：專屬 SCD41
  - `co2_ppm`：SCD41 必填（啟用 SCD41 時）
  - `pir_active`：PIR 主鍵；`motion` 僅相容別名
- **缺省值處理**
  - 感測值缺失時：該鍵可缺省或為 `null`，不得以 `0` 假值覆蓋真實缺測
  - `pir_active` 缺省時視為未知狀態，不等同 `false`
- **驗收一致性**
  - API `metrics` 與 DB 欄位命名必須與 §2.4.2 完全一致

##### 3.2 Logging Key-Value 範本（§6.0A）

- **標準模板**
  - `ts=%s lvl=%s mod=%s req_id=%s dev_id=%s msg="%s"`
- **MQTT ingest 範本**
  - `ts=%s lvl=info mod=mqtt_ingest dev_id=%s topic="%s" payload="%s" msg="payload accepted"`
- **JWT 失敗範本**
  - `ts=%s lvl=warn mod=auth event=jwt_verify_failed src_ip=%s jwt_err_type=%s endpoint=%s msg="%s"`
- **允許級別**
  - `debug` / `info` / `warn` / `error`

#### 4) 硬體擴充與相容性規範（Pico 2 WH）

##### 4.1 I2C 位址衝突自動掃描與回報（含 `0x68`）

- 啟動時與每次重連後執行雙總線掃描：`I2C0(GPIO4/5)`、`I2C1(GPIO2/3)`
- 產出 `bus -> address_list` 並上報診斷 log
- **衝突規則**
  - `0x68` 在 `I2C0` 預期為 MPU-9250；在 `I2C1` 預期為 DS3231
  - 若同一 bus 發現多裝置宣告同位址（無法可靠識別），標記 `ADDR_CONFLICT`
  - `ADDR_CONFLICT` 觸發降級：停用受影響感測資料上報，保留其餘通道
- 回報欄位至少含：`bus`, `address`, `expected_device`, `actual_probe_result`, `severity`

##### 4.2 AT24C256 斷線緩衝（Ring Buffer）寫入策略

- **記錄單位**：固定長度 frame（建議含 `seq_no`, `device_time`, `payload_crc`, `payload_len`, `payload`）
- **索引**
  - `write_ptr`：下一筆寫入位置
  - `read_ptr`：下一筆待補傳位置
  - `count`：目前有效筆數
- **滿載覆蓋規則（強制）**
  - 當 `count == capacity` 且需新寫入時，覆蓋 `read_ptr`（最舊資料）
  - `read_ptr = (read_ptr + 1) % capacity`
  - `write_ptr = (write_ptr + 1) % capacity`
  - 記錄 `buffer_overwrite=true` 事件 log
- **補傳成功後**
  - 依 ack 前進 `read_ptr`，遞減 `count`
- **完整性**
  - 讀取時驗 `payload_crc`；CRC 失敗該 frame 標記損壞並跳過，保留錯誤 log

### 6.0.5 技術策略深度擬定（開發策略文件）

> 本節為開發策略層級規範，補足 §2.4.2、§2.1A、§2.1B、§6.0.1、§4.2 之跨模組決策；不包含實際程式碼。

#### A. 資料持久化策略（Database Strategy）

##### A.0 讀取路徑：EF Core 與 Dapper（後端實作）

- **Schema 與寫入**：資料表仍以 **EF Core Migration** 與 **`ApplicationDbContext`** 為單一結構來源；**MQTT ingest 落庫**、一般 **Command／Repository 寫入** 以 **EF Core** 為準。
- **讀取查詢**：第二層僅依賴介面（如 `ILogQueryRepository`、`ITelemetrySeriesQuery`、`IUiEventsQuery`），與具體技術解耦；**HTTP 讀路徑**（日誌列表、UI 事件、遙測時序）實作均為 **Dapper**／**PostgreSQL** 參數化 SQL（**無** EF／Dapper 切換旗標）。
  - 結構化日誌列表（`GET /api/v1/logs`）：`LogDapperQuery`。
  - 遙測時序（`GET /api/v1/telemetry/series`）：`TelemetrySeriesDapperQuery`（SQL 側聚合）。
  - UI 事件列表（`GET /api/v1/ui-events`）：`UiEventsDapperQuery`。
- **組態**（`Database`）：以 **`DefaultSchema`**、**`AutoMigrate`** 等為主（見 `DatabaseOptions`）。
- **完整檔案位置、DI、連線與整合測試（DapperReadQueryTests）** 以 `Pico2WH-Pi5-IIoT-專案開發規格書_v5_ASPNETCORE_4LAYER.md` **§2.4.1a** 為後端 SoT。

##### A.1 PostgreSQL 資料表設計（Telemetry 主表）

**Table：`telemetry_records`**

| 欄位 | 型別 | Null | 說明 |
|---|---|---|---|
| `id` | `BIGSERIAL` | no | 內部流水鍵（技術主鍵） |
| `device_id` | `VARCHAR(64)` | no | 裝置識別碼 |
| `site_id` | `VARCHAR(64)` | no | 站臺識別碼 |
| `device_time` | `TIMESTAMPTZ` | no | 裝置上報時間（UTC） |
| `server_time` | `TIMESTAMPTZ` | no | ingest 寫入時間（UTC） |
| `is_sync_back` | `BOOLEAN` | no | 是否補傳 |
| `temperature_c` | `DOUBLE PRECISION` | yes | BME 溫度 |
| `humidity_pct` | `DOUBLE PRECISION` | yes | BME 濕度 |
| `lux` | `DOUBLE PRECISION` | yes | 光照 |
| `co2_ppm` | `DOUBLE PRECISION` | yes | SCD41 CO2 |
| `temperature_c_scd41` | `DOUBLE PRECISION` | yes | SCD41 溫度 |
| `humidity_pct_scd41` | `DOUBLE PRECISION` | yes | SCD41 濕度 |
| `pir_active` | `BOOLEAN` | yes | PIR 主鍵 |
| `raw_payload` | `JSONB` | yes | 原始遙測（保留欄） |

**Primary Key 策略**

- **技術主鍵**：`PRIMARY KEY (id)`
- **業務唯一鍵**：`UNIQUE (device_id, device_time, is_sync_back)`（避免重複 ingest）

##### A.2 認證與權限管理表（對齊 §6.0.3 Auth / §6.0.5 B）

**Table：`users`**

| 欄位 | 型別 | 約束 | 說明 |
|---|---|---|---|
| `user_id` | `UUID` | PK, NOT NULL | 使用者主鍵 |
| `username` | `VARCHAR(64)` | UNIQUE, NOT NULL | 登入帳號（大小寫策略需全系統一致） |
| `password_hash` | `TEXT` | NOT NULL | 密碼雜湊（不可存明文） |
| `role` | `VARCHAR(16)` | NOT NULL, CHECK(`admin`/`customer`) | 角色 |
| `tenant_scope` | `VARCHAR(64)` | NOT NULL | 租戶/站臺範圍（對齊 `site_id`） |
| `is_active` | `BOOLEAN` | NOT NULL, DEFAULT true | 帳號狀態 |
| `created_at` | `TIMESTAMPTZ` | NOT NULL | 建立時間 |
| `updated_at` | `TIMESTAMPTZ` | NOT NULL | 更新時間 |

**Table：`refresh_tokens`**

| 欄位 | 型別 | 約束 | 說明 |
|---|---|---|---|
| `token_id` | `UUID` | PK, NOT NULL | Refresh Token 主鍵 |
| `user_id` | `UUID` | FK -> `users.user_id`, NOT NULL | 所屬使用者 |
| `token_hash` | `TEXT` | UNIQUE, NOT NULL | Token 雜湊值 |
| `expires_at` | `TIMESTAMPTZ` | NOT NULL | 到期時間 |
| `is_revoked` | `BOOLEAN` | NOT NULL, DEFAULT false | 是否撤銷 |
| `issued_at` | `TIMESTAMPTZ` | NOT NULL | 簽發時間 |
| `revoked_at` | `TIMESTAMPTZ` | NULL | 撤銷時間 |
| `revoked_reason` | `VARCHAR(64)` | NULL | 撤銷原因（logout/rotation/security） |

##### A.3 UI 事件與手勢紀錄表（對齊 `/api/v1/ui-events`）

**Table：`device_ui_events`**

| 欄位 | 型別 | 約束 | 說明 |
|---|---|---|---|
| `event_id` | `BIGSERIAL` | PK, NOT NULL | 事件主鍵 |
| `device_id` | `VARCHAR(64)` | NOT NULL | 裝置識別碼 |
| `device_time` | `TIMESTAMPTZ` | NOT NULL | 裝置事件時間 |
| `event_type` | `VARCHAR(16)` | NOT NULL, CHECK(`gesture`/`ui`) | 事件類型 |
| `event_value` | `VARCHAR(64)` | NOT NULL | 事件值（如 `LEFT`/`RIGHT`） |
| `channel` | `VARCHAR(32)` | NOT NULL | 頻道（建議 `ui-events`） |
| `site_id` | `VARCHAR(64)` | NOT NULL | 站臺識別碼 |
| `payload` | `JSONB` | NULL | 擴充欄位 |
| `ingested_at` | `TIMESTAMPTZ` | NOT NULL | 後端寫入時間 |

**索引建議（500 人規模）**

- `idx_ui_events_device_time`：`(device_id, device_time DESC)`（主查詢路徑）
- `idx_ui_events_site_device_time`：`(site_id, device_id, device_time DESC)`（多租戶查詢）
- `idx_ui_events_channel_level`：`(channel, device_time DESC)`（頻道篩選）

##### A.4 系統狀態監控快取表（對齊 `/api/v1/system/status`）

**Table：`container_status_cache`**

| 欄位 | 型別 | 約束 | 說明 |
|---|---|---|---|
| `container_id` | `VARCHAR(64)` | PK, NOT NULL | 容器 ID |
| `container_name` | `VARCHAR(128)` | UNIQUE, NOT NULL | 容器名稱 |
| `status` | `VARCHAR(16)` | NOT NULL, CHECK(`running`/`stopped`) | 運行狀態 |
| `health_status` | `VARCHAR(16)` | NOT NULL, CHECK(`healthy`/`unhealthy`/`starting`/`unknown`) | 健康狀態 |
| `ip` | `INET` | NULL | 容器 IP |
| `uptime_sec` | `BIGINT` | NOT NULL, DEFAULT 0 | 已運行秒數 |
| `last_updated_at` | `TIMESTAMPTZ` | NOT NULL | 最後更新時間 |

##### A.5 設備控制與命令審計表（對齊 `/api/v1/device/control`）

**Table：`device_control_logs`**

| 欄位 | 型別 | 約束 | 說明 |
|---|---|---|---|
| `request_id` | `UUID` | PK, NOT NULL | 請求追蹤 ID |
| `operator_id` | `UUID` | FK -> `users.user_id`, NOT NULL | 操作者 |
| `device_id` | `VARCHAR(64)` | NOT NULL | 目標裝置 |
| `command` | `VARCHAR(32)` | NOT NULL, CHECK(`set_pwm`) | 控制命令 |
| `value_pct` | `NUMERIC(5,2)` | NOT NULL, CHECK(`0 <= value_pct <= 100`) | 百分比 |
| `value_16bit` | `INTEGER` | NOT NULL, CHECK(`0 <= value_16bit <= 65535`) | 16-bit 映射值 |
| `status` | `VARCHAR(16)` | NOT NULL, CHECK(`accepted`/`published`/`failed`) | 命令狀態 |
| `server_time` | `TIMESTAMPTZ` | NOT NULL | 伺服器時間 |
| `topic` | `VARCHAR(256)` | NULL | 發布到 MQTT 的 topic |
| `error_code` | `VARCHAR(64)` | NULL | 失敗碼 |

##### A.6 全域索引與關聯策略（支援 500 人規模）

- **Telemetry（沿用 A.1）**
  - `idx_telemetry_device_time`：`(device_id, device_time DESC)`
  - `idx_telemetry_site_device_time`：`(site_id, device_id, device_time DESC)`
  - `idx_telemetry_syncback_time`：`(is_sync_back, device_time DESC)`
  - `idx_telemetry_metrics_partial_co2`：`(device_id, device_time)` + `co2_ppm` 非空篩選
  - `idx_telemetry_metrics_partial_scd41_temp`：`(device_id, device_time)` + `temperature_c_scd41` 非空篩選
  - `idx_telemetry_metrics_partial_pir`：`(device_id, device_time)` + `pir_active` 非空篩選
- **Auth**
  - `users.username` 唯一索引（登入熱路徑）
  - `refresh_tokens(token_hash)` 唯一索引
  - `refresh_tokens(user_id, is_revoked, expires_at DESC)`（刷新/撤銷查詢）
- **Control**
  - `idx_control_device_time`：`(device_id, server_time DESC)`
  - `idx_control_operator_time`：`(operator_id, server_time DESC)`
- **System**
  - `idx_container_status_updated`：`(last_updated_at DESC)`

> 策略原則：先以 B-Tree 時間序索引支援 500 人規模；若歷史資料量級持續增長，再評估時間分區（按月）與物化降採樣表。欄位命名與 API/JSON 鍵保持一致：`device_id`、`device_time`、`co2_ppm`、`temperature_c_scd41`、`pir_active`。

#### B. 安全與授權策略（Auth Policy）

##### B.1 JWT Payload 標準 Claims（500 人規模）

**Access Token Claims（標準）**

| Claim | 型別 | 必填 | 說明 |
|---|---|---|---|
| `iss` | string | yes | Token 發行者（API domain） |
| `sub` | string | yes | 使用者 ID |
| `aud` | string | yes | 目標受眾（api） |
| `exp` | number | yes | 到期時間（Unix epoch） |
| `iat` | number | yes | 簽發時間 |
| `jti` | string | yes | Token 唯一識別 |
| `role` | string | yes | `admin` / `customer` |
| `tenant_id` | string | yes | 租戶/客戶識別 |
| `site_scope` | array[string] | yes | 可存取站臺清單 |
| `device_scope` | array[string] | no | 可存取裝置清單（可選） |
| `perm` | array[string] | yes | 權限代碼（如 `telemetry.read`） |

**生命週期建議**

- Access Token：15~30 分鐘
- Refresh Token：7~30 天（可輪替）
- Refresh 撤銷清單保留至少與最大有效期等長

##### B.2 RBAC 權限對照（現有 API）

| API | Admin | Customer | 備註 |
|---|---|---|---|
| `POST /api/v1/auth/login` | allow | allow | 公開登入 |
| `POST /api/v1/auth/refresh` | allow | allow | 持 token 才可 |
| `POST /api/v1/auth/logout` | allow | allow | 僅作用於自身 session |
| `GET /api/v1/telemetry` | allow | allow* | Customer 僅限 `tenant/site/device` scope |
| `GET /api/v1/telemetry/series` | allow | allow* | 同上 |
| `GET /api/v1/logs` | allow | limited | Customer 僅可讀自身裝置且不含敏感系統 log |
| `GET /api/v1/ui-events` | allow | allow* | 同 scope 限制 |
| `GET /api/v1/system/status` | allow | deny | 屬運維管理面 |
| `POST /api/v1/device/control` | allow | deny | 控制命令僅管理者 |

> `allow*`：必須加上租戶與資源範圍約束；若範圍不匹配回 `403 Forbidden`。

#### C. 容錯與降級策略（Fault Tolerance）

##### C.1 Pico 重啟時 Ring Buffer 指標持久化（EEPROM 佈局）

**目標**：Pico 重啟後 `read_ptr` / `write_ptr` / `count` 不遺失，且避免寫損。

**EEPROM 佈局策略**

- 區塊 A：`RB_META_PRIMARY`
- 區塊 B：`RB_META_BACKUP`
- 區塊 C：`RB_FRAMES_DATA`（實際 frame 區）

**Meta 欄位**

- `version`
- `read_ptr`
- `write_ptr`
- `count`
- `generation`（單調遞增）
- `meta_crc`

**寫入策略（兩階段提交）**

1. 更新目標 meta（A/B 交替）並寫入 `generation+1`。  
2. 計算並寫入 `meta_crc`。  
3. 啟動時同時讀 A/B，選擇 `generation` 較新且 CRC 正確者。  
4. 若 A/B 皆損毀，進入安全重置：`read_ptr=write_ptr=0,count=0` 並記錄錯誤事件。

**寫損控制**

- meta flush 採批次（例如每 N 筆或每 T 秒）避免每筆都寫 EEPROM
- 補傳成功與覆蓋事件（滿載）需強制 flush 一次

##### C.2 Nginx 偵測 Backend API 不可用時之前端降級頁策略

**觸發條件**

- Nginx upstream 連續健康檢查失敗，或 API 連續回應 `502/503/504`

**前端降級處理邏輯（靜態資源仍可服務）**

1. SPA shell 可載入（HTML/CSS/JS 正常提供）。  
2. API 探測失敗後導向「服務暫時不可用」狀態頁（非瀏覽器預設錯誤頁）。  
3. 狀態頁需顯示：
   - 服務狀態：`Backend unavailable`
   - 最近探測時間
   - `request_id`（若可取得）
   - 重試按鈕與建議重試間隔（例如 10 秒）
4. 允許讀取快取資料（若前端有本地快取），並顯示「資料可能非即時」警示。  
5. 恢復條件：連續 N 次 API 健康檢查成功後，自動返回正常儀表頁。

**Nginx 回應策略**

- API 路徑：回 `503` + JSON 錯誤模型（含 `request_id`）
- 靜態資源路徑：維持可用（不受 upstream 崩潰影響）

### 6.0.2 第四階段（獨立計畫）：Pi 5 全棧深化與主機監控

**排程**：本節所列項目 **可與 §6 階段 1～3 並行**，無需於韌體階段一完成前全部做完。將 Docker 監控、完整儀表、可選 Pi 端 I2C 周邊（Grove LCD RGB）獨立為第四階段，避免與 Pico 燒錄／感測驗收節奏混線。

**目標**：落實硬體計畫 §2.7「Pi 5 監控架構」與本章 **React + Docker + PostgreSQL** 之 **生產級**整合與維運體驗。

**後端與基礎設施（Clean Architecture：本節增量以新用例＋介面 Adapter 擴充，不污染 Domain）**
1. **Nginx／JWT（生產調優）**：**§6.0** 已分階段完成 Nginx 與 JWT 主線；本階段做 **憑證自動更新**（Let’s Encrypt）、**監控告警**（Nginx 5xx／延遲）、**可選 WAF**／額外 **rate limit** 層；再跑 **全鏈路壓測**（**§2.1A** 約 500 帳號與目標並發）。
2. **C 監控程式（可選）**：讀取 Docker Engine API（`/var/run/docker.sock`），彙整容器健康、重啟次數、CPU／MEM；輸出 JSON（cJSON／jansson）；經 backend API 提供查詢端點或轉發 MQTT。權限：docker 群組、專用 service user、最小權限。
3. **Redis（第四階段）**：導入 Redis 作為查詢快取與短期狀態層（如 `telemetry/series`、`logs` 熱查詢快取、限流計數、token 黑名單）；採 TTL 與 key namespace 規劃，避免快取污染與跨租戶誤讀。
4. **Prometheus + Grafana（第四階段）**：建立 Metrics 監控面，至少涵蓋 API 延遲（p50/p95/p99）、錯誤率、容器 CPU/MEM、PostgreSQL 與 MQTT 連線健康；儀表板與告警閾值納入交付。
5. **Loki + Promtail（第四階段）**：集中收集後端／前端／Nginx／MQTT／系統容器日誌，與 **§6.0A** Key-Value logging 對齊，支援以 `device_id/topic/request_id` 追溯問題。
6. **服務整合**：監控資料寫入 PostgreSQL 或快取層；與既有 MQTT ingest 分層，避免單一進程阻塞。
7. **TLS 全鏈路**：Mosquitto、後端與 **Nginx** 對外憑證之更新流程（**§2.1A**、延續 §6.0）。
8. **PGAdmin**：容器化部署、連線僅內網或 VPN；帳密與角色分離。

**前端（Clean Architecture）**
1. **Dashboard 深化**：在 **§6.0.1** 之 **series API 與圖表框架**上擴充：**多裝置／多站臺**切換、**多指標同圖或分面**、**遙測歷程**長區間與 **降採樣** 後之顯示、**告警與補傳** 之視覺區分（疊線／標記）、**響應式版面**（對應官方螢幕等使用情境）。
2. **可選**：即時性需求高時，於後端提供 SSE 或 WebSocket 訂閱摘要（仍避免瀏覽器直連 MQTT）。

**Pi 5 可選周邊（非 Pico 主線）**
1. **Pi 5 端 LCD1602（3.3V）+ KY-016（已購買）**：主機／容器狀態字元顯示與 RGB 狀態燈；接線見 **§2.6**。**可選 Grove LCD RGB Backlight**：硬體計畫 §2.6／§2.8 之 Docker 狀態顯示（5V、電平轉換）；**systemd timer** 週期刷新；與 **Pico LCD1602（I2C1）**、**Pi LCD1602** 為不同硬體路徑，勿混為同一驗收項。

**驗收條件（第四階段）**
- Docker 監控資料可經 API 或 MQTT 被儀表或查詢工具消費。
- 生產環境 TLS、備份與還原演練（PostgreSQL）有書面程序。
- Redis 快取命中率、失效策略與回退方案（停用快取）有量測與紀錄。
- Prometheus + Grafana 儀表板可顯示 API/DB/MQTT 核心指標，且具基本告警規則。
- Loki + Promtail 可依 `request_id` / `device_id` 串聯查詢跨容器日誌。
- （若採用）Pi 端 LCD RGB 可依容器狀態顯示顏色與兩行文字，且權限設定符合最小權限原則。

#### 6.0.2A 容器服務命名與 Compose 對齊建議（第四階段）

> 目的：降低後續整合與排障成本，避免不同文件或環境使用不同服務名稱。

**建議服務名稱（docker-compose）**

| 類別 | 建議 service name | 用途 |
|---|---|---|
| API | `backend_api` | ASP.NET Core API 主服務 |
| 前端 | `frontend_web` | React + Nginx 靜態與反向代理 |
| DB | `postgresql` | PostgreSQL 主資料庫 |
| MQTT | `mqtt_broker` | Mosquitto 訊息代理 |
| 快取 | `redis_cache` | 查詢快取、限流、短期狀態 |
| 指標採集 | `prometheus` | 指標抓取與儲存 |
| 指標儀表 | `grafana` | 監控儀表板與告警視覺化 |
| 日誌儲存 | `loki` | 日誌索引與查詢 |
| 日誌收集 | `promtail` | 讀取容器/檔案 log 並送至 Loki |
| DB 管理（內網） | `pgadmin` | PostgreSQL 管理工具 |

**命名與網路規範**

- service name 與 `container_name` 建議保持一致，避免查 log / script 對照困難。
- 內部 DNS 一律用 service name（例如 `redis_cache:6379`、`prometheus:9090`）。
- `backend_api` 僅暴露對外必要埠；`postgresql`、`redis_cache`、`loki`、`prometheus` 預設僅內網。
- `promtail` 掛載容器 log 路徑時，需與主機/Compose 實際 log 路徑一致（避免空資料）。

**環境變數鍵名建議（與 service name 對齊）**

- `REDIS_HOST=redis_cache`
- `PROMETHEUS_HOST=prometheus`
- `LOKI_HOST=loki`
- `GRAFANA_HOST=grafana`
- `POSTGRES_HOST=postgresql`
- `MQTT_HOST=mqtt_broker`

### 6.1 `iot/2026-04-05 012821.png`：新硬體整合之目標、驗收與接線對照

**採購對照（本圖）**：Sensirion **SCD41**、Seeed **Grove Mini PIR**、**Grove→DuPont 4×1p 公頭線（20 cm，5 條／包）**。

#### 加入新硬體後應達成之階段目標

| 軌道 | 目標 |
|------|------|
| **韌體（對齊階段一／見下方 §6.2 項 1）** | **SCD41** 僅掛 **I2C1**，固定 **7-bit `0x62`**，與 **§2.4** 之 DS3231／AT24C256／LCD1602 並聯；**不可**因 I2C0 Hub 已滿而改接 Hub。**Grove Mini PIR** 僅使用 **GPIO6** 作數位輸入，與 I2C 腳位分離。**Grove→DuPont** 僅作 **連接器轉接**，不改變電氣規格。 |
| **韌體（資料語意）** | 上報 **CO₂／溫／溼**（SCD41）及 **PIR**（**`pir_active`**）；與 **BME680** 並存時 JSON **分鍵**依 **§2.4.2**，避免儀表混用。 |
| **Pi 5 並行（§6.0 階段一）** | **ingest**、遙測表結構、**`GET /api/v1/telemetry/series`** 之 **`metrics`** 能容納 **§2.4.2** 鍵（**`co2_ppm`**、**`temperature_c_scd41`**、**`humidity_pct_scd41`**、**`pir_active`**）；儀表可 **列表或折線** 呈現（**§6.0.1**）。 |

#### 驗收方式

| 層級 | 驗收內容 |
|------|----------|
| **接線（硬體）** | **§2.4.1 接線確認清單** 各項可勾選；**I2C1** 掃描在模組接妥後可見 **`0x62`**；**GPIO6** 與 PIR 動作一致（含有效位準／除彈跳策略與韌體約定一致）。 |
| **韌體／裝置端** | 序列埠或 LCD 可讀到 **合理 CO₂**（依 Sensirion 規範含上電後等待／自調適）；**MQTT `telemetry`** payload 含 **`co2_ppm`**（及約定欄位）與 PIR 相關欄位；與 OLED／LCD 摘要 **時間序一致**（允許秒級延遲）。 |
| **全棧（與 §6.0 對齊階段一驗收）** | 經 **Nginx → HTTPS → backend API** 可查 **列表與 series**；若韌體已送出 SCD41／PIR，**DB 抽樣**與 **儀表** 數值一致；**JWT** 路徑依環境約定。 |

#### 接線與腳位已納入本文件之位置

| 內容 | 章節或檔案 |
|------|------------|
| 匯流排分工、腳位、**`0x62`** | **§2.4**、**§2.4.1**、本節上表 |
| 單板／雙麵包板拓樸與 **GPIO6／SCD41** 文字圖 | **§5.1～§5.2**（**§5.3** 為 **MOSFET** 另述） |
| 逐步線序、ESD、量測注意 | `Pico2WH_Hardware_Plan_v3.md` **§2.9** |

---

### 6.2 韌體階段 1～3（Pico 主線，詳列）

1. **階段一：雙總線掃描顯示（I2C0 Hub：5 孔 I2C + 電源孔；LCD1602 + SCD41 於 I2C1；Grove Mini PIR 於 GPIO6）**
   1. 上電線路確認（先硬體再軟體）
      1. 3.3V 供電：`Pin36 (3V3 OUT)` -> 麵包板 3.3V 正軌，所有 I2C 模組 VCC 從此取電。
      2. 共地：`Pin38 (GND)` -> 麵包板 GND 負軌，SDA/SCL 的參考地也一致。
      3. LCD1602：使用「白底黑字、3.3V 版」，**SDA/SCL 接 I2C1（GPIO2/3）** 與 DS3231／AT24C256 並聯，勿再佔 Hub 埠。
      4. **SCD41**：**SDA/SCL** 併入 **同一 I2C1**（`0x62`）；**VCC 3.3V**；**不可**因 Hub 已滿而誤接 I2C0（見 **§2.4.1**）。
      5. **Grove Mini PIR**：**SIG → GPIO6**，VCC/GND 依模組（優先 3.3V 共軌）；見 **§2.4.1**、硬體計畫 **§2.9**。
      6. MPU-9250：經 Hub 接 I2C0；DS3231 僅接 I2C1，避免兩顆 `0x68` 併在同一 bus。
      7. 每次你切換「stage 模式」（scan only / init only / full init）後，請執行 `第 7 章 flash.sh` 重新燒錄再測。
   2. Pico 端 I2C 掃描（先確定位址）
      1. 開啟「scan only」模式，同時掃描 `I2C0(GPIO4/5)` 與 `I2C1(GPIO2/3)`。
      2. 透過 USB serial 輸出或先在 SH1106 上顯示掃描結果。
      3. 記錄本次實測位址：`I2C0` 的 SH1106/MPU-9250 與其餘 Hub 裝置，以及 `I2C1` 的 DS3231/AT24C256/**LCD1602**/**SCD41（`0x62`）**（接線後）。
   3. 顯示器與 MPU 逐一啟用（避免多故障點同時發生）
      1. SH1106：顯示 `SH1106 OK`，並確認已套用 `Column Offset +2`。
      2. LCD1602（**I2C1**）：顯示 `LCD1602 OK` + `addr=0x??`。
      3. MPU-9250：讀取 ID/感測暫存器，確認典型位址 `0x68` 僅出現在 I2C0。
   4. Hub 主裝置 + I2C1 LCD 同時運作
      1. 同時啟用 SH1106（I2C0）、LCD1602（I2C1）與 MPU 讀值（其餘感測可逐步併入）。
      2. 刷新 SH1106 頁面、LCD1602 除錯字串，並以序列或 SH1106 顯示 MPU 摘要。
      3. 若其中一顆失敗：回到掃描結果判斷是否位址衝突（Hub 不會改位址）。
      4. 若位址衝突：優先嘗試調整模組位址跳線；仍不行則啟用 `TCA9548A` 分通道硬隔離。
   5. **SCD41／PIR 韌體與遙測（階段一交付；與 §6.0 對齊階段一、硬體計畫 🟢 階段一 §6 一致）**
      1. **SCD41**：實作 **SCD4x** 初始化、週期讀值、讀取 **CO₂／溫／溼**；與 BME 並存時 **JSON 分鍵**上報。
      2. **Grove Mini PIR**：讀 **GPIO6**（數位或 IRQ），**除彈跳**可選；上報 **`pir_active`**（可選 **`motion`** 別名，見 **§2.4.2**）。
      3. **MQTT → Pi 5 ingest**：payload 含 **`co2_ppm`** 等欄位；**`GET /api/v1/telemetry/series?metrics=co2_ppm`** 可查詢；儀表可選 **CO₂ 折線**（**§6.0.1**）。
   6. KY-016（已購買）與 Pi 5 端第二片 LCD1602（與 Pico 階段並行驗收）
      1. **Pico**：僅驗收 **一片** LCD1602（**I2C1**，見上列步驟）；**不**在 Pico 預留 KY-016。
      2. **Pi 5**：第二片 **LCD1602（3.3V）** 經 **T 型擴充板**接 **I2C1**；**KY-016** 接 BCM **`GPIO17/27/22`** → R/G/B，見 **§2.6**。
      3. 亮滅／顏色測試：綠（正常）/黃（警告）/紅（錯誤）；若模組為共陽版本，程式輸出需反相。
2. **階段二：PAJ7620U2 手勢切頁 + SH1106 / LCD1602 同步更新**
   1. 位址與讀值驗證：用 I2C0 scan 確認 PAJ7620U2（典型 `0x73`），再用序列輸出確認 `gesture` 會隨揮手改變。
   2. 切頁邏輯：LEFT 切頁 A、RIGHT 切頁 B（NONE 保持）。
   3. 顯示策略對應：
      1. SH1106：A/B 兩頁的完整數據（時間+環境+光照）。
      2. LCD1602：顯示頁面簡稱、`gesture=` 與 `last_update=...`（必要時顯示 I2C err code）。
      3. （選用）序列輸出：補充頁面全名或數值，減輕 16x2 限制。
   4. 測試流程：揮手一次 -> 等 2~5 秒確認 SH1106 與 LCD1602 都更新 -> 長時間運行 10 分鐘不鎖死。
3. **階段三：MPU-9250 震動監控 + AT24C256 斷線快取 + MOSFET(PWM) 告警**
   1. MPU-9250 震動邏輯（I2C0，階段一已掛接；本段為門檻與告警）
      1. 透過 I2C scan 確認 MPU 僅在 I2C0 出現，典型位址 `0x68`（Grove 板無 AD0 跳線時）。
      2. 序列輸出確認原始加速度/角速度會隨敲擊變動。
   2. 震動判斷門檻（先保守）
      1. 先跑背景 RMS，設定 `threshold = k * rms`。
      2. 震動時序列輸出 `rms/peak/trigger`，並在 SH1106/LCD1602 顯示 `ALARM`。
   3. AT24C256 EEPROM 快取測試（I2C1）
      1. 寫入固定 pattern 或遞增 bytes，讀回比對一致性。
      2. 斷電重上驗證資料仍在，確認 ring buffer 寫入指標正常循環。
   4. MOSFET(PWM) 告警測試（GPIO16）
      1. 先用安全負載（假負載/小風扇/小電阻），從低 duty（10%~30%）開始。
      2. 序列輸出 PWM duty，並確認負載狀態符合。
      3. 異常觸發後檢查 MOSFET 不發燙、接線不鬆動。

---

## 7. flash.sh（相容 v5 架構）

```bash
#!/usr/bin/env bash
set -euo pipefail

BUILD_DIR="build"
TARGET="iiot_firmware"

cmake -S . -B "${BUILD_DIR}" -DPICO_BOARD=pico2_w -DI2C_DUAL_BUS=ON
cmake --build "${BUILD_DIR}" -j"$(nproc)"

openocd -f interface/cmsis-dap.cfg -f target/rp2350.cfg \
  -c "adapter speed 5000" \
  -c "init" \
  -c "program ${BUILD_DIR}/${TARGET}.elf verify reset" \
  -c "shutdown"

echo "[OK] flashed v5 dual-bus firmware"
```

---

## 8. 附錄

### 8.1 圖片來源（本次重新掃描）

- `pi/2026-03-18 140954.png`
- `pi/2026-03-18 141136.png`
- `pi/2026-03-18 141516.png`
- `pi/2026-03-20 144410.png`
- `iot/2026-03-17 033328.png`
- `iot/2026-03-27 002723.png`（含 INA219、Grove LCD RGB、BME280、6-Port Hub）
- `iot/2026-03-27 002807.png`
- `iot/2026-03-27 002917.png`
- `iot/2026-03-30 203826.png`
- `iot/2026-03-30 204902.png`
- `iot/2026-04-05 012821.png`（**SCD41**、**Grove Mini PIR**、**Grove→DuPont 公頭線 20 cm**；對齊 **§2.4.1**、**§6.1**）
- `iot/2026-04-02 165148.png`（歷史接線參考；主規格已改為 Hub Port2 接 MPU-9250，非 0.96 OLED）

### 8.2 MQTT 衝突預防（Client ID）

- 若使用 Mosquitto C library，建議：

```c
struct mosquitto *mosq = mosquitto_new(NULL, true, user_data);
```

- 將 `client_id` 設為 `NULL` 可由 library 產生唯一 ID，降低重複 ID 造成的踢線衝突。  
- 同時建議在 Topic 設計中加入裝置序號前綴（例如 `iiot/pico2wh/<device_sn>/...`）。

### 8.3 DB 遠端連線修正（Docker 內 PostgreSQL）

> 以下示範容器名為 `postgres`，請依實際 `docker ps` 名稱替換。

```bash
# 1) 允許遠端 host 連入（示範：全網段，正式環境請限縮 CIDR）
docker exec -it postgres bash -lc "echo 'host all all 0.0.0.0/0 scram-sha-256' >> /var/lib/postgresql/data/pg_hba.conf"

# 2) 開啟 listen_addresses
docker exec -it postgres bash -lc "sed -i \"s/^#listen_addresses =.*/listen_addresses = '*'/\" /var/lib/postgresql/data/postgresql.conf"

# 3) 重啟容器
docker restart postgres

# 4) 驗證
docker exec -it postgres psql -U postgres -c "SHOW listen_addresses;"
```

> 安全建議：正式環境請改為特定管理網段（例如 `192.168.1.0/24`）並搭配強密碼與防火牆規則。

### 8.4 I2C 故障排除

```bash
sudo i2cdetect -y 1
```

- 若掃描不到設備，請確認 `/boot/firmware/config.txt`（或發行版對應路徑）已啟用：`dtparam=i2c_arm=on`。
- 變更後建議重新開機，再以 `i2cdetect -l` 與 `i2cdetect -y 1` 交叉確認。
