import { useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Save, UserRound } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { api } from '@/api/client'
import { getErrorMessage } from '@/api/errors'
import { queryKeys } from '@/api/queryKeys'
import type { AdopterProfile, UpdateAdopterProfile } from '@/api/types'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { PageHeader } from '@/components/ui/PageHeader'
import { ErrorState, PageLoader } from '@/components/ui/States'

export function ProfilePage() {
  const client = useQueryClient()
  const query = useQuery({ queryKey: queryKeys.profile, queryFn: ({ signal }) => api.get<AdopterProfile>('/adopter/profile', signal) })
  const form = useForm<UpdateAdopterProfile>({ defaultValues: { fullName: '', city: 'Cluj-Napoca', housingType: 'Apartment', hasYard: false, hasOtherPets: false, hasChildren: false } })
  useEffect(() => {
    if (!query.data) return
    form.reset({
      fullName: query.data.fullName || '', profileImageUrl: query.data.profileImageUrl,
      address: query.data.address, city: query.data.city || '', phoneNumber: query.data.phoneNumber,
      housingType: query.data.housingType, hasYard: query.data.hasYard,
      hasOtherPets: query.data.hasOtherPets, hasChildren: query.data.hasChildren,
      experienceWithDogs: query.data.experienceWithDogs, additionalNotes: query.data.additionalNotes,
    })
  }, [query.data, form])
  const save = useMutation({ mutationFn: (request: UpdateAdopterProfile) => api.put<AdopterProfile>('/adopter/profile', request), onSuccess: async (profile) => { form.reset(profile as UpdateAdopterProfile); await client.invalidateQueries({ queryKey: queryKeys.profile }) } })
  if (query.isLoading) return <PageLoader label="Loading profile" />
  if (query.isError) return <div className="container page-stack"><ErrorState message={(query.error as Error).message} onRetry={() => query.refetch()} /></div>
  return (
    <div className="container page-stack page-narrow">
      <PageHeader title="Adopter profile" description="Keep the household details used by recommendation and adoption workflows current." />
      <form onSubmit={form.handleSubmit((values) => save.mutate(values))}>
        <Card className="form-card">
          <h2><UserRound />Contact and household</h2>
          <div className="form-grid"><label className="field"><span>Full name</span><input required maxLength={150} {...form.register('fullName')} /></label><label className="field"><span>Phone number</span><input type="tel" maxLength={30} {...form.register('phoneNumber')} /></label><label className="field field--wide"><span>Address</span><input maxLength={250} {...form.register('address')} /></label><label className="field"><span>City</span><input required maxLength={100} {...form.register('city')} /></label><label className="field"><span>Housing type</span><select {...form.register('housingType')}><option>Apartment</option><option>House</option><option>Other</option></select></label></div>
          <div className="choice-row"><label className="check-field"><input type="checkbox" {...form.register('hasYard')} /><span>I have a yard</span></label><label className="check-field"><input type="checkbox" {...form.register('hasOtherPets')} /><span>I have other pets</span></label><label className="check-field"><input type="checkbox" {...form.register('hasChildren')} /><span>Children live at home</span></label></div>
          <div className="form-stack"><label className="field"><span>Experience with dogs</span><textarea rows={4} maxLength={1000} {...form.register('experienceWithDogs')} /></label><label className="field"><span>Additional notes</span><textarea rows={4} maxLength={1000} {...form.register('additionalNotes')} /></label></div>
          {save.isError && <p className="form-error">{getErrorMessage(save.error)}</p>}{save.isSuccess && <p className="form-success">Profile saved.</p>}
          <div className="form-actions"><Button type="submit" disabled={save.isPending || !form.formState.isDirty}><Save />{save.isPending ? 'Saving...' : 'Save profile'}</Button></div>
        </Card>
      </form>
    </div>
  )
}
