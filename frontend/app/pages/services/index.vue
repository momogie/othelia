<script setup lang="ts">
import type { ApiServiceMap, ApiServiceMapNode } from '~/utils/otelTypes'

const map = ref<ApiServiceMap>({ nodes: [], edges: [] })
const loading = ref(false)
const { selectedService } = useServiceScope()

function iconForService(name: string): string {
  const lower = name.toLowerCase()
  if (lower.includes('db') || lower.includes('sql')) return '🗄️'
  if (lower.includes('cache') || lower.includes('redis')) return '⚡'
  if (lower.includes('mq') || lower.includes('rabbit') || lower.includes('kafka')) return '📨'
  if (lower.includes('gateway') || lower.includes('api')) return '🌐'
  return '📡'
}

function nodeClass(node: ApiServiceMapNode): string {
  return node.errorSpans > 0 ? 'tag-error' : 'tag-ok'
}

function fmtTime(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return d.toLocaleString([], { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
}

async function refresh() {
  loading.value = true
  try {
    map.value = (await $fetch<ApiServiceMap>('/api/service-map', {
      query: { service: selectedService.value === 'all' ? undefined : selectedService.value },
    })) ?? { nodes: [], edges: [] }
  } catch (e) {
    console.error('Failed to load service map', e)
    map.value = { nodes: [], edges: [] }
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
watch(selectedService, refresh)
</script>

<template>
  <div class="detail-body" style="padding:20px;overflow:auto;max-width:1100px">
    <div style="font-size:14px;font-weight:700;color:var(--text);margin-bottom:16px">Service Dependency Map</div>

    <div v-if="map.nodes.length" class="svc-map">
      <div class="svc-map-title">📡 Services ({{ map.nodes.length }})</div>
      <div class="svc-nodes" style="gap:12px;flex-wrap:wrap">
        <div v-for="node in map.nodes" :key="node.name" class="svc-node" style="padding:14px 18px">
          <span class="svc-icon" style="font-size:22px">{{ iconForService(node.name) }}</span>
          <span class="svc-name" style="font-size:11.5px">{{ node.name }}</span>
          <span class="svc-latency">{{ node.totalTraces }} traces</span>
          <span class="tag" :class="nodeClass(node)" style="margin-top:4px">
            {{ node.errorSpans ? `${node.errorSpans} errors` : 'OK' }}
          </span>
          <span class="svc-latency" style="margin-top:2px;font-size:10px">seen {{ fmtTime(node.lastSeen) }}</span>
        </div>
      </div>
    </div>
    <div v-else class="empty-state" style="padding:48px 20px;text-align:center">
      <div class="empty-icon">{{ loading ? '⏳' : '🕸️' }}</div>
      <div class="empty-text">{{ loading ? 'Loading service map...' : 'No service map data yet' }}</div>
    </div>

    <div v-if="map.edges.length" class="svc-map" style="margin-top:14px">
      <div class="svc-map-title">🔗 Dependencies</div>
      <div v-for="edge in map.edges" :key="`${edge.source}-${edge.target}`" class="event-row">
        <span class="method-GET" style="padding:2px 8px;border-radius:5px;font-size:10.5px;font-weight:600">
          {{ edge.source }}
        </span>
        <span style="color:var(--text3);padding:0 6px">→</span>
        <span class="method-POST" style="padding:2px 8px;border-radius:5px;font-size:10.5px;font-weight:600">
          {{ edge.target }}
        </span>
        <div class="ev-time">{{ edge.callCount }} calls</div>
      </div>
    </div>
  </div>
</template>
