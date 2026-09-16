<script setup lang="ts">
defineProps<{
  variant?: 'emerald' | 'rose' | 'amber' | 'blue' | 'slate'
}>()
</script>

<template>
  <span
    class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold font-tabular"
    :class="{
      'bg-emerald-950 text-emerald-400 border border-emerald-800': !variant || variant === 'emerald',
      'bg-rose-950 text-rose-400 border border-rose-800': variant === 'rose',
      'bg-amber-950 text-amber-400 border border-amber-800': variant === 'amber',
      'bg-blue-950 text-blue-400 border border-blue-800': variant === 'blue',
      'bg-slate-800 text-slate-300 border border-slate-700': variant === 'slate'
    }"
  >
    <slot />
  </span>
</template>
