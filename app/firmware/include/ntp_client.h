#ifndef NTP_CLIENT_H
#define NTP_CLIENT_H

#include <stdbool.h>

bool ntp_client_init(void);
bool ntp_client_dns_ready(void);
bool ntp_client_sync_to_rtc(void);

#endif
