#ifndef AT24C256_EEPROM_H
#define AT24C256_EEPROM_H

#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>

#include "hardware/i2c.h"

/** 256 Kbit = 32 KiB; typical page 64 bytes */
#define AT24C256_CAPACITY_BYTES 32768U
#define AT24C256_PAGE_SIZE 64U

/**
 * AT24C256 on I2C1 (same bus as DS3231); default 7-bit addr 0x50 (A2=A1=A0=GND).
 */
bool at24c256_eeprom_init(i2c_inst_t *i2c, uint8_t addr_7bit);

bool at24c256_eeprom_read(uint16_t mem_addr, uint8_t *buf, size_t len);
bool at24c256_eeprom_write(uint16_t mem_addr, const uint8_t *buf, size_t len);

/** Simple read/modify/write test at high address (does not touch queue region if queue uses low addresses). */
bool at24c256_eeprom_probe(void);

#endif
