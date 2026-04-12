#include "ds3231.h"

#include <stdio.h>
#include <string.h>
#include <time.h>

#include "pico/aon_timer.h"
#include "pico/stdlib.h"
#include "pico/time.h"

#define DS3231_ADDR 0x68

static i2c_inst_t *g_i2c = NULL;
static uint32_t g_diag_period_ms = 15000;
static uint32_t g_next_diag_ms = 0;

static uint8_t bcd_to_dec(uint8_t val) {
    return (uint8_t)(((val / 16u) * 10u) + (val % 16u));
}

static uint8_t dec_to_bcd(uint8_t val) {
    return (uint8_t)(((val / 10u) << 4) | (val % 10u));
}

static bool bcd_is_valid(uint8_t raw) {
    return ((raw & 0x0Fu) <= 9u) && (((raw >> 4) & 0x0Fu) <= 9u);
}

static void dump_rtc_raw(const uint8_t data[7]) {
    printf("[RTC] raw regs: %02X %02X %02X %02X %02X %02X %02X\n",
           data[0], data[1], data[2], data[3], data[4], data[5], data[6]);
    fflush(stdout);
    printf("[RTC] flags: CH=%u 12H=%u PM=%u\n",
           (unsigned)((data[0] & 0x80u) ? 1u : 0u),
           (unsigned)((data[2] & 0x40u) ? 1u : 0u),
           (unsigned)((data[2] & 0x20u) ? 1u : 0u));
    fflush(stdout);
}

static bool parse_build_time(ds3231_time_t *out) {
    static const char *months[12] = {
        "Jan", "Feb", "Mar", "Apr", "May", "Jun",
        "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
    };
    char mon[4] = {0};
    int day = 0, year = 0, hour = 0, minute = 0, second = 0;
    int parsed_date = sscanf(__DATE__, "%3s %d %d", mon, &day, &year);
    int parsed_time = sscanf(__TIME__, "%d:%d:%d", &hour, &minute, &second);
    if (parsed_date != 3 || parsed_time != 3) {
        return false;
    }

    int month = 0;
    for (int i = 0; i < 12; i++) {
        if (strncmp(mon, months[i], 3) == 0) {
            month = i + 1;
            break;
        }
    }
    if (month == 0 || year < 2000 || year > 2099) {
        return false;
    }

    out->year = (uint8_t)(year - 2000);
    out->month = (uint8_t)month;
    out->day = (uint8_t)day;
    out->hour = (uint8_t)hour;
    out->minute = (uint8_t)minute;
    out->second = (uint8_t)second;
    return true;
}

bool ds3231_init(i2c_inst_t *i2c, uint sda_pin, uint scl_pin, uint32_t baudrate_hz) {
    if (i2c == NULL) {
        return false;
    }
    g_i2c = i2c;
    i2c_init(g_i2c, baudrate_hz);
    gpio_set_function(sda_pin, GPIO_FUNC_I2C);
    gpio_set_function(scl_pin, GPIO_FUNC_I2C);
    gpio_pull_up(sda_pin);
    gpio_pull_up(scl_pin);
    sleep_ms(20);
    return true;
}

void ds3231_set_diag_period_ms(uint32_t period_ms) {
    g_diag_period_ms = period_ms;
}

bool ds3231_get_time(ds3231_time_t *t) {
    uint8_t reg = 0x00;
    uint8_t data[7];
    if (g_i2c == NULL || t == NULL) {
        return false;
    }

    int wr = i2c_write_blocking(g_i2c, DS3231_ADDR, &reg, 1, true);
    if (wr < 0) {
        printf("[RTC] i2c write failed rc=%d\n", wr);
        return false;
    }
    int rd = i2c_read_blocking(g_i2c, DS3231_ADDR, data, 7, false);
    if (rd < 0) {
        printf("[RTC] i2c read failed rc=%d\n", rd);
        return false;
    }

    uint32_t now_ms = to_ms_since_boot(get_absolute_time());
    if ((int32_t)(now_ms - g_next_diag_ms) >= 0) {
        dump_rtc_raw(data);
        if (data[0] & 0x80u) {
            printf("[RTC] warning: CH=1, oscillator halted (time not running)\n");
        }
        g_next_diag_ms = now_ms + g_diag_period_ms;
    }

    if (!bcd_is_valid(data[0] & 0x7Fu) ||
        !bcd_is_valid(data[1] & 0x7Fu) ||
        !bcd_is_valid(data[2] & 0x3Fu) ||
        !bcd_is_valid(data[4] & 0x3Fu) ||
        !bcd_is_valid(data[5] & 0x1Fu) ||
        !bcd_is_valid(data[6])) {
        printf("[RTC] invalid BCD data from DS3231\n");
        dump_rtc_raw(data);
        return false;
    }

    t->second = bcd_to_dec(data[0] & 0x7Fu);
    t->minute = bcd_to_dec(data[1]);
    if (data[2] & 0x40u) {
        uint8_t hour12 = bcd_to_dec(data[2] & 0x1Fu);
        bool pm = (data[2] & 0x20u) != 0u;
        if (hour12 == 12u) {
            t->hour = pm ? 12u : 0u;
        } else {
            t->hour = (uint8_t)(hour12 + (pm ? 12u : 0u));
        }
    } else {
        t->hour = bcd_to_dec(data[2] & 0x3Fu);
    }
    t->day = bcd_to_dec(data[4]);
    t->month = bcd_to_dec(data[5] & 0x7Fu);
    t->year = bcd_to_dec(data[6]);
    return true;
}

