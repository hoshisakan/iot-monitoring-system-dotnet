#ifndef WIFI_HANDLER_H
#define WIFI_HANDLER_H

#include <stdbool.h>
#include <stdint.h>

/**
 * @brief Wi-Fi connection runtime state.
 */
typedef struct {
    const char *ssid;
    const char *password;
    bool connected;
    bool attempting;
    int last_connect_rc;
    uint32_t last_attempt_ms;
    uint32_t backoff_ms;
    uint32_t next_retry_ms;
} wifi_handler_t;

/**
 * @brief Initialize Wi-Fi handler configuration.
 *
 * @param ctx Handler context.
 * @param ssid Wi-Fi SSID.
 * @param password Wi-Fi password.
 * @return true when parameters are valid.
 */
bool wifi_handler_init(wifi_handler_t *ctx, const char *ssid, const char *password);

/**
 * @brief Run Wi-Fi connection state machine with exponential backoff.
 *
 * @param ctx Handler context.
 * @param now_ms Current monotonic time in milliseconds.
 */
void wifi_handler_process(wifi_handler_t *ctx, uint32_t now_ms);

/**
 * @brief Query connection status.
 *
 * @param ctx Handler context.
 * @return true when station has an IP and link is active.
 */
bool wifi_handler_is_connected(const wifi_handler_t *ctx);

/**
 * @brief True while running a connect attempt.
 */
bool wifi_handler_is_attempting(const wifi_handler_t *ctx);

/**
 * @brief Return last connect return code (0 means success).
 */
int wifi_handler_last_connect_rc(const wifi_handler_t *ctx);

/**
 * @brief Return Wi-Fi RSSI in dBm for the STA interface, or a fallback when unavailable.
 */
int wifi_handler_get_rssi_dbm(void);

#endif
