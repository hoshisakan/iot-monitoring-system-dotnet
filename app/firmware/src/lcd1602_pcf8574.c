/*
 * HD44780 16x2 via PCF8574 I2C backpack — **LiquidCrystal_I2C / YWROBOT 相容**接線：
 *   D4–D7 → PCF8574 **P7–P4**（在 I2C 位元組中為 bits 7–4）
 *   Rs → P0 (0x01), Rw → P1 (維持 0), E → P2 (0x04), 背光 → P3 (0x08)
 *
 * 若你的模組仍無顯示，少數板子為「資料在 P0–P3、RS/E 在高 4bit」之舊版，需另一套驅動。
 */

#include "lcd1602_pcf8574.h"

#include "pico/stdlib.h"

#include "hardware/i2c.h"

/* LiquidCrystal_I2C.h */
#define LCD_RS 0x01U
#define LCD_EN 0x04U

#define LCD_I2C_TIMEOUT_US 100000U

static bool lcd_i2c_write_byte(const lcd1602_t *lcd, uint8_t byte) {
    int n = i2c_write_timeout_us(lcd->i2c, lcd->addr_7bit, &byte, 1, false, LCD_I2C_TIMEOUT_US);
    return n == 1;
}

static bool expander_write(const lcd1602_t *lcd, uint8_t data) {
    uint8_t b = (uint8_t)(data | lcd->backlight_mask);
    return lcd_i2c_write_byte(lcd, b);
}

static bool pulse_enable(const lcd1602_t *lcd, uint8_t data) {
    if (!expander_write(lcd, (uint8_t)(data | LCD_EN))) {
        return false;
    }
    sleep_us(1);
    if (!expander_write(lcd, (uint8_t)(data & (uint8_t)~LCD_EN))) {
        return false;
    }
    sleep_us(50);
    return true;
}

static bool write4bits(const lcd1602_t *lcd, uint8_t value) {
    if (!expander_write(lcd, value)) {
        return false;
    }
    return pulse_enable(lcd, value);
}

/** Send 8-bit command or data to HD44780 in 4-bit mode (mode = Rs: 0=cmd, 1=data). */
static bool send_byte(const lcd1602_t *lcd, uint8_t value, bool is_data) {
    uint8_t mode = is_data ? LCD_RS : 0U;
    uint8_t highnib = (uint8_t)(value & 0xf0U);
    uint8_t lownib = (uint8_t)((value << 4) & 0xf0U);
    if (!write4bits(lcd, (uint8_t)(highnib | mode))) {
        return false;
    }
    return write4bits(lcd, (uint8_t)(lownib | mode));
}

static bool lcd_cmd(const lcd1602_t *lcd, uint8_t cmd) {
    if (!send_byte(lcd, cmd, false)) {
        return false;
    }
    if (cmd == 0x01U || cmd == 0x02U) {
        sleep_ms(3);
    }
    return true;
}

bool lcd1602_init(lcd1602_t *lcd, i2c_inst_t *i2c, uint8_t addr_7bit, uint8_t backlight_mask) {
    if (lcd == NULL || i2c == NULL || addr_7bit < 0x08U || addr_7bit > 0x77U) {
        return false;
    }
    lcd->i2c = i2c;
    lcd->addr_7bit = addr_7bit;
    lcd->backlight_mask = backlight_mask;

    /* 與 LiquidCrystal_I2C::begin：先只送背光，確認 I2C */
    if (!lcd_i2c_write_byte(lcd, backlight_mask)) {
        return false;
    }

    sleep_ms(50);

    /* 8-bit wake → 4-bit（write4bits(0x03<<4) 等） */
    if (!write4bits(lcd, 0x30U)) {
        return false;
    }
    sleep_ms(5);
    if (!write4bits(lcd, 0x30U)) {
        return false;
    }
    sleep_us(4500);
    if (!write4bits(lcd, 0x30U)) {
        return false;
    }
    sleep_us(150);
    if (!write4bits(lcd, 0x20U)) {
        return false;
    }

    /* Function set 0x28 */
    if (!lcd_cmd(lcd, 0x28U)) {
        return false;
    }
    /* Display on */
    if (!lcd_cmd(lcd, 0x0CU)) {
        return false;
    }
    /* Entry mode */
    if (!lcd_cmd(lcd, 0x06U)) {
        return false;
    }
    if (!lcd_cmd(lcd, 0x01U)) {
        return false;
    }
    return true;
}

void lcd1602_clear(const lcd1602_t *lcd) {
    if (lcd == NULL) {
        return;
    }
    (void)lcd_cmd(lcd, 0x01U);
}

void lcd1602_put_line(const lcd1602_t *lcd, unsigned row, const char *text) {
    if (lcd == NULL) {
        return;
    }
    uint8_t ddram = (row == 0U) ? 0x80U : 0xC0U;
    if (!lcd_cmd(lcd, ddram)) {
        return;
    }

    char line[17];
    if (text == NULL) {
        text = "";
    }
    size_t i;
    for (i = 0U; i < 16U && text[i] != '\0'; i++) {
        line[i] = text[i];
    }
    for (; i < 16U; i++) {
        line[i] = ' ';
    }
    line[16] = '\0';

    for (i = 0U; i < 16U; i++) {
        if (!send_byte(lcd, (uint8_t)line[i], true)) {
            return;
        }
    }
}
