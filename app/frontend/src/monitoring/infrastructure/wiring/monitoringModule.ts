import { dispatchDeviceControl } from '@/monitoring/application/useCases/dispatchDeviceControl'
import { getDeviceLogsList } from '@/monitoring/application/useCases/getDeviceLogsList'
import { getSystemStatus } from '@/monitoring/application/useCases/getSystemStatus'
import { getDeviceUiEventsList } from '@/monitoring/application/useCases/getDeviceUiEventsList'
import { getTelemetryList } from '@/monitoring/application/useCases/getTelemetryList'
import { getTelemetrySeries } from '@/monitoring/application/useCases/getTelemetrySeries'
import { HttpDeviceControlRepository } from '@/monitoring/infrastructure/repositories/HttpDeviceControlRepository'
import { HttpDeviceLogRepository } from '@/monitoring/infrastructure/repositories/HttpDeviceLogRepository'
import { HttpSystemStatusRepository } from '@/monitoring/infrastructure/repositories/HttpSystemStatusRepository'
import { HttpDeviceUiEventRepository } from '@/monitoring/infrastructure/repositories/HttpDeviceUiEventRepository'
import { HttpTelemetryRepository } from '@/monitoring/infrastructure/repositories/HttpTelemetryRepository'

const telemetryRepository = new HttpTelemetryRepository()
const deviceControlRepository = new HttpDeviceControlRepository()
const deviceLogRepository = new HttpDeviceLogRepository()
const systemStatusRepository = new HttpSystemStatusRepository()
const deviceUiEventRepository = new HttpDeviceUiEventRepository()

/** Composition root for monitoring feature (clean architecture wiring). */
export const monitoringApi = {
  getTelemetryList: (query: Parameters<typeof getTelemetryList>[1]) =>
    getTelemetryList(telemetryRepository, query),
  getTelemetrySeries: (query: Parameters<typeof getTelemetrySeries>[1]) =>
    getTelemetrySeries(telemetryRepository, query),
  dispatchDeviceControl: (payload: Parameters<typeof dispatchDeviceControl>[1]) =>
    dispatchDeviceControl(deviceControlRepository, payload),
  getDeviceLogsList: (query: Parameters<typeof getDeviceLogsList>[1]) =>
    getDeviceLogsList(deviceLogRepository, query),
  getSystemStatus: () => getSystemStatus(systemStatusRepository),
  getDeviceUiEventsList: (query: Parameters<typeof getDeviceUiEventsList>[1]) =>
    getDeviceUiEventsList(deviceUiEventRepository, query),
}
