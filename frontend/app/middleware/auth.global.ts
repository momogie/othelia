export default defineNuxtRouteMiddleware(async () => {
  if (import.meta.server) return

  const { user, initialized, refresh } = useAuth()
  if (!initialized.value) {
    await refresh()
  }
  if (!user.value) {
    window.location.href = '/account/login'
  }
})
