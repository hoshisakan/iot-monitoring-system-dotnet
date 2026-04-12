#ifndef MQTT_TOPICS_H
#define MQTT_TOPICS_H

/*
 * MQTT topic strings for publish. Include after secrets.h.
 *
 * Override in secrets.h before including this header:
 *   #define MQTT_TOPIC_TELEMETRY "iiot/mysite/mydevice/telemetry"
 *   #define MQTT_TOPIC_STATUS    "iiot/mysite/mydevice/status"
 *
 * If not overridden, topics are built from MQTT_NAMESPACE_PREFIX, MQTT_SITE, MQTT_CLIENT_ID.
 */
#ifndef MQTT_TOPIC_TELEMETRY
#define MQTT_TOPIC_TELEMETRY MQTT_NAMESPACE_PREFIX "/" MQTT_SITE "/" MQTT_CLIENT_ID "/telemetry"
#endif
#ifndef MQTT_TOPIC_STATUS
#define MQTT_TOPIC_STATUS MQTT_NAMESPACE_PREFIX "/" MQTT_SITE "/" MQTT_CLIENT_ID "/status"
#endif
#ifndef MQTT_TOPIC_TELEMETRY_SYNC_BACK
#define MQTT_TOPIC_TELEMETRY_SYNC_BACK MQTT_NAMESPACE_PREFIX "/" MQTT_SITE "/" MQTT_CLIENT_ID "/telemetry/sync-back"
#endif
#ifndef MQTT_TOPIC_UI_EVENTS
#define MQTT_TOPIC_UI_EVENTS MQTT_NAMESPACE_PREFIX "/" MQTT_SITE "/" MQTT_CLIENT_ID "/ui-events"
#endif
#ifndef MQTT_TOPIC_CONTROL
#define MQTT_TOPIC_CONTROL MQTT_NAMESPACE_PREFIX "/" MQTT_SITE "/" MQTT_CLIENT_ID "/control"
#endif

#endif /* MQTT_TOPICS_H */
