/**
 * @file tsl2561_sensor.c
 * @brief TAOS TSL2561 luminosity sensor (I2C).
 */

#include "tsl2561_sensor.h"

#include <math.h>
#include <string.h>

#include "hardware/i2c.h"
#include "pico/stdlib.h"

/* Bit 7 = CMD; bit 5 = Word (16-bit) per TSL2561 — use for DATA0/DATA1 only. */
#define TSL2561_CMD(reg) (uint8_t)(0x80U | (reg))
#define TSL2561_CMD_WORD(reg) (uint8_t)(0x80U | 0x20U | (reg))

#define TSL2561_REG_CONTROL 0x00U
#define TSL2561_REG_TIMING 0x01U
#define TSL2561_REG_DATA0L 0x0CU
#define TSL2561_REG_DATA1L 0x0EU

#define TSL2561_CONTROL_POWER_ON 0x03U

/*
 * TIMING (0x01): bits 1-0 = 10 → 402 ms integration; bit 4 = 1 → 16× gain.
 * Bosch/Taos lux formulas in the datasheet assume this nominal setting; 1× (0x02)
 * scales ADC counts down ~16× and yields lux far below typical indoor readings.
 */
#define TSL2561_TIMING_402MS_GAIN16 0x12U

static i2c_inst_t *g_i2c;
static uint8_t g_addr;

static bool write_reg(uint8_t reg, uint8_t val) {
    uint8_t buf[2] = {TSL2561_CMD(reg), val};
    int n = i2c_write_blocking(g_i2c, g_addr, buf, 2, false);
    return n == 2;
}

static bool read_u16(uint8_t reg, uint16_t *out) {
    uint8_t cmd = TSL2561_CMD_WORD(reg);
    int n = i2c_write_blocking(g_i2c, g_addr, &cmd, 1, true);
    if (n != 1) {
        return false;
    }
    uint8_t b[2];
    n = i2c_read_blocking(g_i2c, g_addr, b, 2, false);
    if (n != 2) {
        return false;
    }
    *out = (uint16_t)((uint16_t)b[0] | ((uint16_t)b[1] << 8));
    return true;
}

static bool tsl2561_init_at_addr(i2c_inst_t *i2c, uint8_t addr_7bit) {
    g_i2c = i2c;
    g_addr = addr_7bit;

    if (!write_reg(TSL2561_REG_CONTROL, TSL2561_CONTROL_POWER_ON)) {
        return false;
    }
    sleep_ms(10);
    if (!write_reg(TSL2561_REG_TIMING, TSL2561_TIMING_402MS_GAIN16)) {
        return false;
    }
    sleep_ms(450);
    return true;
}

bool tsl2561_sensor_init(i2c_inst_t *i2c, uint8_t addr_7bit) {
    if (addr_7bit == 0U) {
        if (tsl2561_init_at_addr(i2c, 0x29U)) {
            return true;
        }
        return tsl2561_init_at_addr(i2c, 0x39U);
    }
    return tsl2561_init_at_addr(i2c, addr_7bit);
}

bool tsl2561_sensor_read(tsl2561_sensor_data_t *out) {
    if (out == NULL || g_i2c == NULL) {
        return false;
    }
    memset(out, 0, sizeof(*out));

    uint16_t ch0 = 0;
    uint16_t ch1 = 0;
    if (!read_u16(TSL2561_REG_DATA0L, &ch0) || !read_u16(TSL2561_REG_DATA1L, &ch1)) {
        return false;
    }
    sleep_ms(5);

    out->ch0 = ch0;
    out->ch1 = ch1;

    if (ch0 == 0U) {
        out->lux = 0.0f;
        out->valid = true;
        return true;
    }

    float ratio = (float)ch1 / (float)ch0;
    float lux = 0.0f;

    if (ratio <= 0.50f) {
        lux = 0.0304f * (float)ch0 - 0.062f * (float)ch0 * powf(ratio, 1.4f);
    } else if (ratio <= 0.61f) {
        lux = 0.0224f * (float)ch0 - 0.031f * (float)ch1;
    } else if (ratio <= 0.80f) {
        lux = 0.0128f * (float)ch0 - 0.0153f * (float)ch1;
    } else if (ratio <= 1.30f) {
        lux = 0.00146f * (float)ch0 - 0.00112f * (float)ch1;
    } else {
        lux = 0.0f;
    }

    if (lux < 0.0f) {
        lux = 0.0f;
    }

    out->lux = lux;
    out->valid = true;
    return true;
}
