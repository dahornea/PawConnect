import { useCallback, useEffect, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiUnauthorizedEvent, clearApiSecurityContext } from '@/api/client'
import { ApiError } from '@/api/errors'
import { queryKeys } from '@/api/queryKeys'
import type { UserSession } from '@/api/types'
import { AuthContext } from '@/auth/auth-context'

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const clearUserState = useCallback(() => {
    clearApiSecurityContext()
    queryClient.setQueryData(queryKeys.session, null)
    queryClient.removeQueries({
      predicate: (query) => query.queryKey[0] !== queryKeys.session[0],
    })
  }, [queryClient])

  useEffect(() => {
    window.addEventListener(apiUnauthorizedEvent, clearUserState)
    return () => window.removeEventListener(apiUnauthorizedEvent, clearUserState)
  }, [clearUserState])

  const sessionQuery = useQuery({
    queryKey: queryKeys.session,
    queryFn: ({ signal }) => api.get<UserSession>('/auth/me', signal),
    retry: false,
    staleTime: 60_000,
    throwOnError: false,
  })

  const user = sessionQuery.error instanceof ApiError && [401, 403].includes(sessionQuery.error.status)
    ? null
    : sessionQuery.data ?? null

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.session })
  }

  const logout = async () => {
    try {
      await api.post<void>('/auth/logout')
    } finally {
      clearUserState()
    }
  }

  return (
    <AuthContext.Provider value={{ user, isLoading: sessionQuery.isLoading, refresh, logout }}>
      {children}
    </AuthContext.Provider>
  )
}
