#include "mqtt_manager.h"

#include <stdio.h>
#include <string.h>

#include "mbedtls/x509_crt.h"
#include "pico/cyw43_arch.h"
#include "pico/time.h"

static const uint32_t MQTT_BACKOFF_MIN_MS = 1000;
static const uint32_t MQTT_BACKOFF_MAX_MS = 60000;
static const uint32_t MQTT_CONNECT_TIMEOUT_MS = 15000;
static mqtt_manager_init_error_t g_last_init_error = MQTT_INIT_OK;

static void on_mqtt_incoming_data(void *arg, const u8_t *data, u16_t len, u8_t flags) {
    mqtt_manager_t *ctx = (mqtt_manager_t *)arg;
    if (ctx == NULL || data == NULL || len == 0) {
        return;
    }
    if (!ctx->incoming_active) {
        return;
    }
    if (ctx->incoming_payload_len + len >= sizeof(ctx->incoming_payload)) {
        ctx->incoming_active = 0;
        ctx->incoming_payload_len = 0;
        return;
    }
    memcpy(ctx->incoming_payload + ctx->incoming_payload_len, data, len);
    ctx->incoming_payload_len += len;
    if ((flags & MQTT_DATA_FLAG_LAST) != 0) {
        ctx->incoming_payload[ctx->incoming_payload_len] = '\0';
        if (ctx->control_handler != NULL) {
            ctx->control_handler(ctx->incoming_topic, ctx->incoming_payload, ctx->incoming_payload_len,
                                 ctx->control_handler_user_data);
        }
        ctx->incoming_active = 0;
        ctx->incoming_payload_len = 0;
    }
}

static void on_mqtt_incoming_publish(void *arg, const char *topic, u32_t tot_len) {
    mqtt_manager_t *ctx = (mqtt_manager_t *)arg;
    if (ctx == NULL || topic == NULL) {
        return;
    }
    if (ctx->topic_control == NULL || strcmp(topic, ctx->topic_control) != 0) {
        ctx->incoming_active = 0;
        ctx->incoming_payload_len = 0;
        return;
    }
    size_t tlen = strlen(topic);
    if (tlen >= sizeof(ctx->incoming_topic)) {
        ctx->incoming_active = 0;
        ctx->incoming_payload_len = 0;
        return;
    }
    if (tot_len + 1 > sizeof(ctx->incoming_payload)) {
        printf("[MQTT] incoming control payload too large: %lu\n", (unsigned long)tot_len);
        ctx->incoming_active = 0;
        ctx->incoming_payload_len = 0;
        return;
    }
    strncpy(ctx->incoming_topic, topic, sizeof(ctx->incoming_topic) - 1);
    ctx->incoming_topic[sizeof(ctx->incoming_topic) - 1] = '\0';
    ctx->incoming_active = 1;
    ctx->incoming_payload_len = 0;
}

static void on_mqtt_subscribe(void *arg, err_t result) {
    mqtt_manager_t *ctx = (mqtt_manager_t *)arg;
    if (ctx == NULL) {
        return;
    }
    if (result == ERR_OK) {
        printf("[MQTT] subscribed control topic: %s\n", ctx->topic_control ? ctx->topic_control : "(null)");
    } else {
        printf("[MQTT] subscribe control topic failed: %d\n", result);
    }
}

static uint32_t clamp_backoff(uint32_t value) {
    if (value < MQTT_BACKOFF_MIN_MS) {
        return MQTT_BACKOFF_MIN_MS;
    }
    if (value > MQTT_BACKOFF_MAX_MS) {
        return MQTT_BACKOFF_MAX_MS;
    }
    return value;
}

static void schedule_retry(mqtt_manager_t *ctx, uint32_t now_ms) {
    ctx->next_retry_ms = now_ms + ctx->backoff_ms;
    ctx->backoff_ms = clamp_backoff(ctx->backoff_ms * 2);
}

static void on_mqtt_pub(void *arg, err_t result) {
    mqtt_manager_t *ctx = (mqtt_manager_t *)arg;
    ctx->publish_inflight = false;
    if (result != ERR_OK) {
        printf("[MQTT] publish failed: %d (will disconnect and retry)\n", result);
        ctx->pending_disconnect = true;
    }
}

