import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bell, BellOff, Plus, RefreshCw, Search, Trash2 } from 'lucide-react'
import { Link } from 'react-router-dom'
import { api } from '@/api/client'
import { getErrorMessage } from '@/api/errors'
import { queryKeys } from '@/api/queryKeys'
import type { SavedSearch, SavedSearchCreate, SavedSearchDetails } from '@/api/types'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { PageHeader } from '@/components/ui/PageHeader'
import { EmptyState, ErrorState, PageLoader } from '@/components/ui/States'
import { formatDateTime } from '@/utils/format'

export function SavedSearchesPage() {
  const [creating, setCreating] = useState(false)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const client = useQueryClient()
  const query = useQuery({ queryKey: queryKeys.savedSearches, queryFn: ({ signal }) => api.get<SavedSearch[]>('/saved-searches', signal) })
  const details = useQuery({ queryKey: queryKeys.savedSearch(selectedId ?? 0), queryFn: ({ signal }) => api.get<SavedSearchDetails>(`/saved-searches/${selectedId}`, signal), enabled: selectedId !== null })
  const create = useMutation({ mutationFn: (request: SavedSearchCreate) => api.post<SavedSearch>('/saved-searches', request), onSuccess: async () => { setCreating(false); await client.invalidateQueries({ queryKey: queryKeys.savedSearches }) } })
  const remove = useMutation({ mutationFn: (id: number) => api.delete<void>(`/saved-searches/${id}`), onSuccess: async () => { setSelectedId(null); await client.invalidateQueries({ queryKey: queryKeys.savedSearches }) } })
  const evaluate = useMutation({ mutationFn: (id: number) => api.post<SavedSearchDetails>(`/saved-searches/${id}/evaluate`), onSuccess: async (data) => { if (data.search?.id) setSelectedId(data.search.id); await client.invalidateQueries({ queryKey: queryKeys.savedSearches }) } })
  const alerts = useMutation({ mutationFn: ({ id, enabled }: { id: number; enabled: boolean }) => api.patch<void>(`/saved-searches/${id}/alerts`, { enabled }), onSuccess: async () => client.invalidateQueries({ queryKey: queryKeys.savedSearches }) })

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    create.mutate({
      name: String(data.get('name') || '').trim(),
      alertsEnabled: data.get('alertsEnabled') === 'on',
      alertFrequency: data.get('alertsEnabled') === 'on' ? 'DailyDigest' : 'Disabled',
      criteria: {
        searchText: String(data.get('searchText') || '').trim() || null,
        size: (String(data.get('size') || '') || undefined) as 'Small' | 'Medium' | 'Large' | undefined,
        coatColor: String(data.get('coatColor') || '').trim() || null,
        location: String(data.get('location') || '').trim() || null,
        status: 'Available',
        sortOption: 'NewestFirst',
      },
    })
  }

  return (
    <div className="container page-stack">
      <PageHeader title="Saved searches" description="Keep useful filters and let PawConnect check for new public matches." action={<Button onClick={() => setCreating((value) => !value)}><Plus />New saved search</Button>} />
      {creating && <Card className="form-card"><form className="saved-search-form" onSubmit={submit}><label className="field"><span>Name</span><input name="name" required maxLength={100} placeholder="Small apartment-friendly dogs" /></label><label className="field"><span>Keywords</span><input name="searchText" placeholder="calm, friendly, Labrador" /></label><label className="field"><span>Size</span><select name="size"><option value="">Any size</option><option>Small</option><option>Medium</option><option>Large</option></select></label><label className="field"><span>Coat color</span><input name="coatColor" placeholder="Black and tan" /></label><label className="field"><span>Location</span><input name="location" placeholder="Cluj-Napoca" /></label><label className="check-field"><input type="checkbox" name="alertsEnabled" defaultChecked /><span>Notify me about new matches</span></label><div className="form-actions"><Button variant="quiet" onClick={() => setCreating(false)}>Cancel</Button><Button type="submit" disabled={create.isPending}><Search />{create.isPending ? 'Saving...' : 'Save search'}</Button></div>{create.isError && <p className="form-error">{getErrorMessage(create.error)}</p>}</form></Card>}
      {query.isLoading ? <PageLoader label="Loading saved searches" /> : query.isError ? <ErrorState message={(query.error as Error).message} onRetry={() => query.refetch()} /> : (query.data ?? []).length === 0 ? <EmptyState title="No saved searches" message="Save a focused set of criteria to revisit it later." action={<Button onClick={() => setCreating(true)}>Create a saved search</Button>} /> : <div className="saved-search-layout"><div className="saved-search-list">{query.data?.map((item) => <Card key={item.id} className={`saved-search-card ${selectedId === item.id ? 'is-selected' : ''}`}><button className="saved-search-card__main" onClick={() => setSelectedId(item.id ?? null)}><div><h2>{item.name}</h2><div className="chip-row">{item.criteriaLabels?.map((label) => <Badge key={label}>{label}</Badge>)}</div></div><div className="saved-search-card__counts"><strong>{item.totalMatches ?? 0}</strong><span>matches</span>{(item.newMatches ?? 0) > 0 && <Badge tone="success">{item.newMatches} new</Badge>}</div></button><footer><span>Checked {formatDateTime(item.lastEvaluatedAtUtc)}</span><div><Button size="icon" variant="quiet" onClick={() => item.id && alerts.mutate({ id: item.id, enabled: !item.alertsEnabled })} aria-label={item.alertsEnabled ? 'Disable alerts' : 'Enable alerts'}>{item.alertsEnabled ? <Bell /> : <BellOff />}</Button><Button size="icon" variant="quiet" onClick={() => item.id && evaluate.mutate(item.id)} aria-label="Check for matches"><RefreshCw /></Button><Button size="icon" variant="quiet" onClick={() => item.id && window.confirm('Delete this saved search?') && remove.mutate(item.id)} aria-label="Delete saved search"><Trash2 /></Button></div></footer></Card>)}</div><aside className="saved-search-detail">{selectedId === null ? <div className="state-panel"><Search /><h2>Select a saved search</h2><p>Its latest matching dogs will appear here.</p></div> : details.isLoading ? <PageLoader label="Loading matches" /> : details.isError ? <ErrorState message={(details.error as Error).message} /> : <><h2>Latest matches</h2><p>{details.data?.matches?.length ?? 0} currently matched dogs.</p><div className="match-list">{details.data?.matches?.slice(0, 8).map((match) => <Link key={match.id} to={`/dogs/${match.dogId}`}><div><strong>{match.dogName}</strong><span>{match.breedText} · {match.ageText}</span></div>{match.statusInSearch === 'New' && <Badge tone="success">New</Badge>}</Link>)}</div></>}</aside></div>}
    </div>
  )
}
