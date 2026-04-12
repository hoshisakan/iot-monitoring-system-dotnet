/**
 * @file bme680_sensor.c
 * @brief Bosch BME680 IAQ driver (forced mode, I2C).
 *
 * Compensation and register layout follow the Linux kernel driver (Bosch BME680 API)
 * and BST-BME680-DS000 datasheet.
 */

#include "bme680_sensor.h"

#include <stdint.h>
#include <stdio.h>
#include <string.h>

#include "hardware/i2c.h"
#include "pico/stdlib.h"

#define BME680_REG_CHIP_ID 0xD0U
#define BME680_CHIP_ID_VAL 0x61U
#define BME680_REG_SOFT_RESET 0xE0U
#define BME680_CMD_SOFTRESET 0xB6U

#define BME680_REG_CTRL_HUMIDITY 0x72U
#define BME680_REG_CTRL_MEAS 0x74U
#define BME680_REG_CONFIG 0x75U
#define BME680_REG_CTRL_GAS_1 0x71U
#define BME680_REG_IDAC_HEAT_0 0x50U
#define BME680_REG_RES_HEAT_0 0x5AU
#define BME680_REG_GAS_WAIT_0 0x64U

#define BME680_REG_MEAS_STAT_0 0x1DU
#define BME680_NEW_DATA_BIT 0x80U
#define BME680_GAS_MEAS_BIT 0x40U
#define BME680_MEAS_BIT 0x20U

#define BME680_MODE_MASK 0x03U
#define BME680_MODE_SLEEP 0U
#define BME680_MODE_FORCED 1U

#define BME680_OSRS_TEMP_MASK 0xE0U
#define BME680_OSRS_PRESS_MASK 0x1CU
#define BME680_OSRS_HUMIDITY_MASK 0x07U

#define BME680_RUN_GAS_MASK 0x10U
#define BME680_NB_CONV_MASK 0x0FU

#define BME680_MEAS_TRIM_MASK 0xFFFFFFU
#define BME680_MEAS_SKIPPED 0x8000U

#define BME680_MAX_OVERFLOW_VAL 0x40000000U

#define BME680_HUM_REG_SHIFT_VAL 4U
#define BME680_BIT_H1_DATA_MASK 0x0FU

#define BME680_RHRANGE_MASK 0x30U
#define BME680_RSERROR_MASK 0xF0U

#define BME680_ADC_GAS_RES_MASK 0xFFC0U
#define BME680_GAS_RANGE_MASK 0x0FU
#define BME680_GAS_STAB_BIT 0x10U

#define BME680_T2_LSB_REG 0x8AU
#define BME680_CALIB_RANGE_1_LEN 23U
#define BME680_CALIB_RANGE_3_LEN 5U

#define BME680_AMB_TEMP 25

#define BME680_STARTUP_TIME_MS 2U
#define BME680_DEBUG_HUMIDITY 1
#define BME680_HUM_MAX_STEP_MILLI 5000U

typedef struct {
    uint16_t par_t1;
    int16_t par_t2;
    int8_t par_t3;
    uint16_t par_p1;
    int16_t par_p2;
    int8_t par_p3;
    int16_t par_p4;
    int16_t par_p5;
    int8_t par_p6;
    int8_t par_p7;
    int16_t par_p8;
    int16_t par_p9;
    uint8_t par_p10;
    /* Humidity trim: types match Bosch BST-BME680-DS000 / Linux bme680_calib */
    uint16_t par_h1;
    uint16_t par_h2;
    int8_t par_h3;
    int8_t par_h4;
    int8_t par_h5;
    uint8_t par_h6;
    int8_t par_h7;
    int8_t par_gh1;
    int16_t par_gh2;
    int8_t par_gh3;
    uint8_t res_heat_range;
    int8_t res_heat_val;
    int8_t range_sw_err;
} bme680_calib_t;

static i2c_inst_t *g_i2c;
static uint8_t g_addr;
static bme680_calib_t g_cal;
static uint8_t g_ctrl_meas_base;
static bool g_humidity_filter_initialized;
static uint32_t g_last_humidity_milli;

static uint8_t os_to_field(uint8_t os_ratio) {
    uint8_t v = 1U;
    for (uint8_t i = 1U; i <= 5U; i++) {
        if (v == os_ratio) {
            return i;
        }
        v = (uint8_t)(v << 1U);
    }
    return 1U;
}

