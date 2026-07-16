import { useState, type FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import { BrainCircuit, Search, Sparkles } from 'lucide-react'
import { api } from '@/api/client'
import { getErrorMessage } from '@/api/errors'
import type { CopilotResponse } from '@/api/types'
import { DogCard } from '@/components/dogs/DogCard'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { PageHeader } from '@/components/ui/PageHeader'
import { EmptyState } from '@/components/ui/States'

const examples = [
  'I live in an apartment and want a calm medium-sized dog.',
  'I have a cat at home and enjoy daily walks.',
  'Show me an active dog for a house with a yard.',
]

export function CopilotPage() {
  const [message, setMessage] = useState(examples[0])
  const search = useMutation({ mutationFn: (prompt: string) => api.post<CopilotResponse>('/adoption-copilot/search', { message: prompt }) })
  const submit = (event: FormEvent) => { event.preventDefault(); if (message.trim()) search.mutate(message.trim()) }
  return (
    <div className="container page-stack">
      <PageHeader title="Adoption Copilot" description="Describe your situation naturally, then review matches grounded in real PawConnect dog profiles." />
      <section className="copilot-composer">
        <form onSubmit={submit}><label className="field"><span>What kind of dog are you looking for?</span><textarea value={message} onChange={(event) => setMessage(event.target.value)} rows={4} maxLength={1000} /></label><div className="copilot-composer__actions"><Button type="submit" disabled={search.isPending || !message.trim()}><Search />{search.isPending ? 'Finding matches...' : 'Ask Copilot'}</Button><span>Results are validated against public PawConnect dog data.</span></div></form>
        <div className="prompt-examples">{examples.map((example) => <button key={example} onClick={() => setMessage(example)}>{example}</button>)}</div>
      </section>
      {search.isError && <div className="notice notice--error" role="alert"><strong>Copilot could not complete the search</strong><p>{getErrorMessage(search.error)}</p></div>}
      {search.data && <>
        <section className="copilot-summary"><div className="copilot-summary__icon"><BrainCircuit /></div><div><span className="eyebrow">Copilot suggestions</span><h2>{search.data.assistantMessage || 'Here are the strongest matches from PawConnect.'}</h2><div className="chip-row"><Badge tone="info"><Sparkles />{search.data.usedAiEnhancement ? 'AI-assisted explanation' : 'Rules-based explanation'}</Badge>{search.data.usedSemanticSearch && <Badge tone="success">Semantic search</Badge>}{search.data.appliedConstraints?.map((item) => <Badge key={`${item.label}-${item.value}`}>{item.label}: {item.value}</Badge>)}</div></div></section>
        {(search.data.results ?? []).length === 0 ? <EmptyState title="No matching dogs" message="Try removing one strict requirement or using a broader location." /> : <div className="copilot-results">{search.data.results?.map((result) => result.dog && <article className="copilot-result" key={result.dog.id}><div className="copilot-result__score"><strong>{result.scorePercent}%</strong><span>{result.matchLabel}</span></div><DogCard dog={result.dog} /><div className="copilot-result__reason"><p>{result.suggestedNextAction || result.reasons?.[0]}</p><div className="chip-row">{result.displayTags?.map((tag) => <Badge tone="success" key={tag}>{tag}</Badge>)}{result.cautionTags?.map((tag) => <Badge tone="warning" key={tag}>{tag}</Badge>)}</div></div></article>)}</div>}
      </>}
    </div>
  )
}
