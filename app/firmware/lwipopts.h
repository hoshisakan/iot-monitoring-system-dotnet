#ifndef _LWIPOPTS_H
#define _LWIPOPTS_H

#include <stdint.h>

#define NO_SYS 1
#define MEM_ALIGNMENT 4
#define MEM_SIZE (64 * 1024)

#define LWIP_ARP 1
#define LWIP_ETHERNET 1
#define LWIP_ICMP 1
#define LWIP_RAW 1
#define LWIP_UDP 1
#define LWIP_TCP 1
#define LWIP_DHCP 1
#define LWIP_DNS 1

#define LWIP_SOCKET 0
#define LWIP_NETCONN 0

#define LWIP_ALTCP 1
#define LWIP_ALTCP_TLS 1
#define LWIP_ALTCP_TLS_MBEDTLS 1
#define LWIP_MQTT 1
#define LWIP_SNTP 1
extern void ntp_client_handle_callback(uint32_t sec);
#define SNTP_SET_SYSTEM_TIME(sec) ntp_client_handle_callback(sec)
#define SNTP_SERVER_DNS 1

#define MEMP_NUM_ALTCP_PCB 8
#define MEMP_NUM_TCP_PCB 8
#define MEMP_NUM_SYS_TIMEOUT 10

#define TCP_MSS 1460
#define TCP_WND (8 * TCP_MSS)
#define TCP_SND_BUF (8 * TCP_MSS)
#define TCP_SND_QUEUELEN 64
#define MEMP_NUM_TCP_SEG 64

#define MQTT_OUTPUT_RINGBUF_SIZE 4096
#define MQTT_VAR_HEADER_BUFFER_LEN 256

#define LWIP_TLS_MAX_CONTENT_LEN 4096

#endif
