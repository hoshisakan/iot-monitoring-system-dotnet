import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Paper, Title, TextInput, PasswordInput, Button, Stack, Text } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconGauge } from '@tabler/icons-react'
import { getApiBase } from '@/infrastructure/apiBase'
import { setTokens } from '@/infrastructure/authStorage'

export function LoginPage() {
  const navigate = useNavigate()
  const [username, setUsername] = useState('admin')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    try {
      const r = await fetch(`${getApiBase()}/api/v1/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password }),
      })
      const ct = r.headers.get('content-type') ?? ''
      if (!ct.includes('application/json')) {
        notifications.show({
          title: '登入失敗',
          message: `伺服器未回傳 JSON（${r.status}）。若經 Nginx，請確認 Api 已於宿主機 :5163 監聽且可被 Docker 連線。`,
          color: 'red',
        })
        return
      }
      const data = (await r.json()) as Record<string, unknown>
      if (!r.ok) {
        notifications.show({
          title: '登入失敗',
          message: typeof data.error === 'string' ? data.error : r.statusText,
          color: 'red',
        })
        return
      }
      setTokens(data.access_token as string, data.refresh_token as string)
      notifications.show({ title: '已登入', message: '歡迎使用儀表', color: 'teal' })
      navigate('/dashboard/telemetry', { replace: true })
    } finally {
      setLoading(false)
    }
  }

  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background:
          'radial-gradient(ellipse 120% 80% at 20% 0%, rgba(18, 184, 134, 0.15), transparent), #0c0e12',
        padding: 24,
      }}
    >
      <Paper radius="md" p="xl" withBorder shadow="xl" maw={420} w="100%" bg="dark.8">
        <Stack gap="lg">
          <div>
            <IconGauge size={40} color="var(--mantine-color-teal-4)" style={{ marginBottom: 8 }} />
            <Title order={2} c="gray.0">
              IIoT 監控
            </Title>
            <Text size="sm" c="dimmed" mt={4}>
              登入後可檢視遙測、裝置日誌與介面事件（同源 /api，不直連 MQTT）
            </Text>
          </div>
          <form onSubmit={onSubmit}>
            <Stack gap="md">
              <TextInput
                label="使用者名稱"
                value={username}
                onChange={(e) => setUsername(e.currentTarget.value)}
                required
                autoComplete="username"
              />
              <PasswordInput
                label="密碼"
                value={password}
                onChange={(e) => setPassword(e.currentTarget.value)}
                required
                autoComplete="current-password"
              />
              <Button type="submit" fullWidth loading={loading} color="teal">
                登入
              </Button>
            </Stack>
          </form>
        </Stack>
      </Paper>
    </div>
  )
}
