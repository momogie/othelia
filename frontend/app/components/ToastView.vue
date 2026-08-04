<template>
  <div class="toast" :class="toast.type">
    <span>{{ toast.icon }}</span>
    <span>{{ toast.msg }}</span>
  </div>
</template>

<script setup lang="ts">
const { toast } = useToast()
</script>