static void on_mqtt_connection(mqtt_client_t *client, void *arg, mqtt_connection_status_t status) {
    (void)client;
    mqtt_manager_t *ctx = (mqtt_manager_t *)arg;
    ctx->connecting = false;
    if (status == MQTT_CONNECT_ACCEPTED) {
        ctx->backoff_ms = MQTT_BACKOFF_MIN_MS;
        printf("[MQTT] connected (%s)\n", ctx->tls_enabled ? "TLS" : "PLAINTEXT");
        if (ctx->topic_control != NULL && ctx->topic_control[0] != '\0') {
            mqtt_set_inpub_callback(ctx->client, on_mqtt_incoming_publish, on_mqtt_incoming_data, ctx);
            err_t sub_rc = mqtt_sub_unsub(ctx->client, ctx->topic_control, 1, on_mqtt_subscribe, ctx, 1);
            if (sub_rc != ERR_OK) {
                printf("[MQTT] subscribe invoke failed: %d\n", sub_rc);
            }
        }
        return;
    }
    printf("[MQTT] connect failed status=%d\n", status);
    schedule_retry(ctx, to_ms_since_boot(get_absolute_time()));
}

bool mqtt_manager_init(mqtt_manager_t *ctx, const mqtt_manager_config_t *cfg) {
    int parse_ret = 0;
    g_last_init_error = MQTT_INIT_OK;
    printf("[MQTT:init] ctx=%p cfg=%p\n", (void *)ctx, (void *)cfg);
    if (cfg != NULL) {
        printf("[MQTT:init] broker_ip=%p broker_port=%u topic=%p client_id=%p user=%p pass=%p ca_cert=%p ca_cert_len=%u\n",
               (void *)cfg->broker_ip,
               (unsigned)cfg->broker_port,
               (void *)cfg->topic,
               (void *)cfg->client_id,
               (void *)cfg->mqtt_user,
               (void *)cfg->mqtt_pass,
               (const void *)cfg->ca_cert,
               (unsigned)cfg->ca_cert_len);
    }

    if (ctx == NULL || cfg == NULL || cfg->broker_ip == NULL || cfg->topic == NULL ||
        cfg->client_id == NULL) {
        g_last_init_error = MQTT_INIT_ERR_INVALID_ARGS;
        printf("[MQTT:init] invalid argument detected\n");
        return false;
    }
    if (cfg->broker_port == 0) {
        g_last_init_error = MQTT_INIT_ERR_INVALID_PORT;
        printf("[MQTT:init] invalid broker_port=0\n");
        return false;
    }

    memset(ctx, 0, sizeof(*ctx));
    ctx->backoff_ms = MQTT_BACKOFF_MIN_MS;
    ctx->broker_port = cfg->broker_port;
    ctx->topic = cfg->topic;
    ctx->topic_control = cfg->topic_control;
    ctx->tls_enabled = (cfg->broker_port != 1883);

    if (!ipaddr_aton(cfg->broker_ip, &ctx->broker_addr)) {
        g_last_init_error = MQTT_INIT_ERR_INVALID_BROKER_IP;
        printf("[MQTT:init] ipaddr_aton failed for broker_ip='%s'\n", cfg->broker_ip);
        return false;
    }

    if (cfg->broker_port == 1883) {
        printf("[MQTT:init] using plain MQTT (1883)\n");
        ctx->tls_config = NULL;
    } else {
        if (cfg->ca_cert == NULL || cfg->ca_cert_len == 0) {
            g_last_init_error = MQTT_INIT_ERR_INVALID_ARGS;
            printf("[MQTT:init] TLS mode requires ca_cert and ca_cert_len\n");
            return false;
        }

        /* Pre-parse CA cert to expose exact mbedTLS parser errors. */
        {
            mbedtls_x509_crt probe;
            mbedtls_x509_crt_init(&probe);
            parse_ret = mbedtls_x509_crt_parse(&probe, cfg->ca_cert, cfg->ca_cert_len);
            if (parse_ret != 0 && cfg->ca_cert_len > 1) {
                parse_ret = mbedtls_x509_crt_parse(&probe, cfg->ca_cert, cfg->ca_cert_len - 1);
            }
            if (parse_ret != 0 && cfg->ca_cert != NULL &&
                strncmp((const char *)cfg->ca_cert, "-----BEGIN CERTIFICATE-----", 27) == 0) {
                size_t pem_len = strlen((const char *)cfg->ca_cert);
                parse_ret = mbedtls_x509_crt_parse(&probe, cfg->ca_cert, pem_len + 1);
                if (parse_ret != 0 && pem_len > 0) {
                    parse_ret = mbedtls_x509_crt_parse(&probe, cfg->ca_cert, pem_len);
                }
            }
            if (parse_ret != 0) {
                printf("[MQTT:init] CA parse probe failed ret=%d hex=0x%x\n", parse_ret, (unsigned)(-parse_ret));
                mbedtls_x509_crt_free(&probe);
                fflush(stdout);
                g_last_init_error = MQTT_INIT_ERR_TLS_CONFIG;
                return false;
            } else {
                printf("[MQTT:init] CA parse probe success\n");
            }
            mbedtls_x509_crt_free(&probe);
        }

        ctx->tls_config = altcp_tls_create_config_client(cfg->ca_cert, (int)cfg->ca_cert_len);
        if (ctx->tls_config == NULL && cfg->ca_cert_len > 1) {
            /* Fallback #1: some stacks expect PEM length without trailing NUL */
            printf("[MQTT:init] TLS config retry with len-1 (%u)\n", (unsigned)(cfg->ca_cert_len - 1));
            ctx->tls_config = altcp_tls_create_config_client(cfg->ca_cert, (int)(cfg->ca_cert_len - 1));
        }
        if (ctx->tls_config == NULL && cfg->ca_cert != NULL &&
            strncmp((const char *)cfg->ca_cert, "-----BEGIN CERTIFICATE-----", 27) == 0) {
            /* Fallback #2: use runtime PEM string length */
            size_t pem_len = strlen((const char *)cfg->ca_cert);
            printf("[MQTT:init] TLS config retry with pem strlen+1 (%u)\n", (unsigned)(pem_len + 1));
            ctx->tls_config = altcp_tls_create_config_client(cfg->ca_cert, (int)(pem_len + 1));
            if (ctx->tls_config == NULL && pem_len > 0) {
                printf("[MQTT:init] TLS config retry with pem strlen (%u)\n", (unsigned)pem_len);
                ctx->tls_config = altcp_tls_create_config_client(cfg->ca_cert, (int)pem_len);
            }
        }
        if (ctx->tls_config == NULL) {
            g_last_init_error = MQTT_INIT_ERR_TLS_CONFIG;
            printf("[MQTT:init] altcp_tls_create_config_client failed (ca_cert_len=%u)\n",
                   (unsigned)cfg->ca_cert_len);
            printf("[MQTT:init] hint: check mbedtls_heap size and CA certificate format\n");
            fflush(stdout);
            return false;
        }
    }

    ctx->client = mqtt_client_new();
    if (ctx->client == NULL) {
        g_last_init_error = MQTT_INIT_ERR_CLIENT_ALLOC;
        printf("[MQTT:init] mqtt_client_new failed (likely memory pressure)\n");
        return false;
    }

    ctx->client_info.client_id = cfg->client_id;
    ctx->client_info.client_user = cfg->mqtt_user;
    ctx->client_info.client_pass = cfg->mqtt_pass;
    ctx->client_info.keep_alive = 60;
    ctx->client_info.tls_config = ctx->tls_config;
    ctx->next_retry_ms = to_ms_since_boot(get_absolute_time());
    printf("[MQTT:init] success, broker=%s:%u topic=%s\n",
           cfg->broker_ip, (unsigned)cfg->broker_port, cfg->topic);
    return true;
}

