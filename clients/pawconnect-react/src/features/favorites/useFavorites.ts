import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/api/client'
import { queryKeys } from '@/api/queryKeys'

export function useFavoriteIds(enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.favoriteIds,
    queryFn: ({ signal }) => api.get<number[]>('/favorites/ids', signal),
    enabled,
    staleTime: 30_000,
  })
}

export function useToggleFavorite() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ dogId, favorite }: { dogId: number; favorite: boolean }) => favorite
      ? api.delete<void>(`/favorites/${dogId}`)
      : api.put<void>(`/favorites/${dogId}`),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.favoriteIds }),
        queryClient.invalidateQueries({ queryKey: queryKeys.favorites }),
      ])
    },
  })
}
