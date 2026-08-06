<script setup lang="ts">
defineProps<{ label: string }>()

const { toggleSidebar } = useAppState()
const toast = useToast()
const { refresh } = useTracing()
const { selectedService, services, loadServices, setService } = useServiceScope()

onMounted(() => {
  loadServices()
})

function onServiceChange(event: Event) {
  const target = event.target as HTMLSelectElement
  setService(target.value)
  toast.show(
    target.value === 'all' ? 'Showing all services' : `Filtering by ${target.value}`,
    '🔍',
    'info',
  )
}

function onRefresh() {
  refresh()
  toast.show('Traces refreshed', '🔄', 'info')
}

function onExport() {
  toast.show('Exporting traces...', '📤', 'info')
}
</script>

<template>
  <div class="header">
    <button class="hamburger" title="Toggle sidebar" @click="toggleSidebar">☰</button>
    <div class="header-breadcrumb">
      <span>Othelia</span>
      <span class="sep">/</span>
      <strong>{{ label }}</strong>
    </div>
    <div class="header-spacer"></div>
    <div class="filter-chip" :class="{ active: selectedService !== 'all' }" style="cursor:default">
      <span>Service</span>
      <select :value="selectedService" @change="onServiceChange">
        <option value="all">All</option>
        <option v-for="s in services" :key="s.name" :value="s.name">{{ s.name }}</option>
      </select>
    </div>
    <div style="display:flex;align-items:center;gap:6px;margin-right:4px;">
      <div class="live-dot"></div>
      <span style="font-size:11px;color:var(--green);font-weight:600;">Live</span>
    </div>
    <button class="hdr-btn" @click="onRefresh">⟳ Refresh</button>
    <button class="hdr-btn primary" @click="onExport">↑ Export</button>
    <ThemeToggle />
  </div>
</template>
