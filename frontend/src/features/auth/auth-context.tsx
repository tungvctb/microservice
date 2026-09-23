import {
  createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode,
} from 'react'
import { api, tokenStore } from '@/lib/api-client'
import type { AuthResult, User } from '@/lib/types'

interface AuthState {
  user: User | null
  loading: boolean
  isAuthenticated: boolean
  hasRole: (...roles: string[]) => boolean
  login: (email: string, password: string) => Promise<void>
  register: (input: RegisterInput) => Promise<void>
  logout: () => Promise<void>
  refreshProfile: () => Promise<void>
}

export interface RegisterInput {
  email: string
  password: string
  fullName: string
  phone?: string
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

  const refreshProfile = useCallback(async () => {
    if (!tokenStore.access) {
      setUser(null)
      return
    }
    try {
      setUser(await api.get<User>('/users/me'))
    } catch {
      // Token hỏng/hết hạn: xóa sạch để buộc đăng nhập lại.
      tokenStore.clear()
      setUser(null)
    }
  }, [])

  // Khôi phục phiên khi mở lại tab.
  useEffect(() => {
    void refreshProfile().finally(() => setLoading(false))
  }, [refreshProfile])

  const login = useCallback(async (email: string, password: string) => {
    const result = await api.post<AuthResult>('/auth/login', { email, password })
    tokenStore.save(result)
    setUser(result.user)
  }, [])

  const register = useCallback(async (input: RegisterInput) => {
    const result = await api.post<AuthResult>('/auth/register', input)
    tokenStore.save(result)
    setUser(result.user)
  }, [])

  const logout = useCallback(async () => {
    try {
      await api.post('/auth/logout')
    } catch {
      // Server không phản hồi cũng phải đăng xuất được ở phía client.
    } finally {
      tokenStore.clear()
      setUser(null)
    }
  }, [])

  const value = useMemo<AuthState>(
    () => ({
      user,
      loading,
      isAuthenticated: user !== null,
      hasRole: (...roles) => roles.some((role) => user?.roles.includes(role) ?? false),
      login,
      register,
      logout,
      refreshProfile,
    }),
    [user, loading, login, register, logout, refreshProfile],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthState {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth phải nằm trong <AuthProvider>.')
  return context
}
