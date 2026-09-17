<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useSyncStore } from '../../stores/useSyncStore'
import { RefreshCw, CheckCircle2, ExternalLink, Globe } from 'lucide-vue-next'

const route = useRoute()
const syncStore = useSyncStore()
const { t, locale } = useI18n()

const navItems = computed(() => [
  { name: t('nav.valueBets'), path: '/value-bets', icon: '💰' },
  { name: t('nav.ladder'), path: '/ladder', icon: '🪜' },
  { name: t('nav.fixtures'), path: '/fixtures', icon: '📅' },
  { name: t('nav.performance'), path: '/performance', icon: '📈' },
  { name: t('nav.backtest'), path: '/backtest', icon: '🔬' }
])

const toggleLanguage = () => {
  locale.value = locale.value === 'pt-PT' ? 'en' : 'pt-PT'
}
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
            <span class="text-[10px] uppercase font-bold tracking-widest text-emerald-400">{{ t('header.subtitle') }}</span>
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
          
          <!-- Language Toggle -->
          <button
            @click="toggleLanguage"
            class="px-2 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg border border-slate-700 flex items-center gap-1.5 transition-all cursor-pointer"
            title="Toggle Language"
          >
            <Globe class="w-3.5 h-3.5" />
            <span class="uppercase">{{ locale }}</span>
          </button>

          <!-- Sync Live Odds -->
          <button
            @click="syncStore.syncOdds"
            :disabled="syncStore.syncingOdds"
            class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg border border-slate-700 flex items-center gap-1.5 transition-all disabled:opacity-50 cursor-pointer"
          >
            <RefreshCw class="w-3.5 h-3.5" :class="{ 'animate-spin': syncStore.syncingOdds }" />
            <span>{{ syncStore.syncingOdds ? t('header.syncing') : t('header.syncOdds') }}</span>
          </button>

          <!-- Settle Bets -->
          <button
            @click="syncStore.syncResults"
            :disabled="syncStore.syncingResults"
            class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg border border-slate-700 flex items-center gap-1.5 transition-all disabled:opacity-50 cursor-pointer"
          >
            <CheckCircle2 class="w-3.5 h-3.5 text-emerald-400" :class="{ 'animate-spin': syncStore.syncingResults }" />
            <span>{{ syncStore.syncingResults ? t('header.settling') : t('header.settle') }}</span>
          </button>

          <!-- Swagger Link -->
          <a
            href="/swagger"
            target="_blank"
            class="px-2.5 py-1.5 bg-slate-950 hover:bg-slate-800 text-slate-400 hover:text-slate-200 text-xs font-medium rounded-lg border border-slate-800 flex items-center gap-1 transition-colors"
          >
            <span>{{ t('header.apiDocs') }}</span>
            <ExternalLink class="w-3 h-3" />
          </a>
        </div>
      </div>
    </div>

    <!-- Progress Indicator -->
    <div
      v-if="syncStore.syncingOdds || syncStore.syncingResults"
      class="border-t text-xs py-2 px-4 flex items-center justify-between bg-blue-950/80 text-blue-300 border-blue-800"
    >
      <div class="max-w-7xl mx-auto w-full flex items-center gap-2">
        <RefreshCw class="w-3.5 h-3.5 animate-spin" />
        <span class="font-medium animate-pulse">{{ syncStore.syncProgress || t('header.processing') }}</span>
      </div>
    </div>

    <!-- Notification Toast if Sync Message exists -->
    <div
      v-else-if="syncStore.lastResult"
      class="border-t text-xs py-2 px-4 flex items-center justify-between"
      :class="syncStore.lastResult.success ? 'bg-emerald-950/80 text-emerald-300 border-emerald-800' : 'bg-rose-950/80 text-rose-300 border-rose-800'"
    >
      <div class="max-w-7xl mx-auto w-full flex justify-between items-center">
        <span>{{ syncStore.lastResult.message }}</span>
        <button @click="syncStore.lastResult = null" class="text-xs hover:underline cursor-pointer">{{ t('header.close') }}</button>
      </div>
    </div>
  </header>
</template>