void mqtt_manager_set_control_handler(mqtt_manager_t *ctx, mqtt_control_message_handler_t handler, void *user_data) {
    if (ctx == NULL) {
        return;
    }
    ctx->control_handler = handler;
    ctx->control_handler_user_data = user_data;
}

mqtt_manager_init_error_t mqtt_manager_get_last_init_error(void) {
    return g_last_init_error;
}

const char *mqtt_manager_get_last_init_error_text(void) {
    switch (g_last_init_error) {
        case MQTT_INIT_OK:
            return "OK";
        case MQTT_INIT_ERR_INVALID_ARGS:
            return "INVALID_ARGS";
        case MQTT_INIT_ERR_INVALID_PORT:
            return "INVALID_PORT";
        case MQTT_INIT_ERR_INVALID_BROKER_IP:
            return "INVALID_BROKER_IP";
        case MQTT_INIT_ERR_CLIENT_ALLOC:
            return "CLIENT_ALLOC_FAILED";
        case MQTT_INIT_ERR_TLS_CONFIG:
            return "TLS_CONFIG_FAILED";
        default:
            return "UNKNOWN";
    }
}

static void mqtt_force_disconnect(mqtt_manager_t *ctx, uint32_t now_ms) {
    if (ctx == NULL || ctx->client == NULL) {
        return;
    }
    cyw43_arch_lwip_begin();
    mqtt_disconnect(ctx->client);
    cyw43_arch_lwip_end();
    ctx->connecting = false;
    ctx->publish_inflight = false;
    ctx->backoff_ms = MQTT_BACKOFF_MIN_MS;
    ctx->next_retry_ms = now_ms;
}

