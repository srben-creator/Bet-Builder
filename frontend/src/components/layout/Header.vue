<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useSyncStore } from '../../stores/useSyncStore'
import { useSettingsStore } from '../../stores/useSettingsStore'
import { RefreshCw, CheckCircle2, ExternalLink, Settings, Moon, Sun, Palette, Monitor } from 'lucide-vue-next'

const route = useRoute()
const syncStore = useSyncStore()
const settingsStore = useSettingsStore()
const { t, locale } = useI18n()

const navItems = computed(() => [
  { name: t('nav.valueBets'), path: '/value-bets', icon: '💰' },
  { name: t('nav.ladder'), path: '/ladder', icon: '🪜' },
  { name: t('nav.fixtures'), path: '/fixtures', icon: '📅' },
  { name: t('nav.performance'), path: '/performance', icon: '📈' },
  { name: t('nav.backtest'), path: '/backtest', icon: '🔬' },
  { name: t('nav.leagues') || 'Leagues', path: '/leagues', icon: '🌍' }
])

const showSettings = ref(false)

const toggleSettings = () => {
  showSettings.value = !showSettings.value
}

// Close dropdown when clicking outside
const closeSettings = (e: MouseEvent) => {
  const target = e.target as HTMLElement
  if (!target.closest('.settings-dropdown-container')) {
    showSettings.value = false
  }
}

onMounted(() => {
  document.addEventListener('click', closeSettings)
})

onUnmounted(() => {
  document.removeEventListener('click', closeSettings)
})

const setLanguage = (lang: string) => {
  locale.value = lang
  settingsStore.setLocale(lang)
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
          
          <!-- Settings Dropdown -->
          <div class="relative settings-dropdown-container">
            <button
              @click="toggleSettings"
              class="px-2 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg border border-slate-700 flex items-center gap-1.5 transition-all cursor-pointer"
              :title="t('settings.title')"
            >
              <Settings class="w-3.5 h-3.5" />
            </button>
            
            <div v-if="showSettings" class="absolute right-0 mt-2 w-56 bg-slate-900 border border-slate-800 rounded-xl shadow-2xl py-3 z-50">
              <div class="px-4 pb-2 text-xs font-bold uppercase text-slate-500 tracking-wider">
                {{ t('settings.language') }}
              </div>
              <div class="flex px-3 gap-2">
                <button
                  @click="setLanguage('pt-PT')"
                  class="flex-1 py-1.5 text-xs font-semibold rounded border transition-colors cursor-pointer"
                  :class="locale === 'pt-PT' ? 'bg-emerald-900/40 text-emerald-400 border-emerald-800' : 'bg-slate-800 text-slate-400 border-transparent hover:bg-slate-700 hover:text-slate-200'"
                >
                  PT
                </button>
                <button
                  @click="setLanguage('en')"
                  class="flex-1 py-1.5 text-xs font-semibold rounded border transition-colors cursor-pointer"
                  :class="locale === 'en' ? 'bg-emerald-900/40 text-emerald-400 border-emerald-800' : 'bg-slate-800 text-slate-400 border-transparent hover:bg-slate-700 hover:text-slate-200'"
                >
                  EN
                </button>
              </div>

              <div class="border-t border-slate-800 my-3"></div>

              <div class="px-4 pb-2 text-xs font-bold uppercase text-slate-500 tracking-wider">
                {{ t('settings.theme') }}
              </div>
              <div class="px-3 space-y-1">
                <button
                  @click="settingsStore.setTheme('dark')"
                  class="w-full text-left px-3 py-1.5 text-xs font-medium rounded-lg flex items-center gap-2 transition-colors cursor-pointer"
                  :class="settingsStore.currentTheme === 'dark' ? 'bg-slate-800 text-emerald-400' : 'text-slate-400 hover:bg-slate-800 hover:text-slate-200'"
                >
                  <Moon class="w-3.5 h-3.5" /> {{ t('settings.themes.dark') }}
                </button>
                <button
                  @click="settingsStore.setTheme('light')"
                  class="w-full text-left px-3 py-1.5 text-xs font-medium rounded-lg flex items-center gap-2 transition-colors cursor-pointer"
                  :class="settingsStore.currentTheme === 'light' ? 'bg-slate-800 text-emerald-400' : 'text-slate-400 hover:bg-slate-800 hover:text-slate-200'"
                >
                  <Sun class="w-3.5 h-3.5" /> {{ t('settings.themes.light') }}
                </button>
                <button
                  @click="settingsStore.setTheme('midnight')"
                  class="w-full text-left px-3 py-1.5 text-xs font-medium rounded-lg flex items-center gap-2 transition-colors cursor-pointer"
                  :class="settingsStore.currentTheme === 'midnight' ? 'bg-slate-800 text-emerald-400' : 'text-slate-400 hover:bg-slate-800 hover:text-slate-200'"
                >
                  <Monitor class="w-3.5 h-3.5" /> {{ t('settings.themes.midnight') }}
                </button>
                <button
                  @click="settingsStore.setTheme('dracula')"
                  class="w-full text-left px-3 py-1.5 text-xs font-medium rounded-lg flex items-center gap-2 transition-colors cursor-pointer"
                  :class="settingsStore.currentTheme === 'dracula' ? 'bg-slate-800 text-emerald-400' : 'text-slate-400 hover:bg-slate-800 hover:text-slate-200'"
                >
                  <Palette class="w-3.5 h-3.5" /> {{ t('settings.themes.dracula') }}
                </button>
              </div>
            </div>
          </div>

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
