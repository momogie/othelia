<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, shallowRef, watch } from 'vue'

export interface LinePoint {
  time: string
  timestamp: string
  value: number
}

const props = withDefaults(
  defineProps<{
    points: LinePoint[]
    height?: number
    unit?: string
    color?: string
    animated?: boolean
    showLatest?: boolean
    windowSeconds?: number
  }>(),
  { height: 130, unit: 'req', color: '#4a9eff', animated: false, showLatest: true, windowSeconds: 60 },
)

const el = ref<HTMLDivElement | null>(null)
const chart = shallowRef<any>(null)

const latest = computed(() => (props.points.length ? props.points[props.points.length - 1].value : 0))

function buildOptions() {
  return {
    series: [{ name: props.unit, data: [] as { x: number; y: number }[] }],
    chart: {
      type: 'area' as const,
      height: props.height,
      parentHeightOffset: 0,
      toolbar: { show: false },
      zoom: { enabled: false },
      animations: { enabled: false, dynamicAnimation: { enabled: false } },
      fontFamily: 'Inter, sans-serif',
      foreColor: '#8b93a7',
      background: 'transparent',
    },
    colors: [props.color],
    dataLabels: { enabled: false },
    stroke: { curve: 'smooth', width: 2 },
    fill: {
      type: 'gradient' as const,
      gradient: { shadeIntensity: 1, opacityFrom: 0.32, opacityTo: 0.02, stops: [0, 100] },
    },
    grid: {
      borderColor: 'rgba(255,255,255,0.11)',
      strokeDashArray: 3,
      padding: { left: 8, right: 8 },
      xaxis: { lines: { show: false } },
    },
    xaxis: {
      type: 'datetime' as const,
      datetimeUTC: true,
      labels: {
        show: true,
        datetimeUTC: true,
        format: 'HH:mm:ss',
        style: { fontSize: '9px' },
      },
      axisBorder: { show: false },
      axisTicks: { show: false },
      crosshairs: { show: true },
      tooltip: { enabled: false },
    },
    yaxis: {
      min: 0,
      forceNiceScale: true,
      labels: {
        formatter: (v: number) => String(Math.round(v)),
        style: { fontSize: '9px' },
      },
    },
    tooltip: {
      theme: 'dark' as const,
      x: { show: true, format: 'HH:mm:ss' },
      y: { formatter: (v: number) => `${Math.round(v)} ${props.unit}` },
    },
  }
}

const seriesData = ref<{ x: number; y: number }[]>([])

function applyData() {
  const c = chart.value
  if (!c || !ready.value) return
  const incoming = props.points.map((p) => ({ x: new Date(p.timestamp).getTime(), y: p.value }))
  if (!incoming.length) {
    seriesData.value = []
    c.updateSeries([{ data: [] }])
    return
  }

  if (props.animated) {
    if (!seriesData.value.length) {
      seriesData.value = [...incoming]
    } else {
      const lastX = seriesData.value[seriesData.value.length - 1].x
      const lastIncoming = incoming[incoming.length - 1]
      if (lastIncoming.x === lastX) {
        seriesData.value[seriesData.value.length - 1] = lastIncoming
      }
      const additions = incoming.filter((p) => p.x > lastX)
      if (additions.length) {
        seriesData.value = [...seriesData.value, ...additions]
      }
      if (seriesData.value.length > props.points.length + 4) {
        seriesData.value = seriesData.value.slice(-(props.points.length + 4))
      }
    }
    const last = seriesData.value[seriesData.value.length - 1].x
    c.updateOptions({
      xaxis: { min: last - props.windowSeconds * 1000, max: last },
      series: [{ data: seriesData.value }],
    })
  } else {
    seriesData.value = [...incoming]
    c.updateSeries([{ data: seriesData.value }])
  }
}

const ready = ref(false)

onMounted(async () => {
  const mod = await import('apexcharts')
  const ApexCharts = (mod as any).default ?? mod
  chart.value = new ApexCharts(el.value, buildOptions())
  await chart.value.render()
  ready.value = true
  applyData()
})

watch(
  () => props.points,
  () => applyData(),
)

onBeforeUnmount(() => {
  chart.value?.destroy()
  chart.value = null
})
</script>

<template>
  <div class="line-chart-wrap" :style="{ height: `${height}px` }">
    <div v-if="showLatest && points.length" class="lc-value" :style="{ color }">
      {{ latest }}
      <span class="lc-value-unit">{{ unit }}</span>
    </div>
    <div ref="el" class="line-chart-apex"></div>
  </div>
</template>
