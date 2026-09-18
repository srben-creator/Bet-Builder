<script setup lang="ts">
defineProps<{
  size?: number | string
}>()
</script>

<template>
  <svg
    viewBox="0 0 128 128"
    fill="none"
    xmlns="http://www.w3.org/2000/svg"
    class="inline-block shrink-0 rounded-sm overflow-hidden select-none"
    :style="size ? { width: `${size}px`, height: `${size}px` } : undefined"
    aria-label="Pinnacle"
  >
    <!-- Dark navy background -->
    <rect width="128" height="128" rx="26" fill="#000415" />
    <!-- Crisp bold P -->
    <path
      fill="#FFFFFF"
      fill-rule="evenodd"
      clip-rule="evenodd"
      d="M45 28H67.5C76 28 82 34.5 82 45.5C82 56.5 76 63 67.5 63H56.5V77.5H45V28ZM56.5 38.5V52.5H66.5C70.2 52.5 72 49.8 72 45.5C72 41.2 70.2 38.5 66.5 38.5H56.5Z"
    />
    <!-- Pinnacle signature orange bar -->
    <rect x="45" y="90" width="37" height="11" rx="1.5" fill="#FF5300" />
  </svg>
</template>
