<template>
  <div class="shell">
    <AppSidebar />
    <div class="main">
      <AppHeader :label="pageLabel" />
      <main class="page-body">
        <slot />
      </main>
    </div>
    <ToastView />
  </div>
</template>

<script setup lang="ts">
const route = useRoute()

const pageLabels: Record<string, string> = {
  '/': 'Dashboard',
  '/traces': 'Traces',
  '/services': 'Service Map',
  '/metrics': 'Metrics',
  '/logs': 'Logs',
  '/collectors': 'Collectors',
  '/alerts': 'Alerts',
  '/settings': 'Settings',
}

const pageLabel = computed(() => {
  if (route.path === '/') return 'Dashboard'
  if (route.path.startsWith('/traces')) return 'Traces'
  return pageLabels[route.path] ?? 'Othelia'
})
</script>
