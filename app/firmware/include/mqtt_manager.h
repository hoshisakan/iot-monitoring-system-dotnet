#ifndef MQTT_MANAGER_H
#define MQTT_MANAGER_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "lwip/apps/mqtt.h"
#include "lwip/altcp_tls.h"
#include "lwip/ip_addr.h"

typedef void (*mqtt_control_message_handler_t)(const char *topic, const char *payload, size_t payload_len, void *user_data);

/**
 * @brief MQTT over TLS manager runtime state.
 */
typedef struct {
    mqtt_client_t *client;
    struct altcp_tls_config *tls_config;
    struct mqtt_connect_client_info_t client_info;
    ip_addr_t broker_addr;
    uint16_t broker_port;
    const char *topic;
    const char *topic_control;
    mqtt_control_message_handler_t control_handler;
    void *control_handler_user_data;
    char incoming_topic[128];
    char incoming_payload[512];
    size_t incoming_payload_len;
    uint8_t incoming_active;
    bool connecting;
    bool publish_inflight;
    /** Set from lwIP publish callback; main loop disconnects and reconnects (broker down / half-open TCP). */
    bool pending_disconnect;
    bool tls_enabled;
    int last_connect_status;
    int last_connect_invoke_rc;
    int last_publish_result;
    uint32_t backoff_ms;
    uint32_t next_retry_ms;
    uint32_t connect_started_ms;
} mqtt_manager_t;

/**
 * @brief MQTT manager static configuration.
 */
typedef struct {
    const char *broker_ip;
    uint16_t broker_port;
    const char *topic;
    const char *topic_control;
    const char *client_id;
    const char *mqtt_user;
    const char *mqtt_pass;
    const unsigned char *ca_cert;
    size_t ca_cert_len;
} mqtt_manager_config_t;

typedef enum {
    MQTT_INIT_OK = 0,
    MQTT_INIT_ERR_INVALID_ARGS,
    MQTT_INIT_ERR_INVALID_PORT,
    MQTT_INIT_ERR_INVALID_BROKER_IP,
    MQTT_INIT_ERR_CLIENT_ALLOC,
    MQTT_INIT_ERR_TLS_CONFIG
} mqtt_manager_init_error_t;

/**
 * @brief Initialize MQTT manager and TLS settings.
 */
bool mqtt_manager_init(mqtt_manager_t *ctx, const mqtt_manager_config_t *cfg);
void mqtt_manager_set_control_handler(mqtt_manager_t *ctx, mqtt_control_message_handler_t handler, void *user_data);

/**
 * @brief Return last MQTT init error code.
 */
mqtt_manager_init_error_t mqtt_manager_get_last_init_error(void);

/**
 * @brief Return readable text for last MQTT init error.
 */
const char *mqtt_manager_get_last_init_error_text(void);

/**
 * @brief Execute connect/reconnect state machine with exponential backoff.
 */
void mqtt_manager_process(mqtt_manager_t *ctx, uint32_t now_ms);

/**
 * @brief Wi-Fi STA link lost: drop MQTT TCP session so the next process() can reconnect cleanly.
 */
void mqtt_manager_on_wifi_down(mqtt_manager_t *ctx, uint32_t now_ms);

/**
 * @brief Publish telemetry JSON payload.
 */
bool mqtt_manager_publish(mqtt_manager_t *ctx, const char *json_payload);
bool mqtt_manager_publish_topic(mqtt_manager_t *ctx, const char *topic, const char *json_payload);

/**
 * @brief True when connected and no publish is awaiting completion (QoS1 ACK).
 *        Use to serialize multiple publishes across loop iterations.
 */
bool mqtt_manager_publish_ready(const mqtt_manager_t *ctx);

/**
 * @brief Return true if MQTT session is connected.
 */
bool mqtt_manager_is_connected(const mqtt_manager_t *ctx);

/**
 * @brief Release TLS resources.
 */
void mqtt_manager_deinit(mqtt_manager_t *ctx);

#endif
