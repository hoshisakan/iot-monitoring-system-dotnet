#ifndef DS3231_H
#define DS3231_H

#include <stdbool.h>
#include <stdint.h>

#include "hardware/i2c.h"

typedef struct {
    uint8_t second;
    uint8_t minute;
    uint8_t hour;
    uint8_t day;
    uint8_t month;
    uint8_t year;
} ds3231_time_t;

bool ds3231_init(i2c_inst_t *i2c, uint sda_pin, uint scl_pin, uint32_t baudrate_hz);
void ds3231_set_diag_period_ms(uint32_t period_ms);
bool ds3231_get_time(ds3231_time_t *t);
bool ds3231_set_time(const ds3231_time_t *t);
bool ds3231_get_status_reg(uint8_t *status);
bool ds3231_clear_osf(void);
bool ds3231_time_is_valid(const ds3231_time_t *t);
bool ds3231_sync_system_time(bool auto_init_from_build);

#endif
