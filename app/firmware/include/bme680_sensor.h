#ifndef BME680_SENSOR_H
#define BME680_SENSOR_H

#include <stdbool.h>
#include <stdint.h>

#include "hardware/i2c.h"

/**
 * @brief Compensated BME680 environmental sample (temperature / humidity / pressure / gas).
 */
typedef struct {
    bool valid;
    float temperature_c;
    float humidity_pct;
    float pressure_hpa;
    float gas_resistance_ohm;
} bme680_sensor_data_t;

/**
 * @brief Attach driver to an already-initialized I2C bus.
 *
 * @param addr_7bit 0x76 / 0x77, or 0 to probe 0x76 then 0x77.
 */
bool bme680_sensor_init(i2c_inst_t *i2c, uint8_t addr_7bit);

/**
 * @brief Run one forced-mode measurement (T/H/P + gas resistance).
 */
bool bme680_sensor_read(bme680_sensor_data_t *out);

#endif