static bool i2c_write_reg(uint8_t reg, uint8_t val) {
    uint8_t buf[2] = {reg, val};
    int rc = i2c_write_blocking(g_i2c, g_addr, buf, 2, false);
    return rc == 2;
}

static bool i2c_read_reg(uint8_t reg, uint8_t *val) {
    int rc = i2c_write_blocking(g_i2c, g_addr, &reg, 1, true);
    if (rc != 1) {
        return false;
    }
    rc = i2c_read_blocking(g_i2c, g_addr, val, 1, false);
    return rc == 1;
}

static bool i2c_read_burst(uint8_t reg, uint8_t *buf, size_t len) {
    int rc = i2c_write_blocking(g_i2c, g_addr, &reg, 1, true);
    if (rc != 1) {
        return false;
    }
    rc = i2c_read_blocking(g_i2c, g_addr, buf, len, false);
    return (size_t)rc == len;
}

static uint16_t get_le16(const uint8_t *p) {
    return (uint16_t)p[0] | ((uint16_t)p[1] << 8);
}

static uint32_t get_be24_adc(const uint8_t *p) {
    uint32_t v = ((uint32_t)p[0] << 16) | ((uint32_t)p[1] << 8) | (uint32_t)p[2];
    return (v >> 4) & 0xFFFFFU;
}

static bool load_calibration(void) {
    uint8_t b1[BME680_CALIB_RANGE_1_LEN];
    uint8_t b3[BME680_CALIB_RANGE_3_LEN];

    if (!i2c_read_burst(BME680_T2_LSB_REG, b1, sizeof(b1))) {
        return false;
    }
    if (!i2c_read_burst(0x00U, b3, sizeof(b3))) {
        return false;
    }

    g_cal.par_t2 = (int16_t)get_le16(&b1[0]);
    g_cal.par_t3 = (int8_t)b1[2];
    g_cal.par_p1 = get_le16(&b1[4]);
    g_cal.par_p2 = (int16_t)get_le16(&b1[6]);
    g_cal.par_p3 = (int8_t)b1[8];
    g_cal.par_p4 = (int16_t)get_le16(&b1[10]);
    g_cal.par_p5 = (int16_t)get_le16(&b1[12]);
    g_cal.par_p7 = (int8_t)b1[14];
    g_cal.par_p6 = (int8_t)b1[15];
    g_cal.par_p8 = (int16_t)get_le16(&b1[18]);
    g_cal.par_p9 = (int16_t)get_le16(&b1[20]);
    g_cal.par_p10 = b1[22];

    /*
     * Humidity trim: one byte per read (no burst).
     * H1/H2 from E1–E3 (E2 shared):
     *   par_h1 = (E3 << 4) | (E2 & 0x0F)
     *   par_h2 = (E1 << 4) | (E2 >> 4)
     * For stability we follow Linux/Bosch mapping for H3..H7:
     *   H3=E3, H4=E4, H5=E5, H6=E6, H7=E7.
     */
    uint8_t e1 = 0, e2 = 0, e3 = 0;
    uint8_t e4 = 0, e5 = 0, e6 = 0, e7 = 0, e8 = 0;
    if (!i2c_read_reg(0xE1U, &e1) || !i2c_read_reg(0xE2U, &e2) || !i2c_read_reg(0xE3U, &e3)) {
        return false;
    }
    g_cal.par_h1 = (uint16_t)(((uint16_t)e3 << BME680_HUM_REG_SHIFT_VAL) | (e2 & BME680_BIT_H1_DATA_MASK));
    g_cal.par_h2 = (uint16_t)(((uint16_t)e1 << BME680_HUM_REG_SHIFT_VAL) | (e2 >> BME680_HUM_REG_SHIFT_VAL));

    if (!i2c_read_reg(0xE4U, &e4) || !i2c_read_reg(0xE5U, &e5) || !i2c_read_reg(0xE6U, &e6) ||
        !i2c_read_reg(0xE7U, &e7) || !i2c_read_reg(0xE8U, &e8)) {
        return false;
    }
    g_cal.par_h3 = (int8_t)e3;
    g_cal.par_h4 = (int8_t)e4;
    g_cal.par_h5 = (int8_t)e5;
    g_cal.par_h6 = (uint8_t)e6;
    g_cal.par_h7 = (int8_t)e7;

    uint8_t e9 = 0, ea = 0, eb = 0, ec = 0, ed = 0, ee = 0;
    if (!i2c_read_reg(0xE9U, &e9) || !i2c_read_reg(0xEAU, &ea)) {
        return false;
    }
    uint8_t t1_le[2] = {e9, ea};
    g_cal.par_t1 = get_le16(t1_le);

    if (!i2c_read_reg(0xEBU, &eb) || !i2c_read_reg(0xECU, &ec)) {
        return false;
    }
    uint8_t gh2_le[2] = {eb, ec};
    g_cal.par_gh2 = (int16_t)get_le16(gh2_le);

    if (!i2c_read_reg(0xEDU, &ed) || !i2c_read_reg(0xEEU, &ee)) {
        return false;
    }
    g_cal.par_gh1 = (int8_t)ed;
    g_cal.par_gh3 = (int8_t)ee;

#if BME680_DEBUG_HUMIDITY
    printf("BME680 E1..E8 (single-byte reads): %02x %02x %02x %02x %02x %02x %02x %02x\n", e1, e2, e3, e4, e5, e6, e7,
           e8);
#endif

    g_cal.res_heat_val = (int8_t)b3[0];
    g_cal.res_heat_range = (uint8_t)((b3[2] & BME680_RHRANGE_MASK) >> 4);
    g_cal.range_sw_err = (int8_t)((b3[4] & BME680_RSERROR_MASK) >> 4);

    return true;
}

