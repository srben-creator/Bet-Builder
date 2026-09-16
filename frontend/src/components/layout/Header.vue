<script setup lang="ts">
import { useRoute } from 'vue-router'
import { useSyncStore } from '../../stores/useSyncStore'
import { RefreshCw, CheckCircle2, ExternalLink } from 'lucide-vue-next'

const route = useRoute()
const syncStore = useSyncStore()

const navItems = [
  { name: 'Value Bets (+EV)', path: '/value-bets', icon: '💰' },
  { name: 'Honest Ladder', path: '/ladder', icon: '🪜' },
  { name: 'Jogos Agendados', path: '/fixtures', icon: '📅' },
  { name: 'Performance & P&L', path: '/performance', icon: '📈' },
  { name: 'Backtest & CLV', path: '/backtest', icon: '🔬' }
]
</script>

<template>
  <header class="bg-slate-900/90 backdrop-blur border-b border-slate-800 sticky top-0 z-50">
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
      <div class="flex items-center justify-between h-16">
        <!-- Logo & Title -->
        <div class="flex items-center gap-3">
          <span class="text-2xl">⚽</span>
          <div>
            <h1 class="text-lg font-black tracking-tight text-white m-0 leading-none">Bet-Builder</h1>
            <span class="text-[10px] uppercase font-bold tracking-widest text-emerald-400">Quantitative Edge</span>
          </div>
        </div>

        <!-- Navigation Links -->
        <nav class="hidden md:flex items-center gap-1">
          <router-link
            v-for="item in navItems"
            :key="item.path"
            :to="item.path"
            class="px-3 py-2 rounded-lg text-sm font-medium transition-colors flex items-center gap-1.5"
            :class="route.path === item.path ? 'bg-slate-800 text-emerald-400 font-semibold shadow-inner' : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'"
          >
            <span>{{ item.icon }}</span>
            <span>{{ item.name }}</span>
          </router-link>
        </nav>

        <!-- Header Actions -->
        <div class="flex items-center gap-2">
          <!-- Sync Live Odds -->
          <button
            @click="syncStore.syncOdds"
            :disabled="syncStore.syncingOdds"
            class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg border border-slate-700 flex items-center gap-1.5 transition-all disabled:opacity-50 cursor-pointer"
            title="Buscar odds na The Odds API e calcular +EV"
          >
            <RefreshCw class="w-3.5 h-3.5" :class="{ 'animate-spin': syncStore.syncingOdds }" />
            <span>{{ syncStore.syncingOdds ? 'Buscando...' : 'Sync Odds' }}</span>
          </button>

          <!-- Settle Bets -->
          <button
            @click="syncStore.syncResults"
            :disabled="syncStore.syncingResults"
            class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg border border-slate-700 flex items-center gap-1.5 transition-all disabled:opacity-50 cursor-pointer"
            title="Baixar resultados do football-data e liquidar apostas"
          >
            <CheckCircle2 class="w-3.5 h-3.5 text-emerald-400" :class="{ 'animate-spin': syncStore.syncingResults }" />
            <span>{{ syncStore.syncingResults ? 'Liquidando...' : 'Liquidar' }}</span>
          </button>

          <!-- Swagger Link -->
          <a
            href="/swagger"
            target="_blank"
            class="px-2.5 py-1.5 bg-slate-950 hover:bg-slate-800 text-slate-400 hover:text-slate-200 text-xs font-medium rounded-lg border border-slate-800 flex items-center gap-1 transition-colors"
            title="Abrir documentação Swagger da API"
          >
            <span>API Docs</span>
            <ExternalLink class="w-3 h-3" />
          </a>
        </div>
      </div>
    </div>

    <!-- Notification Toast if Sync Message exists -->
    <div
      v-if="syncStore.lastResult"
      class="border-t text-xs py-2 px-4 flex items-center justify-between"
      :class="syncStore.lastResult.success ? 'bg-emerald-950/80 text-emerald-300 border-emerald-800' : 'bg-rose-950/80 text-rose-300 border-rose-800'"
    >
      <div class="max-w-7xl mx-auto w-full flex justify-between items-center">
        <span>{{ syncStore.lastResult.message }}</span>
        <button @click="syncStore.lastResult = null" class="text-xs hover:underline cursor-pointer">Fechar</button>
      </div>
    </div>
  </header>
</template>
