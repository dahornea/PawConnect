import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, ChevronLeft, ChevronRight, Heart, Mail, MapPin, Phone, X } from 'lucide-react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api } from '@/api/client'
import { queryKeys } from '@/api/queryKeys'
import type { DogDetails } from '@/api/types'
import { useAuth } from '@/auth/useAuth'
import { DogImage } from '@/components/dogs/DogImage'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ErrorState, PageLoader } from '@/components/ui/States'
import { useFavoriteIds, useToggleFavorite } from '@/features/favorites/useFavorites'
import { formatDate, titleCase } from '@/utils/format'

export function DogDetailsPage() {
  const dogId = Number(useParams().dogId)
  const { user } = useAuth()
  const navigate = useNavigate()
  const [selectedImage, setSelectedImage] = useState(0)
  const [lightboxOpen, setLightboxOpen] = useState(false)
  const dogQuery = useQuery({ queryKey: queryKeys.dog(dogId), queryFn: ({ signal }) => api.get<DogDetails>(`/dogs/${dogId}`, signal), enabled: Number.isInteger(dogId) })
  const favorites = useFavoriteIds(Boolean(user))
  const toggleFavorite = useToggleFavorite()

  if (dogQuery.isLoading) return <PageLoader label="Loading dog profile" />
  if (dogQuery.isError || !dogQuery.data) return <div className="container page-stack"><ErrorState title="Dog profile unavailable" message={(dogQuery.error as Error)?.message || 'This dog could not be found.'} onRetry={() => dogQuery.refetch()} /></div>

  const dog = dogQuery.data
  const images = (dog.images ?? []).filter((image) => Boolean(image.imageUrl))
  const currentImage = images[Math.min(selectedImage, Math.max(images.length - 1, 0))]
  const isFavorite = favorites.data?.includes(dogId) ?? false
  const changeImage = (direction: number) => setSelectedImage((index) => (index + direction + images.length) % images.length)
  const handleFavorite = () => {
    if (!user) return navigate(`/login?returnTo=/dogs/${dogId}`)
    toggleFavorite.mutate({ dogId, favorite: isFavorite })
  }

  return (
    <div className="container page-stack">
      <Link className="back-link" to="/dogs"><ArrowLeft />Back to dogs</Link>
      <div className="dog-detail-grid">
        <section className="gallery" aria-label={`${dog.name} photos`}>
          <button className="gallery__main" onClick={() => images.length > 0 && setLightboxOpen(true)} disabled={images.length === 0} aria-label={images.length > 0 ? 'Open image preview' : 'No image available'}>
            <DogImage src={currentImage?.imageUrl ?? undefined} alt={`${dog.name || 'Dog'} photo ${selectedImage + 1}`} />
            {images.length > 1 && <span className="gallery__counter">{selectedImage + 1} / {images.length}</span>}
          </button>
          {images.length > 1 && <div className="gallery__thumbs">{images.map((image, index) => <button key={image.id ?? index} className={index === selectedImage ? 'is-active' : ''} onClick={() => setSelectedImage(index)} aria-label={`View photo ${index + 1}`}><DogImage src={image.imageUrl ?? undefined} alt="" /></button>)}</div>}
        </section>

        <section className="dog-profile">
          <div className="dog-profile__heading"><div><span className="eyebrow">{dog.shelter?.name || 'PawConnect shelter'}</span><h1>{dog.name}</h1><p>{dog.breed} · {dog.ageDisplay}</p></div><Badge tone={dog.status === 'Available' ? 'success' : 'warning'}>{dog.status}</Badge></div>
          <div className="profile-actions"><Link className="button button--primary button--md" to={user ? `/dogs/${dogId}/apply` : `/login?returnTo=/dogs/${dogId}/apply`}>Start application</Link><Button variant="secondary" onClick={handleFavorite} disabled={toggleFavorite.isPending}><Heart className={isFavorite ? 'heart--active' : undefined} />{isFavorite ? 'Saved' : 'Save dog'}</Button></div>
          <div className="fact-grid"><div><span>Size</span><strong>{dog.size}</strong></div><div><span>Coat</span><strong>{dog.coatColor || 'Not listed'}</strong></div><div><span>Activity</span><strong>{titleCase(dog.activityLevel)}</strong></div><div><span>Apartment fit</span><strong>{titleCase(dog.apartmentSuitability)}</strong></div></div>
          <div className="prose-section"><h2>About {dog.name}</h2><p>{dog.description || 'The shelter has not added a full description yet.'}</p>{dog.behaviorDescription && <p>{dog.behaviorDescription}</p>}</div>
          <div className="compatibility-grid">
            <Card><span>Cats</span><strong>{titleCase(dog.catCompatibility)}</strong></Card>
            <Card><span>Other dogs</span><strong>{titleCase(dog.dogCompatibility)}</strong></Card>
            <Card><span>Children</span><strong>{titleCase(dog.childrenCompatibility)}</strong></Card>
            <Card><span>Experience</span><strong>{titleCase(dog.experienceNeeded)}</strong></Card>
          </div>
          {dog.compatibilityNotes && <div className="notice notice--info"><strong>Compatibility notes</strong><p>{dog.compatibilityNotes}</p></div>}
        </section>
      </div>

      <section className="detail-band">
        <div><span className="eyebrow">Health and care</span><h2>Practical information</h2><p>{dog.medicalStatus || 'Ask the shelter for the latest health information.'}</p>{dog.preferredFood && <p><strong>Food:</strong> {dog.preferredFood.foodTypeName || 'Not specified'}{dog.preferredFood.dailyAmountGrams ? `, ${dog.preferredFood.dailyAmountGrams} g daily` : ''}</p>}</div>
        <div><h3>Recent records</h3>{(dog.medicalRecords ?? []).length ? <ul className="plain-list">{dog.medicalRecords?.slice(0, 4).map((record) => <li key={record.id}><strong>{record.vaccineName || record.treatmentDescription || 'Medical record'}</strong><span>{formatDate(record.recordDate)}</span></li>)}</ul> : <p>No public medical records listed.</p>}</div>
      </section>

      {dog.breedInformation?.length ? <section className="detail-band"><div><span className="eyebrow">Breed information</span><h2>{dog.breedInformation.map((breed) => breed.name).join(' and ')}</h2><p>{dog.breedInformation[0].generalDescription}</p></div><div><h3>Care considerations</h3><p>{dog.breedInformation[0].careNotes || dog.breedInformation[0].commonHealthConsiderations}</p></div></section> : null}

      <section className="shelter-band"><div><MapPin /><div><span className="eyebrow">Shelter</span><h2>{dog.shelter?.name}</h2><p>{[dog.shelter?.neighborhood, dog.shelter?.city].filter(Boolean).join(', ')}</p></div></div><div className="shelter-contact">{dog.shelter?.email && <a href={`mailto:${dog.shelter.email}`}><Mail />{dog.shelter.email}</a>}{dog.shelter?.phoneNumber && <a href={`tel:${dog.shelter.phoneNumber}`}><Phone />{dog.shelter.phoneNumber}</a>}</div></section>

      {lightboxOpen && <div className="lightbox" role="dialog" aria-modal="true" aria-label={`${dog.name} image preview`} onClick={() => setLightboxOpen(false)}><Button className="lightbox__close" size="icon" variant="secondary" onClick={() => setLightboxOpen(false)} aria-label="Close preview"><X /></Button>{images.length > 1 && <Button className="lightbox__previous" size="icon" variant="secondary" onClick={(event) => { event.stopPropagation(); changeImage(-1) }} aria-label="Previous image"><ChevronLeft /></Button>}<div className="lightbox__image" onClick={(event) => event.stopPropagation()}><DogImage src={currentImage?.imageUrl ?? undefined} alt={`${dog.name} enlarged photo`} /><span>{selectedImage + 1} / {images.length}</span></div>{images.length > 1 && <Button className="lightbox__next" size="icon" variant="secondary" onClick={(event) => { event.stopPropagation(); changeImage(1) }} aria-label="Next image"><ChevronRight /></Button>}</div>}
    </div>
  )
}
