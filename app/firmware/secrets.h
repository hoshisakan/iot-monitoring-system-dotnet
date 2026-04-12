#ifndef _SECRETS_H
#define _SECRETS_H

#define WIFI_SSID "hoshiyou"
#define WIFI_PASSWORD "Asahi302496"

#define MQTT_BROKER_IP "172.20.10.3"
#define MQTT_BROKER_PORT 8883

/*
 * 統一命名空間：預設會組出
 *   iiot/<MQTT_SITE>/<MQTT_CLIENT_ID>/telemetry
 *   iiot/<MQTT_SITE>/<MQTT_CLIENT_ID>/status
 *   iiot/<MQTT_SITE>/<MQTT_CLIENT_ID>/telemetry/sync-back
 *   iiot/<MQTT_SITE>/<MQTT_CLIENT_ID>/ui-events
 * 完整規則見 include/mqtt_topics.h；若需覆寫完整 topic，可取消下方註解並自行填寫字串。
 */
#define MQTT_NAMESPACE_PREFIX "iiot"
#define MQTT_SITE "default"
#define MQTT_CLIENT_ID "pico2_wh_1"

/* 選填：覆寫完整 MQTT 發布 topic（不設定則由前三項自動組字串） */
/* #define MQTT_TOPIC_TELEMETRY "iiot/default/pico2_wh_1/telemetry" */
/* #define MQTT_TOPIC_STATUS "iiot/default/pico2_wh_1/status" */
/* #define MQTT_TOPIC_TELEMETRY_SYNC_BACK "iiot/default/pico2_wh_1/telemetry/sync-back" */
/* #define MQTT_TOPIC_UI_EVENTS "iiot/default/pico2_wh_1/ui-events" */
/* #define MQTT_TOPIC_CONTROL "iiot/default/pico2_wh_1/control" */

#define MQTT_USER "mahiro"
#define MQTT_PASS "Mafuyu9325160@"

/* RTC：裝置端可信年份下限（20xx，例如 26 表示 2026 年） */
#define RTC_TRUSTED_MIN_YEAR 26

/* 遙測上報間隔（毫秒；主迴圈非阻塞排程） */
#define ENV_REPORT_INTERVAL_MS 10000U

/* IMU 取樣間隔（毫秒；主迴圈非阻塞） */
#define IMU_SAMPLING_INTERVAL_MS 100U

/* 加速度超過此閾值（單位：g）時觸發緊急上報（與 IMU 整合預留） */
#define ACCEL_URGENT_THRESHOLD 1.5f

/* 測試用：無 MPU-9250 硬體時設為 1 可強制觸發緊急上報路徑 */
#define IMU_SIMULATE_URGENT 0

/* NTP 時間同步：重試次數與重試間隔（毫秒） */
#define NTP_SYNC_MAX_RETRIES 5U
#define NTP_RETRY_DELAY_MS 2000U

/* OLED 分頁輪播間隔（毫秒） */
#define OLED_PAGE_ROTATE_INTERVAL_MS 2000U

/* 選填：MPU-9250 的 7-bit I2C 位址（預設見 firmware_config_defaults.h：0x68；AD0 接 3.3V 時常為 0x69） */
/* #define MPU9250_I2C_ADDR_7BIT 0x69 */

/* LCD1602 I2C 背包（PCF8574，接 I2C1）：設 0 關閉；未設定時見 firmware_config_defaults.h */
#define ENABLE_LCD1602 1
/* 選填：覆寫 7-bit 位址（常見 0x27 或 0x3F；預設見 firmware_config_defaults.h） */
#define LCD1602_I2C_ADDR_7BIT 0x27U
/* 背光：LiquidCrystal 相容預設 0x08；全暗時可試 0x00 或 0x80（預設見 firmware_config_defaults.h） */
/* #define LCD1602_BACKLIGHT_MASK 0x08U */

/* mbedTLS 堆積大小（KB）；實際配置位元組數 = MBEDTLS_HEAP_KB * 1024 */
#define MBEDTLS_HEAP_KB 320U

/* SH1106 OLED 的 7-bit I2C 位址（常見為 0x3C） */
#define SH1106_OLED_I2C_ADDR_7BIT 0x3CU

/* 環境感測器 7-bit 位址（設為 0 可改回自動探測） */
#define BME680_I2C_ADDR_7BIT 0x76U
#define TSL2561_I2C_ADDR_7BIT 0x29U

/*
 * 選填：環境感測器匯流排
 * 預設 BME680／TSL2561 與 DS3231 分開匯流排；若設為 0 則改為與 RTC 共用同一條 I2C，
 * 並可一併指定 SDA／SCL 腳位與鮑率。
 */
/* #define ENV_I2C_USE_I2C0_FOR_SENSORS 0 */
/* #define ENV_I2C_SDA_PIN 4 */
/* #define ENV_I2C_SCL_PIN 5 */
/* #define ENV_I2C_BAUD_HZ (100 * 1000) */

/* AT24C256 7-bit 位址（預設/建議 0x50U） */
#define AT24C256_I2C_ADDR_7BIT 0x50U
/* SCD41 7-bit 位址（預設 0x62U；若硬體設計不同可覆寫） */
#define SCD41_I2C_ADDR_7BIT 0x62U
#define SCD41_POLL_INTERVAL_MS 5000U
/* 斷線遙測佇列筆數上限（單筆約 520B；32KiB EEPROM 理論約 ≤62；預設 24） */
#define TELEM_EEPROM_NUM_SLOTS 32U
/* 斷線遙測寫入 EEPROM：設 0 關閉（僅即時 MQTT，不會印 [EEPROM] probe）；未設定時 defaults 為 1 */
#define ENABLE_TELEMETRY_EEPROM_CACHE 1
/* sync-back 回放節流（毫秒）；獨立於 ENV_REPORT_INTERVAL_MS */
#define SYNC_BACK_INTERVAL_MS 1000U
#define ENABLE_I2C_BOOT_SCAN 1
/* 無 PAJ7620 時可測後端 device_ui_events：MQTT 連上並送出 SESSION 狀態後，發一筆至 .../ui-events */
#define ENABLE_TEST_UI_EVENTS_MQTT_PUBLISH 0
/* PAJ7620 手勢事件（LEFT/RIGHT/UP/DOWN/...）發佈到 .../ui-events（需 boot 掃描可探測到 0x73） */
#define ENABLE_PAJ7620_UI_EVENTS 0
#define PAJ7620_POLL_INTERVAL_MS 80U
#define PAJ7620_GESTURE_MIN_INTERVAL_MS 180U
/* bring-up 期間建議開啟，觀察 0x43/0x44 原始手勢寄存器；穩定後可設 0 */
#define ENABLE_PAJ7620_DEBUG_LOG 0
/* bring-up 進階：只要 g43/g44 非 0 即發 raw ui-event（繞過 mapping/debounce） */
#define ENABLE_PAJ7620_RAW_UI_EVENT_DEBUG 0
/* 掃描結束後暫停多久再初始化感測器（毫秒），方便閱讀 I2C scan log；僅 ENABLE_I2C_BOOT_SCAN 為 1 時有效；0=不延遲；未設定時見 firmware_config_defaults.h（3000） */
#define I2C_BOOT_SCAN_HOLD_MS 5000U

/* USB 就緒後緩衝（毫秒）：與「相同作用」設定請只改此巨集，勿再新增重複變數。預設見 firmware_config_defaults.h */
#define BOOT_POST_USB_DELAY_MS 30000U

#endif
