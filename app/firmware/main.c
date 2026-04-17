#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#include "hardware/structs/rosc.h"
#include "pico/aon_timer.h"
#include "pico/cyw43_arch.h"
#include "pico/stdlib.h"
#include "pico/time.h"

#include "hardware/gpio.h"
#include "hardware/i2c.h"

#include "at24c256_eeprom.h"
#include "bme680_sensor.h"
#include "scd41_sensor.h"
#include "ds3231.h"
#include "mbedtls/memory_buffer_alloc.h"
#include "mqtt_manager.h"
#include "ntp_client.h"
#include "secrets.h"
#include "firmware_config_defaults.h"
#include "mqtt_topics.h"
#include "mpu9250_sensor.h"
#include "sh1106_oled.h"
#include "telemetry_eeprom_queue.h"
#include "tsl2561_sensor.h"
#include "wifi_handler.h"
#include "generated_ca_cert.h"

#if ENABLE_LCD1602
#include "lcd1602_pcf8574.h"
#endif

#if ENABLE_I2C_BOOT_SCAN || ENABLE_PAJ7620_UI_EVENTS
/* Build log: confirms this TU was compiled with boot I2C scan enabled (requires secrets.h define). */
#pragma message("ENABLE_I2C_BOOT_SCAN: ON — I2C scan code is compiled in")
#endif
#if ENABLE_TELEMETRY_EEPROM_CACHE
#pragma message("ENABLE_TELEMETRY_EEPROM_CACHE: ON — AT24C256 probe at boot")
#else
#pragma message("ENABLE_TELEMETRY_EEPROM_CACHE: OFF — no EEPROM queue/probe")
#endif
#if ENABLE_LCD1602
#pragma message("ENABLE_LCD1602: ON — LCD1602 status on I2C1")
#else
#pragma message("ENABLE_LCD1602: OFF — no LCD1602")
#endif
#if ENABLE_TEST_UI_EVENTS_MQTT_PUBLISH
#pragma message("ENABLE_TEST_UI_EVENTS_MQTT_PUBLISH: ON — one-shot ui-events MQTT test after session online")
#endif
#if ENABLE_PAJ7620_UI_EVENTS
#pragma message("ENABLE_PAJ7620_UI_EVENTS: ON — periodic PAJ7620 gesture ui-events publish")
#endif
#define STR_IMPL(x) #x
#define STR(x) STR_IMPL(x)
#pragma message("BOOT_POST_USB_DELAY_MS=" STR(BOOT_POST_USB_DELAY_MS))

#define I2C0_SDA 4
#define I2C0_SCL 5
#define I2C0_BUS_HZ (100 * 1000)
#define I2C1_SDA 2
#define I2C1_SCL 3
/** Standard-mode 100 kHz for DS3231 and I2C1 slaves; bus recovery must use the same baud. */
#define I2C1_BUS_HZ (100 * 1000)
/** Grove Gesture (PAJ7620U2) fixed 7-bit I2C address. */
#define PAJ7620_I2C_ADDR_7BIT 0x73

typedef struct {
    uint32_t telemetry_interval_ms; /* from ENV_REPORT_INTERVAL_MS */
    float accel_urgent_threshold_g; /* urgent if |a| exceeds this */
} DeviceConfig;

typedef struct {
    float accel_x;
    float accel_y;
    float accel_z;
    float gyro_x;
    float gyro_y;
    float gyro_z;
} imu_sample_t;

typedef struct {
    bool detected;
    bool used_recovery;
    int wake_attempts;
    uint8_t part_id;
} paj_boot_diag_t;

/* Test helper is configured in secrets.h: IMU_SIMULATE_URGENT */
#ifndef IMU_SIMULATE_URGENT
#define IMU_SIMULATE_URGENT 0
#endif

/* Optional overrides in secrets.h — defaults match common Pico 2 WH wiring:
 * DS3231 on I2C1 (GP2/GP3); BME680 / TSL2561 on I2C0 (GP4/GP5). */
#ifndef ENV_I2C_USE_I2C0_FOR_SENSORS
#define ENV_I2C_USE_I2C0_FOR_SENSORS 1
#endif
#ifndef ENV_I2C_SDA_PIN
#define ENV_I2C_SDA_PIN 4
#endif
#ifndef ENV_I2C_SCL_PIN
#define ENV_I2C_SCL_PIN 5
#endif
/* RP2350: i2c_init returns actual Hz (depends on clk_sys); Standard-mode 100 kHz is safe for breadboard wiring. */
#ifndef ENV_I2C_BAUD_HZ
#define ENV_I2C_BAUD_HZ (100 * 1000)
#endif
static uint8_t mbedtls_heap[MBEDTLS_HEAP_SIZE] __attribute__((aligned(4)));
static wifi_handler_t g_wifi;
static mqtt_manager_t g_mqtt_telemetry;
static mqtt_manager_t g_mqtt_status;
static mqtt_manager_config_t mqtt_cfg_telemetry;
static mqtt_manager_config_t mqtt_cfg_status;
static char g_mqtt_client_id_telemetry[96];
static char g_mqtt_client_id_status[96];
static bool g_last_wifi_connected = false;
static bool g_prev_wifi_link = false;
static bool g_last_mqtt_connected = false;
static bool g_led_on = false;
static uint32_t g_next_led_toggle_ms = 0;
static uint32_t g_next_rtc_log_ms = 0;
static bool g_rtc_debug_logged_once = false;
static bool g_alert_boot_sent = false;
static bool g_prev_mqtt_connected_for_alert = false;
/** Cleared on each MQTT connect edge; set after MQTT_SESSION_ONLINE is queued. */
static bool g_mqtt_session_alert_sent = false;
#if ENABLE_TEST_UI_EVENTS_MQTT_PUBLISH
static bool g_ui_event_test_sent = false;
#endif
static bool g_bme680_ok = false;
static bool g_tsl2561_ok = false;
static bool g_mpu9250_ok = false;
static bool g_oled_ok = false;
static i2c_inst_t *g_env_i2c = NULL;
static bme680_sensor_data_t g_last_bme;
static tsl2561_sensor_data_t g_last_tsl;
static imu_sample_t g_last_imu;
static int g_last_rssi_dbm = 0;
static uint32_t g_last_sample_ms = 0;
static bool g_has_last_sample = false;
static uint32_t g_next_oled_rotate_ms = 0;
static uint8_t g_oled_page_idx = 0;
static paj_boot_diag_t g_paj_diag = {0};
static bool g_paj_gesture_ready = false;
static bool g_scd41_ok = false;
static bool g_scd41_started = false;
static uint32_t g_next_scd41_poll_ms = 0;
static uint32_t g_next_scd41_retry_ms = 0;
static uint32_t g_scd41_retry_count = 0;
static scd41_sensor_data_t g_last_scd41 = {0};
static bool g_pir_test_active = false;
static uint32_t g_next_pir_test_heartbeat_ms = 0;
static float g_control_pwm = 0.0f;
static bool g_control_alarm = false;
static bool g_control_fan = false;
static char g_control_mode[24] = "auto";
#if ENABLE_PAJ7620_UI_EVENTS
static uint32_t g_next_paj_poll_ms = 0;
static uint32_t g_paj_last_event_ms = 0;
static uint8_t g_paj_last_code = 0;
#endif
#if ENABLE_TELEMETRY_EEPROM_CACHE
static bool g_eeprom_queue_ok = false;
static char g_replay_buf[520];
static bool g_eeprom_boot_queue_init_ok = false;
static bool g_eeprom_boot_probe_ok = false;
static uint32_t g_next_sync_back_ms = 0;
#endif

#if ENABLE_LCD1602
static lcd1602_t g_lcd;
static bool g_lcd_ok = false;
static uint32_t g_next_lcd_refresh_ms = 0;
#endif

static const DeviceConfig g_device_cfg = {
    .telemetry_interval_ms = ENV_REPORT_INTERVAL_MS,
    .accel_urgent_threshold_g = ACCEL_URGENT_THRESHOLD,
};
#ifndef mbedtls_ms_time_t
typedef long long mbedtls_ms_time_t;
#endif

mbedtls_ms_time_t mbedtls_ms_time(void) {
    return (mbedtls_ms_time_t)to_ms_since_boot(get_absolute_time());
}

static uint32_t rosc_entropy_word(void) {
    uint32_t value = 0;
    for (int bit = 0; bit < 32; bit++) {
        value = (value << 1) | (rosc_hw->randombit & 0x1u);
    }
    return value;
}

__attribute__((weak)) int mbedtls_hardware_poll(void *data, unsigned char *output, size_t len, size_t *olen) {
    (void)data;
    uint32_t word = 0;
    uint32_t fallback = 0x7f4a7c15u ^ (uint32_t)to_ms_since_boot(get_absolute_time());
    uint32_t start_us = time_us_32();

    for (size_t i = 0; i < len; i++) {
        if ((i & 0x3u) == 0u) {
            if ((uint32_t)(time_us_32() - start_us) < 2000u) {
                word = rosc_entropy_word();
            } else {
                /* Bound runtime to keep USB/network interrupt service responsive. */
                fallback ^= fallback << 13;
                fallback ^= fallback >> 17;
                fallback ^= fallback << 5;
                word = fallback ^ (uint32_t)to_ms_since_boot(get_absolute_time());
            }
        }
        output[i] = (unsigned char)(word & 0xffu);
        word >>= 8;
    }
    *olen = len;
    return 0;
}

#if ENABLE_LCD1602
static void lcd_show_fatal_message(const char *reason) {
    if (!g_lcd_ok) {
        return;
    }
    char line1[17] = "FATAL ERROR";
    char line2[17] = {0};
    if (reason != NULL && reason[0] != '\0') {
        (void)snprintf(line2, sizeof(line2), "%.16s", reason);
    } else {
        (void)snprintf(line2, sizeof(line2), "see usb log");
    }
    lcd1602_put_line(&g_lcd, 0, line1);
    lcd1602_put_line(&g_lcd, 1, line2);
}
#endif

static void enter_safe_mode(const char *reason) {
    printf("[SAFE] %s\r\n", reason);
    fflush(stdout);
    while (true) {
        printf("[FATAL ERROR] %s\r\n", reason);
        fflush(stdout);
#if ENABLE_LCD1602
        lcd_show_fatal_message(reason);
#endif
        cyw43_arch_gpio_put(CYW43_WL_GPIO_LED_PIN, 1);
        sleep_ms(150);
        cyw43_arch_gpio_put(CYW43_WL_GPIO_LED_PIN, 0);
        sleep_ms(1850);
    }
}