static int32_t bme680_calc_t_fine(uint32_t adc_temp) {
    int64_t var1 = ((int32_t)((int32_t)adc_temp >> 3) - ((int32_t)g_cal.par_t1 << 1)) * (int32_t)g_cal.par_t2;
    var1 >>= 11;
    int64_t var2 = (((((int32_t)adc_temp >> 4) - (int32_t)g_cal.par_t1) *
                     (((int32_t)adc_temp >> 4) - (int32_t)g_cal.par_t1)) >>
                    12);
    var2 = (var2 * ((int32_t)g_cal.par_t3 << 4)) >> 14;
    return (int32_t)(var1 + var2);
}

static int16_t bme680_compensate_temp(uint32_t adc_temp) {
    int32_t t_fine = bme680_calc_t_fine(adc_temp);
    return (int16_t)((t_fine * 5 + 128) / 256);
}

static uint32_t bme680_compensate_press(uint32_t adc_press, int32_t t_fine) {
    int32_t var1 = (t_fine >> 1) - 64000;
    int32_t var2 = ((((var1 >> 2) * (var1 >> 2)) >> 11) * (int32_t)g_cal.par_p6) >> 2;
    var2 = var2 + ((var1 * (int32_t)g_cal.par_p5) << 1);
    var2 = (var2 >> 2) + ((int32_t)g_cal.par_p4 << 16);
    var1 = (((((var1 >> 2) * (var1 >> 2)) >> 13) * ((int32_t)g_cal.par_p3 << 5)) >> 3) +
           (((int32_t)g_cal.par_p2 * var1) >> 1);
    var1 = var1 >> 18;
    var1 = ((32768 + var1) * (int32_t)g_cal.par_p1) >> 15;

    int32_t press_comp = 1048576 - (int32_t)adc_press;
    press_comp = (int32_t)(((int32_t)press_comp - (var2 >> 12)) * 3125);

    if (press_comp >= (int32_t)BME680_MAX_OVERFLOW_VAL) {
        press_comp = ((press_comp / var1) << 1);
    } else {
        press_comp = ((press_comp << 1) / var1);
    }

    var1 = ((int32_t)g_cal.par_p9 * (int32_t) ((((press_comp >> 3) * (press_comp >> 3)) >> 13))) >> 12;
    var2 = ((int32_t)(((int32_t)press_comp >> 2) * (int32_t)g_cal.par_p8)) >> 13;
    int32_t var3 = ((int32_t)(((int32_t)press_comp >> 8) * ((int32_t)press_comp >> 8) * ((int32_t)press_comp >> 8) *
                              (int32_t)g_cal.par_p10) >>
                    17);

    press_comp += (var1 + var2 + var3 + ((int32_t)g_cal.par_p7 << 7)) >> 4;

    return (uint32_t)press_comp;
}

