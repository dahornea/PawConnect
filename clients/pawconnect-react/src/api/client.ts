import { normalizeApiError } from '@/api/errors'
import { env } from '@/app/env'

type RequestOptions = Omit<RequestInit, 'body'> & {
  body?: unknown
  skipAntiforgery?: boolean
}

type AntiforgeryResponse = {
  token?: string | null
  headerName?: string | null
}

let antiforgery: { token: string; headerName: string } | undefined
export const apiUnauthorizedEvent = 'pawconnect:api-unauthorized'
const isUnsafeMethod = (method: string) => !['GET', 'HEAD', 'OPTIONS'].includes(method)

async function loadAntiforgery(signal?: AbortSignal) {
  const response = await fetch(`${env.apiBaseUrl}/auth/antiforgery`, {
    credentials: 'include',
    signal,
  })

  if (!response.ok) throw await normalizeApiError(response)
  const data = await response.json() as AntiforgeryResponse
  if (!data.token) throw new Error('PawConnect did not return an antiforgery token.')

  antiforgery = {
    token: data.token,
    headerName: data.headerName || 'X-XSRF-TOKEN',
  }
  return antiforgery
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const method = (options.method || 'GET').toUpperCase()
  const headers = new Headers(options.headers)

  if (isUnsafeMethod(method) && !options.skipAntiforgery) {
    const currentToken = antiforgery ?? await loadAntiforgery(options.signal ?? undefined)
    headers.set(currentToken.headerName, currentToken.token)
  }

  if (options.body !== undefined) headers.set('Content-Type', 'application/json')

  let response: Response
  try {
    response = await fetch(`${env.apiBaseUrl}${path}`, {
      ...options,
      method,
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      credentials: 'include',
    })
  } catch {
    throw new Error('PawConnect is not reachable. Start the ASP.NET Core backend and try again.')
  }

  if (!response.ok) {
    if (response.status === 401 && path !== '/auth/login' && path !== '/auth/me') {
      antiforgery = undefined
      if (typeof window !== 'undefined') window.dispatchEvent(new Event(apiUnauthorizedEvent))
    }
    if (response.status === 400 && isUnsafeMethod(method) && !options.skipAntiforgery) {
      antiforgery = undefined
    }
    throw await normalizeApiError(response)
  }

  if (response.status === 204) return undefined as T
  return await response.json() as T
}

export const api = {
  get: <T>(path: string, signal?: AbortSignal) => apiRequest<T>(path, { signal }),
  post: <T>(path: string, body?: unknown, signal?: AbortSignal) =>
    apiRequest<T>(path, { method: 'POST', body, signal }),
  put: <T>(path: string, body?: unknown, signal?: AbortSignal) =>
    apiRequest<T>(path, { method: 'PUT', body, signal }),
  patch: <T>(path: string, body?: unknown, signal?: AbortSignal) =>
    apiRequest<T>(path, { method: 'PATCH', body, signal }),
  delete: <T>(path: string, signal?: AbortSignal) =>
    apiRequest<T>(path, { method: 'DELETE', signal }),
}

export function clearApiSecurityContext() {
  antiforgery = undefined
}