static void update_status_outputs(uint32_t now_ms, bool wifi_connected, bool mqtt_connected) {
    uint32_t blink_period_ms = 150;

    if (wifi_connected && mqtt_connected) {
        blink_period_ms = 1000;
    } else if (wifi_connected) {
        blink_period_ms = 350;
    }

    if ((int32_t)(now_ms - g_next_led_toggle_ms) >= 0) {
        g_led_on = !g_led_on;
        cyw43_arch_gpio_put(CYW43_WL_GPIO_LED_PIN, g_led_on ? 1 : 0);
        g_next_led_toggle_ms = now_ms + blink_period_ms;
    }

    if (wifi_connected != g_last_wifi_connected) {
        printf("[WiFi] %s\n", wifi_connected ? "connected" : "disconnected");
        fflush(stdout);
        g_last_wifi_connected = wifi_connected;
    }
    if (mqtt_connected != g_last_mqtt_connected) {
        printf("[MQTT] %s\n", mqtt_connected ? "connected" : "disconnected");
        fflush(stdout);
        g_last_mqtt_connected = mqtt_connected;
    }
}

#if ENABLE_LCD1602
static int mqtt_diag_code_one(const mqtt_manager_t *ctx) {
    if (ctx == NULL) {
        return 0;
    }
    if (ctx->last_connect_invoke_rc != 0) {
        return ctx->last_connect_invoke_rc;
    }
    if (ctx->last_connect_status != 0 && ctx->last_connect_status != MQTT_CONNECT_ACCEPTED) {
        return ctx->last_connect_status;
    }
    if (ctx->last_publish_result != 0) {
        return ctx->last_publish_result;
    }
    return 0;
}

static int mqtt_diag_code_any(const mqtt_manager_t *a, const mqtt_manager_t *b) {
    int ea = mqtt_diag_code_one(a);
    if (ea != 0) {
        return ea;
    }
    return mqtt_diag_code_one(b);
}

static void lcd_refresh_mvp(uint32_t now_ms, bool wifi_connected, bool mqtt_connected, int rssi_dbm,
                            bool urgent, bool wifi_trying, bool mqtt_trying, int wifi_err_code, int mqtt_err_code) {
    if (!g_lcd_ok) {
        return;
    }
    /* Always throttle (including urgent). Previously urgent bypassed this and refreshed every
     * tight-loop iteration (~kHz), which strobes the LCD and looks like blank flicker. */
    if ((int32_t)(now_ms - g_next_lcd_refresh_ms) < 0) {
        return;
    }

    char line1[17];
    char line2[17];

    if (urgent) {
        (void)snprintf(line1, sizeof(line1), "    ALARM!      ");
        (void)snprintf(line2, sizeof(line2), " ACCEL URGENT  ");
    } else {
        const char *wf_state = wifi_connected ? "OK" : (wifi_trying ? "TRY" : (wifi_err_code != 0 ? "ERR" : "---"));
        const char *mq_state = mqtt_connected ? "OK" : (mqtt_trying ? "TRY" : (mqtt_err_code != 0 ? "ERR" : "---"));
        (void)snprintf(line1, sizeof(line1), "WF:%-3s MQ:%-3s", wf_state, mq_state);
        char qpart[8];
#if ENABLE_TELEMETRY_EEPROM_CACHE
        if (g_eeprom_queue_ok) {
            (void)snprintf(qpart, sizeof(qpart), "%03u", (unsigned)telemetry_eeprom_queue_count());
        } else {
            (void)snprintf(qpart, sizeof(qpart), "--");
        }
#else
        (void)snprintf(qpart, sizeof(qpart), "--");
#endif
        if (wifi_connected && mqtt_connected && !wifi_trying && !mqtt_trying && wifi_err_code == 0 && mqtt_err_code == 0) {
            (void)snprintf(line2, sizeof(line2), "RS:%d Q:%s", rssi_dbm, qpart);
        } else if (wifi_connected) {
            (void)snprintf(line2, sizeof(line2), "Ew:%d Em:%d", wifi_err_code, mqtt_err_code);
        } else {
            (void)snprintf(line2, sizeof(line2), "WF TRY:%d E:%d", wifi_trying ? 1 : 0, wifi_err_code);
        }
    }

    lcd1602_put_line(&g_lcd, 0, line1);
    lcd1602_put_line(&g_lcd, 1, line2);
    g_next_lcd_refresh_ms = now_ms + LCD1602_REFRESH_MIN_MS;
}
#endif /* ENABLE_LCD1602 */

static bool accel_is_urgent(const imu_sample_t *imu, float threshold_g) {
    if (imu == NULL) {
        return false;
    }
    float thr2 = threshold_g * threshold_g;
    float a2 = imu->accel_x * imu->accel_x + imu->accel_y * imu->accel_y + imu->accel_z * imu->accel_z;
    return a2 > thr2;
}

static void mpu9250_read_imu(imu_sample_t *out) {
    if (out == NULL) {
        return;
    }
#if IMU_SIMULATE_URGENT
    out->accel_x = g_device_cfg.accel_urgent_threshold_g + 0.3f;
    out->accel_y = 0.0f;
    out->accel_z = 0.0f;
    out->gyro_x = 0.0f;
    out->gyro_y = 0.0f;
    out->gyro_z = 0.0f;
#else
    if (g_mpu9250_ok &&
        mpu9250_sensor_read(&out->accel_x, &out->accel_y, &out->accel_z, &out->gyro_x, &out->gyro_y,
                              &out->gyro_z)) {
        return;
    }
    memset(out, 0, sizeof(*out));
#endif
}

#if ENABLE_I2C_BOOT_SCAN
static const char *i2c_known_addr_name(const char *tag, uint8_t addr) {
    if (strcmp(tag, "I2C0") == 0) {
        switch (addr) {
            case 0x73: return "PAJ7620";
            case 0x3C: return "SH1106";
            case 0x29: return "TSL2561";
            case 0x68: return "MPU-9250";
            case 0x76: return "BME680";
            default: return NULL;
        }
    }
    if (strcmp(tag, "I2C1") == 0) {
        switch (addr) {
            case 0x27: return "LCD1602";
            case 0x50: return "AT24C256";
            case 0x57: return "AT24C32/EEPROM";
            case 0x62: return "SCD41";
            case 0x68: return "DS3231";
            default: return NULL;
        }
    }
    return NULL;
}

static bool i2c_probe_addr(i2c_inst_t *i2c, uint8_t addr) {
    /* Primary probe: 1-byte write (classic bus scan behavior). */
    uint8_t dummy = 0;
    int r = i2c_write_timeout_us(i2c, addr, &dummy, 1, false, 25000);
    if (r == 1) {
        return true;
    }
    /* Fallback: 1-byte read probe, for slaves that respond better on read transaction. */
    uint8_t tmp = 0;
    r = i2c_read_timeout_us(i2c, addr, &tmp, 1, false, 25000);
    return r == 1;
}

static bool i2c_write_reg8(i2c_inst_t *i2c, uint8_t addr, uint8_t reg, uint8_t val) {
    uint8_t buf[2] = {reg, val};
    return i2c_write_timeout_us(i2c, addr, buf, 2, false, 25000) == 2;
}

static bool i2c_read_reg8(i2c_inst_t *i2c, uint8_t addr, uint8_t reg, uint8_t *out) {
    if (out == NULL) {
        return false;
    }
    if (i2c_write_timeout_us(i2c, addr, &reg, 1, true, 25000) != 1) {
        return false;
    }
    return i2c_read_timeout_us(i2c, addr, out, 1, false, 25000) == 1;
}

static void i2c0_recover_pins_and_reinit(uint32_t baud_hz) {
    i2c_deinit(i2c0);
    gpio_set_function(I2C0_SDA, GPIO_FUNC_SIO);
    gpio_set_function(I2C0_SCL, GPIO_FUNC_SIO);
    gpio_pull_up(I2C0_SDA);
    gpio_pull_up(I2C0_SCL);
    gpio_set_dir(I2C0_SDA, GPIO_IN);
    gpio_set_dir(I2C0_SCL, GPIO_OUT);
    gpio_put(I2C0_SCL, 1);
    sleep_us(20);
    for (int k = 0; k < 9 && !gpio_get(I2C0_SDA); k++) {
        gpio_put(I2C0_SCL, 0);
        sleep_us(5);
        gpio_put(I2C0_SCL, 1);
        sleep_us(5);
    }
    (void)i2c_init(i2c0, baud_hz);
    gpio_set_function(I2C0_SDA, GPIO_FUNC_I2C);
    gpio_set_function(I2C0_SCL, GPIO_FUNC_I2C);
    gpio_pull_up(I2C0_SDA);
    gpio_pull_up(I2C0_SCL);
    gpio_set_slew_rate(I2C0_SDA, GPIO_SLEW_RATE_SLOW);
    gpio_set_slew_rate(I2C0_SCL, GPIO_SLEW_RATE_SLOW);
}

/**
 * PAJ7620U2 may appear "missing" after warm reboot if still in low-power/undefined state.
 * Try a lightweight wake/register-bank select sequence before scan.
 */
static bool paj7620_try_wake_and_probe(i2c_inst_t *i2c) {
    const uint8_t addr = PAJ7620_I2C_ADDR_7BIT;
    g_paj_diag.detected = false;
    g_paj_diag.used_recovery = false;
    g_paj_diag.wake_attempts = 0;
    g_paj_diag.part_id = 0;
    if (i2c_probe_addr(i2c, addr)) {
        g_paj_diag.detected = true;
        g_paj_diag.wake_attempts = 0;
        return true;
    }

    for (int attempt = 0; attempt < 3; ++attempt) {
        g_paj_diag.wake_attempts = attempt + 1;
        /* Common PAJ init preamble: select register bank 0 via 0xEF=0x00. */
        (void)i2c_write_reg8(i2c, addr, 0xEF, 0x00);
        sleep_us(1200); /* keep >700us style wake settle margin */

        uint8_t part = 0;
        if (i2c_read_reg8(i2c, addr, 0x00, &part)) {
            g_paj_diag.detected = true;
            g_paj_diag.part_id = part;
            printf("[PAJ7620] wake probe OK (attempt=%d, part=0x%02X)\n", attempt + 1, part);
            fflush(stdout);
            return true;
        }
        sleep_ms(2);
    }
    printf("[PAJ7620] wake probe failed, trying I2C0 recovery once\n");
    fflush(stdout);
    i2c0_recover_pins_and_reinit(I2C0_BUS_HZ);
    g_paj_diag.used_recovery = true;
    for (int attempt = 0; attempt < 2; ++attempt) {
        g_paj_diag.wake_attempts = 3 + attempt + 1;
        (void)i2c_write_reg8(i2c, addr, 0xEF, 0x00);
        sleep_us(1200);
        uint8_t part = 0;
        if (i2c_read_reg8(i2c, addr, 0x00, &part)) {
            g_paj_diag.detected = true;
            g_paj_diag.part_id = part;
            printf("[PAJ7620] wake probe OK after recovery (attempt=%d, part=0x%02X)\n", g_paj_diag.wake_attempts,
                   part);
            fflush(stdout);
            return true;
        }
    }
    return false;
}

