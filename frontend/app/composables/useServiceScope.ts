import type { ApiService } from '~/utils/otelTypes'

export function useServiceScope() {
  const selectedService = useState<string>('serviceScope', () => 'all')
  const services = useState<ApiService[]>('serviceScopeServices', () => [])

  async function loadServices() {
    if (services.value.length) return
    try {
      services.value = (await $fetch<ApiService[]>('/api/services')) ?? []
    } catch (e) {
      console.error('Failed to load services', e)
      services.value = []
    }
  }

  function setService(name: string) {
    selectedService.value = name
  }

  return { selectedService, services, loadServices, setService }
}
