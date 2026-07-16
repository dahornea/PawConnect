import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, CalendarDays, FileText, Home, XCircle } from 'lucide-react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api } from '@/api/client'
import { getErrorMessage } from '@/api/errors'
import { queryKeys } from '@/api/queryKeys'
import type { Application } from '@/api/types'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ErrorState, PageLoader } from '@/components/ui/States'
import { formatDateTime, titleCase } from '@/utils/format'

export function ApplicationDetailsPage() {
  const id = Number(useParams().applicationId)
  const navigate = useNavigate()
  const client = useQueryClient()
  const query = useQuery({ queryKey: queryKeys.application(id), queryFn: ({ signal }) => api.get<Application>(`/adoption-applications/${id}`, signal), enabled: Number.isInteger(id) })
  const cancel = useMutation({ mutationFn: () => api.delete<void>(`/adoption-applications/${id}`), onSuccess: async () => { await client.invalidateQueries({ queryKey: queryKeys.applications }); navigate('/applications') } })
  if (query.isLoading) return <PageLoader label="Loading application" />
  if (query.isError || !query.data) return <div className="container page-stack"><ErrorState message={(query.error as Error)?.message || 'Application not found.'} /></div>
  const application = query.data
  const canCancel = application.status === 'Pending'
  return (
    <div className="container page-stack page-narrow">
      <Link className="back-link" to="/applications"><ArrowLeft />Back to applications</Link>
      <div className="application-heading"><div><span className="eyebrow">Application #{application.id}</span><h1>{application.dogName}</h1><p>{application.dogBreed} · {application.shelterName}</p></div><Badge tone={application.status === 'Accepted' ? 'success' : application.status === 'Rejected' || application.status === 'Cancelled' ? 'danger' : 'warning'}>{titleCase(application.status)}</Badge></div>
      <div className="timeline-strip"><div className="is-complete"><span>1</span><strong>Submitted</strong></div><div className={application.status === 'VisitConfirmed' || application.status === 'Accepted' ? 'is-complete' : ''}><span>2</span><strong>Visit</strong></div><div className={application.status === 'Accepted' || application.status === 'Rejected' ? 'is-complete' : ''}><span>3</span><strong>Decision</strong></div></div>
      <div className="detail-two-column">
        <Card className="detail-card"><h2><FileText />Your answers</h2><dl><dt>Reason for adoption</dt><dd>{application.reasonForAdoption}</dd><dt>Hours alone per day</dt><dd>{application.hoursAlonePerDay ?? 'Not specified'}</dd><dt>Additional information</dt><dd>{application.additionalInformation || 'No additional information provided.'}</dd></dl></Card>
        <Card className="detail-card"><h2><CalendarDays />Visit and status</h2><dl><dt>Preferred visit</dt><dd>{formatDateTime(application.preferredVisitDateTime)}</dd><dt>Visit status</dt><dd>{titleCase(application.visitStatus)}</dd><dt>Last updated</dt><dd>{formatDateTime(application.updatedAt)}</dd></dl><Link className="text-link" to={`/dogs/${application.dogId}`}>Open dog profile</Link></Card>
      </div>
      <div className="notice notice--info"><Home /><div><strong>The shelter makes the final decision</strong><p>PawConnect keeps the request and visit status visible, but compatibility is confirmed directly with shelter staff.</p></div></div>
      {canCancel && <div className="danger-zone"><div><h2>Withdraw this application</h2><p>Only pending applications can be cancelled.</p></div><Button variant="danger" onClick={() => window.confirm('Cancel this adoption application?') && cancel.mutate()} disabled={cancel.isPending}><XCircle />{cancel.isPending ? 'Cancelling...' : 'Cancel application'}</Button></div>}
      {cancel.isError && <p className="form-error" role="alert">{getErrorMessage(cancel.error)}</p>}
    </div>
  )
}