static bool paj7620_init_gesture_mode(i2c_inst_t *i2c) {
    /* Full init register table (aligned with common Seeed/PAJ reference drivers). */
    static const uint8_t init_reg[][2] = {
        {0xEF,0x00},{0x32,0x29},{0x33,0x01},{0x34,0x00},{0x35,0x01},{0x36,0x00},{0x37,0x07},{0x38,0x17},
        {0x39,0x06},{0x3A,0x12},{0x3F,0x00},{0x40,0x02},{0x41,0xFF},{0x42,0x01},{0x46,0x2D},{0x47,0x0F},
        {0x48,0x3C},{0x49,0x00},{0x4A,0x1E},{0x4B,0x00},{0x4C,0x20},{0x4D,0x00},{0x4E,0x1A},{0x4F,0x14},
        {0x50,0x00},{0x51,0x10},{0x52,0x00},{0x5C,0x02},{0x5D,0x00},{0x5E,0x10},{0x5F,0x3F},{0x60,0x27},
        {0x61,0x28},{0x62,0x00},{0x63,0x03},{0x64,0xF7},{0x65,0x03},{0x66,0xD9},{0x67,0x03},{0x68,0x01},
        {0x69,0xC8},{0x6A,0x40},{0x6D,0x04},{0x6E,0x00},{0x6F,0x00},{0x70,0x80},{0x71,0x00},{0x72,0x00},
        {0x73,0x00},{0x74,0xF0},{0x75,0x00},{0x80,0x42},{0x81,0x44},{0x82,0x04},{0x83,0x20},{0x84,0x20},
        {0x85,0x00},{0x86,0x10},{0x87,0x00},{0x88,0x05},{0x89,0x18},{0x8A,0x10},{0x8B,0x01},{0x8C,0x37},
        {0x8D,0x00},{0x8E,0xF0},{0x8F,0x81},{0x90,0x06},{0x91,0x06},{0x92,0x1E},{0x93,0x0D},{0x94,0x0A},
        {0x95,0x0A},{0x96,0x0C},{0x97,0x05},{0x98,0x0A},{0x99,0x41},{0x9A,0x14},{0x9B,0x0A},{0x9C,0x3F},
        {0x9D,0x33},{0x9E,0xAE},{0x9F,0xF9},{0xA0,0x48},{0xA1,0x13},{0xA2,0x10},{0xA3,0x08},{0xA4,0x30},
        {0xA5,0x19},{0xA6,0x10},{0xA7,0x08},{0xA8,0x24},{0xA9,0x04},{0xAA,0x1E},{0xAB,0x1E},{0xCC,0x19},
        {0xCD,0x0B},{0xCE,0x13},{0xCF,0x64},{0xD0,0x21},{0xD1,0x0F},{0xD2,0x88},{0xE0,0x01},{0xE1,0x04},
        {0xE2,0x41},{0xE3,0xD6},{0xE4,0x00},{0xE5,0x0C},{0xE6,0x0A},{0xE7,0x00},{0xE8,0x00},{0xE9,0x00},
        {0xEE,0x07},{0xEF,0x01},{0x00,0x1E},{0x01,0x1E},{0x02,0x0F},{0x03,0x10},{0x04,0x02},{0x05,0x00},
        {0x06,0xB0},{0x07,0x04},{0x08,0x0D},{0x09,0x0E},{0x0A,0x9C},{0x0B,0x04},{0x0C,0x05},{0x0D,0x0F},
        {0x0E,0x02},{0x0F,0x12},{0x10,0x02},{0x11,0x02},{0x12,0x00},{0x13,0x01},{0x14,0x05},{0x15,0x07},
        {0x16,0x05},{0x17,0x07},{0x18,0x01},{0x19,0x04},{0x1A,0x05},{0x1B,0x0C},{0x1C,0x2A},{0x1D,0x01},
        {0x1E,0x00},{0x21,0x00},{0x22,0x00},{0x23,0x00},{0x25,0x01},{0x26,0x00},{0x27,0x39},{0x28,0x7F},
        {0x29,0x08},{0x30,0x03},{0x31,0x00},{0x32,0x1A},{0x33,0x1A},{0x34,0x07},{0x35,0x07},{0x36,0x01},
        {0x37,0xFF},{0x38,0x36},{0x39,0x07},{0x3A,0x00},{0x3E,0xFF},{0x3F,0x00},{0x40,0x77},{0x41,0x40},
        {0x42,0x00},{0x43,0x30},{0x44,0xA0},{0x45,0x5C},{0x46,0x00},{0x47,0x00},{0x48,0x58},{0x4A,0x1E},
        {0x4B,0x1E},{0x4C,0x00},{0x4D,0x00},{0x4E,0xA0},{0x4F,0x80},{0x50,0x00},{0x51,0x00},{0x52,0x00},
        {0x53,0x00},{0x54,0x00},{0x57,0x80},{0x59,0x10},{0x5A,0x08},{0x5B,0x94},{0x5C,0xE8},{0x5D,0x08},
        {0x5E,0x3D},{0x5F,0x99},{0x60,0x45},{0x61,0x40},{0x63,0x2D},{0x64,0x02},{0x65,0x96},{0x66,0x00},
        {0x67,0x97},{0x68,0x01},{0x69,0xCD},{0x6A,0x01},{0x6B,0xB0},{0x6C,0x04},{0x6D,0x2C},{0x6E,0x01},
        {0x6F,0x32},{0x71,0x00},{0x72,0x01},{0x73,0x35},{0x74,0x00},{0x75,0x33},{0x76,0x31},{0x77,0x01},
        {0x7C,0x84},{0x7D,0x03},{0x7E,0x01}
    };

    for (int attempt = 0; attempt < 3; ++attempt) {
        /* Wake and verify part id first. */
        (void)i2c_write_reg8(i2c, PAJ7620_I2C_ADDR_7BIT, 0xEF, 0x00);
        sleep_ms(8);
        uint8_t part = 0;
        if (!i2c_read_reg8(i2c, PAJ7620_I2C_ADDR_7BIT, 0x00, &part)) {
            sleep_ms(3);
            continue;
        }

        bool ok = true;
        for (size_t i = 0; i < (sizeof(init_reg) / sizeof(init_reg[0])); ++i) {
            if (!i2c_write_reg8(i2c, PAJ7620_I2C_ADDR_7BIT, init_reg[i][0], init_reg[i][1])) {
                ok = false;
                break;
            }
            sleep_us(300);
        }
        if (!ok) {
            sleep_ms(3);
            continue;
        }

        /* Back to bank0 and clear latched gesture regs. */
        (void)i2c_write_reg8(i2c, PAJ7620_I2C_ADDR_7BIT, 0xEF, 0x00);
        (void)i2c_write_reg8(i2c, PAJ7620_I2C_ADDR_7BIT, 0x43, 0x00);
        (void)i2c_write_reg8(i2c, PAJ7620_I2C_ADDR_7BIT, 0x44, 0x00);

        g_paj_diag.part_id = part;
        printf("[PAJ7620] full init table applied (attempt=%d, part=0x%02X, regs=%u)\n",
               attempt + 1, part, (unsigned)(sizeof(init_reg) / sizeof(init_reg[0])));
        fflush(stdout);
        return true;
    }
    printf("[PAJ7620] gesture init failed (full init table)\n");
    fflush(stdout);
    return false;
}

#if ENABLE_I2C_BOOT_SCAN
static void i2c_boot_scan(i2c_inst_t *i2c, const char *tag) {
    printf("[I2C] scan %s\n", tag);
    fflush(stdout);
    int count = 0;

    if (strcmp(tag, "I2C0") == 0) {
        bool paj_ok = paj7620_try_wake_and_probe(i2c);
        if (paj_ok) {
            const char *name = i2c_known_addr_name(tag, PAJ7620_I2C_ADDR_7BIT);
            if (name != NULL) {
                printf("  0x%02X (%s)\n", PAJ7620_I2C_ADDR_7BIT, name);
            } else {
                printf("  0x%02X\n", PAJ7620_I2C_ADDR_7BIT);
            }
            count++;
        } else {
            printf("[PAJ7620] probe failed on I2C0 (addr 0x%02X)\n", PAJ7620_I2C_ADDR_7BIT);
        }
    }

    for (int addr = 0x08; addr < 0x78; ++addr) {
        if (strcmp(tag, "I2C0") == 0 && addr == PAJ7620_I2C_ADDR_7BIT) {
            continue; /* already handled with dedicated wake/probe path */
        }
        if (i2c_probe_addr(i2c, (uint8_t)addr)) {
            const char *name = i2c_known_addr_name(tag, (uint8_t)addr);
            if (name != NULL) {
                printf("  0x%02X (%s)\n", addr, name);
            } else {
                printf("  0x%02X\n", addr);
            }
            count++;
        }
    }
    printf("[I2C] %s: %d device(s)\n", tag, count);
    fflush(stdout);
}
#endif
#endif

/** Re-print EEPROM status after WiFi/time (serial often opens too late for early [EEPROM] lines). */
static void print_eeprom_boot_summary_delayed(void) {
#if ENABLE_TELEMETRY_EEPROM_CACHE
    printf("[EEPROM] boot summary: queue_init=%s probe=%s addr=0x%02x pending=%u (repeat; early log may be missed)\n",
           g_eeprom_boot_queue_init_ok ? "ok" : "fail",
           g_eeprom_boot_probe_ok ? "ok" : "fail",
           (unsigned)AT24C256_I2C_ADDR_7BIT, (unsigned)telemetry_eeprom_queue_count());
#else
    printf("[EEPROM] boot summary: cache=OFF (ENABLE_TELEMETRY_EEPROM_CACHE=0)\n");
#endif
    fflush(stdout);
}

#if ENABLE_TELEMETRY_EEPROM_CACHE
/** Flip stored live JSON so replay on .../telemetry/sync-back sets is_sync_back true. */
static void mark_json_sync_back(char *buf, size_t cap) {
    const char *old = "\"is_sync_back\":false";
    const char *new = "\"is_sync_back\":true";
    char *p = strstr(buf, old);
    if (p == NULL) {
        return;
    }
    size_t oldlen = strlen(old);
    size_t newlen = strlen(new);
    size_t tail = strlen(p + oldlen);
    if ((size_t)(p - buf) + newlen + tail + 1U > cap) {
        return;
    }
    memmove(p + newlen, p + oldlen, tail + 1U);
    memcpy(p, new, newlen);
}
#endif

static void read_env_sensors(bme680_sensor_data_t *bme, tsl2561_sensor_data_t *tsl) {
    if (bme != NULL) {
        if (g_bme680_ok) {
            (void)bme680_sensor_read(bme);
        } else {
            memset(bme, 0, sizeof(*bme));
        }
    }
    if (tsl != NULL) {
        if (g_tsl2561_ok) {
            (void)tsl2561_sensor_read(tsl);
        } else {
            memset(tsl, 0, sizeof(*tsl));
        }
    }
}

/**
 * @brief Build telemetry JSON aligned with backend ingest (snake_case + unit suffix per spec).
 * is_urgent is reserved for future schema updates.
 */
