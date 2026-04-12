#include "ntp_client.h"

#include <stdio.h>
#include <time.h>

#include "ds3231.h"
#include "lwip/apps/sntp.h"
#include "lwip/dns.h"
#include "lwip/ip_addr.h"
#include "pico/aon_timer.h"
#include "pico/stdlib.h"

#define NTP_TZ_OFFSET_SECONDS (8 * 3600)
#define NTP_SYNC_TIMEOUT_MS 20000
#define NTP_SYNC_POLL_MS 250

static bool g_ntp_initialized = false;
static bool g_sntp_updated = false;
static uint32_t g_sntp_epoch_utc = 0;

void ntp_client_handle_callback(uint32_t sec) {
    struct timespec ts = {.tv_sec = (time_t)sec, .tv_nsec = 0};
    aon_timer_start(&ts);
    g_sntp_epoch_utc = sec;
    g_sntp_updated = true;
    printf("[NTP] callback sec=%lu\n", (unsigned long)sec);
}

bool ntp_client_dns_ready(void) {
    const ip_addr_t *dns0 = dns_getserver(0);
    return dns0 != NULL && !ip_addr_isany(dns0);
}

bool ntp_client_init(void) {
    if (g_ntp_initialized) {
        return true;
    }

    sntp_setoperatingmode(SNTP_OPMODE_POLL);
    sntp_setservername(0, "pool.ntp.org");
    sntp_init();
    g_ntp_initialized = true;
    printf("[NTP] SNTP init ok (server=pool.ntp.org)\n");
    return true;
}

bool ntp_client_sync_to_rtc(void) {
    if (!g_ntp_initialized) {
        printf("[NTP] sync requested before init\n");
        return false;
    }

    g_sntp_updated = false;
    g_sntp_epoch_utc = 0;

    uint32_t waited_ms = 0;
    time_t utc_epoch = 0;
    while (!g_sntp_updated && waited_ms < NTP_SYNC_TIMEOUT_MS) {
        sleep_ms(NTP_SYNC_POLL_MS);
        waited_ms += NTP_SYNC_POLL_MS;
    }

    if (!g_sntp_updated || g_sntp_epoch_utc == 0) {
        printf("[NTP] timeout waiting for SNTP callback\n");
        return false;
    }

    utc_epoch = (time_t)g_sntp_epoch_utc;
    struct tm *utc_tm = gmtime(&utc_epoch);
    if (utc_tm == NULL || (utc_tm->tm_year + 1900) <= 2024) {
        printf("[NTP] invalid callback epoch=%lld\n", (long long)utc_epoch);
        return false;
    }

    time_t local_epoch = utc_epoch + NTP_TZ_OFFSET_SECONDS;
    printf("[NTP] epochs: utc=%lld local_utc8=%lld\n",
           (long long)utc_epoch, (long long)local_epoch);
    struct tm *local_tm = gmtime(&local_epoch);
    if (local_tm == NULL) {
        printf("[NTP] gmtime failed\n");
        return false;
    }

    ds3231_time_t rtc_time = {
        .second = (uint8_t)local_tm->tm_sec,
        .minute = (uint8_t)local_tm->tm_min,
        .hour = (uint8_t)local_tm->tm_hour,
        .day = (uint8_t)local_tm->tm_mday,
        .month = (uint8_t)(local_tm->tm_mon + 1),
        .year = (uint8_t)(local_tm->tm_year - 100)
    };

    if (!ds3231_set_time(&rtc_time)) {
        printf("[NTP] failed to write DS3231\n");
        return false;
    }
    if (!ds3231_clear_osf()) {
        printf("[NTP] warning: failed to clear RTC OSF flag\n");
    } else {
        printf("[NTP] RTC OSF cleared\n");
    }

    struct timespec ts = {.tv_sec = local_epoch, .tv_nsec = 0};
    aon_timer_start(&ts);
    printf("[NTP] sync ok: 20%02u-%02u-%02uT%02u:%02u:%02u (UTC+8)\n",
           rtc_time.year, rtc_time.month, rtc_time.day,
           rtc_time.hour, rtc_time.minute, rtc_time.second);
    return true;
}
