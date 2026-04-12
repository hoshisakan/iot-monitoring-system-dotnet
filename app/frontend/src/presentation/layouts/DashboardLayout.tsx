import { AppShell, Box, Button, Group, Text, Title } from '@mantine/core'
import { IconActivity, IconFileText, IconHandStop, IconHeartbeat, IconLogout, IconSettings } from '@tabler/icons-react'
import { useNavigate, Outlet, NavLink } from 'react-router-dom'
import { getApiBase } from '@/infrastructure/apiBase'
import { clearTokens, getRefreshToken } from '@/infrastructure/authStorage'
import classes from './DashboardLayout.module.css'

const navItems = [
  { to: '/dashboard/telemetry', label: '遙測', icon: IconActivity },
  { to: '/dashboard/control', label: '裝置控制', icon: IconSettings },
  { to: '/dashboard/system-status', label: '系統狀態', icon: IconHeartbeat },
  { to: '/dashboard/logs', label: '裝置日誌', icon: IconFileText },
  { to: '/dashboard/ui-events', label: '介面事件', icon: IconHandStop },
]

export function DashboardLayout() {
  const navigate = useNavigate()

  async function logout() {
    const refresh = getRefreshToken()
    if (refresh) {
      await fetch(`${getApiBase()}/api/v1/auth/logout`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refresh_token: refresh }),
      })
    }
    clearTokens()
    navigate('/login', { replace: true })
  }

  return (
    <AppShell
      header={{ height: 56 }}
      navbar={{ width: 280, breakpoint: 'sm', collapsed: { mobile: false } }}
      padding="md"
    >
      <AppShell.Navbar p="md" className={classes.navbar}>
        <Box mb="lg">
          <Text size="xs" tt="uppercase" fw={600} c="dimmed" mb={4}>
            IIoT
          </Text>
          <Title order={5}>監控儀表</Title>
          <Text size="xs" c="dimmed" mt={4}>
            REST /api/v1
          </Text>
        </Box>
        {navItems.map(({ to, label, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) => `${classes.link} ${isActive ? classes.linkActive : ''}`}
          >
            <Group gap="sm" wrap="nowrap">
              <Icon size={18} stroke={1.5} />
              <span>{label}</span>
            </Group>
          </NavLink>
        ))}
      </AppShell.Navbar>

      <AppShell.Header px="md" className={classes.header}>
        <Group h="100%" justify="flex-end" wrap="nowrap">
          <Button leftSection={<IconLogout size={16} />} variant="default" size="xs" onClick={() => void logout()}>
            登出
          </Button>
        </Group>
      </AppShell.Header>

      <AppShell.Main>
        <Outlet />
      </AppShell.Main>
    </AppShell>
  )
}