static void build_payload(char *buffer, size_t buffer_size, const ds3231_time_t *t, const bme680_sensor_data_t *bme,
                          const tsl2561_sensor_data_t *tsl, const imu_sample_t *imu, int rssi_dbm,
                          bool is_urgent, bool is_sync_back) {
    float temp_c = 0.0f;
    float hum = 0.0f;
    float press = 0.0f;
    float gas = 0.0f;
    float lux = 0.0f;
    if (bme != NULL && bme->valid) {
        temp_c = bme->temperature_c;
        hum = bme->humidity_pct;
        press = bme->pressure_hpa;
        gas = bme->gas_resistance_ohm;
    }
    if (tsl != NULL && tsl->valid) {
        lux = tsl->lux;
    }
    float accel_x = 0.0f;
    float accel_y = 0.0f;
    float accel_z = 0.0f;
    float gyro_x = 0.0f;
    float gyro_y = 0.0f;
    float gyro_z = 0.0f;
    float co2_ppm = 0.0f;
    float temp_scd41 = 0.0f;
    float hum_scd41 = 0.0f;
    if (imu != NULL) {
        accel_x = imu->accel_x;
        accel_y = imu->accel_y;
        accel_z = imu->accel_z;
        gyro_x = imu->gyro_x;
        gyro_y = imu->gyro_y;
        gyro_z = imu->gyro_z;
    }
    if (g_last_scd41.valid) {
        co2_ppm = (float)g_last_scd41.co2_ppm;
        temp_scd41 = g_last_scd41.temperature_c;
        hum_scd41 = g_last_scd41.humidity_pct;
    }

    (void)is_urgent; /* reserved */
    const int sync_flag = is_sync_back ? 1 : 0;
    snprintf(
        buffer,
        buffer_size,
        "{\"device_id\":\"%s\",\"temperature_c\":%.2f,\"humidity_pct\":%.2f,"
        "\"pressure\":%.2f,\"gas_resistance\":%.2f,"
        "\"co2_ppm\":%.2f,\"temperature_c_scd41\":%.2f,\"humidity_pct_scd41\":%.2f,"
        "\"pir_active\":%s,"
        "\"lux\":%.2f,"
        "\"accel_x\":%.4f,\"accel_y\":%.4f,\"accel_z\":%.4f,"
        "\"gyro_x\":%.4f,\"gyro_y\":%.4f,\"gyro_z\":%.4f,"
        "\"rssi\":%d,\"is_sync_back\":%s,"
        "\"device_time\":\"20%02u-%02u-%02uT%02u:%02u:%02u+08:00\"}",
        MQTT_CLIENT_ID,
        (double)temp_c,
        (double)hum,
        (double)press,
        (double)gas,
        (double)co2_ppm,
        (double)temp_scd41,
        (double)hum_scd41,
        g_pir_test_active ? "true" : "false",
        (double)lux,
        (double)accel_x,
        (double)accel_y,
        (double)accel_z,
        (double)gyro_x,
        (double)gyro_y,
        (double)gyro_z,
        rssi_dbm,
        sync_flag ? "true" : "false",
        t->year,
        t->month,
        t->day,
        t->hour,
        t->minute,
        t->second
    );
}

/* Forward declare: send_telemetry_packet() prints env readings. */
static void print_env_readings(const bme680_sensor_data_t *bme, const tsl2561_sensor_data_t *tsl, int rssi_dbm);

static void pir_test_init(void) {
    gpio_init(PIR_TEST_GPIO);
    gpio_set_dir(PIR_TEST_GPIO, GPIO_IN);
#if PIR_TEST_PULL_UP
    gpio_pull_up(PIR_TEST_GPIO);
#else
    gpio_pull_down(PIR_TEST_GPIO);
#endif
    bool raw = gpio_get(PIR_TEST_GPIO);
    g_pir_test_active = PIR_TEST_ACTIVE_HIGH ? raw : !raw;
    printf("[PIR] test init: gpio=GP%d pull_up=%d active_high=%d raw=%d initial_active=%s\n", PIR_TEST_GPIO,
           PIR_TEST_PULL_UP ? 1 : 0, PIR_TEST_ACTIVE_HIGH, raw ? 1 : 0, g_pir_test_active ? "true" : "false");
}

static void pir_test_poll(uint32_t now_ms) {
    bool raw = gpio_get(PIR_TEST_GPIO);
    bool active = PIR_TEST_ACTIVE_HIGH ? raw : !raw;
    if (active != g_pir_test_active) {
        g_pir_test_active = active;
        printf("[PIR] state changed: active=%s raw=%d\n", g_pir_test_active ? "true" : "false", raw ? 1 : 0);
        g_next_pir_test_heartbeat_ms = now_ms + 5000U;
        return;
    }
    if ((int32_t)(now_ms - g_next_pir_test_heartbeat_ms) >= 0) {
        printf("[PIR] heartbeat: active=%s raw=%d\n", g_pir_test_active ? "true" : "false", raw ? 1 : 0);
        g_next_pir_test_heartbeat_ms = now_ms + 5000U;
    }
}

static bool scd41_try_init_and_start(i2c_inst_t *i2c, uint32_t now_ms) {
    g_scd41_ok = scd41_sensor_init(i2c, SCD41_I2C_ADDR_7BIT);
    if (!g_scd41_ok) {
        g_scd41_started = false;
        g_next_scd41_retry_ms = now_ms + SCD41_RETRY_INTERVAL_MS;
        printf("[SCD41] init failed (addr=0x%02X), retry in %u ms\n", (unsigned)SCD41_I2C_ADDR_7BIT,
               (unsigned)SCD41_RETRY_INTERVAL_MS);
        return false;
    }

    g_scd41_started = scd41_sensor_start_periodic_measurement();
    if (!g_scd41_started) {
        g_next_scd41_retry_ms = now_ms + SCD41_RETRY_INTERVAL_MS;
        printf("[SCD41] start periodic failed (addr=0x%02X), retry in %u ms\n", (unsigned)SCD41_I2C_ADDR_7BIT,
               (unsigned)SCD41_RETRY_INTERVAL_MS);
        return false;
    }

    g_next_scd41_poll_ms = now_ms + SCD41_POLL_INTERVAL_MS;
    g_next_scd41_retry_ms = 0;
    g_scd41_retry_count = 0;
    printf("[SCD41] init ok (addr=0x%02X), periodic started\n", (unsigned)SCD41_I2C_ADDR_7BIT);
    return true;
}

static void send_telemetry_packet(const DeviceConfig *cfg, const ds3231_time_t *t, const imu_sample_t *imu,
                                  bool is_urgent, bool publish_to_mqtt) {
    (void)cfg; /* currently only used for scheduling */
    char payload[512];
    bme680_sensor_data_t bme;
    tsl2561_sensor_data_t tsl;

    read_env_sensors(&bme, &tsl);
    int rssi_dbm = wifi_handler_get_rssi_dbm();

    g_last_bme = bme;
    g_last_tsl = tsl;
    g_last_rssi_dbm = rssi_dbm;
    if (imu != NULL) {
        g_last_imu = *imu;
    } else {
        memset(&g_last_imu, 0, sizeof(g_last_imu));
    }
    g_last_sample_ms = to_ms_since_boot(get_absolute_time());
    g_has_last_sample = true;

    build_payload(payload, sizeof(payload), t, &bme, &tsl, imu, rssi_dbm, is_urgent, false);
    print_env_readings(&bme, &tsl, rssi_dbm);
    if (publish_to_mqtt && mqtt_manager_publish(&g_mqtt_telemetry, payload)) {
        printf("[MQTT] %s\n", payload);
        return;
    }
#if ENABLE_TELEMETRY_EEPROM_CACHE
    if (g_eeprom_queue_ok) {
        uint16_t plen = (uint16_t)strlen(payload);
        if (telemetry_eeprom_queue_push(payload, plen)) {
            printf("[EEPROM] queued offline (%u bytes, pending=%u)\n", (unsigned)plen,
                   (unsigned)telemetry_eeprom_queue_count());
        } else {
            printf("[EEPROM] queue push failed (len=%u)\n", (unsigned)plen);
        }
    }
#endif
}

/** Human-readable env line on USB serial (same cadence as MQTT telemetry publish). */
static void print_env_readings(const bme680_sensor_data_t *bme, const tsl2561_sensor_data_t *tsl, int rssi_dbm) {
    printf("[ENV] ");
    if (bme != NULL && bme->valid) {
        printf("T=%.2f°C H=%.2f%% P=%.2f hPa G=%.0f Ω ",
               (double)bme->temperature_c,
               (double)bme->humidity_pct,
               (double)bme->pressure_hpa,
               (double)bme->gas_resistance_ohm);
    } else {
        printf("BME680=n/a ");
    }
    if (tsl != NULL && tsl->valid) {
        printf("L=%.2f lux ", (double)tsl->lux);
    } else {
        printf("L=n/a ");
    }
    printf("RSSI=%d dBm\n", rssi_dbm);
}

static void oled_show_rotating_pages(uint32_t now_ms) {
    if (!g_oled_ok || !g_has_last_sample) {
        return;
    }
    char line[32];
    uint32_t age_s = (now_ms - g_last_sample_ms) / 1000U;

    sh1106_oled_clear();
    switch (g_oled_page_idx % 3U) {
        case 0U:
            snprintf(line, sizeof(line), "T:%.1fC H:%.0f%%", (double)g_last_bme.temperature_c,
                     (double)g_last_bme.humidity_pct);
            sh1106_oled_draw_string(0U, 0U, line);
            snprintf(line, sizeof(line), "P:%.0fHPA", (double)g_last_bme.pressure_hpa);
            sh1106_oled_draw_string(1U, 0U, line);
            snprintf(line, sizeof(line), "AGE:%luS", (unsigned long)age_s);
            sh1106_oled_draw_string(3U, 0U, line);
            break;
        case 1U:
            snprintf(line, sizeof(line), "L:%.1f G:%.0f", (double)g_last_tsl.lux, (double)g_last_bme.gas_resistance_ohm);
            sh1106_oled_draw_string(0U, 0U, line);
            snprintf(line, sizeof(line), "RSSI:%d", g_last_rssi_dbm);
            sh1106_oled_draw_string(1U, 0U, line);
            snprintf(line, sizeof(line), "AGE:%luS", (unsigned long)age_s);
            sh1106_oled_draw_string(3U, 0U, line);
            break;
        default:
            snprintf(line, sizeof(line), "A1:%.2f A2:%.2f", (double)g_last_imu.accel_x, (double)g_last_imu.accel_y);
            sh1106_oled_draw_string(0U, 0U, line);
            snprintf(line, sizeof(line), "A3:%.2f", (double)g_last_imu.accel_z);
            sh1106_oled_draw_string(1U, 0U, line);
            snprintf(line, sizeof(line), "AGE:%luS", (unsigned long)age_s);
            sh1106_oled_draw_string(3U, 0U, line);
            break;
    }
    sh1106_oled_flush();
    g_oled_page_idx = (uint8_t)((g_oled_page_idx + 1U) % 3U);
}

static void build_log_payload(char *buffer, size_t buffer_size, const ds3231_time_t *t,
                              const char *module, const char *level, int error_code, const char *message) {
    snprintf(
        buffer,
        buffer_size,
        "{\"device_id\":\"%s\",\"module\":\"%s\",\"log_level\":\"%s\","
        "\"error_code\":%d,\"message\":\"%s\","
        "\"device_time\":\"20%02u-%02u-%02uT%02u:%02u:%02u+08:00\"}",
        MQTT_CLIENT_ID,
        module,
        level,
        error_code,
        message,
        t->year,
        t->month,
        t->day,
        t->hour,
        t->minute,
        t->second
    );
}