void mqtt_manager_on_wifi_down(mqtt_manager_t *ctx, uint32_t now_ms) {
    if (ctx == NULL || ctx->client == NULL) {
        return;
    }
    ctx->pending_disconnect = false;
    mqtt_force_disconnect(ctx, now_ms);
}

void mqtt_manager_process(mqtt_manager_t *ctx, uint32_t now_ms) {
    if (ctx == NULL || ctx->client == NULL) {
        return;
    }

    if (ctx->pending_disconnect) {
        ctx->pending_disconnect = false;
        printf("[MQTT] disconnecting after publish failure / stale session\n");
        mqtt_force_disconnect(ctx, now_ms);
    }

    if (ctx->connecting) {
        if ((int32_t)(now_ms - ctx->connect_started_ms) > (int32_t)MQTT_CONNECT_TIMEOUT_MS) {
            ctx->connecting = false;
            printf("[MQTT] connect timeout, scheduling retry\n");
            schedule_retry(ctx, now_ms);
        }
        return;
    }
    bool connected = false;
    cyw43_arch_lwip_begin();
    connected = mqtt_client_is_connected(ctx->client);
    cyw43_arch_lwip_end();
    if (connected) {
        return;
    }
    if ((int32_t)(now_ms - ctx->next_retry_ms) < 0) {
        return;
    }

    ctx->connecting = true;
    ctx->connect_started_ms = now_ms;
    cyw43_arch_lwip_begin();
    err_t rc = mqtt_client_connect(
        ctx->client,
        &ctx->broker_addr,
        ctx->broker_port,
        on_mqtt_connection,
        ctx,
        &ctx->client_info
    );
    cyw43_arch_lwip_end();
    if (rc != ERR_OK) {
        ctx->connecting = false;
        printf("[MQTT] connect invoke failed: %d\n", rc);
        schedule_retry(ctx, now_ms);
    }
}

bool mqtt_manager_publish_ready(const mqtt_manager_t *ctx) {
    if (ctx == NULL || ctx->client == NULL || ctx->publish_inflight) {
        return false;
    }
    bool connected = false;
    cyw43_arch_lwip_begin();
    connected = mqtt_client_is_connected(ctx->client);
    cyw43_arch_lwip_end();
    return connected;
}

bool mqtt_manager_publish_topic(mqtt_manager_t *ctx, const char *topic, const char *json_payload) {
    if (ctx == NULL || topic == NULL || json_payload == NULL || ctx->publish_inflight) {
        return false;
    }
    bool connected = false;
    cyw43_arch_lwip_begin();
    connected = mqtt_client_is_connected(ctx->client);
    cyw43_arch_lwip_end();
    if (!connected) {
        return false;
    }

    size_t len = strlen(json_payload);
    if (len == 0 || len > UINT16_MAX) {
        return false;
    }

    ctx->publish_inflight = true;
    cyw43_arch_lwip_begin();
    err_t rc = mqtt_publish(
        ctx->client,
        topic,
        json_payload,
        (uint16_t)len,
        1,
        0,
        on_mqtt_pub,
        ctx
    );
    cyw43_arch_lwip_end();
    if (rc != ERR_OK) {
        ctx->publish_inflight = false;
        return false;
    }
    return true;
}

bool mqtt_manager_publish(mqtt_manager_t *ctx, const char *json_payload) {
    if (ctx == NULL) {
        return false;
    }
    return mqtt_manager_publish_topic(ctx, ctx->topic, json_payload);
}

bool mqtt_manager_is_connected(const mqtt_manager_t *ctx) {
    if (ctx == NULL || ctx->client == NULL) {
        return false;
    }
    bool connected = false;
    cyw43_arch_lwip_begin();
    connected = mqtt_client_is_connected(ctx->client);
    cyw43_arch_lwip_end();
    return connected;
}

void mqtt_manager_deinit(mqtt_manager_t *ctx) {
    if (ctx == NULL) {
        return;
    }
    if (ctx->tls_config != NULL) {
        altcp_tls_free_config(ctx->tls_config);
    }
    memset(ctx, 0, sizeof(*ctx));
}
