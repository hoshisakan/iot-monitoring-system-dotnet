import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Button,
  Group,
  Loader,
  MultiSelect,
  Paper,
  ScrollArea,
  Table,
  Text,
  TextInput,
  Title,
} from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconRefresh } from '@tabler/icons-react'
import { monitoringApi } from '@/monitoring/infrastructure/wiring/monitoringModule'
import { UnauthorizedError } from '@/monitoring/infrastructure/errors/UnauthorizedError'
import type { TelemetryRow, TelemetrySeriesResponse } from '@/monitoring/domain/entities/telemetry'
import { clearTokens } from '@/infrastructure/authStorage'
import { PaginationBar } from '@/presentation/components/monitoring/PaginationBar'

/** 值須為後端 `SupportedMetrics` 名稱 */
const SERIES_METRIC_OPTIONS = [
  { value: 'temperature_c', label: 'BME 溫度' },
  { value: 'humidity_pct', label: 'BME 濕度' },
  { value: 'pressure_hpa', label: '氣壓' },
  { value: 'lux', label: '照度' },
  { value: 'co2_ppm', label: 'SCD41 CO2' },
  { value: 'temperature_c_scd41', label: 'SCD41 溫度' },
  { value: 'humidity_pct_scd41', label: 'SCD41 濕度' },
  { value: 'pir_active', label: 'PIR' },
  { value: 'rssi', label: 'RSSI' },
]

const SERIES_COLORS = ['#0ca678', '#1c7ed6', '#f08c00', '#7048e8', '#e03131', '#0b7285']

