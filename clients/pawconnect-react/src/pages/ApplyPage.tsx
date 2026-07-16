import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, ArrowRight, Check, ClipboardCheck } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { api } from '@/api/client'
import { getErrorMessage } from '@/api/errors'
import { queryKeys } from '@/api/queryKeys'
import type { Application, CreateApplication, DogDetails } from '@/api/types'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ErrorState, PageLoader } from '@/components/ui/States'

const schema = z.object({
  reasonForAdoption: z.string().trim().min(20, 'Please write at least 20 characters.').max(2000),
  hoursAlonePerDay: z.coerce.number().int().min(0).max(24),
  preferredVisitDateTime: z.string().optional(),
  additionalInformation: z.string().max(2000).optional(),
})

type ApplyForm = z.input<typeof schema>

export function ApplyPage() {
  const dogId = Number(useParams().dogId)
  const [step, setStep] = useState(1)
  const navigate = useNavigate()
  const client = useQueryClient()
  const dogQuery = useQuery({ queryKey: queryKeys.dog(dogId), queryFn: ({ signal }) => api.get<DogDetails>(`/dogs/${dogId}`, signal), enabled: Number.isInteger(dogId) })
  const form = useForm<ApplyForm>({ resolver: zodResolver(schema), defaultValues: { reasonForAdoption: '', hoursAlonePerDay: 4, preferredVisitDateTime: '', additionalInformation: '' } })
  const submit = useMutation({
    mutationFn: (values: ApplyForm) => {
      const parsed = schema.parse(values)
      const request: CreateApplication = { ...parsed, preferredVisitDateTime: parsed.preferredVisitDateTime ? new Date(parsed.preferredVisitDateTime).toISOString() : null }
      return api.post<Application>(`/dogs/${dogId}/adoption-applications`, request)
    },
    onSuccess: async (application) => { await client.invalidateQueries({ queryKey: queryKeys.applications }); navigate(`/applications/${application.id}`) },
  })
  if (dogQuery.isLoading) return <PageLoader label="Preparing application" />
  if (dogQuery.isError || !dogQuery.data) return <div className="container page-stack"><ErrorState message={(dogQuery.error as Error)?.message || 'Dog not found.'} /></div>
  const dog = dogQuery.data
  return (
    <div className="container page-stack page-narrow">
      <Link className="back-link" to={`/dogs/${dogId}`}><ArrowLeft />Back to {dog.name}</Link>
      <header className="application-heading"><div><span className="eyebrow">Adoption application</span><h1>Apply for {dog.name}</h1><p>{dog.breed} · {dog.shelter?.name}</p></div><Badge tone={dog.status === 'Available' ? 'success' : 'warning'}>{dog.status}</Badge></header>
      <div className="stepper"><div className={step >= 1 ? 'is-active' : ''}><span>1</span><strong>Your routine</strong></div><div className={step >= 2 ? 'is-active' : ''}><span>2</span><strong>Review and submit</strong></div></div>
      <form onSubmit={form.handleSubmit((values) => submit.mutate(values))}>
        {step === 1 ? <Card className="form-card"><h2>Tell the shelter about your plans</h2><p>These answers help shelter staff prepare a useful conversation. They do not make an automatic decision.</p><div className="form-stack"><label className="field"><span>Why would you like to adopt {dog.name}?</span><textarea rows={6} {...form.register('reasonForAdoption')} />{form.formState.errors.reasonForAdoption && <small className="field-error">{form.formState.errors.reasonForAdoption.message}</small>}</label><label className="field"><span>How many hours would the dog usually be alone each day?</span><input type="number" min="0" max="24" {...form.register('hoursAlonePerDay')} />{form.formState.errors.hoursAlonePerDay && <small className="field-error">Enter a value from 0 to 24.</small>}</label><label className="field"><span>Preferred shelter visit (optional)</span><input type="datetime-local" {...form.register('preferredVisitDateTime')} /></label><label className="field"><span>Anything else the shelter should know? (optional)</span><textarea rows={4} {...form.register('additionalInformation')} /></label></div><div className="form-actions"><Button onClick={async () => (await form.trigger(['reasonForAdoption', 'hoursAlonePerDay'])) && setStep(2)}>Review application <ArrowRight /></Button></div></Card> : <Card className="form-card"><h2>Review before submitting</h2><div className="review-list"><div><span>Reason for adoption</span><p>{form.getValues('reasonForAdoption')}</p></div><div><span>Daily time alone</span><p>{String(form.getValues('hoursAlonePerDay'))} hours</p></div><div><span>Preferred visit</span><p>{form.getValues('preferredVisitDateTime') || 'No preference provided'}</p></div><div><span>Additional information</span><p>{form.getValues('additionalInformation') || 'None provided'}</p></div></div><div className="notice notice--info"><ClipboardCheck /><p>Submitting creates a real PawConnect adoption request for this dog. You can track it from My Applications.</p></div>{submit.isError && <p className="form-error" role="alert">{getErrorMessage(submit.error)}</p>}<div className="form-actions form-actions--spread"><Button variant="secondary" onClick={() => setStep(1)}><ArrowLeft />Edit answers</Button><Button type="submit" disabled={submit.isPending}><Check />{submit.isPending ? 'Submitting...' : 'Submit application'}</Button></div></Card>}
      </form>
    </div>
  )
}
