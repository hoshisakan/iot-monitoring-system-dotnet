#ifndef FIRMWARE_CONFIG_DEFAULTS_H
#define FIRMWARE_CONFIG_DEFAULTS_H

/*
 * Optional tunables with defaults. secrets.h may #define these before this file is included.
 */

#ifndef NTP_SYNC_MAX_RETRIES
#define NTP_SYNC_MAX_RETRIES 5U
#endif

#ifndef NTP_RETRY_DELAY_MS
#define NTP_RETRY_DELAY_MS 2000U
#endif

#ifndef OLED_PAGE_ROTATE_INTERVAL_MS
#define OLED_PAGE_ROTATE_INTERVAL_MS 2000U
#endif

#ifndef MBEDTLS_HEAP_KB
#define MBEDTLS_HEAP_KB 320U
#endif

#ifndef MBEDTLS_HEAP_SIZE
#define MBEDTLS_HEAP_SIZE ((unsigned)(MBEDTLS_HEAP_KB) * 1024U)
#endif

#ifndef SH1106_OLED_I2C_ADDR_7BIT
#define SH1106_OLED_I2C_ADDR_7BIT 0x3CU
#endif

/*
 * BME680 / TSL2561 address selection:
 * - Set to specific 7-bit address to lock initialization to one device.
 * - Set to 0 to keep legacy auto-probe behavior.
 */
#ifndef BME680_I2C_ADDR_7BIT
#define BME680_I2C_ADDR_7BIT 0x76U
#endif

#ifndef TSL2561_I2C_ADDR_7BIT
#define TSL2561_I2C_ADDR_7BIT 0x29U
#endif

/* MPU-9250 on I2C0 (same bus as env sensors); AD0 low=0x68, high=0x69 */
#ifndef MPU9250_I2C_ADDR_7BIT
#define MPU9250_I2C_ADDR_7BIT 0x68U
#endif

/* AT24C256 on I2C1 with DS3231; A2=A1=A0=GND -> 0x50 (board AT24C32 is often 0x57 — do not confuse) */
#ifndef AT24C256_I2C_ADDR_7BIT
#define AT24C256_I2C_ADDR_7BIT 0x50U
#endif

/* SCD41 on I2C1 (default 7-bit address 0x62; allow override in secrets.h). */
#ifndef SCD41_I2C_ADDR_7BIT
#define SCD41_I2C_ADDR_7BIT 0x62U
#endif

#ifndef SCD41_POLL_INTERVAL_MS
#define SCD41_POLL_INTERVAL_MS 5000U
#endif

#ifndef SCD41_RETRY_INTERVAL_MS
#define SCD41_RETRY_INTERVAL_MS 3000U
#endif

/* Grove Mini PIR on GPIO6 (SIG/REL -> GP6). Most modules need internal pull-up; set 0 only if you use external resistor. */
#ifndef PIR_TEST_GPIO
#define PIR_TEST_GPIO 6
#endif
/* 1: motion = GPIO high; 0: motion = GPIO low (invert). */
#ifndef PIR_TEST_ACTIVE_HIGH
#define PIR_TEST_ACTIVE_HIGH 1
#endif
#ifndef PIR_TEST_PULL_UP
#define PIR_TEST_PULL_UP 1
#endif

/*
 * Offline telemetry FIFO in AT24C256: number of fixed-size slots (see telemetry_eeprom_queue.c).
 * Each slot is 520 bytes; 64 bytes reserved for queue header → practical max ~62 on 32 KiB AT24C256.
 * Default 24 balances retention vs write time / wear.
 */
#ifndef TELEM_EEPROM_NUM_SLOTS
#define TELEM_EEPROM_NUM_SLOTS 24U
#endif

/* Stage 3B: buffer telemetry to EEPROM when MQTT publish fails or MQTT is down */
#ifndef ENABLE_TELEMETRY_EEPROM_CACHE
#define ENABLE_TELEMETRY_EEPROM_CACHE 1
#endif

/*
 * Sync-back replay pacing (ms): when EEPROM backlog exists, publish at most one replay payload
 * per interval to avoid flooding and to keep live telemetry cadence intuitive.
 */
