import { useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Filter, RotateCcw, Search, X } from 'lucide-react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { api } from '@/api/client'
import { queryKeys } from '@/api/queryKeys'
import type { DogListItem, DogPage, ShelterPage } from '@/api/types'
import { useAuth } from '@/auth/useAuth'
import { DogGrid } from '@/components/dogs/DogGrid'
import { Button } from '@/components/ui/Button'
import { ErrorState } from '@/components/ui/States'
import { useFavoriteIds, useToggleFavorite } from '@/features/favorites/useFavorites'
import { buildDogApiPath } from '@/features/dogs/searchParams'

const allowedParams = ['search', 'size', 'status', 'location', 'shelterId', 'coatColor', 'sort', 'page']
const filterLabels: Record<string, string> = {
  search: 'Search',
  size: 'Size',
  status: 'Status',
  location: 'Location',
  shelterId: 'Shelter',
  coatColor: 'Coat',
}

export function DogsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [filtersOpen, setFiltersOpen] = useState(false)
  const navigate = useNavigate()
  const { user } = useAuth()
  const queryString = searchParams.toString()
  const dogsQuery = useQuery({
    queryKey: queryKeys.dogs(queryString),
    queryFn: ({ signal }) => api.get<DogPage>(buildDogApiPath(searchParams), signal),
  })
  const sheltersQuery = useQuery({
    queryKey: queryKeys.shelters,
    queryFn: ({ signal }) => api.get<ShelterPage>('/shelters?page=1&pageSize=100', signal),
    staleTime: 5 * 60_000,
  })
  const favoriteIds = useFavoriteIds(Boolean(user))
  const toggleFavorite = useToggleFavorite()

  const applyFilters = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const next = new URLSearchParams()
    allowedParams.filter((key) => key !== 'page').forEach((key) => {
      const value = data.get(key)?.toString().trim()
      if (value) next.set(key, value)
    })
    setSearchParams(next)
    setFiltersOpen(false)
  }

  const handleFavorite = (dog: DogListItem) => {
    if (!user) {
      navigate(`/login?returnTo=${encodeURIComponent(`/dogs?${queryString}`)}`)
      return
    }
    if (!dog.id) return
    toggleFavorite.mutate({ dogId: dog.id, favorite: favoriteIds.data?.includes(dog.id) ?? false })
  }

  const setPage = (page: number) => {
    const next = new URLSearchParams(searchParams)
    if (page <= 1) next.delete('page'); else next.set('page', String(page))
    setSearchParams(next)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const currentPage = Number(searchParams.get('page') || 1)
  const activeFilters = Object.entries(filterLabels)
    .map(([key, label]) => ({ key, label, value: searchParams.get(key) }))
    .filter((filter): filter is { key: string; label: string; value: string } => Boolean(filter.value))

  const removeFilter = (key: string) => {
    const next = new URLSearchParams(searchParams)
    next.delete(key)
    next.delete('page')
    setSearchParams(next)
  }

  const clearFilters = () => {
    setSearchParams({})
    setFiltersOpen(false)
  }

  return (
    <div className="container page-stack">
      <header className="page-header"><div><span className="eyebrow">Public dog discovery</span><h1>Find your next companion</h1><p>Every result comes from PawConnect's public-safe shelter records.</p></div></header>

      <div className="filter-summary">
        <Button className="filter-toggle" variant="secondary" onClick={() => setFiltersOpen((open) => !open)} aria-expanded={filtersOpen}>
          <Filter />Filters{activeFilters.length > 0 && <span>{activeFilters.length}</span>}
        </Button>
        {activeFilters.length > 0 && <div className="active-filters" aria-label="Active dog filters">
          {activeFilters.map((filter) => <button type="button" className="active-filter" key={filter.key} onClick={() => removeFilter(filter.key)} aria-label={`Remove ${filter.label.toLowerCase()} filter`}>
            <span>{filter.label}: {filter.value}</span><X aria-hidden="true" />
          </button>)}
          <button type="button" className="clear-filter-link" onClick={clearFilters}>Clear all</button>
        </div>}
      </div>

      <form key={queryString} className={`filter-panel ${filtersOpen ? 'filter-panel--open' : ''}`} onSubmit={applyFilters}>
        <label className="field field--wide"><span>Search</span><div className="input-with-icon"><Search /><input name="search" defaultValue={searchParams.get('search') ?? ''} placeholder="Name, breed, or description" /></div></label>
        <label className="field"><span>Size</span><select name="size" defaultValue={searchParams.get('size') ?? ''}><option value="">Any size</option><option>Small</option><option>Medium</option><option>Large</option></select></label>
        <label className="field"><span>Status</span><select name="status" defaultValue={searchParams.get('status') ?? ''}><option value="">Available or reserved</option><option>Available</option><option>Reserved</option></select></label>
        <label className="field"><span>Shelter</span><select name="shelterId" defaultValue={searchParams.get('shelterId') ?? ''}><option value="">All shelters</option>{(sheltersQuery.data?.items ?? []).map((shelter) => <option key={shelter.id} value={shelter.id}>{shelter.name}</option>)}</select></label>
        <label className="field"><span>Coat color</span><input name="coatColor" defaultValue={searchParams.get('coatColor') ?? ''} placeholder="e.g. Black and tan" /></label>
        <label className="field"><span>Location</span><input name="location" defaultValue={searchParams.get('location') ?? ''} placeholder="City or neighborhood" /></label>
        <label className="field"><span>Sort by</span><select name="sort" defaultValue={searchParams.get('sort') ?? 'NameAsc'}><option value="NameAsc">Name A-Z</option><option value="NewestFirst">Newest first</option><option value="AgeAsc">Youngest first</option><option value="BreedAsc">Breed</option></select></label>
        <div className="filter-panel__actions"><Button type="submit"><Filter />Apply filters</Button><Button variant="quiet" onClick={clearFilters}><RotateCcw />Clear</Button></div>
      </form>

      <div className="results-toolbar"><strong>{dogsQuery.data?.totalCount ?? 0} dogs</strong><span>Page {currentPage} of {Math.max(dogsQuery.data?.totalPages ?? 1, 1)}</span></div>
      {dogsQuery.isError
        ? <ErrorState message={(dogsQuery.error as Error).message} onRetry={() => dogsQuery.refetch()} />
        : <DogGrid dogs={dogsQuery.data?.items ?? []} isLoading={dogsQuery.isLoading} favorites={favoriteIds.data} onFavorite={handleFavorite} favoriteBusyId={toggleFavorite.isPending ? toggleFavorite.variables?.dogId : undefined} />}
      {(dogsQuery.data?.totalPages ?? 0) > 1 && <nav className="pagination" aria-label="Dog results pages"><Button variant="secondary" disabled={currentPage <= 1} onClick={() => setPage(currentPage - 1)}>Previous</Button><span>{currentPage} / {dogsQuery.data?.totalPages}</span><Button variant="secondary" disabled={currentPage >= (dogsQuery.data?.totalPages ?? 1)} onClick={() => setPage(currentPage + 1)}>Next</Button></nav>}
    </div>
  )
}
