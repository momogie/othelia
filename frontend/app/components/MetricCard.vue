<script setup lang="ts">
import type { MetricVM } from '~/utils/mockData'

defineProps<{ metric: MetricVM }>()
</script>

<template>
  <div class="metric-card">
    <div class="mc-label">{{ metric.label }}</div>
    <div class="mc-value" :style="{ color: metric.color }">{{ metric.value }}</div>
    <div class="mc-bar">
      <div class="mc-bar-fill" :style="{ width: `${metric.pct}%`, background: metric.color }"></div>
    </div>
  </div>
</template>
