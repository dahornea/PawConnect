import type { DogListItem } from '@/api/types'
import { DogCard } from '@/components/dogs/DogCard'
import { CardSkeleton, EmptyState } from '@/components/ui/States'

export function DogGrid({ dogs, isLoading, favorites = [], onFavorite, favoriteBusyId }: {
  dogs: DogListItem[]
  isLoading?: boolean
  favorites?: number[]
  onFavorite?: (dog: DogListItem) => void
  favoriteBusyId?: number
}) {
  if (isLoading) return <div className="dog-grid">{Array.from({ length: 6 }, (_, index) => <CardSkeleton key={index} />)}</div>
  if (dogs.length === 0) return <EmptyState title="No dogs found" message="Try changing the filters or clearing the search." />
  return (
    <div className="dog-grid">
      {dogs.map((dog) => (
        <DogCard
          key={dog.id}
          dog={dog}
          favorite={favorites.includes(dog.id ?? -1)}
          onFavorite={onFavorite ? () => onFavorite(dog) : undefined}
          favoriteBusy={favoriteBusyId === dog.id}
        />
      ))}
    </div>
  )
}