bool ds3231_set_time(const ds3231_time_t *t) {
    uint8_t reg_data[8];
    if (g_i2c == NULL || t == NULL) {
        return false;
    }
    reg_data[0] = 0x00;
    reg_data[1] = dec_to_bcd(t->second);
    reg_data[2] = dec_to_bcd(t->minute);
    reg_data[3] = dec_to_bcd(t->hour);
    reg_data[4] = 0x01;
    reg_data[5] = dec_to_bcd(t->day);
    reg_data[6] = dec_to_bcd(t->month);
    reg_data[7] = dec_to_bcd(t->year);
    int wr = i2c_write_blocking(g_i2c, DS3231_ADDR, reg_data, 8, false);
    if (wr < 0) {
        printf("[RTC] i2c write-time failed rc=%d\n", wr);
        return false;
    }
    return true;
}

bool ds3231_get_status_reg(uint8_t *status) {
    uint8_t reg = 0x0F;
    uint8_t value = 0;
    if (g_i2c == NULL || status == NULL) {
        return false;
    }
    int wr = i2c_write_blocking(g_i2c, DS3231_ADDR, &reg, 1, true);
    if (wr < 0) {
        printf("[RTC] i2c write(status) failed rc=%d\n", wr);
        return false;
    }
    int rd = i2c_read_blocking(g_i2c, DS3231_ADDR, &value, 1, false);
    if (rd < 0) {
        printf("[RTC] i2c read(status) failed rc=%d\n", rd);
        return false;
    }
    *status = value;
    return true;
}

bool ds3231_clear_osf(void) {
    uint8_t status = 0;
    uint8_t reg_data[2];
    if (!ds3231_get_status_reg(&status)) {
        return false;
    }
    status &= (uint8_t)~0x80u;
    reg_data[0] = 0x0F;
    reg_data[1] = status;
    int wr = i2c_write_blocking(g_i2c, DS3231_ADDR, reg_data, 2, false);
    if (wr < 0) {
        printf("[RTC] i2c write(clear osf) failed rc=%d\n", wr);
        return false;
    }
    return true;
}

bool ds3231_time_is_valid(const ds3231_time_t *t) {
    if (t == NULL) {
        return false;
    }
    if (t->year < 24 || t->year > 99) return false;
    if (t->month < 1 || t->month > 12) return false;
    if (t->day < 1 || t->day > 31) return false;
    if (t->hour > 23 || t->minute > 59 || t->second > 59) return false;
    return true;
}

bool ds3231_sync_system_time(bool auto_init_from_build) {
    ds3231_time_t t;
    printf("[RTC] sync start\n");
    if (!ds3231_get_time(&t)) {
        printf("[RTC] read failed, keep default epoch\n");
        return false;
    }
    if (!ds3231_time_is_valid(&t) && auto_init_from_build) {
        ds3231_time_t build_t;
        printf("[RTC] detected uninitialized time: 20%02u-%02u-%02uT%02u:%02u:%02u\n",
               t.year, t.month, t.day, t.hour, t.minute, t.second);
        if (parse_build_time(&build_t)) {
            printf("[RTC] setting DS3231 from build time: 20%02u-%02u-%02uT%02u:%02u:%02u\n",
                   build_t.year, build_t.month, build_t.day,
                   build_t.hour, build_t.minute, build_t.second);
            if (ds3231_set_time(&build_t) && ds3231_get_time(&t)) {
                printf("[RTC] DS3231 write ok\n");
            } else {
                printf("[RTC] DS3231 write failed, keep RTC original value\n");
            }
        } else {
            printf("[RTC] parse __DATE__/__TIME__ failed, keep RTC original value\n");
        }
    }

    struct tm rtc_tm = {
        .tm_sec = t.second,
        .tm_min = t.minute,
        .tm_hour = t.hour,
        .tm_mday = t.day,
        .tm_mon = t.month - 1,
        .tm_year = t.year + 100,
        .tm_isdst = -1
    };
    time_t epoch = mktime(&rtc_tm);
    if (epoch == (time_t)-1) {
        printf("[RTC] mktime failed, keep default epoch\n");
        return false;
    }
    struct timespec ts = {.tv_sec = epoch, .tv_nsec = 0};
    aon_timer_start(&ts);
    printf("[RTC] sync ok: 20%02u-%02u-%02uT%02u:%02u:%02u\n",
           t.year, t.month, t.day, t.hour, t.minute, t.second);
    return true;
}
