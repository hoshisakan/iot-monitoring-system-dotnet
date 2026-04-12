#include "sh1106_oled.h"

#include <stdio.h>
#include <string.h>

#include "pico/stdlib.h"

#define SH1106_COL_OFFSET 2U
#ifndef SH1106_SEG_REMAP_CMD
#define SH1106_SEG_REMAP_CMD 0xA0U
#endif
#ifndef SH1106_COM_SCAN_CMD
#define SH1106_COM_SCAN_CMD 0xC0U
#endif
#ifndef SH1106_GLYPH_REV8
#define SH1106_GLYPH_REV8 1
#endif

static i2c_inst_t *g_i2c = NULL;
static uint8_t g_addr = 0x3CU; /* Common 1.3" SH1106 module address */
static uint8_t g_fb[8][128];

static bool sh1106_cmd(uint8_t cmd) {
    uint8_t b[2] = {0x00U, cmd};
    return i2c_write_blocking(g_i2c, g_addr, b, 2, false) == 2;
}

static bool sh1106_data(const uint8_t *data, size_t len) {
    static uint8_t tx[17];
    tx[0] = 0x40U;
    for (size_t off = 0; off < len; off += 16U) {
        size_t n = len - off;
        if (n > 16U) {
            n = 16U;
        }
        memcpy(&tx[1], &data[off], n);
        if (i2c_write_blocking(g_i2c, g_addr, tx, n + 1U, false) != (int)(n + 1U)) {
            return false;
        }
    }
    return true;
}

static bool sh1106_set_page_col(uint8_t page, uint8_t logical_col) {
    uint8_t c = (uint8_t)(logical_col + SH1106_COL_OFFSET);
    return sh1106_cmd((uint8_t)(0xB0U | (page & 0x07U))) && sh1106_cmd((uint8_t)(0x10U | ((c >> 4) & 0x0FU))) &&
           sh1106_cmd((uint8_t)(c & 0x0FU));
}

static uint8_t rev8(uint8_t b) {
    b = (uint8_t)(((b & 0xF0U) >> 4) | ((b & 0x0FU) << 4));
    b = (uint8_t)(((b & 0xCCU) >> 2) | ((b & 0x33U) << 2));
    b = (uint8_t)(((b & 0xAAU) >> 1) | ((b & 0x55U) << 1));
    return b;
}

static const uint8_t *glyph5x7(char c) {
    static const uint8_t blank[5] = {0, 0, 0, 0, 0};
    static const uint8_t space[5] = {0, 0, 0, 0, 0};
    static const uint8_t dot[5] = {0x00, 0x60, 0x60, 0x00, 0x00};
    static const uint8_t colon[5] = {0x00, 0x36, 0x36, 0x00, 0x00};
    static const uint8_t minus[5] = {0x08, 0x08, 0x08, 0x08, 0x08};
    static const uint8_t percent[5] = {0x62, 0x64, 0x08, 0x13, 0x23};

    static const uint8_t n0[5] = {0x3E, 0x45, 0x49, 0x51, 0x3E};
    static const uint8_t n1[5] = {0x00, 0x21, 0x7F, 0x01, 0x00};
    static const uint8_t n2[5] = {0x23, 0x45, 0x49, 0x51, 0x21};
    static const uint8_t n3[5] = {0x42, 0x41, 0x51, 0x69, 0x46};
    static const uint8_t n4[5] = {0x0C, 0x14, 0x24, 0x7F, 0x04};
    static const uint8_t n5[5] = {0x72, 0x51, 0x51, 0x51, 0x4E};
    static const uint8_t n6[5] = {0x1E, 0x29, 0x49, 0x49, 0x06};
    static const uint8_t n7[5] = {0x40, 0x47, 0x48, 0x50, 0x60};
    static const uint8_t n8[5] = {0x36, 0x49, 0x49, 0x49, 0x36};
    static const uint8_t n9[5] = {0x30, 0x49, 0x49, 0x4A, 0x3C};

    static const uint8_t A[5] = {0x1F, 0x24, 0x44, 0x24, 0x1F};
    static const uint8_t C[5] = {0x3E, 0x41, 0x41, 0x41, 0x22};
    static const uint8_t D[5] = {0x7F, 0x41, 0x41, 0x22, 0x1C};
    static const uint8_t E[5] = {0x7F, 0x49, 0x49, 0x49, 0x41};
    static const uint8_t G[5] = {0x3E, 0x41, 0x49, 0x49, 0x2E};
    static const uint8_t H[5] = {0x7F, 0x08, 0x08, 0x08, 0x7F};
    static const uint8_t I[5] = {0x00, 0x41, 0x7F, 0x41, 0x00};
    static const uint8_t L[5] = {0x7F, 0x01, 0x01, 0x01, 0x01};
    static const uint8_t O[5] = {0x3E, 0x41, 0x41, 0x41, 0x3E};
    static const uint8_t P[5] = {0x7F, 0x48, 0x48, 0x48, 0x30};
    static const uint8_t R[5] = {0x7F, 0x48, 0x4C, 0x4A, 0x31};
    static const uint8_t S[5] = {0x31, 0x49, 0x49, 0x49, 0x46};
    static const uint8_t T[5] = {0x40, 0x40, 0x7F, 0x40, 0x40};
    static const uint8_t W[5] = {0x7F, 0x02, 0x04, 0x02, 0x7F};

    static const uint8_t d[5] = {0x0E, 0x11, 0x11, 0x12, 0x7F};
    static const uint8_t e[5] = {0x0E, 0x15, 0x15, 0x15, 0x0C};
    static const uint8_t l[5] = {0x00, 0x41, 0x7F, 0x01, 0x00};
    static const uint8_t o[5] = {0x0E, 0x11, 0x11, 0x11, 0x0E};
    static const uint8_t r[5] = {0x1F, 0x08, 0x10, 0x10, 0x08};

    switch (c) {
        case ' ':
            return space;
        case '.':
            return dot;
        case ':':
            return colon;
        case '-':
            return minus;
        case '%':
            return percent;
        case '0':
            return n0;
        case '1':
            return n1;
        case '2':
            return n2;
        case '3':
            return n3;
        case '4':
            return n4;
        case '5':
            return n5;
        case '6':
            return n6;
        case '7':
            return n7;
        case '8':
            return n8;
        case '9':
            return n9;
        case 'A':
            return A;
        case 'C':
            return C;
        case 'D':
            return D;
        case 'E':
            return E;
        case 'G':
            return G;
        case 'H':
            return H;
        case 'I':
            return I;
        case 'L':
            return L;
        case 'O':
            return O;
        case 'P':
            return P;
        case 'R':
            return R;
        case 'S':
            return S;
        case 'T':
            return T;
        case 'W':
            return W;
        case 'd':
            return d;
        case 'e':
            return e;
        case 'l':
            return l;
        case 'o':
            return o;
        case 'r':
            return r;
        default:
            return blank;
    }
}

