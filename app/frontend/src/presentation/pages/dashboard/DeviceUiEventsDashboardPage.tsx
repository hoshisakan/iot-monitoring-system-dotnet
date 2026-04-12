import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, Group, Loader, Paper, ScrollArea, Table, Text, TextInput, Title } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconRefresh } from '@tabler/icons-react'
import { monitoringApi } from '@/monitoring/infrastructure/wiring/monitoringModule'
import { UnauthorizedError } from '@/monitoring/infrastructure/errors/UnauthorizedError'
import type { DeviceUiEventRow } from '@/monitoring/domain/entities/deviceUiEvent'
import { clearTokens } from '@/infrastructure/authStorage'
import { PaginationBar } from '@/presentation/components/monitoring/PaginationBar'

export function DeviceUiEventsDashboardPage() {
  const navigate = useNavigate()
  const [rows, setRows] = useState<DeviceUiEventRow[]>([])
  const [totalCount, setTotalCount] = useState<number | undefined>(undefined)
  const [loading, setLoading] = useState(true)
  const [limit, setLimit] = useState(50)
  const [offset, setOffset] = useState(0)
  const [deviceId, setDeviceId] = useState('')
  const [siteId, setSiteId] = useState('')
  const [appliedDeviceId, setAppliedDeviceId] = useState('')
  const [appliedSiteId, setAppliedSiteId] = useState('')

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
      const page = Math.floor(offset / limit) + 1
      const result = await monitoringApi.getDeviceUiEventsList({
        deviceId: did,
        page,
        pageSize: limit,
        siteId: appliedSiteId.trim() || undefined,
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
  }, [appliedDeviceId, appliedSiteId, limit, offset, navigate])

  useEffect(() => {
    void load()
  }, [load])

  function applyFilters() {
    setAppliedDeviceId(deviceId.trim())
    setAppliedSiteId(siteId.trim())
    setOffset(0)
  }

  return (
    <Paper withBorder radius="md" p="md">
      <Group justify="space-between" mb="md" wrap="wrap">
        <Title order={4}>介面事件</Title>
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
        對齊 <code>GET /api/v1/ui-events</code>，必填 <code>device_id</code>。
      </Text>

      <Group align="flex-end" gap="sm" mb="md" wrap="wrap">
        <TextInput
          label="device_id"
          placeholder="必填"
          value={deviceId}
          onChange={(e) => setDeviceId(e.currentTarget.value)}
          size="xs"
          w={200}
        />
        <TextInput
          label="site_id"
          placeholder="選填"
          value={siteId}
          onChange={(e) => setSiteId(e.currentTarget.value)}
          size="xs"
          w={200}
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
                <Table.Th>event_id</Table.Th>
                <Table.Th>裝置</Table.Th>
                <Table.Th>site</Table.Th>
                <Table.Th>channel</Table.Th>
                <Table.Th>event_type</Table.Th>
                <Table.Th>event_value</Table.Th>
                <Table.Th>device_time_utc</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows.map((r) => (
                <Table.Tr key={r.event_id}>
                  <Table.Td>{r.event_id}</Table.Td>
                  <Table.Td>
                    <Text size="sm" fw={500}>
                      {r.device_id}
                    </Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs">{r.site_id}</Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs">{r.channel}</Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs">{r.event_type}</Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs" lineClamp={2}>
                      {r.event_value}
                    </Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs" c="dimmed">
                      {r.device_time_utc}
                    </Text>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
        {!loading && rows.length === 0 && (
          <Text c="dimmed" ta="center" p="xl">
            {appliedDeviceId.trim() ? '尚無介面事件' : '請輸入 device_id 並套用'}
          </Text>
        )}
      </ScrollArea>
    </Paper>
  )
}
