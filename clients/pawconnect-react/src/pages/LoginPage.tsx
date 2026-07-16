import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { LogIn, PawPrint } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { z } from 'zod'
import { api, clearApiSecurityContext } from '@/api/client'
import { getErrorMessage } from '@/api/errors'
import { queryKeys } from '@/api/queryKeys'
import type { UserSession } from '@/api/types'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'

const schema = z.object({ email: z.string().email('Enter a valid email address.'), password: z.string().min(1, 'Password is required.'), rememberMe: z.boolean() })
type LoginForm = z.infer<typeof schema>

export function LoginPage() {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const queryClient = useQueryClient()
  const form = useForm<LoginForm>({ resolver: zodResolver(schema), defaultValues: { email: 'adopter@mail.com', password: '', rememberMe: false } })
  const login = useMutation({
    mutationFn: (request: LoginForm) => api.post<UserSession>('/auth/login', request),
    onSuccess: (user) => {
      clearApiSecurityContext()
      queryClient.setQueryData(queryKeys.session, user)
      const returnTo = params.get('returnTo')
      navigate(returnTo?.startsWith('/') ? returnTo : '/dogs', { replace: true })
    },
  })

  return (
    <div className="auth-page">
      <Card className="auth-card">
        <div className="auth-card__mark"><PawPrint /></div>
        <span className="eyebrow">Adopter portal</span><h1>Welcome back</h1><p>Sign in with an adopter account to save dogs and manage applications.</p>
        <form onSubmit={form.handleSubmit((values) => login.mutate(values))} className="form-stack">
          <label className="field"><span>Email</span><input type="email" autoComplete="email" {...form.register('email')} />{form.formState.errors.email && <small className="field-error">{form.formState.errors.email.message}</small>}</label>
          <label className="field"><span>Password</span><input type="password" autoComplete="current-password" {...form.register('password')} />{form.formState.errors.password && <small className="field-error">{form.formState.errors.password.message}</small>}</label>
          <label className="check-field"><input type="checkbox" {...form.register('rememberMe')} /><span>Keep me signed in</span></label>
          {login.isError && <p className="form-error" role="alert">{getErrorMessage(login.error)}</p>}
          <Button type="submit" disabled={login.isPending}><LogIn />{login.isPending ? 'Signing in...' : 'Sign in'}</Button>
        </form>
        <p className="auth-card__footer">Need an account? <Link to="/dogs">Browse first, then register in the main PawConnect application.</Link></p>
      </Card>
    </div>
  )
}
