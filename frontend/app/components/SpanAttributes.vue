<script setup lang="ts">
import type { Attr } from '~/utils/mockData'

defineProps<{ title: string; items: Attr[] }>()
</script>

<template>
  <div class="attr-section">
    <div class="attr-section-title">{{ title }}</div>
    <div class="attr-grid">
      <div v-for="a in items" :key="a.key" class="attr-row">
        <span class="attr-key">{{ a.key }}</span>
        <span class="attr-val" :class="{ err: a.err, ok: a.ok }">{{ a.val }}</span>
      </div>
    </div>
  </div>
</template>
