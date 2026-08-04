<script setup lang="ts">
const { serviceMap, svcMetrics } = useTracing()
</script>

<template>
  <div class="content" style="padding:20px;overflow:auto;flex-direction:column">
    <div style="font-size:14px;font-weight:700;color:var(--text);margin-bottom:16px">Service Dependency Map</div>

    <div class="svc-map" style="max-width:900px">
      <div class="svc-nodes" style="gap:12px;flex-wrap:wrap">
        <template v-for="(node, idx) in serviceMap" :key="node.name">
          <div class="svc-node" style="padding:14px 18px">
            <span class="svc-icon" style="font-size:22px">{{ node.icon }}</span>
            <span class="svc-name" style="font-size:11.5px">{{ node.name }}</span>
            <span class="svc-latency">p99: {{ node.latency }}</span>
            <span class="tag" :class="node.status === 'ok' ? 'tag-ok' : 'tag-error'" style="margin-top:4px">
              {{ node.status.toUpperCase() }}
            </span>
          </div>
          <span v-if="idx < serviceMap.length - 1" class="svc-arrow" style="font-size:20px">→</span>
        </template>
      </div>
    </div>

    <div class="metrics-row" style="max-width:900px;margin-top:16px;grid-template-columns:repeat(3,1fr)">
      <MetricCard v-for="m in svcMetrics" :key="m.label" :metric="m" />
    </div>
  </div>
</template>