/**
 * Humidity compensation (Bosch API order; see Linux bme680_compensate_humid).
 * Uses int64_t intermediates to avoid int32_t overflow in var1*var2 and var4*var5.
 */
static uint32_t bme680_compensate_humid(uint16_t adc_humid, int32_t t_fine) {
#if BME680_DEBUG_HUMIDITY
    printf("H_RAW: %u, T_FINE: %d\n", (unsigned)adc_humid, (int)t_fine);
    printf("par_h: h1=%u h2=%u h3=%d h4=%d h5=%d h6=%u h7=%d\n", (unsigned)g_cal.par_h1, (unsigned)g_cal.par_h2,
           (int)g_cal.par_h3, (int)g_cal.par_h4, (int)g_cal.par_h5, (unsigned)g_cal.par_h6, (int)g_cal.par_h7);
#endif

    int64_t temp_scaled = ((int64_t)t_fine * 5 + 128) >> 8;

    int64_t var1 = (int64_t)adc_humid - ((int64_t)g_cal.par_h1 * 16) -
                   (((temp_scaled * (int64_t)g_cal.par_h3) / 100) >> 1);

    int64_t inner = ((temp_scaled * (int64_t)g_cal.par_h4) / 100) +
                    (((temp_scaled * ((temp_scaled * (int64_t)g_cal.par_h5) / 100)) >> 6) / 100) + (1 << 14);

    int64_t var2 = (((int64_t)g_cal.par_h2 * inner) >> 10);

    int64_t var3 = var1 * var2;

    int64_t var4 = ((int64_t)g_cal.par_h6 << 7);
    var4 = (var4 + ((temp_scaled * (int64_t)g_cal.par_h7) / 100)) >> 4;

    int64_t var5 = ((var3 >> 14) * (var3 >> 14)) >> 10;
    int64_t var6 = (var4 * var5) >> 1;

    int64_t calc_hum = (((var3 + var6) >> 10) * 1000) >> 12;

    if (calc_hum < 0) {
        calc_hum = 0;
    }
    if (calc_hum > 100000) {
        calc_hum = 100000;
    }
    return (uint32_t)calc_hum;
}

static const uint32_t BME680_GAS_LOOKUP[16] = {
    2147483647u, 2147483647u, 2147483647u, 2147483647u, 2147483647u, 2126008810u, 2147483647u, 2130303777u,
    2147483647u, 2147483647u, 2143188679u, 2136746228u, 2147483647u, 2126008810u, 2147483647u, 2147483647u
};

static uint32_t bme680_compensate_gas(uint16_t gas_res_adc, uint8_t gas_range) {
    int64_t var1 =
        ((1340LL + (5LL * (int64_t)g_cal.range_sw_err)) * (int64_t)BME680_GAS_LOOKUP[gas_range & 0x0FU]) >> 16;
    int64_t var2 = (((int64_t)gas_res_adc << 15) - 16777216LL) + var1;
    int64_t var3 = (((125000LL << (15UL - (int32_t)gas_range)) * var1) >> 9);
    var3 += (var2 >> 1);
    if (var2 == 0) {
        return 0U;
    }
    return (uint32_t)(var3 / var2);
}

static uint8_t bme680_calc_heater_res(uint16_t temp_c) {
    if (temp_c > 400U) {
        temp_c = 400U;
    }

    int32_t var1 = (((int32_t)BME680_AMB_TEMP * (int32_t)g_cal.par_gh3) / 1000) * 256;
    int32_t var2 = ((int32_t)g_cal.par_gh1 + 784) *
                   (((((((int32_t)g_cal.par_gh2 + 154009) * (int32_t)temp_c * 5) / 100) + 3276800) / 10));
    int32_t var3 = var1 + (var2 / 2);
    int32_t var4 = (var3 / ((int32_t)g_cal.res_heat_range + 4));
    int32_t var5 = 131 * (int32_t)g_cal.res_heat_val + 65536;
    int32_t heatr_res_x100 = ((var4 / var5) - 250) * 34;
    int32_t heatr_res = (heatr_res_x100 + 50) / 100;
    if (heatr_res < 0) {
        return 0U;
    }
    if (heatr_res > 255) {
        return 255U;
    }
    return (uint8_t)heatr_res;
}

