<script setup lang="ts">
const { sidebarCollapsed, toggleSidebar } = useAppState()
const { traces, errorLogs } = useTracing()
const { unacknowledgedAlerts } = useDashboard()
const { user, logout } = useAuth()

const collectorCount = ref<number | null>(null)
onMounted(async () => {
  try {
    const cols = (await $fetch<{ name: string }[]>('/api/collectors')) ?? []
    collectorCount.value = cols.length
  } catch {
    collectorCount.value = null
  }
})

const userInitials = computed(() => {
  const name = user.value?.displayName ?? 'Guest'
  return name.split(/\s+/).map((p) => p[0]).join('').slice(0, 2).toUpperCase() || '?'
})

interface NavEntry {
  to: string
  icon: string
  label: string
  badge?: string
  badgeClass?: string
}

const sections: { label: string; items: NavEntry[] }[] = [
  {
    label: 'Observability',
    items: [
      { to: '/', icon: '📊', label: 'Dashboard' },
      { to: '/traces', icon: '📡', label: 'Traces', badge: '', badgeClass: 'nb-blue' },
      { to: '/services', icon: '🕸️', label: 'Service Map' },
      { to: '/metrics', icon: '📈', label: 'Metrics' },
      { to: '/logs', icon: '📋', label: 'Logs', badge: '', badgeClass: 'nb-red' },
    ],
  },
  {
    label: 'Config',
    items: [
      { to: '/collectors', icon: '⚙️', label: 'Collectors', badge: '', badgeClass: 'nb-green' },
      { to: '/alerts', icon: '🔔', label: 'Alerts', badge: '', badgeClass: 'nb-red' },
      { to: '/settings', icon: '🛠️', label: 'Settings' },
    ],
  },
]

const route = useRoute()

function badgeText(item: NavEntry) {
  if (item.to === '/traces') return String(traces.value.length)
  if (item.to === '/logs') return String(errorLogs.value)
  if (item.to === '/alerts') return String(unacknowledgedAlerts.value || '')
  if (item.to === '/collectors') return collectorCount.value == null ? '' : String(collectorCount.value)
  return item.badge ?? ''
}

function isActive(item: NavEntry) {
  return route.path === item.to || route.path.startsWith(`${item.to}/`)
}
</script>

<template>
  <aside class="sidebar" :class="{ collapsed: sidebarCollapsed }">
    <div class="sidebar-logo">
      <div class="logo-icon">🔭</div>
      <div class="sidebar-logo-text-wrap">
        <span class="logo-name">Othelia</span>
        <span class="logo-sub">Distributed Tracing</span>
      </div>
      <button class="sidebar-toggle" title="Toggle sidebar" @click="toggleSidebar">
        {{ sidebarCollapsed ? '›' : '‹' }}
      </button>
    </div>

    <div v-for="section in sections" :key="section.label" class="sidebar-section">
      <div class="sidebar-section-label">{{ section.label }}</div>
      <NuxtLink
        v-for="item in section.items"
        :key="item.to"
        :to="item.to"
        class="nav-item"
        :class="{ active: isActive(item) }"
      >
        <span class="icon">{{ item.icon }}</span>
        <span class="nav-label">{{ item.label }}</span>
        <span v-if="badgeText(item)" class="nav-badge" :class="item.badgeClass">{{ badgeText(item) }}</span>
      </NuxtLink>
    </div>

    <div class="sidebar-bottom">
      <div class="user-card">
        <div class="user-av">{{ userInitials }}</div>
        <div class="user-info">
          <div class="user-name">{{ user?.displayName ?? 'Guest' }}</div>
          <div class="user-role">{{ user?.role ?? 'Not signed in' }}</div>
        </div>
        <button class="user-logout" title="Sign out" @click="logout">⎋</button>
      </div>
    </div>
  </aside>
</template>
