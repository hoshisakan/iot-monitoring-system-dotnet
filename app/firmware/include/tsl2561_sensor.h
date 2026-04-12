#ifndef TSL2561_SENSOR_H
#define TSL2561_SENSOR_H

#include <stdbool.h>
#include <stdint.h>

#include "hardware/i2c.h"

typedef struct {
    bool valid;
    float lux;
    uint16_t ch0;
    uint16_t ch1;
} tsl2561_sensor_data_t;

/**
 * @brief Initialize TSL2561 on an already-configured I2C bus.
 *
 * @param addr_7bit 0x29 / 0x39, or 0 to probe 0x29 then 0x39.
 */
bool tsl2561_sensor_init(i2c_inst_t *i2c, uint8_t addr_7bit);

/**
 * @brief Read broadband / IR channels and compute lux (Taos / AMS formula).
 */
bool tsl2561_sensor_read(tsl2561_sensor_data_t *out);

#endif
