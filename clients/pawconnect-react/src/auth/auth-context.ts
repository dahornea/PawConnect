import { createContext } from 'react'
import type { UserSession } from '@/api/types'

export type AuthContextValue = {
  user: UserSession | null
  isLoading: boolean
  refresh: () => Promise<void>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