static uint8_t bme680_calc_heater_dur(uint16_t dur_ms) {
    uint8_t durval = 0;
    uint8_t factor = 0;
    uint16_t dur = dur_ms;

    if (dur >= 0xfc0U) {
        durval = 0xFFU;
    } else {
        while (dur > 0x3FU) {
            dur = (uint16_t)(dur / 4U);
            factor++;
        }
        durval = (uint8_t)(dur + (factor * 64U));
    }
    return durval;
}

static uint8_t bme680_calc_heater_preheat_current(uint8_t curr_ma) {
    if (curr_ma == 0U) {
        return 0U;
    }
    return (uint8_t)(8U * curr_ma - 1U);
}

static bool bme680_chip_config(uint8_t os_h, uint8_t os_t, uint8_t os_p) {
    uint8_t h = os_to_field(os_h) & 0x07U;
    if (!i2c_write_reg(BME680_REG_CTRL_HUMIDITY, h)) {
        return false;
    }
    /* IIR filter coefficient 1 (bits 4:2); matches Linux BME680_FILTER_COEFF_VAL usage. */
    if (!i2c_write_reg(BME680_REG_CONFIG, 0x04U)) {
        return false;
    }
    uint8_t t = (uint8_t)((os_to_field(os_t) << 5) & BME680_OSRS_TEMP_MASK);
    uint8_t p = (uint8_t)((os_to_field(os_p) << 2) & BME680_OSRS_PRESS_MASK);
    g_ctrl_meas_base = (uint8_t)(t | p | BME680_MODE_SLEEP);
    if (!i2c_write_reg(BME680_REG_CTRL_MEAS, g_ctrl_meas_base)) {
        return false;
    }
    return true;
}

static bool bme680_gas_config(uint16_t heater_temp_c, uint16_t heater_dur_ms, uint8_t preheat_curr_ma) {
    if (!i2c_write_reg(BME680_REG_CTRL_MEAS, (uint8_t)(g_ctrl_meas_base & (uint8_t)~BME680_MODE_MASK))) {
        return false;
    }
    sleep_ms(1);

    uint8_t heatr_res = bme680_calc_heater_res(heater_temp_c);
    if (!i2c_write_reg(BME680_REG_RES_HEAT_0, heatr_res)) {
        return false;
    }
    uint8_t heatr_dur = bme680_calc_heater_dur(heater_dur_ms);
    if (!i2c_write_reg(BME680_REG_GAS_WAIT_0, heatr_dur)) {
        return false;
    }
    uint8_t idac = bme680_calc_heater_preheat_current(preheat_curr_ma);
    if (!i2c_write_reg(BME680_REG_IDAC_HEAT_0, idac)) {
        return false;
    }
    uint8_t gas1 = (uint8_t)(0x10U | 0x00U);
    if (!i2c_write_reg(BME680_REG_CTRL_GAS_1, gas1)) {
        return false;
    }
    return true;
}

static bool bme680_set_mode(uint8_t mode) {
    uint8_t v = (uint8_t)((g_ctrl_meas_base & (uint8_t)~BME680_MODE_MASK) | (mode & BME680_MODE_MASK));
    return i2c_write_reg(BME680_REG_CTRL_MEAS, v);
}

static bool bme680_wait_eoc(void) {
    uint32_t wait_ms =
        (uint32_t)((2U + 2U + 2U) * 1963U) / 1000U + (477U * 4U + 477U * 5U + 1000U) / 1000U + 180U + 80U;
    sleep_ms(wait_ms);

    for (int i = 0; i < 80; i++) {
        uint8_t st = 0;
        if (!i2c_read_reg(BME680_REG_MEAS_STAT_0, &st)) {
            return false;
        }
        if ((st & BME680_MEAS_BIT) == 0U && (st & BME680_NEW_DATA_BIT) != 0U) {
            return true;
        }
        sleep_ms(10);
    }
    return false;
}

