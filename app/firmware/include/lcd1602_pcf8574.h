#ifndef LCD1602_PCF8574_H
#define LCD1602_PCF8574_H

#include <stdbool.h>
#include <stddef.h>

#include "hardware/i2c.h"

typedef struct {
    i2c_inst_t *i2c;
    uint8_t addr_7bit;
    /** OR 入每次寫入；LiquidCrystal_I2C 相容板為 0x08（P3 背光）。少數舊板可試 0x00 */
    uint8_t backlight_mask;
} lcd1602_t;

/**
 * Initialize HD44780 in 4-bit mode via PCF8574 backpack (I2C).
 * @param addr_7bit PCF8574 7-bit address (typically 0x27 or 0x3F).
 * @param backlight_mask OR'ed into every expander write (see LCD1602_BACKLIGHT_MASK).
 */
bool lcd1602_init(lcd1602_t *lcd, i2c_inst_t *i2c, uint8_t addr_7bit, uint8_t backlight_mask);

void lcd1602_clear(const lcd1602_t *lcd);

/** Write a single line (row 0 or 1), padded/truncated to 16 ASCII characters. */
void lcd1602_put_line(const lcd1602_t *lcd, unsigned row, const char *text);

#endif /* LCD1602_PCF8574_H */
