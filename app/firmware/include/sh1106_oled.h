#ifndef SH1106_OLED_H
#define SH1106_OLED_H

#include <stdbool.h>
#include <stdint.h>

#include "hardware/i2c.h"

bool sh1106_oled_init(i2c_inst_t *i2c, uint8_t addr_7bit);
void sh1106_oled_clear(void);
void sh1106_oled_draw_string(uint8_t page, uint8_t col, const char *text);
void sh1106_oled_flush(void);
void sh1106_oled_show_env_readings(float temp_c, float humidity_pct, float pressure_hpa, float lux, float gas_ohm,
                                   int rssi_dbm);

#endif
