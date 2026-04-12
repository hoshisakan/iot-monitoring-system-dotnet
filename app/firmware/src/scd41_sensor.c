#include "scd41_sensor.h"

#include <string.h>

#include "pico/stdlib.h"

#define SCD41_CMD_STOP_PERIODIC 0x3F86U
#define SCD41_CMD_START_PERIODIC 0x21B1U
#define SCD41_CMD_REINIT 0x3646U
#define SCD41_CMD_READ_SERIAL 0x3682U
#define SCD41_CMD_GET_DATA_READY 0xE4B8U
#define SCD41_CMD_READ_MEASUREMENT 0xEC05U

static i2c_inst_t *g_i2c = NULL;
static uint8_t g_addr = 0;
static bool g_connected = false;

static uint8_t scd41_crc8(const uint8_t *data, size_t len) {
    uint8_t crc = 0xFFU;
    for (size_t i = 0; i < len; i++) {
        crc ^= data[i];
        for (int b = 0; b < 8; b++) {
            if (crc & 0x80U) {
                crc = (uint8_t)((crc << 1) ^ 0x31U);
            } else {
                crc <<= 1;
            }
        }
    }
    return crc;
}

static bool write_cmd(uint16_t cmd) {
    uint8_t buf[2] = {(uint8_t)(cmd >> 8), (uint8_t)(cmd & 0xFFU)};
    int rc = i2c_write_timeout_us(g_i2c, g_addr, buf, 2, false, 30000);
    return rc == 2;
}

static bool read_buf(uint8_t *buf, size_t len) {
    int rc = i2c_read_timeout_us(g_i2c, g_addr, buf, len, false, 30000);
    return (size_t)rc == len;
}

static bool read_words_with_crc(uint16_t cmd, uint16_t *words, size_t word_count, uint32_t wait_ms) {
    if (!write_cmd(cmd)) {
        return false;
    }
    if (wait_ms > 0U) {
        sleep_ms(wait_ms);
    }
    uint8_t raw[18];
    size_t need = word_count * 3U;
    if (need > sizeof(raw)) {
        return false;
    }
    if (!read_buf(raw, need)) {
        return false;
    }
    for (size_t i = 0; i < word_count; i++) {
        const uint8_t *p = &raw[i * 3U];
        uint8_t crc = scd41_crc8(p, 2);
        if (crc != p[2]) {
            return false;
        }
        words[i] = (uint16_t)(((uint16_t)p[0] << 8) | p[1]);
    }
    return true;
}

bool scd41_sensor_init(i2c_inst_t *i2c, uint8_t addr_7bit) {
    g_i2c = i2c;
    g_addr = addr_7bit;
    g_connected = false;

    /*
     * Make sure the sensor is in idle command mode first.
     * SCD4x requires a long settle after stop-periodic before accepting
     * commands like read-serial/reinit.
     */
    (void)write_cmd(SCD41_CMD_STOP_PERIODIC);
    sleep_ms(500);
    (void)write_cmd(SCD41_CMD_REINIT);
    sleep_ms(30);

    /* Probe via serial read command in idle mode. */
    uint16_t serial_words[3] = {0};
    if (!read_words_with_crc(SCD41_CMD_READ_SERIAL, serial_words, 3, 1U)) {
        return false;
    }
    g_connected = true;
    return true;
}

bool scd41_sensor_is_connected(void) {
    return g_connected;
}

bool scd41_sensor_start_periodic_measurement(void) {
    if (!g_connected) {
        return false;
    }
    if (write_cmd(SCD41_CMD_START_PERIODIC)) {
        return true;
    }
    /*
     * Recovery path: if sensor was still in transition, force stop + settle,
     * then start again.
     */
    (void)write_cmd(SCD41_CMD_STOP_PERIODIC);
    sleep_ms(500);
    (void)write_cmd(SCD41_CMD_REINIT);
    sleep_ms(30);
    return write_cmd(SCD41_CMD_START_PERIODIC);
}

bool scd41_sensor_data_ready(void) {
    if (!g_connected) {
        return false;
    }
    uint16_t status_word = 0;
    if (!read_words_with_crc(SCD41_CMD_GET_DATA_READY, &status_word, 1, 1U)) {
        return false;
    }
    return (status_word & 0x07FFU) != 0U;
}

bool scd41_sensor_read_measurement(scd41_sensor_data_t *out) {
    if (out == NULL || !g_connected) {
        return false;
    }
    memset(out, 0, sizeof(*out));

    uint16_t words[3] = {0};
    if (!read_words_with_crc(SCD41_CMD_READ_MEASUREMENT, words, 3, 1U)) {
        return false;
    }

    out->co2_ppm = words[0];
    out->temperature_c = -45.0f + (175.0f * (float)words[1] / 65535.0f);
    out->humidity_pct = 100.0f * (float)words[2] / 65535.0f;
    out->valid = true;
    return true;
}
