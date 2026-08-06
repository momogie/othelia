export interface AuthUser {
  username: string
  displayName: string
  role: string
}

export function useAuth() {
  const user = useState<AuthUser | null>('authUser', () => null)
  const initialized = useState<boolean>('authInitialized', () => false)

  async function refresh() {
    try {
      const data = await $fetch<{ authenticated: boolean; username?: string; displayName?: string; role?: string }>(
        '/api/auth/me',
      )
      if (data.authenticated) {
        user.value = {
          username: data.username ?? '',
          displayName: data.displayName ?? data.username ?? 'User',
          role: data.role ?? 'User',
        }
      } else {
        user.value = null
      }
    } catch {
      user.value = null
    } finally {
      initialized.value = true
    }
  }

  async function logout() {
    try {
      await $fetch('/api/auth/logout', { method: 'POST' })
    } catch {
      // ignore
    }
    user.value = null
    window.location.href = '/account/login'
  }

  return { user, initialized, refresh, logout }
}
