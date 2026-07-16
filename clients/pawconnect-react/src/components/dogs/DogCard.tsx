import { ArrowRight, Heart, MapPin } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { DogListItem } from '@/api/types'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { DogImage } from '@/components/dogs/DogImage'

export function DogCard({ dog, favorite, onFavorite, favoriteBusy = false }: {
  dog: DogListItem
  favorite?: boolean
  onFavorite?: () => void
  favoriteBusy?: boolean
}) {
  const id = dog.id ?? 0
  return (
    <article className="dog-card">
      <Link className="dog-card__image" to={`/dogs/${id}`} aria-label={`View ${dog.name || 'dog'}`}>
        <DogImage src={dog.mainImageUrl ?? undefined} alt={dog.name ? `${dog.name}, ${dog.breed || 'dog'}` : 'Dog available for adoption'} loading="lazy" />
      </Link>
      <div className="dog-card__body">
        <div className="dog-card__title-row">
          <div><h2><Link to={`/dogs/${id}`}>{dog.name || 'Unnamed dog'}</Link></h2><p>{dog.breed || 'Mixed breed'} · {dog.ageDisplay || 'Age not listed'}</p></div>
          <Badge tone={dog.status === 'Available' ? 'success' : 'warning'}>{dog.status || 'Unknown'}</Badge>
        </div>
        <p className="dog-card__location"><MapPin aria-hidden="true" />{dog.shelterName || 'PawConnect shelter'} · {dog.location || dog.shelterNeighborhood || 'Cluj-Napoca'}</p>
        <p className="dog-card__description">{dog.shortDescription || 'Open this profile to learn more about this dog.'}</p>
        <div className="dog-card__meta">
          {dog.size && <Badge>{dog.size}</Badge>}
          {dog.coatColor && <Badge>{dog.coatColor}</Badge>}
        </div>
      </div>
      <footer className="dog-card__footer">
        <Link className="text-link" to={`/dogs/${id}`}>View details <ArrowRight aria-hidden="true" /></Link>
        {onFavorite && (
          <Button variant="quiet" size="sm" onClick={onFavorite} disabled={favoriteBusy} aria-label={favorite ? `Remove ${dog.name} from favorites` : `Save ${dog.name} to favorites`}>
            <Heart aria-hidden="true" className={favorite ? 'heart--active' : undefined} />{favorite ? 'Saved' : 'Save'}
          </Button>
        )}
      </footer>
    </article>
  )
}
