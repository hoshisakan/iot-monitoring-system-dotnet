#ifndef MPU9250_SENSOR_H
#define MPU9250_SENSOR_H

#include <stdbool.h>
#include <stdint.h>

#include "hardware/i2c.h"

/**
 * MPU-9250 on I2C0 (same bus as BME680/TSL2561/SH1106 per hardware plan).
 * Typical 7-bit address 0x68 (Grove); use 0x69 if AD0 is tied high.
 */

bool mpu9250_sensor_init(i2c_inst_t *i2c, uint8_t addr_7bit);

/**
 * Read accelerometer (g) and gyroscope (deg/s).
 * @return false if bus error or driver not initialized.
 */
bool mpu9250_sensor_read(float *accel_x_g,
                         float *accel_y_g,
                         float *accel_z_g,
                         float *gyro_x_dps,
                         float *gyro_y_dps,
                         float *gyro_z_dps);

#endif
