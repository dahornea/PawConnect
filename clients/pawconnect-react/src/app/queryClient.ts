import { QueryClient } from '@tanstack/react-query'
import { ApiError } from '@/api/errors'

const shouldRetry = (failureCount: number, error: unknown) => {
  if (failureCount >= 2) return false
  if (error instanceof ApiError && [400, 401, 403, 404, 409, 429].includes(error.status)) return false
  return true
}

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      gcTime: 5 * 60_000,
      retry: shouldRetry,
      refetchOnWindowFocus: false,
    },
    mutations: { retry: false },
  },
})
