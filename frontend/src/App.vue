<script setup lang="ts">
import Header from './components/layout/Header.vue'
</script>

<template>
  <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col selection:bg-emerald-500 selection:text-black">
    <!-- Top Header -->
    <Header />

    <!-- Main Router Content -->
    <main class="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <router-view />
    </main>

    <!-- Footer -->
    <footer class="border-t border-slate-800/80 bg-slate-950 py-6 text-center text-xs text-slate-500">
      <div class="max-w-7xl mx-auto px-4 flex flex-col sm:flex-row items-center justify-between gap-2">
        <div class="flex items-center gap-1.5">
          <span class="inline-block w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
          <span>Motor Dixon-Coles Bivariate Poisson ativo (.NET 10 + Vue 3)</span>
        </div>
        <div class="text-slate-600">
          Bet-Builder Quantitative Edge • Desenvolvido para máxima precisão matemática
        </div>
      </div>
    </footer>
  </div>
</template>