static void draw_char(uint8_t page, uint8_t col, char c) {
    if (page >= 8U || col >= 128U) {
        return;
    }
    const uint8_t *g = glyph5x7(c);
    uint8_t x = col;
    for (int i = 0; i < 5 && x < 128U; i++) {
#if SH1106_GLYPH_REV8
        g_fb[page][x++] = rev8(g[i]);
#else
        g_fb[page][x++] = g[i];
#endif
    }
    if (x < 128U) {
        g_fb[page][x] = 0x00;
    }
}

bool sh1106_oled_init(i2c_inst_t *i2c, uint8_t addr_7bit) {
    if (i2c == NULL) {
        return false;
    }
    g_i2c = i2c;
    g_addr = (addr_7bit == 0U) ? 0x3CU : addr_7bit; /* Default SH1106 address: 0x3C */
    sleep_ms(50);

    if (!sh1106_cmd(0xAEU) || !sh1106_cmd(0xD5U) || !sh1106_cmd(0x80U) || !sh1106_cmd(0xA8U) ||
        !sh1106_cmd(0x3FU) || !sh1106_cmd(0xD3U) || !sh1106_cmd(0x00U) || !sh1106_cmd(0x40U) ||
        !sh1106_cmd(0xADU) || !sh1106_cmd(0x8BU) || !sh1106_cmd(SH1106_SEG_REMAP_CMD) ||
        !sh1106_cmd(SH1106_COM_SCAN_CMD) ||
        !sh1106_cmd(0xDAU) || !sh1106_cmd(0x12U) || !sh1106_cmd(0x81U) || !sh1106_cmd(0x7FU) ||
        !sh1106_cmd(0xD9U) || !sh1106_cmd(0x22U) || !sh1106_cmd(0xDBU) || !sh1106_cmd(0x35U) ||
        !sh1106_cmd(0xA4U) || !sh1106_cmd(0xA6U) || !sh1106_cmd(0xAFU)) {
        return false;
    }

    sh1106_oled_clear();
    sh1106_oled_flush();
    return true;
}

void sh1106_oled_clear(void) {
    memset(g_fb, 0, sizeof(g_fb));
}

void sh1106_oled_draw_string(uint8_t page, uint8_t col, const char *text) {
    if (text == NULL || page >= 8U || col >= 128U) {
        return;
    }
    uint8_t x = col;
    for (size_t i = 0; text[i] != '\0' && x < 128U; i++) {
        draw_char(page, x, text[i]);
        x = (uint8_t)(x + 6U);
    }
}

void sh1106_oled_flush(void) {
    for (uint8_t page = 0; page < 8U; page++) {
        if (!sh1106_set_page_col(page, 0U)) {
            return;
        }
        if (!sh1106_data(g_fb[page], 128U)) {
            return;
        }
    }
}

void sh1106_oled_show_env_readings(float temp_c, float humidity_pct, float pressure_hpa, float lux, float gas_ohm,
                                   int rssi_dbm) {
    char line[32];
    sh1106_oled_clear();

    snprintf(line, sizeof(line), "T:%.1fC H:%.0f%%", (double)temp_c, (double)humidity_pct);
    sh1106_oled_draw_string(0U, 0U, line);
    snprintf(line, sizeof(line), "P:%.0fhPa", (double)pressure_hpa);
    sh1106_oled_draw_string(1U, 0U, line);
    snprintf(line, sizeof(line), "L:%.0f G:%.0f", (double)lux, (double)gas_ohm);
    sh1106_oled_draw_string(2U, 0U, line);
    snprintf(line, sizeof(line), "RSSI:%ddBm", rssi_dbm);
    sh1106_oled_draw_string(3U, 0U, line);

    sh1106_oled_flush();
}
