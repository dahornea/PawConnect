import { lazy } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from '@/auth/ProtectedRoute'
import { AppShell } from '@/layouts/AppShell'

const ApplicationDetailsPage = lazy(async () => ({ default: (await import('@/pages/ApplicationDetailsPage')).ApplicationDetailsPage }))
const ApplicationsPage = lazy(async () => ({ default: (await import('@/pages/ApplicationsPage')).ApplicationsPage }))
const ApplyPage = lazy(async () => ({ default: (await import('@/pages/ApplyPage')).ApplyPage }))
const CopilotPage = lazy(async () => ({ default: (await import('@/pages/CopilotPage')).CopilotPage }))
const DogDetailsPage = lazy(async () => ({ default: (await import('@/pages/DogDetailsPage')).DogDetailsPage }))
const DogsPage = lazy(async () => ({ default: (await import('@/pages/DogsPage')).DogsPage }))
const FavoritesPage = lazy(async () => ({ default: (await import('@/pages/FavoritesPage')).FavoritesPage }))
const InsightsPage = lazy(async () => ({ default: (await import('@/pages/InsightsPage')).InsightsPage }))
const LandingPage = lazy(async () => ({ default: (await import('@/pages/LandingPage')).LandingPage }))
const LoginPage = lazy(async () => ({ default: (await import('@/pages/LoginPage')).LoginPage }))
const NotFoundPage = lazy(async () => ({ default: (await import('@/pages/NotFoundPage')).NotFoundPage }))
const NotificationsPage = lazy(async () => ({ default: (await import('@/pages/NotificationsPage')).NotificationsPage }))
const ProfilePage = lazy(async () => ({ default: (await import('@/pages/ProfilePage')).ProfilePage }))
const SavedSearchesPage = lazy(async () => ({ default: (await import('@/pages/SavedSearchesPage')).SavedSearchesPage }))
const UnauthorizedPage = lazy(async () => ({ default: (await import('@/pages/UnauthorizedPage')).UnauthorizedPage }))

export default function App() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<LandingPage />} />
        <Route path="dogs" element={<DogsPage />} />
        <Route path="dogs/:dogId" element={<DogDetailsPage />} />
        <Route path="login" element={<LoginPage />} />
        <Route path="unauthorized" element={<UnauthorizedPage />} />

        <Route element={<ProtectedRoute />}>
          <Route path="favorites" element={<FavoritesPage />} />
          <Route path="saved-searches" element={<SavedSearchesPage />} />
          <Route path="applications" element={<ApplicationsPage />} />
          <Route path="applications/:applicationId" element={<ApplicationDetailsPage />} />
          <Route path="dogs/:dogId/apply" element={<ApplyPage />} />
          <Route path="notifications" element={<NotificationsPage />} />
          <Route path="profile" element={<ProfilePage />} />
          <Route path="insights" element={<InsightsPage />} />
          <Route path="copilot" element={<CopilotPage />} />
        </Route>

        <Route path="home" element={<Navigate to="/" replace />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  )
}
