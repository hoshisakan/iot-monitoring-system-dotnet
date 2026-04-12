# IoT 監控系統（.NET）

邊緣裝置（Raspberry Pi Pico 2 W）蒐集感測資料，經 **MQTT** 送至後端 **ASP.NET Core** 服務，資料持久化於 **PostgreSQL**，並以 **React** 前端儀表板呈現。基礎設施（Broker、資料庫、**Nginx** 等）可透過儲存庫根目錄的 **Docker Compose** 啟動。

## 儲存庫結構（`app/`）

| 路徑 | 說明 |
|------|------|
| `app/firmware/` | **Pico SDK** 韌體（C）：Wi‑Fi、**MQTT**（TLS）、感測器與裝置端排程。 |
| `app/backend/` | **.NET 8** Web API：**JWT** 驗證、**MediatR**、**EF Core**、背景 **MQTT ingest**、**Docker Engine API** 系統狀態查詢。 |
| `app/frontend/` | **Vite** + **React** + **Mantine**：登入、遙測、裝置日誌／**UI events**、裝置控制、系統狀態等頁面。 |

各層建置與設定細節見該目錄內之 `README.md`。

## 根目錄其餘重點

- **環境變數**：請複製 **`.env.example`** 為 **`.env`**，再依環境填入實際帳密、**IP** 與埠位；範本不含敏感資訊，可納入版本庫，而 **`.env`** 已列於 **`.gitignore`**，切勿提交。
- **`docker-compose.yml`**：協調 **Mosquitto**、**PostgreSQL**、前端 **Nginx** 等服務；啟動時會讀取專案根目錄之 **`.env`**。
- **`conf/`**：**Nginx** 設定與憑證、**MQTT Broker** 設定與憑證、資料庫映像建置檔等。
- **`app/backend/db-migration-commands.txt`**：**EF Core** 遷移與 `dotnet ef database update` 範例指令。

## 建議開發流程（摘要）

1. 啟動或設定 **PostgreSQL**、**MQTT Broker**（可沿用 **Docker Compose** 服務）。
2. 於 `app/backend/` 套用資料庫遷移並執行 API（詳見該目錄 **README**）。
3. 於 `app/frontend/` 以開發模式連線至 API（**Reverse Proxy** 或本機 **CORS**／**Proxy** 依部署而定）。
4. 韌體燒錄與 **Broker**／**TLS** 憑證對齊見 `app/firmware/README.md`。

---