import type { TracingSettingsPatch, TracingSettingsVM } from '~/utils/settingsTypes'

export function useSettings() {
  const settings = useState<TracingSettingsVM | null>('tracingSettings', () => null)
  const loading = useState<boolean>('settingsLoading', () => false)
  const saving = useState<boolean>('settingsSaving', () => false)
  const error = useState<string | null>('settingsError', () => null)
  const success = useState<string | null>('settingsSuccess', () => null)

  async function load() {
    loading.value = true
    error.value = null
    try {
      settings.value = await $fetch<TracingSettingsVM>('/api/settings')
    } catch (e) {
      error.value = 'Failed to load settings from server'
      console.error('Failed to load settings', e)
    } finally {
      loading.value = false
    }
  }

  async function save(patch: TracingSettingsPatch) {
    saving.value = true
    error.value = null
    success.value = null
    try {
      await $fetch('/api/settings', { method: 'PUT', body: patch })
      success.value = 'Settings saved'
      await load()
    } catch (e) {
      error.value = 'Failed to save settings'
      console.error('Failed to save settings', e)
    } finally {
      saving.value = false
    }
  }

  async function reset() {
    saving.value = true
    error.value = null
    success.value = null
    try {
      await $fetch('/api/settings', { method: 'DELETE' })
      success.value = 'Settings reset to defaults'
      await load()
    } catch (e) {
      error.value = 'Failed to reset settings'
      console.error('Failed to reset settings', e)
    } finally {
      saving.value = false
    }
  }

  return { settings, loading, saving, error, success, load, save, reset }
}
