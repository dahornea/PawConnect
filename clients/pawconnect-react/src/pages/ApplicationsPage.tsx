import { useQuery } from '@tanstack/react-query'
import { ArrowRight, CalendarDays, ClipboardList } from 'lucide-react'
import { Link } from 'react-router-dom'
import { api } from '@/api/client'
import { queryKeys } from '@/api/queryKeys'
import type { ApplicationPage } from '@/api/types'
import { Badge } from '@/components/ui/Badge'
import { Card } from '@/components/ui/Card'
import { PageHeader } from '@/components/ui/PageHeader'
import { EmptyState, ErrorState, PageLoader } from '@/components/ui/States'
import { formatDateTime, titleCase } from '@/utils/format'

export function ApplicationsPage() {
  const query = useQuery({ queryKey: queryKeys.applications, queryFn: ({ signal }) => api.get<ApplicationPage>('/adoption-applications?page=1&pageSize=50', signal) })
  const applications = query.data?.items ?? []
  return (
    <div className="container page-stack">
      <PageHeader title="My adoption applications" description="Track each request from submission through the shelter's decision." action={<Link className="button button--primary button--md" to="/dogs">Find a dog</Link>} />
      {query.isLoading ? <PageLoader label="Loading applications" /> : query.isError ? <ErrorState message={(query.error as Error).message} onRetry={() => query.refetch()} /> : applications.length === 0 ? <EmptyState title="No applications yet" message="When you are ready, open a dog profile and start an application." action={<Link className="button button--primary button--md" to="/dogs">Browse dogs</Link>} /> : (
        <div className="application-list">
          {applications.map((application) => <Card className="application-row" key={application.id}>
            <span className="application-row__icon"><ClipboardList /></span>
            <div className="application-row__main"><div><h2>{application.dogName}</h2><p>{application.dogBreed} · {application.shelterName}</p></div><div className="application-row__badges"><Badge tone={application.status === 'Accepted' ? 'success' : application.status === 'Rejected' || application.status === 'Cancelled' ? 'danger' : 'warning'}>{titleCase(application.status)}</Badge>{application.visitStatus && application.visitStatus !== 'NotScheduled' && <Badge tone="info">Visit: {titleCase(application.visitStatus)}</Badge>}</div></div>
            <div className="application-row__date"><CalendarDays /><span>{application.preferredVisitDateTime ? formatDateTime(application.preferredVisitDateTime) : `Submitted ${formatDateTime(application.createdAt)}`}</span></div>
            <Link className="text-link" to={`/applications/${application.id}`}>View application <ArrowRight /></Link>
          </Card>)}
        </div>
      )}
    </div>
  )
}
