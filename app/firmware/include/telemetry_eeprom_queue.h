#ifndef TELEMETRY_EEPROM_QUEUE_H
#define TELEMETRY_EEPROM_QUEUE_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "hardware/i2c.h"

#define TELEM_EEPROM_MAX_PAYLOAD 512U
/** Max queued telemetry records (FIFO); full queue drops oldest on push. Override in secrets.h / firmware_config_defaults.h */
#ifndef TELEM_EEPROM_NUM_SLOTS
#define TELEM_EEPROM_NUM_SLOTS 24U
#endif

/**
 * Slot-based FIFO in AT24C256 (persistent across reboots).
 * Returns false if EEPROM unavailable or payload too large.
 */
bool telemetry_eeprom_queue_init(i2c_inst_t *i2c, uint8_t eeprom_addr_7bit);

/** Append JSON telemetry (MQTT offline). Drops oldest if queue full. */
bool telemetry_eeprom_queue_push(const char *json, uint16_t json_len);

/** Pop oldest record into buffer; clears slot. */
bool telemetry_eeprom_queue_pop(char *out, size_t out_cap, uint16_t *out_len);

unsigned telemetry_eeprom_queue_count(void);

#endif