static bool json_extract_string_field(const char *json, const char *key, char *out, size_t out_len) {
    if (json == NULL || key == NULL || out == NULL || out_len == 0) {
        return false;
    }
    char needle[48];
    snprintf(needle, sizeof(needle), "\"%s\":\"", key);
    const char *p = strstr(json, needle);
    if (p == NULL) {
        return false;
    }
    p += strlen(needle);
    const char *end = strchr(p, '"');
    if (end == NULL) {
        return false;
    }
    size_t n = (size_t)(end - p);
    if (n >= out_len) {
        n = out_len - 1;
    }
    memcpy(out, p, n);
    out[n] = '\0';
    return true;
}

static bool json_extract_number_field(const char *json, const char *key, double *out) {
    if (json == NULL || key == NULL || out == NULL) {
        return false;
    }
    char needle[48];
    snprintf(needle, sizeof(needle), "\"%s\":", key);
    const char *p = strstr(json, needle);
    if (p == NULL) {
        return false;
    }
    p += strlen(needle);
    while (*p == ' ' || *p == '\t') {
        p++;
    }
    char *endptr = NULL;
    double v = strtod(p, &endptr);
    if (endptr == p) {
        return false;
    }
    *out = v;
    return true;
}

static bool json_extract_bool_field(const char *json, const char *key, bool *out) {
    if (json == NULL || key == NULL || out == NULL) {
        return false;
    }
    char needle[48];
    snprintf(needle, sizeof(needle), "\"%s\":", key);
    const char *p = strstr(json, needle);
    if (p == NULL) {
        return false;
    }
    p += strlen(needle);
    while (*p == ' ' || *p == '\t') {
        p++;
    }
    if (strncmp(p, "true", 4) == 0) {
        *out = true;
        return true;
    }
    if (strncmp(p, "false", 5) == 0) {
        *out = false;
        return true;
    }
    return false;
}

static void publish_control_ack(const char *action, const char *request_id, bool accepted, const char *reason) {
    ds3231_time_t t;
    if (!ds3231_get_time(&t)) {
        return;
    }
    char payload[320];
    snprintf(payload, sizeof(payload),
             "{\"device_id\":\"%s\",\"module\":\"CONTROL\",\"log_level\":\"%s\",\"error_code\":%d,"
             "\"message\":\"action=%s request_id=%s accepted=%s reason=%s\","
             "\"device_time\":\"20%02u-%02u-%02uT%02u:%02u:%02u+08:00\"}",
             MQTT_CLIENT_ID, accepted ? "INFO" : "WARN", accepted ? 2000 : 2400, action ? action : "-",
             request_id ? request_id : "-", accepted ? "true" : "false", reason ? reason : "-", t.year, t.month,
             t.day, t.hour, t.minute, t.second);
    (void)mqtt_manager_publish_topic(&g_mqtt_status, MQTT_TOPIC_STATUS, payload);
}

static void handle_control_message(const char *topic, const char *payload, size_t payload_len, void *user_data) {
    (void)topic;
    (void)payload_len;
    (void)user_data;
    if (payload == NULL) {
        return;
    }

    char action[32] = {0};
    char request_id[96] = {0};
    (void)json_extract_string_field(payload, "request_id", request_id, sizeof(request_id));
    if (!json_extract_string_field(payload, "action", action, sizeof(action))) {
        printf("[CTRL] ignore payload without action\n");
        publish_control_ack("-", request_id, false, "missing_action");
        return;
    }

    if (strcmp(action, "set_pwm") == 0) {
        double v = 0.0;
        if (!json_extract_number_field(payload, "value", &v) || v < 0.0 || v > 1.0) {
            printf("[CTRL] set_pwm invalid value\n");
            publish_control_ack(action, request_id, false, "invalid_pwm");
            return;
        }
        g_control_pwm = (float)v;
        printf("[CTRL] set_pwm=%.3f\n", (double)g_control_pwm);
        publish_control_ack(action, request_id, true, "ok");
        return;
    }
    if (strcmp(action, "set_alarm") == 0) {
        bool v = false;
        if (!json_extract_bool_field(payload, "value", &v)) {
            printf("[CTRL] set_alarm invalid bool\n");
            publish_control_ack(action, request_id, false, "invalid_alarm");
            return;
        }
        g_control_alarm = v;
        printf("[CTRL] set_alarm=%s\n", g_control_alarm ? "true" : "false");
        publish_control_ack(action, request_id, true, "ok");
        return;
    }
    if (strcmp(action, "set_fan") == 0) {
        bool v = false;
        if (!json_extract_bool_field(payload, "value", &v)) {
            printf("[CTRL] set_fan invalid bool\n");
            publish_control_ack(action, request_id, false, "invalid_fan");
            return;
        }
        g_control_fan = v;
        printf("[CTRL] set_fan=%s\n", g_control_fan ? "true" : "false");
        publish_control_ack(action, request_id, true, "ok");
        return;
    }
    if (strcmp(action, "set_mode") == 0) {
        char mode[24] = {0};
        if (!json_extract_string_field(payload, "value", mode, sizeof(mode)) || mode[0] == '\0') {
            printf("[CTRL] set_mode invalid string\n");
            publish_control_ack(action, request_id, false, "invalid_mode");
            return;
        }
        strncpy(g_control_mode, mode, sizeof(g_control_mode) - 1);
        g_control_mode[sizeof(g_control_mode) - 1] = '\0';
        printf("[CTRL] set_mode=%s\n", g_control_mode);
        publish_control_ack(action, request_id, true, "ok");
        return;
    }

    printf("[CTRL] unsupported action=%s\n", action);
    publish_control_ack(action, request_id, false, "unsupported_action");
}

#if ENABLE_TEST_UI_EVENTS_MQTT_PUBLISH
/** Minimal JSON for backend ui_event ingest; aligns with HTTP/MQTT codec (device_id, event_type, to_page, device_time). */
static void build_ui_event_test_payload(char *buffer, size_t buffer_size, const ds3231_time_t *t) {
    snprintf(
        buffer,
        buffer_size,
        "{\"device_id\":\"%s\",\"input_source\":\"API\",\"event_type\":\"TEST_HARNESS\","
        "\"to_page\":\"fw_boot\",\"page_seq\":0,"
        "\"device_time\":\"20%02u-%02u-%02uT%02u:%02u:%02u+08:00\"}",
        MQTT_CLIENT_ID,
        t->year,
        t->month,
        t->day,
        t->hour,
        t->minute,
        t->second);
}
#endif

#if ENABLE_PAJ7620_UI_EVENTS
static const char *paj7620_gesture_name(uint8_t code) {
    switch (code) {
        case 0x01: return "RIGHT";
        case 0x02: return "LEFT";
        case 0x04: return "UP";
        case 0x08: return "DOWN";
        case 0x10: return "FORWARD";
        case 0x20: return "BACKWARD";
        case 0x40: return "CLOCKWISE";
        case 0x80: return "ANTICLOCKWISE";
        case 0xF1: return "WAVE";
        default: return NULL;
    }
}

static bool paj7620_read_gesture_regs(i2c_inst_t *i2c, uint8_t *out_g43, uint8_t *out_g44) {
    if (out_g43 == NULL || out_g44 == NULL) {
        return false;
    }
    /* Ensure bank0 then read gesture result register. */
    (void)i2c_write_reg8(i2c, PAJ7620_I2C_ADDR_7BIT, 0xEF, 0x00);
    if (!i2c_read_reg8(i2c, PAJ7620_I2C_ADDR_7BIT, 0x43, out_g43)) {
        return false;
    }
    if (!i2c_read_reg8(i2c, PAJ7620_I2C_ADDR_7BIT, 0x44, out_g44)) {
        return false;
    }
    return true;
}

static void paj7620_clear_gesture_regs(i2c_inst_t *i2c) {
    (void)i2c_write_reg8(i2c, PAJ7620_I2C_ADDR_7BIT, 0xEF, 0x00);
    (void)i2c_write_reg8(i2c, PAJ7620_I2C_ADDR_7BIT, 0x43, 0x00);
    (void)i2c_write_reg8(i2c, PAJ7620_I2C_ADDR_7BIT, 0x44, 0x00);
}

static uint8_t paj7620_pick_gesture_code(uint8_t g43, uint8_t g44) {
    if (g43 != 0) {
        return g43;
    }
    /* In PAJ7620 reference drivers, g44 bit0 is commonly used for WAVE. */
    if ((g44 & 0x01U) != 0U) {
        return 0xF1U;
    }
    return g44;
}

static void build_ui_event_gesture_payload(char *buffer, size_t buffer_size, const ds3231_time_t *t,
                                           const char *gesture_name) {
    snprintf(buffer, buffer_size,
             "{\"device_id\":\"%s\",\"input_source\":\"GESTURE\",\"event_type\":\"PAGE_SWITCH\","
             "\"to_page\":\"%s\",\"metadata\":{\"gesture\":\"%s\"},"
             "\"device_time\":\"20%02u-%02u-%02uT%02u:%02u:%02u+08:00\"}",
             MQTT_CLIENT_ID, gesture_name, gesture_name, t->year, t->month, t->day, t->hour, t->minute, t->second);
}

#if ENABLE_PAJ7620_RAW_UI_EVENT_DEBUG
static void build_ui_event_gesture_raw_payload(char *buffer, size_t buffer_size, const ds3231_time_t *t,
                                               uint8_t g43, uint8_t g44, uint8_t picked) {
    snprintf(buffer, buffer_size,
             "{\"device_id\":\"%s\",\"input_source\":\"GESTURE\",\"event_type\":\"GESTURE_RAW\","
             "\"to_page\":\"gesture_raw\",\"metadata\":{\"g43\":%u,\"g44\":%u,\"picked\":%u},"
             "\"device_time\":\"20%02u-%02u-%02uT%02u:%02u:%02u+08:00\"}",
             MQTT_CLIENT_ID, (unsigned)g43, (unsigned)g44, (unsigned)picked, t->year, t->month, t->day, t->hour,
             t->minute, t->second);
}
#endif
#endif

static bool rtc_to_epoch_utc8(const ds3231_time_t *t, time_t *out_epoch) {
    if (t == NULL || out_epoch == NULL || !ds3231_time_is_valid(t)) {
        return false;
    }
    struct tm rtc_tm = {
        .tm_sec = t->second,
        .tm_min = t->minute,
        .tm_hour = t->hour,
        .tm_mday = t->day,
        .tm_mon = t->month - 1,
        .tm_year = t->year + 100,
        .tm_isdst = -1
    };
    time_t epoch = mktime(&rtc_tm);
    if (epoch == (time_t)-1) {
        return false;
    }
    *out_epoch = epoch;
    return true;
}

