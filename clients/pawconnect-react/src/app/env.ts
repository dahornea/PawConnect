const toBoolean = (value: string | undefined, fallback: boolean) => {
  if (value === undefined) return fallback
  return value.toLowerCase() === 'true'
}

export const env = {
  apiBaseUrl: (import.meta.env.VITE_API_BASE_URL || '/api/v1').replace(/\/$/, ''),
  appName: import.meta.env.VITE_APP_NAME || 'PawConnect',
  copilotEnabled: toBoolean(import.meta.env.VITE_ENABLE_COPILOT, true),
  queryDevtoolsEnabled: import.meta.env.DEV &&
    toBoolean(import.meta.env.VITE_ENABLE_REACT_QUERY_DEVTOOLS, false),
} as const
