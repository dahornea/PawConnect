import { useQuery } from '@tanstack/react-query'
import { ArrowRight, CheckCircle2, Lightbulb, ShieldAlert } from 'lucide-react'
import { Link } from 'react-router-dom'
import { api } from '@/api/client'
import { queryKeys } from '@/api/queryKeys'
import type { InsightPage } from '@/api/types'
import { Badge } from '@/components/ui/Badge'
import { Card } from '@/components/ui/Card'
import { PageHeader } from '@/components/ui/PageHeader'
import { EmptyState, ErrorState, PageLoader } from '@/components/ui/States'
import { titleCase } from '@/utils/format'

export function InsightsPage() {
  const query = useQuery({ queryKey: queryKeys.insights, queryFn: ({ signal }) => api.get<InsightPage>('/adopter/insights', signal) })
  return (
    <div className="container page-stack">
      <PageHeader title="My PawConnect insights" description="Explainable next steps generated from your own adopter activity and real PawConnect records." />
      <div className="notice notice--info"><Lightbulb /><div><strong>These are guidance, not automatic decisions</strong><p>Each insight includes the evidence and recommendation that PawConnect used.</p></div></div>
      {query.isLoading ? <PageLoader label="Loading insights" /> : query.isError ? <ErrorState message={(query.error as Error).message} onRetry={() => query.refetch()} /> : (query.data?.items ?? []).length === 0 ? <EmptyState title="No active insights" message="Keep your profile current and interact with dogs to receive useful next steps." /> : <div className="insight-grid">{query.data?.items?.map((insight) => <Card className="insight-card" key={insight.id}><div className="insight-card__heading"><span className={`insight-icon insight-icon--${(insight.severity || 'Informational').toLowerCase()}`}>{insight.severity === 'High' || insight.severity === 'Critical' ? <ShieldAlert /> : <Lightbulb />}</span><div><div className="chip-row"><Badge tone={insight.severity === 'High' || insight.severity === 'Critical' ? 'danger' : insight.severity === 'Medium' ? 'warning' : 'info'}>{titleCase(insight.severity)}</Badge><Badge>{titleCase(insight.category)}</Badge></div><h2>{insight.title}</h2></div></div><p>{insight.summary}</p>{insight.explanation && <div className="insight-card__explanation"><strong>Why this appears</strong><p>{insight.explanation}</p></div>}{insight.evidence?.length ? <ul className="evidence-list">{insight.evidence.slice(0, 3).map((item) => <li key={item}><CheckCircle2 />{item}</li>)}</ul> : null}<footer>{insight.recommendedActions?.filter((action) => action.isAvailable && action.route?.startsWith('/')).slice(0, 1).map((action) => <Link key={action.key} className="text-link" to={action.route!}>{action.label}<ArrowRight /></Link>)}<span>Priority {insight.priorityScore ?? 0}</span></footer></Card>)}</div>}
    </div>
  )
}
