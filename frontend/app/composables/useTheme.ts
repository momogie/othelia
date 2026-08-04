export function useTheme() {
  const isDark = useState<boolean>('theme', () => true)

  function apply() {
    if (import.meta.client) {
      document.documentElement.setAttribute('data-theme', isDark.value ? 'dark' : 'light')
    }
  }

  function toggle() {
    isDark.value = !isDark.value
    apply()
  }

  if (import.meta.client) {
    onMounted(apply)
  }

  return { isDark, apply, toggle }
}