static bool bme680_sensor_init_at_addr(i2c_inst_t *i2c, uint8_t addr_7bit) {
    g_i2c = i2c;
    g_addr = addr_7bit;
    memset(&g_cal, 0, sizeof(g_cal));
    g_humidity_filter_initialized = false;
    g_last_humidity_milli = 0U;

    if (!i2c_write_reg(BME680_REG_SOFT_RESET, BME680_CMD_SOFTRESET)) {
        return false;
    }
    sleep_ms(10U);

    uint8_t chip_id = 0;
    if (!i2c_read_reg(BME680_REG_CHIP_ID, &chip_id) || chip_id != BME680_CHIP_ID_VAL) {
        return false;
    }

    if (!load_calibration()) {
        return false;
    }

    if (!bme680_chip_config(2U, 2U, 2U)) {
        return false;
    }
    if (!bme680_gas_config(320U, 150U, 0U)) {
        return false;
    }

    g_ctrl_meas_base = (uint8_t)(g_ctrl_meas_base & (uint8_t)~BME680_MODE_MASK);
    return true;
}

bool bme680_sensor_init(i2c_inst_t *i2c, uint8_t addr_7bit) {
    if (addr_7bit == 0U) {
        if (bme680_sensor_init_at_addr(i2c, 0x76U)) {
            return true;
        }
        return bme680_sensor_init_at_addr(i2c, 0x77U);
    }
    return bme680_sensor_init_at_addr(i2c, addr_7bit);
}

bool bme680_sensor_read(bme680_sensor_data_t *out) {
    if (out == NULL || g_i2c == NULL) {
        return false;
    }
    memset(out, 0, sizeof(*out));

    if (!bme680_set_mode(BME680_MODE_FORCED)) {
        return false;
    }
    if (!bme680_wait_eoc()) {
        return false;
    }

    uint8_t buf[15];
    if (!i2c_read_burst(BME680_REG_MEAS_STAT_0, buf, sizeof(buf))) {
        return false;
    }

    if ((buf[0] & BME680_GAS_MEAS_BIT) != 0U) {
        sleep_ms(80);
        if (!i2c_read_burst(BME680_REG_MEAS_STAT_0, buf, sizeof(buf))) {
            return false;
        }
    }

    uint32_t adc_temp = get_be24_adc(&buf[5]);
    uint32_t adc_press = get_be24_adc(&buf[2]);
    /* Humidity ADC is big-endian 16-bit (HUMIDITY_MSB=0x25, LSB=0x26). */
    uint16_t adc_humid = (uint16_t)(((uint16_t)buf[8] << 8) | (uint16_t)buf[9]);
    uint16_t gas_regs = (uint16_t)(((uint16_t)buf[13] << 8) | buf[14]);

    if (adc_temp == 0U) {
        return false;
    }
    if (adc_press == 0U) {
        return false;
    }
    if (adc_humid == BME680_MEAS_SKIPPED) {
        return false;
    }

    int32_t t_fine = bme680_calc_t_fine(adc_temp);
    int16_t t_centi = (int16_t)((t_fine * 5 + 128) / 256);
    out->temperature_c = (float)t_centi / 100.0f;

    uint32_t press_pa = bme680_compensate_press(adc_press, t_fine);
    out->pressure_hpa = (float)press_pa / 100.0f;

    uint32_t hum_milli = bme680_compensate_humid(adc_humid, t_fine);
    if (!g_humidity_filter_initialized) {
        g_last_humidity_milli = hum_milli;
        g_humidity_filter_initialized = true;
    } else {
        int32_t delta = (int32_t)hum_milli - (int32_t)g_last_humidity_milli;
        if (delta > (int32_t)BME680_HUM_MAX_STEP_MILLI) {
            hum_milli = g_last_humidity_milli + BME680_HUM_MAX_STEP_MILLI;
        } else if (delta < -(int32_t)BME680_HUM_MAX_STEP_MILLI) {
            hum_milli = g_last_humidity_milli - BME680_HUM_MAX_STEP_MILLI;
        }
        g_last_humidity_milli = hum_milli;
    }
    out->humidity_pct = (float)hum_milli / 1000.0f;

    uint16_t gas_adc = (uint16_t)((gas_regs & BME680_ADC_GAS_RES_MASK) >> 6);
    uint8_t gas_range = (uint8_t)(gas_regs & BME680_GAS_RANGE_MASK);

    if ((gas_regs & BME680_GAS_STAB_BIT) == 0U) {
        out->gas_resistance_ohm = 0.0f;
    } else {
        out->gas_resistance_ohm = (float)bme680_compensate_gas(gas_adc, gas_range);
    }

    out->valid = true;
    return true;
}
