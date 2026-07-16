import { Suspense, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  Bell, BookHeart, BrainCircuit, ClipboardList, Dog, Heart, Home,
  Lightbulb, LogIn, LogOut, Menu, PawPrint, Search, UserRound, X,
} from 'lucide-react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { api } from '@/api/client'
import { queryKeys } from '@/api/queryKeys'
import { useAuth } from '@/auth/useAuth'
import { Button } from '@/components/ui/Button'
import { PageLoader } from '@/components/ui/States'
import { env } from '@/app/env'
import { cn } from '@/utils/cn'

const adopterLinks = [
  { to: '/favorites', label: 'Favorites', icon: Heart },
  { to: '/saved-searches', label: 'Saved searches', icon: Search },
  { to: '/applications', label: 'Applications', icon: ClipboardList },
  ...(env.copilotEnabled ? [{ to: '/copilot', label: 'Adoption Copilot', icon: BrainCircuit }] : []),
  { to: '/insights', label: 'My insights', icon: Lightbulb },
]

export function AppShell() {
  const [open, setOpen] = useState(false)
  const { user, logout } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const unreadQuery = useQuery({
    queryKey: queryKeys.unreadNotifications,
    queryFn: ({ signal }) => api.get<{ count?: number }>('/notifications/unread-count', signal),
    enabled: Boolean(user),
    refetchInterval: 60_000,
  })

  const signOut = async () => {
    navigate('/', { replace: true })
    await logout()
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="topbar__inner">
          <Link className="brand" to="/" aria-label="PawConnect home">
            <span className="brand__mark"><PawPrint aria-hidden="true" /></span>
            <span>PawConnect</span>
          </Link>
          <nav className="desktop-nav" aria-label="Primary navigation">
            <NavLink to="/" end><Home aria-hidden="true" />Home</NavLink>
            <NavLink to="/dogs"><Dog aria-hidden="true" />Discover dogs</NavLink>
            {user && adopterLinks.slice(0, 3).map(({ to, label, icon: Icon }) => (
              <NavLink key={to} to={to}><Icon aria-hidden="true" />{label}</NavLink>
            ))}
          </nav>
          <div className="topbar__actions">
            {user ? (
              <>
                <Link className="icon-link" to="/notifications" aria-label="Notifications">
                  <Bell aria-hidden="true" />
                  {(unreadQuery.data?.count ?? 0) > 0 && <span>{Math.min(unreadQuery.data?.count ?? 0, 99)}</span>}
                </Link>
                <Link className="account-link" to="/profile">
                  <UserRound aria-hidden="true" />
                  <span>{user.displayName || user.email}</span>
                </Link>
                <Button size="icon" variant="quiet" onClick={signOut} aria-label="Sign out"><LogOut /></Button>
              </>
            ) : (
              <Link className="button button--primary button--md" to={`/login?returnTo=${encodeURIComponent(location.pathname)}`}>
                <LogIn aria-hidden="true" />Sign in
              </Link>
            )}
            <Button className="mobile-menu" size="icon" variant="quiet" onClick={() => setOpen((value) => !value)} aria-label="Toggle navigation">
              {open ? <X /> : <Menu />}
            </Button>
          </div>
        </div>
        <nav className={cn('mobile-nav', open && 'mobile-nav--open')} aria-label="Mobile navigation">
          <NavLink to="/" end onClick={() => setOpen(false)}>Home</NavLink>
          <NavLink to="/dogs" onClick={() => setOpen(false)}>Discover dogs</NavLink>
          {user && adopterLinks.map(({ to, label }) => <NavLink key={to} to={to} onClick={() => setOpen(false)}>{label}</NavLink>)}
          {user && <NavLink to="/profile" onClick={() => setOpen(false)}>Profile</NavLink>}
        </nav>
      </header>

      {user && (
        <div className="adopter-strip">
          <div className="container adopter-strip__inner">
            <span><BookHeart aria-hidden="true" />Adopter portal</span>
            <nav aria-label="Adopter tools">
              {adopterLinks.map(({ to, label }) => <NavLink key={to} to={to}>{label}</NavLink>)}
            </nav>
          </div>
        </div>
      )}

      <main><Suspense fallback={<div className="container page-stack"><PageLoader label="Loading page" /></div>}><Outlet /></Suspense></main>
      <footer className="site-footer">
        <div className="container"><strong>PawConnect</strong><span>Thoughtful dog discovery backed by real shelter data.</span></div>
      </footer>
    </div>
  )
}
