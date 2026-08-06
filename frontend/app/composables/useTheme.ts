const STORAGE_KEY = 'othelia-theme'

let initialized = false

function resolveInitial(): boolean {
  try {
    const saved = localStorage.getItem(STORAGE_KEY)
    if (saved === 'dark') return true
    if (saved === 'light') return false
  } catch {
    // ignore storage errors (e.g. privacy mode)
  }
  return window.matchMedia('(prefers-color-scheme: dark)').matches
}

export function useTheme() {
  const isDark = useState<boolean>('theme', () => true)

  function apply() {
    if (!import.meta.client) return
    document.documentElement.setAttribute('data-theme', isDark.value ? 'dark' : 'light')
  }

  function init() {
    if (!import.meta.client || initialized) return
    initialized = true
    isDark.value = resolveInitial()
    apply()

    const mq = window.matchMedia('(prefers-color-scheme: dark)')
    mq.addEventListener('change', (e) => {
      if (!localStorage.getItem(STORAGE_KEY)) {
        isDark.value = e.matches
        apply()
      }
    })
  }

  function toggle() {
    isDark.value = !isDark.value
    try {
      localStorage.setItem(STORAGE_KEY, isDark.value ? 'dark' : 'light')
    } catch {
      // ignore storage errors
    }
    apply()
  }

  if (import.meta.client) {
    onMounted(init)
  }

  return { isDark, apply, init, toggle }
}
