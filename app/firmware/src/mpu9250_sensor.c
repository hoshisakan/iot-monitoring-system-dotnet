/**
 * @file mpu9250_sensor.c
 * @brief InvenSense MPU-9250 accel + gyro over I2C (MPU-6500-compatible bank).
 */

#include "mpu9250_sensor.h"

#include "hardware/i2c.h"
#include "pico/stdlib.h"

#define MPU9250_REG_WHO_AM_I 0x75U
#define MPU9250_REG_PWR_MGMT_1 0x6BU
#define MPU9250_REG_PWR_MGMT_2 0x6CU
#define MPU9250_REG_CONFIG 0x1AU
#define MPU9250_REG_GYRO_CONFIG 0x1BU
#define MPU9250_REG_ACCEL_CONFIG 0x1CU
#define MPU9250_REG_ACCEL_XOUT_H 0x3BU

/* WHO_AM_I for MPU-9250 (MPU-6500 section) */
#define MPU9250_WHO_AM_I_VALUE 0x71U

/* ±2g: 16384 LSB/g; ±250 °/s: 131 LSB/(°/s) */
#define MPU9250_ACCEL_LSB_PER_G 16384.0f
#define MPU9250_GYRO_LSB_PER_DPS 131.0f

static i2c_inst_t *g_i2c;
static uint8_t g_addr;
static bool g_ready;

static bool write_reg(uint8_t reg, uint8_t val) {
    if (g_i2c == NULL) {
        return false;
    }
    uint8_t buf[2] = {reg, val};
    int n = i2c_write_blocking(g_i2c, g_addr, buf, 2, false);
    return n == 2;
}

static bool read_reg(uint8_t reg, uint8_t *out) {
    if (g_i2c == NULL || out == NULL) {
        return false;
    }
    int n = i2c_write_blocking(g_i2c, g_addr, &reg, 1, true);
    if (n != 1) {
        return false;
    }
    n = i2c_read_blocking(g_i2c, g_addr, out, 1, false);
    return n == 1;
}

static bool read_burst(uint8_t reg, uint8_t *buf, size_t len) {
    if (g_i2c == NULL || buf == NULL || len == 0U) {
        return false;
    }
    int n = i2c_write_blocking(g_i2c, g_addr, &reg, 1, true);
    if (n != 1) {
        return false;
    }
    n = i2c_read_blocking(g_i2c, g_addr, buf, len, false);
    return (size_t)n == len;
}

bool mpu9250_sensor_init(i2c_inst_t *i2c, uint8_t addr_7bit) {
    g_ready = false;
    g_i2c = i2c;
    g_addr = addr_7bit;

    if (i2c == NULL) {
        return false;
    }

    /* Exit sleep (SLEEP bit in PWR_MGMT_1) before ID read */
    if (!write_reg(MPU9250_REG_PWR_MGMT_1, 0x00U)) {
        return false;
    }
    sleep_ms(10);

    uint8_t who = 0;
    if (!read_reg(MPU9250_REG_WHO_AM_I, &who) || who != MPU9250_WHO_AM_I_VALUE) {
        return false;
    }

    /* Internal clock: use PLL when ready (0x01); common for stable rates */
    if (!write_reg(MPU9250_REG_PWR_MGMT_1, 0x01U)) {
        return false;
    }
    sleep_ms(10);

    /* DLPF ~ 20 Hz bandwidth */
    if (!write_reg(MPU9250_REG_CONFIG, 0x04U)) {
        return false;
    }

    /* ±250 °/s */
    if (!write_reg(MPU9250_REG_GYRO_CONFIG, 0x00U)) {
        return false;
    }

    /* ±2g */
    if (!write_reg(MPU9250_REG_ACCEL_CONFIG, 0x00U)) {
        return false;
    }

    /* Standby off for accel + gyro */
    if (!write_reg(MPU9250_REG_PWR_MGMT_2, 0x00U)) {
        return false;
    }

    sleep_ms(10);
    g_ready = true;
    return true;
}

bool mpu9250_sensor_read(float *accel_x_g,
                         float *accel_y_g,
                         float *accel_z_g,
                         float *gyro_x_dps,
                         float *gyro_y_dps,
                         float *gyro_z_dps) {
    if (!g_ready || g_i2c == NULL) {
        return false;
    }
    if (accel_x_g == NULL || accel_y_g == NULL || accel_z_g == NULL || gyro_x_dps == NULL ||
        gyro_y_dps == NULL || gyro_z_dps == NULL) {
        return false;
    }

    uint8_t raw[14];
    if (!read_burst(MPU9250_REG_ACCEL_XOUT_H, raw, sizeof(raw))) {
        return false;
    }

    int16_t ax = (int16_t)(((int16_t)raw[0] << 8) | raw[1]);
    int16_t ay = (int16_t)(((int16_t)raw[2] << 8) | raw[3]);
    int16_t az = (int16_t)(((int16_t)raw[4] << 8) | raw[5]);
    int16_t gx = (int16_t)(((int16_t)raw[8] << 8) | raw[9]);
    int16_t gy = (int16_t)(((int16_t)raw[10] << 8) | raw[11]);
    int16_t gz = (int16_t)(((int16_t)raw[12] << 8) | raw[13]);

    *accel_x_g = (float)ax / MPU9250_ACCEL_LSB_PER_G;
    *accel_y_g = (float)ay / MPU9250_ACCEL_LSB_PER_G;
    *accel_z_g = (float)az / MPU9250_ACCEL_LSB_PER_G;
    *gyro_x_dps = (float)gx / MPU9250_GYRO_LSB_PER_DPS;
    *gyro_y_dps = (float)gy / MPU9250_GYRO_LSB_PER_DPS;
    *gyro_z_dps = (float)gz / MPU9250_GYRO_LSB_PER_DPS;
    return true;
}
