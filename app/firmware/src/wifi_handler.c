#include "wifi_handler.h"

#include <stdio.h>
#include <string.h>

#include "cyw43.h"
#include "cyw43_ll.h"
#include "pico/cyw43_arch.h"

static const uint32_t WIFI_BACKOFF_MIN_MS = 1000;
static const uint32_t WIFI_BACKOFF_MAX_MS = 60000;

static uint32_t clamp_backoff(uint32_t value) {
    if (value < WIFI_BACKOFF_MIN_MS) {
        return WIFI_BACKOFF_MIN_MS;
    }
    if (value > WIFI_BACKOFF_MAX_MS) {
        return WIFI_BACKOFF_MAX_MS;
    }
    return value;
}

bool wifi_handler_init(wifi_handler_t *ctx, const char *ssid, const char *password) {
    if (ctx == NULL || ssid == NULL || password == NULL) {
        return false;
    }
    memset(ctx, 0, sizeof(*ctx));
    ctx->ssid = ssid;
    ctx->password = password;
    ctx->backoff_ms = WIFI_BACKOFF_MIN_MS;
    return true;
}

void wifi_handler_process(wifi_handler_t *ctx, uint32_t now_ms) {
    if (ctx == NULL) {
        return;
    }

    int link_status = cyw43_tcpip_link_status(&cyw43_state, CYW43_ITF_STA);
    ctx->connected = (link_status >= CYW43_LINK_UP);
    if (ctx->connected) {
        ctx->backoff_ms = WIFI_BACKOFF_MIN_MS;
        return;
    }

    if ((int32_t)(now_ms - ctx->next_retry_ms) < 0) {
        return;
    }

    printf("[WiFi] Connecting SSID=%s\n", ctx->ssid);
    fflush(stdout);
    int rc = cyw43_arch_wifi_connect_timeout_ms(
        ctx->ssid,
        ctx->password,
        CYW43_AUTH_WPA2_AES_PSK,
        10000
    );
    if (rc == 0) {
        printf("[WiFi] Connected\n");
        fflush(stdout);
        ctx->connected = true;
        ctx->backoff_ms = WIFI_BACKOFF_MIN_MS;
        return;
    }

    printf("[WiFi] Connect failed rc=%d, retry in %lu ms\n", rc, (unsigned long)ctx->backoff_ms);
    fflush(stdout);
    ctx->next_retry_ms = now_ms + ctx->backoff_ms;
    ctx->backoff_ms = clamp_backoff(ctx->backoff_ms * 2);
}

bool wifi_handler_is_connected(const wifi_handler_t *ctx) {
    return ctx != NULL && ctx->connected;
}

int wifi_handler_get_rssi_dbm(void) {
    int32_t rssi = 0;
    if (cyw43_ioctl(&cyw43_state, CYW43_IOCTL_GET_RSSI, sizeof(rssi), (uint8_t *)&rssi,
                    (uint32_t)CYW43_ITF_STA) != 0) {
        return -60;
    }
    return (int)rssi;
}
