import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Alert, Badge, Button, Group, NumberInput, Paper, Stack, Text, TextInput, Title } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconPlayerPlay, IconServerCog } from '@tabler/icons-react'
import { clearTokens } from '@/infrastructure/authStorage'
import type { DeviceControlResponse } from '@/monitoring/domain/entities/deviceControl'
import { UnauthorizedError } from '@/monitoring/infrastructure/errors/UnauthorizedError'
import { ForbiddenError } from '@/monitoring/infrastructure/errors/ForbiddenError'
import { monitoringApi } from '@/monitoring/infrastructure/wiring/monitoringModule'

function parseErrorMessage(raw: string): string {
  try {
    const data = JSON.parse(raw) as { error?: { message?: string } }
    return data.error?.message || raw
  } catch {
    return raw
  }
}

export function DeviceControlDashboardPage() {
  const navigate = useNavigate()
  const [siteId, setSiteId] = useState('default')
  const [deviceId, setDeviceId] = useState('')
  const [valuePct, setValuePct] = useState<number>(50)
  const [submitting, setSubmitting] = useState(false)
  const [lastResult, setLastResult] = useState<DeviceControlResponse | null>(null)

  async function submit() {
    const did = deviceId.trim()
    if (!did) {
      notifications.show({ title: '參數錯誤', message: 'device_id 必填', color: 'red' })
      return
    }
    if (!Number.isFinite(valuePct) || valuePct < 0 || valuePct > 100) {
      notifications.show({ title: '參數錯誤', message: 'value 必須在 0～100（後端百分比）', color: 'red' })
      return
    }

    setSubmitting(true)
    try {
      const result = await monitoringApi.dispatchDeviceControl({
        site_id: siteId.trim() || undefined,
        device_id: did,
        command: 'set_pwm',
        value: valuePct,
      })
      setLastResult(result)
      notifications.show({
        title: '控制命令已接受',
        message: `status=${result.status}`,
        color: 'teal',
      })
    } catch (e) {
      if (e instanceof UnauthorizedError) {
        clearTokens()
        navigate('/login', { replace: true })
        return
      }
      if (e instanceof ForbiddenError) {
        notifications.show({ title: '無權限', message: e.message, color: 'yellow' })
        return
      }
      const msg = e instanceof Error ? parseErrorMessage(e.message) : String(e)
      notifications.show({ title: '送出失敗', message: msg, color: 'red' })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Paper withBorder radius="md" p="md">
      <Group justify="space-between" mb="md" wrap="wrap">
        <Title order={4}>裝置控制</Title>
        <Badge color="orange" variant="light">
          POST /api/v1/device/control（AdminOnly）
        </Badge>
      </Group>

      <Text size="xs" c="dimmed" mb="sm">
        後端目前僅支援 <code>command: set_pwm</code>，<code>value</code> 為 <strong>0～100 整數（百分比）</strong>。
      </Text>

      <Stack gap="sm">
        <Group align="flex-end" gap="sm" wrap="wrap">
          <TextInput
            label="site_id"
            value={siteId}
            onChange={(e) => setSiteId(e.currentTarget.value)}
            size="xs"
            w={160}
          />
          <TextInput
            label="device_id"
            placeholder="必填"
            value={deviceId}
            onChange={(e) => setDeviceId(e.currentTarget.value)}
            size="xs"
            w={220}
          />
          <NumberInput
            label="value（0～100 %）"
            value={valuePct}
            onChange={(v) => setValuePct(typeof v === 'number' ? v : Number(v) || 0)}
            min={0}
            max={100}
            size="xs"
            w={180}
          />
          <Button
            leftSection={<IconPlayerPlay size={16} />}
            size="xs"
            loading={submitting}
            onClick={() => void submit()}
          >
            送出 set_pwm
          </Button>
        </Group>

        <Group gap="xs" wrap="wrap">
          {[0, 25, 50, 75, 100].map((p) => (
            <Button key={p} variant="default" size="xs" loading={submitting} onClick={() => setValuePct(p)}>
              {p}%
            </Button>
          ))}
        </Group>

        {lastResult && (
          <Alert icon={<IconServerCog size={16} />} color="teal" variant="light">
            <Text size="sm">status: {lastResult.status}</Text>
            <Text size="sm">device_id: {lastResult.device_id}</Text>
            <Text size="sm">command: {lastResult.command}</Text>
            <Text size="sm">value: {lastResult.value}</Text>
          </Alert>
        )}
      </Stack>
    </Paper>
  )
}
