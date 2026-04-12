/**
 * @file telemetry_eeprom_queue.c
 * @brief Persistent telemetry FIFO in AT24C256 for offline MQTT buffering.
 */

#include "secrets.h"
#include "firmware_config_defaults.h"
#include "telemetry_eeprom_queue.h"

#include <stdint.h>
#include <string.h>

#include "at24c256_eeprom.h"
#include "pico/stdlib.h"

#define HDR_MAGIC "TEQ1"
#define HDR_VERSION 1U

#define SLOT_MAGIC_OK 0xA55AU
#define SLOT_STRIDE 520U
#define NUM_SLOTS TELEM_EEPROM_NUM_SLOTS

#define EEPROM_HEADER_ADDR 0U
#define EEPROM_HEADER_SIZE 64U
#define SLOT_BASE_ADDR EEPROM_HEADER_SIZE

_Static_assert(
    TELEM_EEPROM_NUM_SLOTS > 0U &&
        (uint32_t)EEPROM_HEADER_SIZE +
                (uint32_t)TELEM_EEPROM_NUM_SLOTS * (uint32_t)SLOT_STRIDE <=
            (uint32_t)AT24C256_CAPACITY_BYTES,
    "TELEM_EEPROM_NUM_SLOTS too large for AT24C256 queue layout");

static struct {
    bool ready;
    uint8_t ridx;
    uint8_t widx;
    uint8_t count;
} g_q;

static bool header_load(void) {
    uint8_t raw[EEPROM_HEADER_SIZE];
    if (!at24c256_eeprom_read(EEPROM_HEADER_ADDR, raw, sizeof(raw))) {
        return false;
    }
    if (memcmp(raw, HDR_MAGIC, 4) != 0 || raw[4] != HDR_VERSION) {
        return false;
    }
    g_q.ridx = raw[5] % NUM_SLOTS;
    g_q.widx = raw[6] % NUM_SLOTS;
    g_q.count = raw[7];
    if (g_q.count > NUM_SLOTS) {
        g_q.count = 0;
        g_q.ridx = g_q.widx = 0;
    }
    return true;
}

static bool header_save(void) {
    uint8_t raw[EEPROM_HEADER_SIZE];
    memset(raw, 0xFF, sizeof(raw));
    memcpy(raw, HDR_MAGIC, 4);
    raw[4] = (uint8_t)HDR_VERSION;
    raw[5] = g_q.ridx;
    raw[6] = g_q.widx;
    raw[7] = g_q.count;
    return at24c256_eeprom_write(EEPROM_HEADER_ADDR, raw, sizeof(raw));
}

static uint16_t slot_addr(uint8_t idx) {
    return (uint16_t)(SLOT_BASE_ADDR + (uint16_t)idx * (uint16_t)SLOT_STRIDE);
}

static bool read_slot(uint8_t idx, uint16_t *magic, uint16_t *len, uint8_t *payload, size_t payload_cap) {
    uint8_t buf[4];
    uint16_t a = slot_addr(idx);
    if (!at24c256_eeprom_read(a, buf, 4)) {
        return false;
    }
    *magic = (uint16_t)(((uint16_t)buf[0] << 8) | buf[1]);
    *len = (uint16_t)(((uint16_t)buf[2] << 8) | buf[3]);
    if (*magic != SLOT_MAGIC_OK || *len == 0U || *len > TELEM_EEPROM_MAX_PAYLOAD) {
        return false;
    }
    if (*len > payload_cap) {
        return false;
    }
    return at24c256_eeprom_read((uint16_t)(a + 4U), payload, *len);
}

static bool write_slot(uint8_t idx, uint16_t json_len, const uint8_t *json) {
    uint8_t buf[SLOT_STRIDE];
    memset(buf, 0xFF, sizeof(buf));
    buf[0] = (uint8_t)(SLOT_MAGIC_OK >> 8);
    buf[1] = (uint8_t)(SLOT_MAGIC_OK & 0xFFU);
    buf[2] = (uint8_t)(json_len >> 8);
    buf[3] = (uint8_t)(json_len & 0xFFU);
    memcpy(buf + 4, json, json_len);
    return at24c256_eeprom_write(slot_addr(idx), buf, 4U + json_len);
}

static bool clear_slot(uint8_t idx) {
    uint8_t z[4] = {0, 0, 0, 0};
    return at24c256_eeprom_write(slot_addr(idx), z, 4);
}

bool telemetry_eeprom_queue_init(i2c_inst_t *i2c, uint8_t eeprom_addr_7bit) {
    memset(&g_q, 0, sizeof(g_q));
    if (!at24c256_eeprom_init(i2c, eeprom_addr_7bit)) {
        return false;
    }
    if (!header_load()) {
        g_q.ridx = g_q.widx = g_q.count = 0;
        if (!header_save()) {
            return false;
        }
        sleep_ms(5);
    }
    g_q.ready = true;
    return true;
}

unsigned telemetry_eeprom_queue_count(void) {
    return g_q.ready ? (unsigned)g_q.count : 0U;
}

bool telemetry_eeprom_queue_push(const char *json, uint16_t json_len) {
    if (!g_q.ready || json == NULL || json_len == 0U || json_len > TELEM_EEPROM_MAX_PAYLOAD) {
        return false;
    }
    if (g_q.count >= NUM_SLOTS) {
        (void)clear_slot(g_q.ridx);
        g_q.ridx = (uint8_t)((g_q.ridx + 1U) % NUM_SLOTS);
        g_q.count--;
    }
    if (!write_slot(g_q.widx, json_len, (const uint8_t *)json)) {
        return false;
    }
    g_q.widx = (uint8_t)((g_q.widx + 1U) % NUM_SLOTS);
    g_q.count++;
    return header_save();
}

bool telemetry_eeprom_queue_pop(char *out, size_t out_cap, uint16_t *out_len) {
    if (!g_q.ready || out == NULL || out_cap < 2U || out_len == NULL) {
        return false;
    }
    if (g_q.count == 0U) {
        return false;
    }
    uint16_t magic = 0;
    uint16_t len = 0;
    if (!read_slot(g_q.ridx, &magic, &len, (uint8_t *)out, out_cap - 1U)) {
        (void)clear_slot(g_q.ridx);
        g_q.ridx = (uint8_t)((g_q.ridx + 1U) % NUM_SLOTS);
        if (g_q.count > 0U) {
            g_q.count--;
        }
        (void)header_save();
        *out_len = 0;
        return false;
    }
    if (len + 1U > out_cap) {
        return false;
    }
    out[len] = '\0';
    *out_len = len;
    (void)clear_slot(g_q.ridx);
    g_q.ridx = (uint8_t)((g_q.ridx + 1U) % NUM_SLOTS);
    g_q.count--;
    return header_save();
}
