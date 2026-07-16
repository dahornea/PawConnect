import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bell, CheckCheck, MailOpen, Trash2 } from 'lucide-react'
import { Link } from 'react-router-dom'
import { api } from '@/api/client'
import { queryKeys } from '@/api/queryKeys'
import type { NotificationCenter, NotificationItem, NotificationPreference, UpdateNotificationPreference } from '@/api/types'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { PageHeader } from '@/components/ui/PageHeader'
import { EmptyState, ErrorState, PageLoader } from '@/components/ui/States'

export function NotificationsPage() {
  const [readState, setReadState] = useState('All')
  const client = useQueryClient()
  const query = useQuery({ queryKey: queryKeys.notifications(readState), queryFn: ({ signal }) => api.get<NotificationCenter>(`/notifications?readState=${readState}&count=100`, signal) })
  const preferences = useQuery({ queryKey: queryKeys.notificationPreferences, queryFn: ({ signal }) => api.get<NotificationPreference[]>('/notification-preferences', signal) })
  const refresh = async () => {
    await Promise.all([
      client.invalidateQueries({ queryKey: ['notifications'] }),
      client.invalidateQueries({ queryKey: queryKeys.unreadNotifications }),
    ])
  }
  const mark = useMutation({ mutationFn: ({ id, read }: { id: number; read: boolean }) => api.patch<void>(`/notifications/${id}/${read ? 'read' : 'unread'}`), onSuccess: refresh })
  const readAll = useMutation({ mutationFn: () => api.patch<void>('/notifications/read-all'), onSuccess: refresh })
  const dismiss = useMutation({ mutationFn: (id: number) => api.delete<void>(`/notifications/${id}`), onSuccess: refresh })
  const updatePreference = useMutation({ mutationFn: (request: UpdateNotificationPreference) => api.put<NotificationPreference>('/notification-preferences', request), onSuccess: async () => client.invalidateQueries({ queryKey: queryKeys.notificationPreferences }) })
  const groups = query.data?.groups?.filter((group) => (group.items?.length ?? 0) > 0) ?? []
  const items = groups.flatMap((group) => group.items ?? [])
  const actionFor = (item: NotificationItem) => {
    if (item.relatedEntityType === 'AdoptionRequest' && item.relatedEntityId) {
      return `/applications/${item.relatedEntityId}`
    }

    const relatedUrl = item.relatedUrl
    if (!relatedUrl?.startsWith('/')) return undefined

    const legacyApplication = relatedUrl.match(/^\/my-adoption-requests\/(\d+)/)
    if (legacyApplication) return `/applications/${legacyApplication[1]}`
    if (relatedUrl === '/my-adoption-requests') return '/applications'

    const adopterPortalRoutes = ['/dogs', '/favorites', '/saved-searches', '/applications', '/notifications', '/profile', '/insights', '/copilot']
    return adopterPortalRoutes.some((route) => relatedUrl === route || relatedUrl.startsWith(`${route}/`))
      ? relatedUrl
      : undefined
  }

  return (
    <div className="container page-stack">
      <PageHeader title="Notifications" description="Adoption, saved-search, and account updates in one place." action={<Button variant="secondary" onClick={() => readAll.mutate()} disabled={readAll.isPending || (query.data?.unreadCount ?? 0) === 0}><CheckCheck />Mark all read</Button>} />
      <div className="segmented-control" aria-label="Filter notifications">
        {['All', 'Unread', 'Read'].map((value) => <button key={value} className={readState === value ? 'is-active' : ''} onClick={() => setReadState(value)}>{value}</button>)}
      </div>
      {query.isLoading ? <PageLoader label="Loading notifications" /> : query.isError ? <ErrorState message={(query.error as Error).message} onRetry={() => query.refetch()} /> : items.length === 0 ? <EmptyState title="Nothing here" message={`There are no ${readState.toLowerCase()} notifications.`} /> : (
        <div className="notification-groups">
          {groups.map((group) => <section key={group.label || 'Notifications'} className="notification-group" aria-label={group.label || 'Notifications'}>
            <h2>{group.label || 'Notifications'}</h2>
            <div className="notification-list">
              {group.items?.map((item) => <Card key={item.id} className={`notification-item ${item.isRead ? '' : 'is-unread'}`}>
                <span className="notification-item__icon"><Bell /></span>
                <div>
                  <div className="notification-item__heading"><h3>{item.title}</h3><Badge tone={item.type === 'Warning' ? 'warning' : item.type === 'Error' ? 'danger' : item.type === 'Success' ? 'success' : 'info'}>{item.categoryLabel || item.category}</Badge></div>
                  <p>{item.message}</p><span>{item.relativeTime || item.createdAtUtc}</span>
                  {actionFor(item) && <Link className="text-link" to={actionFor(item)!}>{item.actionLabel || 'Open details'}</Link>}
                </div>
                <div className="notification-item__actions"><Button size="icon" variant="quiet" onClick={() => item.id && mark.mutate({ id: item.id, read: !item.isRead })} aria-label={item.isRead ? 'Mark unread' : 'Mark read'}><MailOpen /></Button><Button size="icon" variant="quiet" onClick={() => item.id && dismiss.mutate(item.id)} aria-label="Dismiss notification"><Trash2 /></Button></div>
              </Card>)}
            </div>
          </section>)}
        </div>
      )}
      <section className="preferences-section">
        <div><span className="eyebrow">Delivery controls</span><h2>Notification preferences</h2><p>In-app notifications remain available even when email is turned off.</p></div>
        {preferences.isLoading ? <PageLoader label="Loading preferences" /> : <div className="preference-list">{preferences.data?.map((preference) => <div className="preference-row" key={preference.notificationType}><div><strong>{preference.displayName}</strong><span>{preference.description}</span></div><label className="toggle"><input type="checkbox" checked={preference.inAppEnabled ?? false} onChange={(event) => updatePreference.mutate({ notificationType: preference.notificationType, inAppEnabled: event.target.checked, emailEnabled: preference.emailEnabled })} /><span />In app</label><label className="toggle"><input type="checkbox" checked={preference.emailEnabled ?? false} onChange={(event) => updatePreference.mutate({ notificationType: preference.notificationType, inAppEnabled: preference.inAppEnabled, emailEnabled: event.target.checked })} /><span />Email</label></div>)}</div>}
      </section>
    </div>
  )
}
