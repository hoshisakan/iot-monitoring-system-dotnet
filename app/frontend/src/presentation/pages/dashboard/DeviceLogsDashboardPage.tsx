import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, Group, Loader, Paper, ScrollArea, Table, Text, TextInput, Title } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconRefresh } from '@tabler/icons-react'
import { monitoringApi } from '@/monitoring/infrastructure/wiring/monitoringModule'
import { UnauthorizedError } from '@/monitoring/infrastructure/errors/UnauthorizedError'
import type { DeviceLogRow } from '@/monitoring/domain/entities/deviceLog'
import { clearTokens } from '@/infrastructure/authStorage'
import { PaginationBar } from '@/presentation/components/monitoring/PaginationBar'

export function DeviceLogsDashboardPage() {
  const navigate = useNavigate()
  const [rows, setRows] = useState<DeviceLogRow[]>([])
  const [totalCount, setTotalCount] = useState<number | undefined>(undefined)
  const [loading, setLoading] = useState(true)
  const [limit, setLimit] = useState(50)
  const [offset, setOffset] = useState(0)
  const [deviceId, setDeviceId] = useState('')
  const [channel, setChannel] = useState('')
  const [level, setLevel] = useState('')
  const [appliedDeviceId, setAppliedDeviceId] = useState('')
  const [appliedChannel, setAppliedChannel] = useState('')
  const [appliedLevel, setAppliedLevel] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const page = Math.floor(offset / limit) + 1
      const result = await monitoringApi.getDeviceLogsList({
        page,
        pageSize: limit,
        deviceId: appliedDeviceId.trim() || undefined,
        channel: appliedChannel.trim() || undefined,
        level: appliedLevel.trim() || undefined,
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
  }, [appliedChannel, appliedDeviceId, appliedLevel, limit, offset, navigate])

  useEffect(() => {
    void load()
  }, [load])

  function applyFilters() {
    setAppliedDeviceId(deviceId)
    setAppliedChannel(channel)
    setAppliedLevel(level)
    setOffset(0)
  }

  return (
    <Paper withBorder radius="md" p="md">
      <Group justify="space-between" mb="md" wrap="wrap">
        <Title order={4}>裝置日誌</Title>
        <Button
          size="xs"
          variant="light"
          leftSection={<IconRefresh size={16} />}
          loading={loading}
          onClick={() => void load()}
        >
          重新整理
        </Button>
      </Group>

      <Text size="xs" c="dimmed" mb="sm">
        對齊 <code>GET /api/v1/logs</code>：可選 <code>device_id</code>、<code>channel</code>（telemetry / ui-events /
        status）、<code>level</code>（debug / info / warn / error）。
      </Text>

      <Group align="flex-end" gap="sm" mb="md" wrap="wrap">
        <TextInput
          label="device_id"
          placeholder="選填"
          value={deviceId}
          onChange={(e) => setDeviceId(e.currentTarget.value)}
          size="xs"
          w={160}
        />
        <TextInput
          label="channel"
          placeholder="telemetry | ui-events | status"
          value={channel}
          onChange={(e) => setChannel(e.currentTarget.value)}
          size="xs"
          w={200}
        />
        <TextInput
          label="level"
          placeholder="debug | info | warn | error"
          value={level}
          onChange={(e) => setLevel(e.currentTarget.value)}
          size="xs"
          w={160}
        />
        <Button size="xs" onClick={applyFilters}>
          套用篩選
        </Button>
      </Group>

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
                <Table.Th>channel</Table.Th>
                <Table.Th>level</Table.Th>
                <Table.Th>message</Table.Th>
                <Table.Th>device_time_utc</Table.Th>
                <Table.Th>created_at_utc</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows.map((r) => (
                <Table.Tr key={r.id}>
                  <Table.Td>
                    <Text size="xs">{r.device_id ?? '—'}</Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs">{r.channel}</Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs">{r.level}</Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs" lineClamp={3}>
                      {r.message}
                    </Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs" c="dimmed">
                      {r.device_time_utc ?? '—'}
                    </Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs" c="dimmed">
                      {r.created_at_utc}
                    </Text>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
        {!loading && rows.length === 0 && (
          <Text c="dimmed" ta="center" p="xl">
            尚無日誌資料
          </Text>
        )}
      </ScrollArea>
    </Paper>
  )
}