function toInputDateTimeLocal(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function nowPlusOneMinuteInput(): string {
  return toInputDateTimeLocal(new Date(Date.now() + 60_000))
}

function localInputToIso(s: string): string {
  const d = new Date(s)
  if (Number.isNaN(d.getTime())) return new Date().toISOString()
  return d.toISOString()
}

/** 後端要求 from 必須小於 to（不可相等） */
function ensureRange(fromIso: string, toIso: string): { from: string; to: string } {
  const a = new Date(fromIso).getTime()
  const b = new Date(toIso).getTime()
  if (Number.isNaN(a) || Number.isNaN(b) || a < b) return { from: fromIso, to: toIso }
  return { from: fromIso, to: new Date(a + 60_000).toISOString() }
}

function normalizeNumber(v: unknown): number | null {
  if (typeof v === 'number' && Number.isFinite(v)) return v
  if (typeof v === 'boolean') return v ? 1 : 0
  return null
}

function fmt(v: number | null | undefined, digits = 2): string {
  if (v == null || Number.isNaN(v)) return '—'
  return Number(v).toFixed(digits)
}

export function TelemetryDashboardPage() {
  const navigate = useNavigate()
  const [rows, setRows] = useState<TelemetryRow[]>([])
  const [totalCount, setTotalCount] = useState<number | undefined>(undefined)
  const [loading, setLoading] = useState(true)
  const [limit, setLimit] = useState(50)
  const [offset, setOffset] = useState(0)
  const [deviceId, setDeviceId] = useState('')
  const [appliedDeviceId, setAppliedDeviceId] = useState('')
  const [fromTime, setFromTime] = useState<string>(() =>
    toInputDateTimeLocal(new Date(Date.now() - 24 * 60 * 60 * 1000)),
  )
  const [toTime, setToTime] = useState<string>(() => nowPlusOneMinuteInput())
  const [metrics, setMetrics] = useState<string[]>(['temperature_c', 'co2_ppm', 'pir_active'])
  const [seriesLoading, setSeriesLoading] = useState(false)
  const [seriesData, setSeriesData] = useState<TelemetrySeriesResponse | null>(null)

  const load = useCallback(async () => {
    const did = appliedDeviceId.trim()
    if (!did) {
      setRows([])
      setTotalCount(0)
      setLoading(false)
      return
    }
    setLoading(true)
    try {
      const rawFrom = localInputToIso(fromTime)
      const rawTo = localInputToIso(toTime)
      const { from: fromIso, to: toIso } = ensureRange(rawFrom, rawTo)
      const page = Math.floor(offset / limit) + 1
      const result = await monitoringApi.getTelemetryList({
        deviceId: did,
        from: fromIso,
        to: toIso,
        page,
        pageSize: limit,
      })
      setRows(result.items)
      setTotalCount(result.total_count)
    } catch (e) {
      if (e instanceof UnauthorizedError) {
        clearTokens()
        navigate('/login', { replace: true })
        return
      }
      const msg = e instanceof Error ? e.message : String(e)
      notifications.show({ title: '載入失敗', message: msg, color: 'red' })
    } finally {
      setLoading(false)
    }
  }, [appliedDeviceId, fromTime, limit, offset, navigate, toTime])

  const loadSeries = useCallback(async () => {
    const did = appliedDeviceId.trim() || deviceId.trim()
    if (!did) {
      setSeriesData(null)
      return
    }
    if (metrics.length === 0) {
      setSeriesData(null)
      return
    }
    setSeriesLoading(true)
    try {
      const rf = localInputToIso(fromTime)
      const rt = localInputToIso(toTime)
      const { from: sf, to: st } = ensureRange(rf, rt)
      const data = await monitoringApi.getTelemetrySeries({
        deviceId: did,
        from: sf,
        to: st,
        metrics,
        maxPoints: 500,
      })
      setSeriesData(data)
    } catch (e) {
      if (e instanceof UnauthorizedError) {
        clearTokens()
        navigate('/login', { replace: true })
        return
      }
      const msg = e instanceof Error ? e.message : String(e)
      notifications.show({ title: '圖表載入失敗', message: msg, color: 'red' })
      setSeriesData(null)
    } finally {
      setSeriesLoading(false)
    }
  }, [appliedDeviceId, deviceId, fromTime, metrics, navigate, toTime])

  const refreshToLatest = useCallback(() => {
    setToTime(nowPlusOneMinuteInput())
    if (offset === 0) {
      void load()
      if (appliedDeviceId.trim()) {
        void loadSeries()
      }
      return
    }
    setOffset(0)
  }, [appliedDeviceId, load, loadSeries, offset])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    if (!appliedDeviceId.trim()) return
    void loadSeries()
  }, [appliedDeviceId, fromTime, toTime, metrics, loadSeries])

  const chartWidth = 1000
  const chartHeight = 260
  const chartPadding = 24
  const buckets = (seriesData?.series ?? []).map((s, idx) => {
    const points = s.points
      .map((p) => ({
        t: new Date(p.t).getTime(),
        v: normalizeNumber(p.v),
      }))
      .filter((p): p is { t: number; v: number } => p.v != null && Number.isFinite(p.t) && Number.isFinite(p.v))
      .sort((a, b) => a.t - b.t)
    return {
      metric: s.metric,
      color: SERIES_COLORS[idx % SERIES_COLORS.length],
      points,
    }
  }).filter((s) => s.points.length > 0)
  const allPoints = buckets.flatMap((b) => b.points)
  const minT = allPoints.length > 0 ? Math.min(...allPoints.map((p) => p.t)) : 0
  const maxT = allPoints.length > 0 ? Math.max(...allPoints.map((p) => p.t)) : 1
  const minV = allPoints.length > 0 ? Math.min(...allPoints.map((p) => p.v)) : 0
  const maxV = allPoints.length > 0 ? Math.max(...allPoints.map((p) => p.v)) : 1
  const spanT = maxT - minT || 1
  const spanV = maxV - minV || 1
  const toX = (t: number) => chartPadding + ((t - minT) / spanT) * (chartWidth - chartPadding * 2)
  const toY = (v: number) => chartHeight - chartPadding - ((v - minV) / spanV) * (chartHeight - chartPadding * 2)

  return (
    <Paper withBorder radius="md" p="md">
      <Group justify="space-between" mb="md" wrap="wrap">
        <Title order={4}>遙測</Title>
        <Button
          size="xs"
          variant="light"
          leftSection={<IconRefresh size={16} />}
          loading={loading}
          onClick={refreshToLatest}
        >
          重新整理
        </Button>
      </Group>

      <Text size="xs" c="dimmed" mb="sm">
        後端 <code>GET /api/v1/telemetry</code> 必填 <code>device_id</code> 與時間區間（<code>from</code>/
        <code>to</code>）。請先輸入裝置並套用，列表與圖表共用上方時間範圍。
      </Text>

      <Group align="flex-end" gap="sm" mb="md" wrap="wrap">
        <TextInput
          label="device_id"
          placeholder="必填"
          value={deviceId}
          onChange={(e) => setDeviceId(e.currentTarget.value)}
          size="xs"
          w={220}
        />
        <TextInput
          label="from"
          type="datetime-local"
          value={fromTime}
          onChange={(e) => setFromTime(e.currentTarget.value)}
          size="xs"
          w={220}
        />
        <TextInput
          label="to"
          type="datetime-local"
          value={toTime}
          onChange={(e) => setToTime(e.currentTarget.value)}
          size="xs"
          w={220}
        />
        <Button
          size="xs"
          onClick={() => {
            setAppliedDeviceId(deviceId.trim())
            setOffset(0)
          }}
        >
          套用並載入圖表
        </Button>
      </Group>

      <Paper withBorder p="sm" radius="md" mb="md">
        <Group align="flex-end" gap="sm" wrap="wrap">
          <MultiSelect
            label="series.metrics"
            data={SERIES_METRIC_OPTIONS}
            value={metrics}
            onChange={setMetrics}
            placeholder="至少選 1 個指標"
            size="xs"
            w={400}
            searchable
          />
          <Button
            size="xs"
            loading={seriesLoading}
            onClick={() => {
              setToTime(nowPlusOneMinuteInput())
              void loadSeries()
            }}
          >
            重新載入圖表
          </Button>
        </Group>
        <ScrollArea mt="sm">
          {seriesLoading ? (
            <Group justify="center" p="sm">
              <Loader color="teal" size="sm" />
            </Group>
          ) : buckets.length === 0 ? (
            <Text size="sm" c="dimmed" ta="center" py="sm">
              請輸入 device_id、套用篩選後載入圖表
            </Text>
          ) : (
            <>
              <Text size="xs" c="dimmed" mb="xs">
                device_id: {seriesData?.device_id ?? '—'} · from_utc: {seriesData?.from_utc ?? '—'} · to_utc:{' '}
                {seriesData?.to_utc ?? '—'}
                {seriesData?.downsampled != null
                  ? ` · downsampled=${String(seriesData.downsampled)} · source_points=${String(seriesData.source_points ?? '—')} · returned_points=${String(seriesData.returned_points ?? '—')}`
                  : ''}
              </Text>
              <svg width={chartWidth} height={chartHeight}>
                <line x1={chartPadding} y1={chartPadding} x2={chartPadding} y2={chartHeight - chartPadding} stroke="#adb5bd" />
                <line
                  x1={chartPadding}
                  y1={chartHeight - chartPadding}
                  x2={chartWidth - chartPadding}
                  y2={chartHeight - chartPadding}
                  stroke="#adb5bd"
                />
                {buckets.map((bucket) => {
                  const poly = bucket.points.map((p) => `${toX(p.t)},${toY(p.v)}`).join(' ')
                  return (
                    <g key={bucket.metric}>
                      <polyline fill="none" stroke={bucket.color} strokeWidth={2} points={poly} />
                      {bucket.points.map((p, idx) => (
                        <circle
                          key={`${bucket.metric}-${idx}`}
                          cx={toX(p.t)}
                          cy={toY(p.v)}
                          r={2.5}
                          fill={bucket.color}
                        />
                      ))}
                    </g>
                  )
                })}
              </svg>
              <Group gap="xs" mt="xs" wrap="wrap">
                {buckets.map((bucket) => (
                  <Group key={bucket.metric} gap={6}>
                    <Text size="xs" c={bucket.color}>
                      ●
                    </Text>
                    <Text size="xs">{bucket.metric}</Text>
                  </Group>
                ))}
              </Group>
            </>
          )}
        </ScrollArea>
      </Paper>

      <PaginationBar
        limit={limit}
        offset={offset}
        pageSize={rows.length}
        totalCount={totalCount}
        onLimitChange={(v) => {
          setLimit(v)
          setOffset(0)
        }}
        onOffsetChange={setOffset}
      />

      <ScrollArea mt="md">
        {loading && rows.length === 0 ? (
          <Group justify="center" p="xl">
            <Loader color="teal" />
          </Group>
        ) : (
          <Table striped highlightOnHover stickyHeader>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>裝置</Table.Th>
                <Table.Th>站臺</Table.Th>
                <Table.Th>裝置時間 (UTC)</Table.Th>
                <Table.Th>BME 溫度</Table.Th>
                <Table.Th>BME 濕度</Table.Th>
                <Table.Th>氣壓</Table.Th>
                <Table.Th>照度</Table.Th>
                <Table.Th>SCD41 CO2</Table.Th>
                <Table.Th>SCD41 溫度</Table.Th>
                <Table.Th>SCD41 濕度</Table.Th>
                <Table.Th>PIR</Table.Th>
                <Table.Th>sync</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows.map((r) => (
                <Table.Tr key={r.id}>
                  <Table.Td>
                    <Text size="sm" fw={500}>
                      {r.device_id}
                    </Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs" c="dimmed">
                      {r.site_id}
                    </Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs" c="dimmed">
                      {r.device_time_utc}
                    </Text>
                  </Table.Td>
                  <Table.Td>{fmt(r.temperature_c)}</Table.Td>
                  <Table.Td>{fmt(r.humidity_pct)}</Table.Td>
                  <Table.Td>{fmt(r.pressure_hpa)}</Table.Td>
                  <Table.Td>{fmt(r.lux)}</Table.Td>
                  <Table.Td>{fmt(r.co2_ppm, 0)}</Table.Td>
                  <Table.Td>{fmt(r.temperature_c_scd41)}</Table.Td>
                  <Table.Td>{fmt(r.humidity_pct_scd41)}</Table.Td>
                  <Table.Td>
                    <Text size="xs">{r.pir_active == null ? '—' : r.pir_active ? 'true' : 'false'}</Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs">{r.is_sync_back ? 'true' : 'false'}</Text>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
        {!loading && rows.length === 0 && (
          <Text c="dimmed" ta="center" p="xl">
            {appliedDeviceId.trim()
              ? '此區間尚無遙測資料（確認 Pico → MQTT → 後端 ingest）'
              : '請輸入 device_id 並套用篩選'}
          </Text>
        )}
      </ScrollArea>
    </Paper>
  )
}
