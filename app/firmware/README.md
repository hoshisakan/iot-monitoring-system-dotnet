# 韌體（Pico 2 W）

本目錄為 **Raspberry Pi Pico 2 W** 之 **C** 專案，使用 **Pico SDK** 與 **CMake** 建置。功能包含 **Wi‑Fi**、**MQTT**（**TLS**）、環境／慣性感測、選配 **UI** 手勢事件等。

## 前置需求

- **Pico SDK**（環境變數 `PICO_SDK_PATH` 或專案內 `pico_sdk_import.cmake`）
- 建置工具：**CMake**、**ARM GCC** toolchain（依 **Pico** 官方文件）

## 組態與機密

- 複製 `secrets.h.example` 為 `secrets.h`，填入 **Wi‑Fi**、**MQTT** 帳密、**Broker** 位址與 **Topic** 命名空間（見 `include/mqtt_topics.h`）。
- 建置時可自 `../../conf/mqtt_broker/certs/local/ca.crt` 產生內嵌 **CA**，供 **TLS** 驗證（見根目錄 `CMakeLists.txt` 中憑證路徑）。

## MQTT 發布（摘要）

預設 **Topic** 形如：`iiot/<site>/<client_id>/telemetry|status|telemetry/sync-back|ui-events`；裝置訂閱 `.../control` 接收後端指令。結構化「裝置日誌」類訊息發布於 **`status`**（非 **USB** 序列埠除錯列印）。

## 建置（範例）

於本目錄建立 `build`、執行 **CMake** 與 **ninja/make**，產出韌體映像；細節請依 **Pico SDK** 官方 **Getting Started** 流程操作。

---