static void env_i2c_attach_bus(void) {
#if ENV_I2C_USE_I2C0_FOR_SENSORS
    uint actual_hz = i2c_init(i2c0, ENV_I2C_BAUD_HZ);
    gpio_set_function(ENV_I2C_SDA_PIN, GPIO_FUNC_I2C);
    gpio_set_function(ENV_I2C_SCL_PIN, GPIO_FUNC_I2C);
    gpio_pull_up(ENV_I2C_SDA_PIN);
    gpio_pull_up(ENV_I2C_SCL_PIN);
    gpio_set_slew_rate(ENV_I2C_SDA_PIN, GPIO_SLEW_RATE_SLOW);
    gpio_set_slew_rate(ENV_I2C_SCL_PIN, GPIO_SLEW_RATE_SLOW);
    g_env_i2c = i2c0;
    printf("[ENV] BME680/TSL2561 on I2C0 (SDA=GP%u SCL=GP%u) requested %u Hz, actual %u Hz\n",
           (unsigned)ENV_I2C_SDA_PIN, (unsigned)ENV_I2C_SCL_PIN, (unsigned)ENV_I2C_BAUD_HZ, (unsigned)actual_hz);
#else
    g_env_i2c = i2c1;
    printf("[ENV] BME680/TSL2561 on I2C1 with DS3231 (SDA=GP%u SCL=GP%u)\n", (unsigned)I2C1_SDA,
           (unsigned)I2C1_SCL);
#endif
}

static bool apply_rtc_time_to_system(const ds3231_time_t *t) {
    time_t epoch = 0;
    if (!rtc_to_epoch_utc8(t, &epoch)) {
        return false;
    }
    struct timespec ts = {.tv_sec = epoch, .tv_nsec = 0};
    aon_timer_start(&ts);
    return true;
}

/**
 * After AT24C256 timeout/NACK the Synopsys master can leave restart_on_next set; a stuck slave may also
 * hold SDA low. i2c_init alone does not bit-bang SCL. Release the bus then re-bind pads to I2C.
 */
static void i2c1_recover_pins_and_reinit(uint32_t baud_hz) {
    i2c_deinit(i2c1);
    gpio_set_function(I2C1_SDA, GPIO_FUNC_SIO);
    gpio_set_function(I2C1_SCL, GPIO_FUNC_SIO);
    gpio_pull_up(I2C1_SDA);
    gpio_pull_up(I2C1_SCL);
    gpio_set_dir(I2C1_SDA, GPIO_IN);
    gpio_set_dir(I2C1_SCL, GPIO_OUT);
    gpio_put(I2C1_SCL, 1);
    sleep_us(20);
    for (int k = 0; k < 9 && !gpio_get(I2C1_SDA); k++) {
        gpio_put(I2C1_SCL, 0);
        sleep_us(5);
        gpio_put(I2C1_SCL, 1);
        sleep_us(5);
    }
    (void)i2c_init(i2c1, baud_hz);
    gpio_set_function(I2C1_SDA, GPIO_FUNC_I2C);
    gpio_set_function(I2C1_SCL, GPIO_FUNC_I2C);
    gpio_pull_up(I2C1_SDA);
    gpio_pull_up(I2C1_SCL);
    gpio_set_slew_rate(I2C1_SDA, GPIO_SLEW_RATE_SLOW);
    gpio_set_slew_rate(I2C1_SCL, GPIO_SLEW_RATE_SLOW);
}

