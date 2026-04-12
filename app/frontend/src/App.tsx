import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { LoginPage } from '@/presentation/pages/LoginPage'
import { DashboardLayout } from '@/presentation/layouts/DashboardLayout'
import { TelemetryDashboardPage } from '@/presentation/pages/dashboard/TelemetryDashboardPage'
import { DeviceLogsDashboardPage } from '@/presentation/pages/dashboard/DeviceLogsDashboardPage'
import { DeviceUiEventsDashboardPage } from '@/presentation/pages/dashboard/DeviceUiEventsDashboardPage'
import { SystemStatusDashboardPage } from '@/presentation/pages/dashboard/SystemStatusDashboardPage'
import { DeviceControlDashboardPage } from '@/presentation/pages/dashboard/DeviceControlDashboardPage'
import { getAccessToken } from '@/infrastructure/authStorage'

function Protected({ children }: { children: React.ReactNode }) {
  if (!getAccessToken()) {
    return <Navigate to="/login" replace />
  }
  return <>{children}</>
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/"
          element={
            <Protected>
              <Navigate to="/dashboard/telemetry" replace />
            </Protected>
          }
        />
        <Route
          path="/dashboard"
          element={
            <Protected>
              <DashboardLayout />
            </Protected>
          }
        >
          <Route index element={<Navigate to="telemetry" replace />} />
          <Route path="telemetry" element={<TelemetryDashboardPage />} />
          <Route path="control" element={<DeviceControlDashboardPage />} />
          <Route path="system-status" element={<SystemStatusDashboardPage />} />
          <Route path="logs" element={<DeviceLogsDashboardPage />} />
          <Route path="ui-events" element={<DeviceUiEventsDashboardPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
