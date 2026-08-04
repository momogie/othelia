<script setup lang="ts">
const { globalMetrics, serviceRates } = useTracing()
</script>

<template>
  <div class="detail-body" style="padding:20px;overflow:auto">
    <div class="metrics-row" style="grid-template-columns:repeat(4,1fr);max-width:900px">
      <MetricCard v-for="m in globalMetrics" :key="m.label" :metric="m" />
    </div>

    <div
      style="margin-top:20px;background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);padding:16px;max-width:900px"
    >
      <div style="font-size:12px;font-weight:600;color:var(--text);margin-bottom:12px">Request Rate per Service</div>
      <div v-for="s in serviceRates" :key="s.name" style="margin-bottom:10px">
        <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:4px">
          <span style="font-size:12px;color:var(--text)">{{ s.name }}</span>
          <span style="font-size:11px;color:var(--text2);font-family:var(--mono)">{{ s.rps }} req/s</span>
        </div>
        <div style="height:6px;background:var(--surface3);border-radius:3px;overflow:hidden">
          <div :style="{ width: `${s.pct}%`, height: '100%', background: s.color, borderRadius: '3px' }"></div>
        </div>
      </div>
    </div>
  </div>
</template>