int main(void) {
    stdio_init_all();
    for (int sec = 5; sec > 0; sec--) {
        printf("[BOOT] Waiting for USB serial... %d\n", sec);
        sleep_ms(1000);
    }
    printf("[BOOT] USB serial ready, continue initialization\n");
    fflush(stdout);

    /* Serial attach grace: single define BOOT_POST_USB_DELAY_MS (see firmware_config_defaults.h). */
#if BOOT_POST_USB_DELAY_MS > 0U
    {
        uint32_t remain_ms = BOOT_POST_USB_DELAY_MS;
        while (remain_ms > 0U) {
            uint32_t sec_left = (remain_ms + 999U) / 1000U;
            printf("[BOOT] Grace period (open serial if needed)... %u s left\n", (unsigned)sec_left);
            fflush(stdout);
            uint32_t slice = remain_ms > 1000U ? 1000U : remain_ms;
            sleep_ms(slice);
            remain_ms -= slice;
        }
    }
    printf("[BOOT] Continuing (I2C / sensors / WiFi)\n");
    fflush(stdout);
#endif

    mbedtls_memory_buffer_alloc_init(mbedtls_heap, sizeof(mbedtls_heap));

    if (cyw43_arch_init()) {
        enter_safe_mode("cyw43_arch_init failed");
    }
    cyw43_arch_enable_sta_mode();

    if (!ds3231_init(i2c1, I2C1_SDA, I2C1_SCL, I2C1_BUS_HZ)) {
        enter_safe_mode("ds3231_init failed");
    }
    env_i2c_attach_bus();

#if ENABLE_TELEMETRY_EEPROM_CACHE
    printf("[BOOT] EEPROM telemetry cache: ON (will probe AT24C256)\n");
#else
    printf("[BOOT] EEPROM telemetry cache: OFF (ENABLE_TELEMETRY_EEPROM_CACHE=0 in secrets / build)\n");
#endif
    fflush(stdout);

    /* AT24C256 on I2C1 — independent of ENABLE_I2C_BOOT_SCAN; run early so logs are not buried by sensor init. */
#if ENABLE_TELEMETRY_EEPROM_CACHE
    /* Clean I2C1 before first 0x50 access so header_load/header_save see a sane bus (offline queue + sync-back). */
    printf("[I2C1] bus recovery before EEPROM queue init\r\n");
    fflush(stdout);
    i2c1_recover_pins_and_reinit(I2C1_BUS_HZ);

    g_eeprom_queue_ok = telemetry_eeprom_queue_init(i2c1, AT24C256_I2C_ADDR_7BIT);
    g_eeprom_boot_queue_init_ok = g_eeprom_queue_ok;
    if (g_eeprom_queue_ok) {
        g_eeprom_boot_probe_ok = at24c256_eeprom_probe();
        if (g_eeprom_boot_probe_ok) {
            printf("[EEPROM] AT24C256 probe OK (addr 0x%02x), pending=%u\n",
                   (unsigned)AT24C256_I2C_ADDR_7BIT, (unsigned)telemetry_eeprom_queue_count());
        } else {
            printf("[EEPROM] WARN: probe failed (addr 0x%02x, check wiring)\n",
                   (unsigned)AT24C256_I2C_ADDR_7BIT);
        }
    } else {
        g_eeprom_boot_probe_ok = false;
        printf("[EEPROM] queue init failed (addr 0x%02x)\n", (unsigned)AT24C256_I2C_ADDR_7BIT);
    }
#else
    printf("[EEPROM] offline cache disabled (ENABLE_TELEMETRY_EEPROM_CACHE=0)\n");
#endif
#if ENABLE_TELEMETRY_EEPROM_CACHE
    /* If queue/probe left restart_on_next or SDA stuck, fix before I2C scan / LCD / DS3231 traffic. */
    printf("[I2C1] bus recovery + reinit after EEPROM step\r\n");
    fflush(stdout);
    i2c1_recover_pins_and_reinit(I2C1_BUS_HZ);
#endif
    fflush(stdout);

#if ENABLE_I2C_BOOT_SCAN
    printf("[I2C] === boot scan start ===\r\n");
    fflush(stdout);
#if ENV_I2C_USE_I2C0_FOR_SENSORS
    i2c_boot_scan(i2c0, "I2C0");
#endif
    i2c_boot_scan(i2c1, "I2C1");
    printf("[I2C] === boot scan done ===\r\n");
    printf("[PAJ7620] boot summary: detected=%s attempts=%d recovery=%s part=0x%02X\n",
           g_paj_diag.detected ? "yes" : "no", g_paj_diag.wake_attempts,
           g_paj_diag.used_recovery ? "yes" : "no", g_paj_diag.part_id);
    fflush(stdout);
#if I2C_BOOT_SCAN_HOLD_MS > 0U
    printf("[I2C] holding %u ms before sensor init (set I2C_BOOT_SCAN_HOLD_MS / 0 to skip)\r\n",
           (unsigned)I2C_BOOT_SCAN_HOLD_MS);
    fflush(stdout);
    sleep_ms(I2C_BOOT_SCAN_HOLD_MS);
#endif
#endif

#if ENABLE_I2C_BOOT_SCAN || ENABLE_PAJ7620_UI_EVENTS
    if (g_paj_diag.detected) {
        g_paj_gesture_ready = paj7620_init_gesture_mode(i2c0);
    } else {
        g_paj_gesture_ready = false;
    }
#else
    g_paj_gesture_ready = false;
#endif

#if ENABLE_LCD1602
    g_lcd_ok = lcd1602_init(&g_lcd, i2c1, LCD1602_I2C_ADDR_7BIT, LCD1602_BACKLIGHT_MASK);
    if (!g_lcd_ok) {
        printf("[LCD] init failed (addr 0x%02x, BLmask=0x%02x) — wiring, ACK, or try "
               "LCD1602_BACKLIGHT_MASK 0x08/0x00/0x80; addr 0x3F\n",
               (unsigned)LCD1602_I2C_ADDR_7BIT, (unsigned)LCD1602_BACKLIGHT_MASK);
    } else {
        char addr_line[17];
        (void)snprintf(addr_line, sizeof(addr_line), "addr=0x%02X", (unsigned)LCD1602_I2C_ADDR_7BIT);
        lcd1602_put_line(&g_lcd, 0, "LCD1602 OK");
        lcd1602_put_line(&g_lcd, 1, addr_line);
        printf("[LCD] LCD1602 OK addr=0x%02x (BLmask=0x%02x)\n", (unsigned)LCD1602_I2C_ADDR_7BIT,
               (unsigned)LCD1602_BACKLIGHT_MASK);
    }
    {
        uint32_t t0 = to_ms_since_boot(get_absolute_time());
        g_next_lcd_refresh_ms = t0 + LCD1602_REFRESH_MIN_MS;
    }
#endif

    g_bme680_ok = bme680_sensor_init(g_env_i2c, BME680_I2C_ADDR_7BIT);
    g_tsl2561_ok = tsl2561_sensor_init(g_env_i2c, TSL2561_I2C_ADDR_7BIT);
#if ENV_I2C_USE_I2C0_FOR_SENSORS
    g_mpu9250_ok = mpu9250_sensor_init(g_env_i2c, MPU9250_I2C_ADDR_7BIT);
    if (!g_mpu9250_ok) {
        printf("[IMU] MPU-9250 init failed (check I2C0 wiring, addr 0x%02x, WHO_AM_I=0x71)\n",
               (unsigned)MPU9250_I2C_ADDR_7BIT);
    } else {
        printf("[IMU] MPU-9250 ok (I2C0, addr 0x%02x)\n", (unsigned)MPU9250_I2C_ADDR_7BIT);
    }
#else
    g_mpu9250_ok = false;
    printf("[IMU] MPU-9250 skipped (use I2C0 for sensors per hardware plan; ENV_I2C_USE_I2C0_FOR_SENSORS=0)\n");
#endif
#if IMU_SIMULATE_URGENT
    printf("[IMU] WARNING: IMU_SIMULATE_URGENT=1 — fake accel (thr+0.3g on X, rest 0); set 0 in secrets.h for real MPU-9250\r\n");
#endif
    g_oled_ok = sh1106_oled_init(g_env_i2c, SH1106_OLED_I2C_ADDR_7BIT); /* 1.3" SH1106，位址見 secrets.h */
    if (!g_bme680_ok) {
        printf("[ENV] BME680 init failed (I2C ACK / 0x76 / 0x77)\n");
    }
    if (!g_tsl2561_ok) {
        printf("[ENV] TSL2561 init failed (I2C ACK / 0x29 / 0x39)\n");
    }
    if (!g_oled_ok) {
        printf("[ENV] SH1106 OLED init failed (I2C ACK / 0x3C)\n");
    } else {
        g_next_oled_rotate_ms = to_ms_since_boot(get_absolute_time()) + OLED_PAGE_ROTATE_INTERVAL_MS;
    }

    ds3231_set_diag_period_ms(15000);
    ds3231_sync_system_time(true);

    if (!wifi_handler_init(&g_wifi, WIFI_SSID, WIFI_PASSWORD)) {
        enter_safe_mode("wifi_handler_init failed");
    }
    while (!wifi_handler_is_connected(&g_wifi)) {
        uint32_t now = to_ms_since_boot(get_absolute_time());
        wifi_handler_process(&g_wifi, now);
        update_status_outputs(now, wifi_handler_is_connected(&g_wifi), false);
        sleep_ms(100);
    }
    sleep_ms(3000);

    ds3231_time_t rtc_now;
    uint8_t rtc_status = 0;
    bool osf_tripped = true;
    bool year_ok = false;
    bool time_valid = false;
    bool rtc_trusted = false;
    bool skip_ntp = false;

    if (ds3231_get_time(&rtc_now)) {
        time_valid = ds3231_time_is_valid(&rtc_now);
        year_ok = rtc_now.year >= RTC_TRUSTED_MIN_YEAR;
        if (ds3231_get_status_reg(&rtc_status)) {
            osf_tripped = (rtc_status & 0x80u) != 0u;
        }
    }

    rtc_trusted = (!osf_tripped && year_ok && time_valid);
    if (rtc_trusted) {
        if (apply_rtc_time_to_system(&rtc_now)) {
            skip_ntp = true;
            printf("[TIME] RTC trusted (OSF=0, Year=20%02u), skipping NTP\n", rtc_now.year);
        } else {
            printf("[TIME] RTC trusted but failed applying system clock, forcing NTP sync\n");
            skip_ntp = false;
        }
    } else {
        const char *reason = "read_failed";
        if (osf_tripped) reason = "OSF=1";
        else if (!year_ok) reason = "YEAR_BELOW_MIN";
        else if (!time_valid) reason = "INVALID_TIME";
        printf("[TIME] RTC untrusted (reason: %s), forcing NTP sync\n", reason);
        skip_ntp = false;
    }

    if (!skip_ntp) {
        uint32_t dns_wait_ms = 0;
        while (!ntp_client_dns_ready() && dns_wait_ms < 10000) {
            printf("[NTP] waiting DNS... (%u ms)\n", (unsigned)dns_wait_ms);
            sleep_ms(500);
            dns_wait_ms += 500;
        }
        if (!ntp_client_dns_ready()) {
            printf("[NTP] DNS not ready, skip NTP sync\n");
            printf("[TIME] using RTC time\n");
        } else if (!ntp_client_init()) {
            printf("[NTP] init failed, keep DS3231/AON time\n");
            printf("[TIME] using RTC time\n");
        } else {
            bool ntp_ok = false;
            for (uint32_t attempt = 1; attempt <= NTP_SYNC_MAX_RETRIES; attempt++) {
                printf("[NTP] sync attempt %u/%u\n", (unsigned)attempt, (unsigned)NTP_SYNC_MAX_RETRIES);
                if (ntp_client_sync_to_rtc()) {
                    ntp_ok = true;
                    break;
                }
                if (attempt < NTP_SYNC_MAX_RETRIES) {
                    sleep_ms(NTP_RETRY_DELAY_MS);
                }
            }
            if (!ntp_ok) {
                printf("[NTP] all sync attempts failed, keep DS3231/AON time\n");
                printf("[TIME] using RTC time\n");
            } else {
                printf("[TIME] using NTP synced time\n");
            }
        }
    }

    print_eeprom_boot_summary_delayed();
    pir_test_init();

    (void)scd41_try_init_and_start(i2c1, to_ms_since_boot(get_absolute_time()));

    mqtt_cfg_telemetry.broker_ip = MQTT_BROKER_IP;
    mqtt_cfg_telemetry.broker_port = MQTT_BROKER_PORT;
    mqtt_cfg_telemetry.topic = MQTT_TOPIC_TELEMETRY;
    mqtt_cfg_telemetry.topic_control = NULL;
    snprintf(g_mqtt_client_id_telemetry, sizeof(g_mqtt_client_id_telemetry), "%s-telemetry", MQTT_CLIENT_ID);
    snprintf(g_mqtt_client_id_status, sizeof(g_mqtt_client_id_status), "%s-status", MQTT_CLIENT_ID);
    mqtt_cfg_telemetry.client_id = g_mqtt_client_id_telemetry;
    mqtt_cfg_telemetry.mqtt_user = MQTT_USER;
    mqtt_cfg_telemetry.mqtt_pass = MQTT_PASS;
    mqtt_cfg_telemetry.ca_cert = ca_crt;
    mqtt_cfg_telemetry.ca_cert_len = ca_crt_len;

    mqtt_cfg_status = mqtt_cfg_telemetry;
    mqtt_cfg_status.topic = MQTT_TOPIC_STATUS;
    mqtt_cfg_status.topic_control = MQTT_TOPIC_CONTROL;
    mqtt_cfg_status.client_id = g_mqtt_client_id_status;

    printf("[DEBUG] Preparing to call mqtt_manager_init...\r\n");
    printf("[DEBUG] mqtt_cfg.broker_ip=%s\r\n", mqtt_cfg_telemetry.broker_ip);
    printf("[DEBUG] mqtt_cfg.broker_port=%u\r\n", (unsigned)mqtt_cfg_telemetry.broker_port);
    printf("[DEBUG] mqtt_cfg.topic=%s (telemetry)\r\n", mqtt_cfg_telemetry.topic);
    printf("[DEBUG] mqtt_cfg.client_id.telemetry=%s\r\n", mqtt_cfg_telemetry.client_id);
    printf("[DEBUG] mqtt_cfg.client_id.status=%s\r\n", mqtt_cfg_status.client_id);
    printf("[DEBUG] MQTT_TOPIC_CONTROL=%s\r\n", MQTT_TOPIC_CONTROL);
    printf("[DEBUG] MQTT_TOPIC_STATUS=%s\r\n", MQTT_TOPIC_STATUS);
    printf("[DEBUG] MQTT_TOPIC_TELEMETRY_SYNC_BACK=%s\r\n", MQTT_TOPIC_TELEMETRY_SYNC_BACK);
    printf("[DEBUG] MQTT_TOPIC_UI_EVENTS=%s\r\n", MQTT_TOPIC_UI_EVENTS);
    printf("[DEBUG] mqtt_cfg.ca_cert=%p\r\n", (const void *)mqtt_cfg_telemetry.ca_cert);
    printf("[DEBUG] mqtt_cfg.ca_cert_len=%u\r\n", (unsigned)mqtt_cfg_telemetry.ca_cert_len);
    printf("[DEBUG] ca_crt_len(symbol)=%u\r\n", (unsigned)ca_crt_len);

    if (!mqtt_manager_init(&g_mqtt_telemetry, &mqtt_cfg_telemetry)) {
        char fatal_reason[96];
        printf("[DEBUG] mqtt_manager_init (telemetry) error=%d (%s)\r\n",
               (int)mqtt_manager_get_last_init_error(),
               mqtt_manager_get_last_init_error_text());
        snprintf(fatal_reason, sizeof(fatal_reason), "mqtt telemetry init failed: %s",
                 mqtt_manager_get_last_init_error_text());
        sleep_ms(1500);
        enter_safe_mode(fatal_reason);
    }
    if (!mqtt_manager_init(&g_mqtt_status, &mqtt_cfg_status)) {
        char fatal_reason[96];
        printf("[DEBUG] mqtt_manager_init (status) error=%d (%s)\r\n",
               (int)mqtt_manager_get_last_init_error(),
               mqtt_manager_get_last_init_error_text());
        snprintf(fatal_reason, sizeof(fatal_reason), "mqtt status init failed: %s",
                 mqtt_manager_get_last_init_error_text());
        sleep_ms(1500);
        enter_safe_mode(fatal_reason);
    }
    mqtt_manager_set_control_handler(&g_mqtt_status, handle_control_message, NULL);

    uint32_t next_telemetry_ms = 0;
    uint32_t next_imu_read_ms = 0;
    imu_sample_t imu_sample;
    memset(&imu_sample, 0, sizeof(imu_sample));
    while (true) {
        uint32_t now = to_ms_since_boot(get_absolute_time());
        pir_test_poll(now);
        wifi_handler_process(&g_wifi, now);
        bool wifi_connected = wifi_handler_is_connected(&g_wifi);
        if (g_prev_wifi_link && !wifi_connected) {
            mqtt_manager_on_wifi_down(&g_mqtt_telemetry, now);
            mqtt_manager_on_wifi_down(&g_mqtt_status, now);
        }
        g_prev_wifi_link = wifi_connected;
        bool mqtt_connected_telemetry = mqtt_manager_is_connected(&g_mqtt_telemetry);
        bool mqtt_connected_status = mqtt_manager_is_connected(&g_mqtt_status);
        bool mqtt_connected = mqtt_connected_telemetry && mqtt_connected_status;
        update_status_outputs(now, wifi_connected, mqtt_connected);

#if ENABLE_LCD1602
        {
            int rssi_dbm = wifi_connected ? wifi_handler_get_rssi_dbm() : 0;
            bool urgent = accel_is_urgent(&imu_sample, g_device_cfg.accel_urgent_threshold_g);
            bool wifi_trying = wifi_handler_is_attempting(&g_wifi);
            bool mqtt_trying = g_mqtt_telemetry.connecting || g_mqtt_status.connecting;
            int wifi_err_code = wifi_handler_last_connect_rc(&g_wifi);
            int mqtt_err_code = mqtt_diag_code_any(&g_mqtt_telemetry, &g_mqtt_status);
            lcd_refresh_mvp(now, wifi_connected, mqtt_connected, rssi_dbm, urgent, wifi_trying, mqtt_trying,
                            wifi_err_code, mqtt_err_code);
        }
#endif
        if (g_oled_ok && g_has_last_sample && (int32_t)(now - g_next_oled_rotate_ms) >= 0) {
            oled_show_rotating_pages(now);
            g_next_oled_rotate_ms = now + OLED_PAGE_ROTATE_INTERVAL_MS;
        }

        if (wifi_connected) {
            mqtt_manager_process(&g_mqtt_telemetry, now);
            mqtt_manager_process(&g_mqtt_status, now);
        }

        if (g_scd41_ok && g_scd41_started && (int32_t)(now - g_next_scd41_poll_ms) >= 0) {
            bool ready = scd41_sensor_data_ready();
            if (ready) {
                scd41_sensor_data_t s;
                if (scd41_sensor_read_measurement(&s) && s.valid) {
                    g_last_scd41 = s;
                    printf("[SCD41] co2=%u ppm temp=%.2fC rh=%.2f%%\n", (unsigned)s.co2_ppm,
                           (double)s.temperature_c, (double)s.humidity_pct);
                } else {
                    printf("[SCD41] read measurement failed, scheduling recover retry\n");
                    g_scd41_ok = false;
                    g_scd41_started = false;
                    g_last_scd41.valid = false;
                    g_next_scd41_retry_ms = now + SCD41_RETRY_INTERVAL_MS;
                }
            } else {
                printf("[SCD41] data not ready\n");
            }
            g_next_scd41_poll_ms = now + SCD41_POLL_INTERVAL_MS;
        }
        if ((!g_scd41_ok || !g_scd41_started) && g_next_scd41_retry_ms != 0 &&
            (int32_t)(now - g_next_scd41_retry_ms) >= 0) {
            g_scd41_retry_count++;
            printf("[SCD41] retry #%u\n", (unsigned)g_scd41_retry_count);
            (void)scd41_try_init_and_start(i2c1, now);
        }

#if ENABLE_PAJ7620_UI_EVENTS
        if (g_paj_diag.detected && g_paj_gesture_ready) {
            if (g_next_paj_poll_ms == 0U) {
                g_next_paj_poll_ms = now + PAJ7620_POLL_INTERVAL_MS;
            }
            if ((int32_t)(now - g_next_paj_poll_ms) >= 0) {
                uint8_t g43 = 0;
                uint8_t g44 = 0;
                if (paj7620_read_gesture_regs(i2c0, &g43, &g44)) {
                    uint8_t gesture_code = paj7620_pick_gesture_code(g43, g44);
#if ENABLE_PAJ7620_DEBUG_LOG
                    static uint32_t next_dbg_ms = 0;
                    if (gesture_code != 0 || (int32_t)(now - next_dbg_ms) >= 0) {
                        printf("[PAJ7620] raw g43=0x%02X g44=0x%02X picked=0x%02X\r\n", g43, g44, gesture_code);
                        next_dbg_ms = now + 1000U;
                    }
#endif
#if ENABLE_PAJ7620_RAW_UI_EVENT_DEBUG
                    if ((g43 != 0U || g44 != 0U) && wifi_connected && mqtt_connected_status &&
                        mqtt_manager_publish_ready(&g_mqtt_status)) {
                        ds3231_time_t gt_raw;
                        if (ds3231_get_time(&gt_raw)) {
                            char raw_payload[320];
                            build_ui_event_gesture_raw_payload(raw_payload, sizeof(raw_payload), &gt_raw, g43, g44,
                                                               gesture_code);
                            if (mqtt_manager_publish_topic(&g_mqtt_status, MQTT_TOPIC_UI_EVENTS, raw_payload)) {
                                printf("[MQTT] ui-events raw g43=0x%02X g44=0x%02X picked=0x%02X\r\n", g43, g44,
                                       gesture_code);
                            }
                        }
                    }
#endif
                    if (gesture_code != 0) {
                        bool same_as_last = (gesture_code == g_paj_last_code);
                        bool within_min_interval = ((int32_t)(now - g_paj_last_event_ms) <
                                                    (int32_t)PAJ7620_GESTURE_MIN_INTERVAL_MS);
                        if (same_as_last && within_min_interval) {
#if ENABLE_PAJ7620_DEBUG_LOG
                            printf("[PAJ7620] suppressed duplicate code=0x%02X within %u ms\r\n", gesture_code,
                                   (unsigned)PAJ7620_GESTURE_MIN_INTERVAL_MS);
#endif
                            paj7620_clear_gesture_regs(i2c0);
                            g_next_paj_poll_ms = now + PAJ7620_POLL_INTERVAL_MS;
                            continue;
                        }

                        const char *gname = paj7620_gesture_name(gesture_code);
                        if (gname != NULL) {
                            ds3231_time_t gt;
                            if (ds3231_get_time(&gt)) {
                                char ui_payload[320];
                                build_ui_event_gesture_payload(ui_payload, sizeof(ui_payload), &gt, gname);
                                if (wifi_connected && mqtt_connected_status && mqtt_manager_publish_ready(&g_mqtt_status)) {
                                    if (mqtt_manager_publish_topic(&g_mqtt_status, MQTT_TOPIC_UI_EVENTS, ui_payload)) {
                                        printf("[MQTT] ui-events gesture code=0x%02X name=%s\r\n", gesture_code, gname);
                                        g_paj_last_event_ms = now;
                                        g_paj_last_code = gesture_code;
                                    }
                                } else {
                                    printf("[PAJ7620] gesture detected but MQTT not ready (code=0x%02X name=%s)\r\n",
                                           gesture_code, gname);
                                }
                            }
                        } else {
                            printf("[PAJ7620] unknown gesture code=0x%02X\r\n", gesture_code);
                        }
                        paj7620_clear_gesture_regs(i2c0);
                    }
                } else {
#if ENABLE_PAJ7620_DEBUG_LOG
                    printf("[PAJ7620] read gesture regs failed\r\n");
#endif
                }
                g_next_paj_poll_ms = now + PAJ7620_POLL_INTERVAL_MS;
            }
        }
#endif

#if ENABLE_TELEMETRY_EEPROM_CACHE
        if (g_next_sync_back_ms == 0U) {
            g_next_sync_back_ms = now;
        }
        if (g_eeprom_queue_ok && wifi_connected && mqtt_connected_telemetry &&
            mqtt_manager_publish_ready(&g_mqtt_telemetry) &&
            telemetry_eeprom_queue_count() > 0U && (int32_t)(now - g_next_sync_back_ms) >= 0) {
            uint16_t rlen = 0;
            if (telemetry_eeprom_queue_pop(g_replay_buf, sizeof(g_replay_buf), &rlen) && rlen > 0U) {
                mark_json_sync_back(g_replay_buf, sizeof(g_replay_buf));
                if (mqtt_manager_publish_topic(&g_mqtt_telemetry, MQTT_TOPIC_TELEMETRY_SYNC_BACK, g_replay_buf)) {
                    printf("[MQTT] sync-back %s\n", g_replay_buf);
                } else {
                    uint16_t retry_len = (uint16_t)strlen(g_replay_buf);
                    if (!telemetry_eeprom_queue_push(g_replay_buf, retry_len)) {
                        printf("[EEPROM] sync-back publish failed and re-queue failed\n");
                    }
                }
            }
            g_next_sync_back_ms = now + SYNC_BACK_INTERVAL_MS;
        }
#endif

        /* Lifecycle on .../status: boot + session online; not tied to telemetry interval. */
        if (wifi_connected && mqtt_connected) {
            if (!g_prev_mqtt_connected_for_alert) {
                g_mqtt_session_alert_sent = false;
            }
            ds3231_time_t conn_t;
            if (ds3231_get_time(&conn_t)) {
                if (!g_alert_boot_sent && mqtt_manager_publish_ready(&g_mqtt_status)) {
                    char event_payload[256];
                    build_log_payload(event_payload, sizeof(event_payload), &conn_t, "SYSTEM", "INFO", 1000,
                                      "PICO_BOOT_OK_MQTT_CONNECTED");
                    if (mqtt_manager_publish_topic(&g_mqtt_status, MQTT_TOPIC_STATUS, event_payload)) {
                        g_alert_boot_sent = true;
                        printf("[MQTT] status %s\n", event_payload);
                    }
                }
                if (g_alert_boot_sent && !g_mqtt_session_alert_sent && mqtt_manager_publish_ready(&g_mqtt_status)) {
                    char event_payload[256];
                    build_log_payload(event_payload, sizeof(event_payload), &conn_t, "MQTT", "INFO", 1001,
                                      "MQTT_SESSION_ONLINE");
                    if (mqtt_manager_publish_topic(&g_mqtt_status, MQTT_TOPIC_STATUS, event_payload)) {
                        g_mqtt_session_alert_sent = true;
                        printf("[MQTT] status %s\n", event_payload);
                    }
                }
#if ENABLE_TEST_UI_EVENTS_MQTT_PUBLISH
                if (g_mqtt_session_alert_sent && !g_ui_event_test_sent && mqtt_manager_publish_ready(&g_mqtt_status)) {
                    char ui_payload[320];
                    build_ui_event_test_payload(ui_payload, sizeof(ui_payload), &conn_t);
                    if (mqtt_manager_publish_topic(&g_mqtt_status, MQTT_TOPIC_UI_EVENTS, ui_payload)) {
                        g_ui_event_test_sent = true;
                        printf("[MQTT] ui-events test publish -> %s\r\n", MQTT_TOPIC_UI_EVENTS);
                    }
                }
#endif
            }
        }

        if (next_imu_read_ms == 0U) {
            next_imu_read_ms = now + IMU_SAMPLING_INTERVAL_MS;
        }
        if (now >= next_imu_read_ms) {
            mpu9250_read_imu(&imu_sample);
            /* Read IMU frequently without blocking telemetry. */
            next_imu_read_ms = now + IMU_SAMPLING_INTERVAL_MS;
        }

        bool is_urgent = accel_is_urgent(&imu_sample, g_device_cfg.accel_urgent_threshold_g);

        if (next_telemetry_ms == 0U) {
            next_telemetry_ms = now + g_device_cfg.telemetry_interval_ms;
        }
        bool due = (int32_t)(now - next_telemetry_ms) >= 0;
        if (is_urgent || due) {
            if (is_urgent) {
                printf("[URGENT] trigger: accel=(%.3f, %.3f, %.3f) thr=%.3f\n",
                       (double)imu_sample.accel_x,
                       (double)imu_sample.accel_y,
                       (double)imu_sample.accel_z,
                       (double)g_device_cfg.accel_urgent_threshold_g);
            }
            ds3231_time_t t;
            if (ds3231_get_time(&t)) {
                if (!g_rtc_debug_logged_once) {
                    printf("[RTC] first read ok after boot\n");
                    g_rtc_debug_logged_once = true;
                }
                if ((int32_t)(now - g_next_rtc_log_ms) >= 0) {
                    printf("[RTC] runtime: 20%02u-%02u-%02uT%02u:%02u:%02u (%s)\n",
                           t.year, t.month, t.day, t.hour, t.minute, t.second,
                           ds3231_time_is_valid(&t) ? "valid" : "invalid");
                    g_next_rtc_log_ms = now + 30000;
                }
                send_telemetry_packet(&g_device_cfg, &t, &imu_sample, is_urgent, mqtt_connected);
            }
            next_telemetry_ms = now + g_device_cfg.telemetry_interval_ms;
        }
        g_prev_mqtt_connected_for_alert = mqtt_connected;

        tight_loop_contents();
    }
}
