import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Badge, Button, Group, Loader, Paper, ScrollArea, Table, Text, Title } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconRefresh } from '@tabler/icons-react'
import { clearTokens } from '@/infrastructure/authStorage'
import type { SystemStatusItem } from '@/monitoring/domain/entities/systemStatus'
import { UnauthorizedError } from '@/monitoring/infrastructure/errors/UnauthorizedError'
import { ForbiddenError } from '@/monitoring/infrastructure/errors/ForbiddenError'
import { monitoringApi } from '@/monitoring/infrastructure/wiring/monitoringModule'

function statusColor(status: string): string {
  const s = status.toLowerCase()
  if (s === 'running' || s === 'healthy' || s === 'up') return 'teal'
  if (s === 'restarting' || s === 'starting') return 'yellow'
  return 'red'
}

export function SystemStatusDashboardPage() {
  const navigate = useNavigate()
  const [items, setItems] = useState<SystemStatusItem[]>([])
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const data = await monitoringApi.getSystemStatus()
      setItems(data.items ?? [])
    } catch (e) {
      if (e instanceof UnauthorizedError) {
        clearTokens()
        navigate('/login', { replace: true })
        return
      }
      if (e instanceof ForbiddenError) {
        notifications.show({
          title: '無權限',
          message: e.message,
          color: 'yellow',
        })
        setItems([])
        return
      }
      const msg = e instanceof Error ? e.message : String(e)
      notifications.show({ title: '載入系統狀態失敗', message: msg, color: 'red' })
    } finally {
      setLoading(false)
    }
  }, [navigate])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <Paper withBorder radius="md" p="md">
      <Group justify="space-between" mb="md" wrap="wrap">
        <div>
          <Title order={4}>系統狀態</Title>
          <Text size="xs" c="dimmed">
            Docker 容器列表（後端 <code>AdminOnly</code>，需 admin 帳號）
          </Text>
        </div>
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

      <ScrollArea>
        {loading && items.length === 0 ? (
          <Group justify="center" p="xl">
            <Loader color="teal" />
          </Group>
        ) : (
          <Table striped highlightOnHover stickyHeader>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>容器</Table.Th>
                <Table.Th>container_id</Table.Th>
                <Table.Th>狀態</Table.Th>
                <Table.Th>health</Table.Th>
                <Table.Th>IP</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {items.map((item, i) => (
                <Table.Tr key={`${item.container_id}-${i}`}>
                  <Table.Td>
                    <Text size="sm" fw={500}>
                      {item.container_name}
                    </Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="xs" c="dimmed" lineClamp={1}>
                      {item.container_id}
                    </Text>
                  </Table.Td>
                  <Table.Td>
                    <Badge color={statusColor(item.status)} variant="light">
                      {item.status}
                    </Badge>
                  </Table.Td>
                  <Table.Td>{item.health}</Table.Td>
                  <Table.Td>{item.ip ?? '—'}</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
        {!loading && items.length === 0 && (
          <Text c="dimmed" ta="center" p="xl">
            無資料或非管理員無法存取
          </Text>
        )}
      </ScrollArea>
    </Paper>
  )
}
