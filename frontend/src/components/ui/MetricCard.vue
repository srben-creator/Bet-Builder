<script setup lang="ts">
defineProps<{
  title: string
  value: string | number
  subtitle?: string
  trend?: 'up' | 'down' | 'neutral'
}>()
</script>

<template>
  <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg flex flex-col justify-between hover:border-slate-700 transition-colors">
    <div class="text-xs font-medium uppercase tracking-wider text-slate-400 mb-1">{{ title }}</div>
    <div class="text-2xl font-bold font-tabular text-slate-100 flex items-baseline gap-2">
      <span>{{ value }}</span>
      <span v-if="trend === 'up'" class="text-emerald-400 text-sm font-normal">▲</span>
      <span v-else-if="trend === 'down'" class="text-rose-400 text-sm font-normal">▼</span>
    </div>
    <div v-if="subtitle" class="text-xs text-slate-500 mt-2">{{ subtitle }}</div>
  </div>
</template>
