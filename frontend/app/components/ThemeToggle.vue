<script setup lang="ts">
const { isDark, toggle } = useTheme()
</script>

<template>
  <button class="theme-toggle" @click="toggle">
    <div class="toggle-track"><div class="toggle-thumb"></div></div>
    <span>{{ isDark ? 'Dark' : 'Light' }}</span>
  </button>
</template>
