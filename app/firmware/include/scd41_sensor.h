#ifndef SCD41_SENSOR_H
#define SCD41_SENSOR_H

#include <stdbool.h>
#include <stdint.h>

#include "hardware/i2c.h"

typedef struct {
    uint16_t co2_ppm;
    float temperature_c;
    float humidity_pct;
    bool valid;
} scd41_sensor_data_t;

bool scd41_sensor_init(i2c_inst_t *i2c, uint8_t addr_7bit);
bool scd41_sensor_is_connected(void);
bool scd41_sensor_start_periodic_measurement(void);
bool scd41_sensor_data_ready(void);
bool scd41_sensor_read_measurement(scd41_sensor_data_t *out);

#endif /* SCD41_SENSOR_H */
