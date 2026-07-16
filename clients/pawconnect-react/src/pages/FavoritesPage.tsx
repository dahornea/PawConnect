import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { api } from '@/api/client'
import { queryKeys } from '@/api/queryKeys'
import type { DogListItem } from '@/api/types'
import { DogGrid } from '@/components/dogs/DogGrid'
import { PageHeader } from '@/components/ui/PageHeader'
import { ErrorState } from '@/components/ui/States'
import { useFavoriteIds, useToggleFavorite } from '@/features/favorites/useFavorites'

export function FavoritesPage() {
  const favoritesQuery = useQuery({ queryKey: queryKeys.favorites, queryFn: ({ signal }) => api.get<DogListItem[]>('/favorites', signal) })
  const ids = useFavoriteIds(true)
  const toggle = useToggleFavorite()
  return (
    <div className="container page-stack">
      <PageHeader title="Favorite dogs" description="A focused shortlist you can revisit before applying." action={<Link className="button button--secondary button--md" to="/dogs">Browse dogs</Link>} />
      {favoritesQuery.isError ? <ErrorState message={(favoritesQuery.error as Error).message} onRetry={() => favoritesQuery.refetch()} /> : <DogGrid dogs={favoritesQuery.data ?? []} isLoading={favoritesQuery.isLoading} favorites={ids.data} onFavorite={(dog) => dog.id && toggle.mutate({ dogId: dog.id, favorite: true })} favoriteBusyId={toggle.isPending ? toggle.variables?.dogId : undefined} />}
    </div>
  )
}