#ifndef SYNC_BACK_INTERVAL_MS
#define SYNC_BACK_INTERVAL_MS 1000U
#endif

/* Stage 1: print I2C0/I2C1 address scan once at boot (write-probe, ~2 ms/addr) */
#ifndef ENABLE_I2C_BOOT_SCAN
#define ENABLE_I2C_BOOT_SCAN 0
#endif

/*
 * When 1: after MQTT session online (same boot phase as MQTT_SESSION_ONLINE status), publish one JSON
 * to MQTT_TOPIC_UI_EVENTS for backend ingest testing (no PAJ7620 required). Default 0.
 */
#ifndef ENABLE_TEST_UI_EVENTS_MQTT_PUBLISH
#define ENABLE_TEST_UI_EVENTS_MQTT_PUBLISH 0
#endif

/*
 * When 1: poll PAJ7620 gesture register and publish minimal ui-events to MQTT_TOPIC_UI_EVENTS.
 * Requires PAJ7620 to be detected during boot scan wake probe.
 */
#ifndef ENABLE_PAJ7620_UI_EVENTS
#define ENABLE_PAJ7620_UI_EVENTS 0
#endif

#ifndef PAJ7620_POLL_INTERVAL_MS
#define PAJ7620_POLL_INTERVAL_MS 80U
#endif

/* Debounce / rate-limit for PAJ gesture publish path. */
#ifndef PAJ7620_GESTURE_MIN_INTERVAL_MS
#define PAJ7620_GESTURE_MIN_INTERVAL_MS 180U
#endif

/* Print raw PAJ gesture registers (0x43/0x44) periodically for bring-up diagnostics. */
#ifndef ENABLE_PAJ7620_DEBUG_LOG
#define ENABLE_PAJ7620_DEBUG_LOG 1
#endif

/*
 * When 1: publish a raw debug ui-event whenever g43/g44 has any non-zero bit.
 * This bypasses gesture name mapping and debounce so bring-up can verify sensor activity quickly.
 */
#ifndef ENABLE_PAJ7620_RAW_UI_EVENT_DEBUG
#define ENABLE_PAJ7620_RAW_UI_EVENT_DEBUG 0
#endif

/*
 * After "[I2C] === boot scan done ===", pause before BME680 / OLED / WiFi init so the scan
 * lines stay readable on serial. Only used when ENABLE_I2C_BOOT_SCAN is 1. Set 0 to skip.
 */
#ifndef I2C_BOOT_SCAN_HOLD_MS
#define I2C_BOOT_SCAN_HOLD_MS 3000U
#endif

/*
 * After "[BOOT] USB serial ready", wait this many milliseconds (1 s steps + optional remainder)
 * so you can attach the serial monitor before I2C scan / sensors / WiFi. Set 0 to disable.
 * Typical: 5000–10000.
 *
 * Single knob for this purpose — do not add a second macro with the same role (avoid drift).
 * If you later need a different delay at another boot phase, introduce a separately named define
 * with a distinct purpose (e.g. pre-MQTT only), not a duplicate "grace" value.
 */
#ifndef BOOT_POST_USB_DELAY_MS
#define BOOT_POST_USB_DELAY_MS 7000U
#endif

/* LCD1602 (PCF8574 backpack) on I2C1 — secrets.h may override address / ENABLE */
#ifndef ENABLE_LCD1602
#define ENABLE_LCD1602 1
#endif

#ifndef LCD1602_I2C_ADDR_7BIT
#define LCD1602_I2C_ADDR_7BIT 0x27U
#endif

#ifndef LCD1602_REFRESH_MIN_MS
#define LCD1602_REFRESH_MIN_MS 500U
#endif

/*
 * 與每次寫入 PCF8574 一併 OR 的背光位元。
 * **LiquidCrystal_I2C / YWROBOT 相容背包**：背光在 P3 → **0x08**（預設）。
 * 若仍全暗可試 0x00；極少數舊板背光在 P7 → 0x80。
 */
#ifndef LCD1602_BACKLIGHT_MASK
#define LCD1602_BACKLIGHT_MASK 0x08U
#endif

#endif /* FIRMWARE_CONFIG_DEFAULTS_H */
