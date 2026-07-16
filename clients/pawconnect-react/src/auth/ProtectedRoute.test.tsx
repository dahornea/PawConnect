import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { vi } from 'vitest'
import { ProtectedRoute } from '@/auth/ProtectedRoute'

const mockUseAuth = vi.fn()
vi.mock('@/auth/useAuth', () => ({ useAuth: () => mockUseAuth() }))

function LocationProbe() {
  const location = useLocation()
  return <div>{location.pathname}{location.search}</div>
}

describe('ProtectedRoute', () => {
  it('redirects an anonymous user to login and preserves the requested route', () => {
    mockUseAuth.mockReturnValue({ user: null, isLoading: false })
    render(<MemoryRouter initialEntries={['/favorites?tab=new']}><Routes><Route element={<ProtectedRoute />}><Route path="/favorites" element={<div>Favorites</div>} /></Route><Route path="/login" element={<LocationProbe />} /></Routes></MemoryRouter>)
    expect(screen.getByText(/\/login\?returnTo=/)).toHaveTextContent('%2Ffavorites%3Ftab%3Dnew')
  })

  it('renders a protected adopter route for an adopter session', () => {
    mockUseAuth.mockReturnValue({ user: { roles: ['Adopter'] }, isLoading: false })
    render(<MemoryRouter initialEntries={['/favorites']}><Routes><Route element={<ProtectedRoute />}><Route path="/favorites" element={<div>Favorite dogs</div>} /></Route></Routes></MemoryRouter>)
    expect(screen.getByText('Favorite dogs')).toBeInTheDocument()
  })

  it('does not allow another role into the adopter portal', () => {
    mockUseAuth.mockReturnValue({ user: { roles: ['Shelter'] }, isLoading: false })
    render(<MemoryRouter initialEntries={['/favorites']}><Routes><Route element={<ProtectedRoute />}><Route path="/favorites" element={<div>Favorites</div>} /></Route><Route path="/unauthorized" element={<div>Not allowed</div>} /></Routes></MemoryRouter>)
    expect(screen.getByText('Not allowed')).toBeInTheDocument()
  })
})
