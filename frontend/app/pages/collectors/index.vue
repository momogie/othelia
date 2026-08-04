<script setup lang="ts">
const { collectors } = useTracing()
</script>

<template>
  <div class="detail-body" style="padding:20px;overflow:auto">
    <div style="font-size:14px;font-weight:700;color:var(--text);margin-bottom:16px">OTel Collectors</div>

    <div
      v-for="col in collectors"
      :key="col.name"
      style="background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);padding:14px 16px;margin-bottom:10px;max-width:700px"
    >
      <div style="display:flex;align-items:center;gap:10px;margin-bottom:10px">
        <div
          :style="{ width: '10px', height: '10px', borderRadius: '50%', background: col.active ? 'var(--green)' : 'var(--red)', flexShrink: 0 }"
        ></div>
        <span style="font-size:13px;font-weight:600;color:var(--text);flex:1">{{ col.name }}</span>
        <span class="tag" :class="col.active ? 'tag-ok' : 'tag-error'">{{ col.active ? 'RUNNING' : 'STOPPED' }}</span>
      </div>
      <div class="attr-grid">
        <div v-for="a in col.attrs" :key="a.key" class="attr-row">
          <span class="attr-key">{{ a.key }}</span>
          <span class="attr-val">{{ a.val }}</span>
        </div>
      </div>
    </div>
  </div>
</template>
