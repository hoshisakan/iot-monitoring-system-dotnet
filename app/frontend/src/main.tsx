import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { MantineProvider, createTheme } from '@mantine/core'
import { Notifications } from '@mantine/notifications'
import '@mantine/core/styles.css'
import '@mantine/notifications/styles.css'
import './index.css'
import App from './App.tsx'

const theme = createTheme({
  primaryColor: 'teal',
  defaultRadius: 'md',
  fontFamily:
    'ui-sans-serif, system-ui, "Segoe UI", Roboto, "Noto Sans TC", sans-serif',
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <MantineProvider defaultColorScheme="dark" theme={theme}>
      <Notifications position="top-right" zIndex={4000} />
      <App />
    </MantineProvider>
  </StrictMode>,
)
