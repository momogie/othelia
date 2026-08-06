<script setup lang="ts">
import type { ApiCollector } from '~/utils/otelTypes'

const collectors = ref<ApiCollector[]>([])
const loading = ref(false)

function fmtTime(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return d.toLocaleString([], { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

async function refresh() {
  loading.value = true
  try {
    collectors.value = (await $fetch<ApiCollector[]>('/api/collectors')) ?? []
  } catch (e) {
    console.error('Failed to load collectors', e)
    collectors.value = []
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <div class="detail-body" style="padding:20px;overflow:auto">
    <div style="font-size:14px;font-weight:700;color:var(--text);margin-bottom:16px">
      Service Collectors
      <span style="font-size:11px;color:var(--text3);font-weight:400;margin-left:8px">
        {{ collectors.filter((c) => c.running).length }}/{{ collectors.length }} running
      </span>
    </div>

    <template v-if="collectors.length">
      <div
        v-for="col in collectors"
        :key="col.name"
        style="background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);padding:14px 16px;margin-bottom:10px;max-width:700px"
      >
        <div style="display:flex;align-items:center;gap:10px;margin-bottom:10px">
          <div
            :style="{ width: '10px', height: '10px', borderRadius: '50%', background: col.running ? 'var(--green)' : 'var(--red)', flexShrink: 0 }"
          ></div>
          <span style="font-size:13px;font-weight:600;color:var(--text);flex:1">{{ col.name }}</span>
          <span class="tag" :class="col.running ? 'tag-ok' : 'tag-error'">{{ col.running ? 'RUNNING' : 'STOPPED' }}</span>
        </div>
        <div class="attr-grid">
          <div class="attr-row"><span class="attr-key">version</span><span class="attr-val">{{ col.version || '—' }}</span></div>
          <div class="attr-row"><span class="attr-key">environment</span><span class="attr-val">{{ col.environment || '—' }}</span></div>
          <div class="attr-row"><span class="attr-key">totalTraces</span><span class="attr-val">{{ col.totalTraces }}</span></div>
          <div class="attr-row"><span class="attr-key">errorSpans</span><span class="attr-val">{{ col.errorSpans }}</span></div>
          <div class="attr-row"><span class="attr-key">lastSeen</span><span class="attr-val">{{ fmtTime(col.lastSeen) }}</span></div>
        </div>
      </div>
    </template>
    <div v-else class="empty-state" style="padding:48px 20px;text-align:center">
      <div class="empty-icon">{{ loading ? '⏳' : '⚙️' }}</div>
      <div class="empty-text">{{ loading ? 'Loading collectors...' : 'No collectors reporting yet' }}</div>
    </div>
  </div>
</template>
