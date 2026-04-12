cmake_minimum_required(VERSION 3.13)

# SDK path resolution
if (NOT PICO_SDK_PATH)
    set(PICO_SDK_PATH $ENV{PICO_SDK_PATH})
endif()

set(PICO_BOARD pico2_w)

if (EXISTS "${CMAKE_CURRENT_LIST_DIR}/pico_sdk_import.cmake")
    include(pico_sdk_import.cmake)
elseif (PICO_SDK_PATH AND EXISTS "${PICO_SDK_PATH}/external/pico_sdk_import.cmake")
    include("${PICO_SDK_PATH}/external/pico_sdk_import.cmake")
endif()

project(iiot_firmware C CXX ASM)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

pico_sdk_init()

# Generate CA certificate header
set(CA_CERT_SOURCE "${CMAKE_CURRENT_LIST_DIR}/../../conf/mqtt_broker/certs/server/ca.crt")
# set(CA_CERT_SOURCE "${CMAKE_CURRENT_LIST_DIR}/../../conf/mqtt_broker/certs/local/ca.crt")
set(GEN_CERT_HEADER "${CMAKE_BINARY_DIR}/generated_ca_cert.h")
if (EXISTS ${CA_CERT_SOURCE})
    file(READ ${CA_CERT_SOURCE} HEX_CONTENT HEX)
    string(REGEX REPLACE "([0-9a-f][0-9a-f])" "0x\\1, " C_ARRAY_CONTENT ${HEX_CONTENT})
    set(HEADER_CONTENT "/* Auto-generated CA certificate */\n\nstatic const unsigned char ca_crt[] = {\n${C_ARRAY_CONTENT} 0x00\n};\n\nstatic const unsigned int ca_crt_len = sizeof(ca_crt) - 1;\n")
    file(WRITE ${GEN_CERT_HEADER} "${HEADER_CONTENT}")
else()
    file(WRITE ${GEN_CERT_HEADER} "/* CA certificate not found */\n\nstatic const unsigned char ca_crt[] = {0x00};\nstatic const unsigned int ca_crt_len = 0;\n")
endif()

add_executable(iiot_firmware
    main.c
    src/ds3231.c
    src/ntp_client.c
    src/wifi_handler.c
    src/mqtt_manager.c
    src/bme680_sensor.c
    src/scd41_sensor.c
    src/tsl2561_sensor.c
    src/mpu9250_sensor.c
    src/sh1106_oled.c
    src/at24c256_eeprom.c
    src/telemetry_eeprom_queue.c
    src/lcd1602_pcf8574.c
)

# Include paths
target_include_directories(iiot_firmware PRIVATE
    ${CMAKE_CURRENT_LIST_DIR}
    ${CMAKE_CURRENT_LIST_DIR}/include
    ${CMAKE_BINARY_DIR}
)

# Link dependencies
target_link_libraries(iiot_firmware
    pico_stdlib
    pico_aon_timer
    hardware_i2c
    pico_lwip_mqtt
    pico_lwip_sntp
    pico_lwip_mbedtls
    pico_mbedtls
    pico_cyw43_arch_lwip_threadsafe_background
    m
)

target_compile_definitions(iiot_firmware PRIVATE
    CYW43_LWIP=1
    LWIP_ALTCP=1
    LWIP_ALTCP_TLS=1
    LWIP_ALTCP_TLS_MBEDTLS=1
)

target_compile_options(iiot_firmware PRIVATE
    -UMBEDTLS_TIMING_C
    -UMBEDTLS_TIMING_ALT
)

if (PICO_SDK_PATH)
    set_source_files_properties(
        "${PICO_SDK_PATH}/lib/mbedtls/library/timing.c"
        PROPERTIES COMPILE_FLAGS "-UMBEDTLS_TIMING_C -UMBEDTLS_TIMING_ALT -D__unix__"
    )
endif()

pico_enable_stdio_usb(iiot_firmware 1)
pico_enable_stdio_uart(iiot_firmware 0)
pico_add_extra_outputs(iiot_firmware)

