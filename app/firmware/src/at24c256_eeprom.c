/**
 * @file at24c256_eeprom.c
 * @brief AT24C256 I2C EEPROM (32 KiB, 64-byte page writes).
 */

#include "at24c256_eeprom.h"

#include "pico/stdlib.h"
#include "hardware/i2c.h"

#define WRITE_CYCLE_MS 5U

/*
 * Bounded I2C (no infinite wait). Stuck SDA/SCL or missing ACK otherwise blocks forever in
 * i2c_*_blocking — seen right after "[BOOT] EEPROM telemetry cache" during queue header read.
 * Timeout per transaction (µs); scaled for long reads below.
 */
#define AT24C256_I2C_TIMEOUT_BASE_US 50000U
#define AT24C256_I2C_TIMEOUT_PER_BYTE_US 200U
#define AT24C256_I2C_TIMEOUT_MAX_US 500000U

static i2c_inst_t *g_i2c;
static uint8_t g_addr;
static bool g_ready;

static uint32_t xfer_timeout_us(size_t nbytes) {
    uint32_t t = AT24C256_I2C_TIMEOUT_BASE_US + (uint32_t)nbytes * AT24C256_I2C_TIMEOUT_PER_BYTE_US;
    if (t > AT24C256_I2C_TIMEOUT_MAX_US) {
        t = AT24C256_I2C_TIMEOUT_MAX_US;
    }
    return t;
}

static bool write_mem_address(uint16_t mem_addr) {
    if (mem_addr >= AT24C256_CAPACITY_BYTES) {
        return false;
    }
    uint8_t abuf[2] = {(uint8_t)((mem_addr >> 8) & 0xFFU), (uint8_t)(mem_addr & 0xFFU)};
    int n = i2c_write_timeout_us(g_i2c, g_addr, abuf, 2, true, xfer_timeout_us(2));
    if (n < 0 || n != 2) {
        return false;
    }
    return true;
}

bool at24c256_eeprom_init(i2c_inst_t *i2c, uint8_t addr_7bit) {
    g_ready = false;
    g_i2c = i2c;
    g_addr = addr_7bit;
    if (i2c == NULL) {
        return false;
    }
    g_ready = true;
    return true;
}

bool at24c256_eeprom_read(uint16_t mem_addr, uint8_t *buf, size_t len) {
    if (!g_ready || buf == NULL || len == 0U || (size_t)mem_addr + len > AT24C256_CAPACITY_BYTES) {
        return false;
    }
    if (!write_mem_address(mem_addr)) {
        return false;
    }
    int n = i2c_read_timeout_us(g_i2c, g_addr, buf, len, false, xfer_timeout_us(len));
    if (n < 0 || (size_t)n != len) {
        return false;
    }
    return true;
}

bool at24c256_eeprom_write(uint16_t mem_addr, const uint8_t *buf, size_t len) {
    if (!g_ready || buf == NULL || len == 0U || (size_t)mem_addr + len > AT24C256_CAPACITY_BYTES) {
        return false;
    }
    size_t left = len;
    uint16_t addr = mem_addr;
    const uint8_t *p = buf;
    while (left > 0U) {
        uint16_t page_off = (uint16_t)(addr % AT24C256_PAGE_SIZE);
        size_t space_in_page = (size_t)AT24C256_PAGE_SIZE - (size_t)page_off;
        size_t chunk = left;
        if (chunk > space_in_page) {
            chunk = space_in_page;
        }
        uint8_t hdr[2] = {(uint8_t)((addr >> 8) & 0xFFU), (uint8_t)(addr & 0xFFU)};
        uint8_t block[2 + 64];
        if (chunk > sizeof(block) - 2U) {
            return false;
        }
        block[0] = hdr[0];
        block[1] = hdr[1];
        for (size_t i = 0; i < chunk; i++) {
            block[2 + i] = p[i];
        }
        int wlen = (int)(2U + chunk);
        int n = i2c_write_timeout_us(g_i2c, g_addr, block, (size_t)wlen, false, xfer_timeout_us((size_t)wlen));
        if (n < 0 || n != wlen) {
            return false;
        }
        sleep_ms(WRITE_CYCLE_MS);
        addr = (uint16_t)(addr + (uint16_t)chunk);
        p += chunk;
        left -= chunk;
    }
    return true;
}

bool at24c256_eeprom_probe(void) {
    if (!g_ready) {
        return false;
    }
    const uint16_t test_addr = (uint16_t)(AT24C256_CAPACITY_BYTES - 8U);
    uint8_t original[8];
    if (!at24c256_eeprom_read(test_addr, original, sizeof(original))) {
        return false;
    }
    const uint8_t pattern[8] = {0xA5U, 0x5AU, 0x3CU, 0xC3U, 0x55U, 0xAAU, 0x10U, 0x01U};
    if (!at24c256_eeprom_write(test_addr, pattern, sizeof(pattern))) {
        return false;
    }
    uint8_t readback[8];
    if (!at24c256_eeprom_read(test_addr, readback, sizeof(readback))) {
        (void)at24c256_eeprom_write(test_addr, original, sizeof(original));
        return false;
    }
    bool ok = true;
    for (size_t i = 0; i < sizeof(pattern); i++) {
        if (readback[i] != pattern[i]) {
            ok = false;
            break;
        }
    }
    (void)at24c256_eeprom_write(test_addr, original, sizeof(original));
    sleep_ms(WRITE_CYCLE_MS);
    return ok;
}
