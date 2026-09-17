<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ApiService } from '../api/client'
import type { ValueBetDto } from '../api/types'
import Badge from '../components/ui/Badge.vue'
import PinnacleIcon from '../components/ui/PinnacleIcon.vue'
import { Filter, Search, TrendingUp } from 'lucide-vue-next'

const { t } = useI18n()

const valueBets = ref<ValueBetDto[]>([])
const loading = ref(true)
const minEdge = ref(2.0)
const searchQuery = ref('')
const selectedLeagueId = ref<string>('')
const availableLeagues = ref<{ id: string; name: string }[]>([])

async function loadData() {
  loading.value = true
  try {
    const idParam = selectedLeagueId.value ? selectedLeagueId.value : undefined
    valueBets.value = await ApiService.getValueBets(minEdge.value, idParam)
  } finally {
    loading.value = false
  }
}

async function loadLeagues() {
  try {
    availableLeagues.value = await ApiService.getLeagues()
  } catch (e) {
    console.error("Failed to load leagues", e)
  }
}

onMounted(() => {
  loadLeagues()
  loadData()
})

watch([minEdge, selectedLeagueId], () => {
  loadData()
})

const filteredBets = computed(() => {
  if (!searchQuery.value) return valueBets.value
  
  return valueBets.value.filter(bet => {
    return bet.match.toLowerCase().includes(searchQuery.value.toLowerCase()) ||
           bet.selection.toLowerCase().includes(searchQuery.value.toLowerCase())
  })
})

const currentPage = ref(1)
const itemsPerPage = ref(20)

watch([searchQuery, minEdge, selectedLeagueId, itemsPerPage], () => {
  currentPage.value = 1
})

const paginatedBets = computed(() => {
  const start = (currentPage.value - 1) * itemsPerPage.value
  return filteredBets.value.slice(start, start + itemsPerPage.value)
})

const totalPages = computed(() => Math.ceil(filteredBets.value.length / itemsPerPage.value))
</script>

<template>
  <div class="space-y-6">
    <!-- Header Page Info -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div>
        <h2 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
          <span>💰</span>
          <span>{{ $t('valueBets.title') }}</span>
        </h2>
        <p class="text-sm text-slate-400 mt-1">
          {{ $t('valueBets.subtitle') }}
        </p>
      </div>

      <!-- Live Count Badge -->
      <div class="flex items-center gap-2">
        <Badge variant="emerald">
          <TrendingUp class="w-3.5 h-3.5 mr-1" />
          {{ $t('valueBets.foundBets', { count: filteredBets.length }) }}
        </Badge>
      </div>
    </div>

    <!-- Filters Bar -->
    <div class="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-lg flex flex-wrap items-center gap-4">
      <!-- Search Input -->
      <div class="relative flex-1 min-w-[220px]">
        <Search class="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
        <input
          v-model="searchQuery"
          type="text"
          :placeholder="$t('valueBets.searchPlaceholder')"
          class="w-full bg-slate-950 border border-slate-700 rounded-xl pl-9 pr-3 py-2 text-sm text-slate-100 placeholder-slate-500 focus:outline-none focus:border-emerald-500"
        />
      </div>

      <!-- League Filter -->
      <div class="flex items-center gap-2">
        <Filter class="w-4 h-4 text-slate-400" />
        <select
          v-model="selectedLeagueId"
          class="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-emerald-500"
        >
          <option value="">{{ $t('valueBets.allLeagues') }}</option>
          <option v-for="l in availableLeagues" :key="l.id" :value="l.id">{{ l.name }}</option>
        </select>
      </div>

      <!-- Min Edge Slider -->
      <div class="flex items-center gap-3 min-w-[200px]">
        <label class="text-xs font-semibold text-slate-400 whitespace-nowrap">
          {{ $t('valueBets.minEdge') }} <span class="text-emerald-400 font-tabular">{{ minEdge.toFixed(1) }}%</span>
        </label>
        <input
          type="range"
          v-model.number="minEdge"
          min="0.5"
          max="15.0"
          step="0.5"
          class="w-full accent-emerald-500 cursor-pointer"
        />
      </div>
    </div>

    <!-- Loading Skeleton -->
    <div v-if="loading" class="py-16 text-center text-slate-400">
      <div class="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-400 mb-3"></div>
      <p class="text-sm">{{ $t('valueBets.loading') }}</p>
    </div>

    <!-- Empty State -->
    <div v-else-if="filteredBets.length === 0" class="bg-slate-900 border border-slate-800 rounded-2xl p-12 text-center">
      <span class="text-4xl">🔍</span>
      <h3 class="text-lg font-bold text-slate-200 mt-3">{{ $t('valueBets.noBetsFound') }}</h3>
      <p class="text-sm text-slate-400 mt-1">{{ $t('valueBets.noBetsTip') }}</p>
    </div>

    <!-- Data Table -->
    <div v-else class="bg-slate-900/90 border border-slate-800 rounded-2xl overflow-hidden shadow-2xl flex flex-col backdrop-blur-sm">
      <div class="overflow-auto max-h-[620px] custom-scroll relative">
        <table class="w-full text-left text-sm relative">
          <thead class="sticky top-0 z-20 bg-slate-950/95 backdrop-blur-md text-slate-400 text-xs uppercase font-semibold border-b border-slate-800 shadow-[0_4px_12px_rgba(0,0,0,0.45)]">
            <tr>
              <th class="py-3.5 px-3 whitespace-nowrap bg-slate-950/95">{{ $t('valueBets.table.date') }}</th>
              <th class="py-3.5 px-3 whitespace-nowrap bg-slate-950/95">{{ $t('valueBets.table.league') }}</th>
              <th class="py-3.5 px-3 whitespace-nowrap bg-slate-950/95">{{ $t('valueBets.table.match') }}</th>
              <th class="py-3.5 px-2.5 whitespace-nowrap bg-slate-950/95">{{ $t('valueBets.table.market') }}</th>
              <th class="py-3.5 px-3 whitespace-nowrap bg-slate-950/95">{{ $t('valueBets.table.selection') }}</th>
              <th class="py-3.5 px-2.5 text-right whitespace-nowrap bg-slate-950/95">{{ $t('valueBets.table.probPercent') }}</th>
              <th class="py-3.5 px-2.5 text-right whitespace-nowrap bg-slate-950/95">{{ $t('valueBets.table.trueOdds') }}</th>
              <th class="py-3.5 px-2.5 text-right whitespace-nowrap bg-slate-950/95">
                <span class="inline-flex items-center gap-1.5 justify-end">
                  <PinnacleIcon class="w-3.5 h-3.5" />
                  <span>{{ $t('valueBets.table.pinnacle') }}</span>
                </span>
              </th>
              <th class="py-3.5 px-2.5 whitespace-nowrap bg-slate-950/95">{{ $t('valueBets.table.bookmaker') }}</th>
              <th class="py-3.5 px-3 text-right whitespace-nowrap bg-slate-950/95">{{ $t('valueBets.table.odd') }}</th>
              <th class="py-3.5 px-3 text-right font-bold text-emerald-400 whitespace-nowrap bg-slate-950/95">{{ $t('valueBets.table.edge') }}</th>
              <th class="py-3.5 px-3 text-right whitespace-nowrap bg-slate-950/95">{{ $t('valueBets.table.stakeKelly') }}</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-800/60 font-tabular">
            <tr
              v-for="bet in paginatedBets"
              :key="bet.id"
              class="hover:bg-slate-800/50 transition-colors duration-150"
            >
              <td class="py-3 px-3 text-slate-300 text-xs whitespace-nowrap">{{ bet.date }}</td>
              <td class="py-3 px-3 text-slate-400 text-xs whitespace-nowrap">{{ bet.league }}</td>
              <td class="py-3 px-3 font-medium text-slate-100 whitespace-nowrap">{{ bet.match }}</td>
              <td class="py-3 px-2.5 text-slate-300 whitespace-nowrap">
                <span class="px-2 py-0.5 bg-slate-800/80 rounded text-xs border border-slate-700/50">{{ bet.market }}</span>
              </td>
              <td class="py-3 px-3 font-semibold text-emerald-300 whitespace-nowrap">{{ bet.selection }}</td>
              <td class="py-3 px-2.5 text-right text-slate-300 whitespace-nowrap">{{ bet.probPercent }}</td>
              <td class="py-3 px-2.5 text-right text-slate-400 whitespace-nowrap">{{ bet.trueOdds }}</td>
              <td class="py-3 px-2.5 text-right text-slate-400 font-medium whitespace-nowrap">{{ bet.pinnacle }}</td>
              <td class="py-3 px-2.5 text-slate-300 text-xs whitespace-nowrap">{{ bet.bookmaker }}</td>
              <td class="py-3 px-3 text-right font-bold text-white whitespace-nowrap">{{ bet.odds.toFixed(2) }}</td>
              <td class="py-3 px-3 text-right whitespace-nowrap">
                <span class="font-bold text-emerald-400 bg-emerald-950/70 px-2 py-0.5 rounded border border-emerald-800/60 shadow-xs">
                  +{{ bet.edgeEv.toFixed(2) }}%
                </span>
              </td>
              <td class="py-3 px-3 text-right text-slate-300 whitespace-nowrap">{{ bet.qKellyStake }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      
      <!-- Pagination Controls -->
      <div class="bg-slate-950/50 border-t border-slate-800 p-4 flex flex-col sm:flex-row items-center justify-between gap-4">
        <div class="flex items-center gap-4 text-xs text-slate-400">
          <div class="flex items-center gap-2">
            <span>{{ $t('valueBets.pagination.showing') }}</span>
            <select
              v-model="itemsPerPage"
              class="bg-slate-900 border border-slate-700 rounded px-2 py-1 focus:outline-none focus:border-emerald-500 text-slate-200 cursor-pointer"
            >
              <option :value="10">10</option>
              <option :value="20">20</option>
              <option :value="50">50</option>
              <option :value="100">100</option>
            </select>
          </div>
          <div>
            {{ $t('valueBets.pagination.showing') }} <span class="font-bold text-slate-200">{{ filteredBets.length === 0 ? 0 : ((currentPage - 1) * itemsPerPage) + 1 }}</span> {{ $t('valueBets.pagination.to') }} 
            <span class="font-bold text-slate-200">{{ Math.min(currentPage * itemsPerPage, filteredBets.length) }}</span> {{ $t('valueBets.pagination.of') }} 
            <span class="font-bold text-slate-200">{{ filteredBets.length }}</span> {{ $t('valueBets.pagination.results') }}
          </div>
        </div>
        
        <div class="flex items-center gap-2">
          <button 
            @click="currentPage > 1 && currentPage--"
            :disabled="currentPage === 1"
            class="px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800 disabled:opacity-50 disabled:cursor-not-allowed transition-colors cursor-pointer"
          >
            {{ $t('valueBets.pagination.prev') }}
          </button>
          <div class="text-xs font-semibold text-slate-400 px-2">
            {{ $t('valueBets.pagination.page', { current: currentPage, total: totalPages || 1 }) }}
          </div>
          <button 
            @click="currentPage < totalPages && currentPage++"
            :disabled="currentPage >= totalPages"
            class="px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800 disabled:opacity-50 disabled:cursor-not-allowed transition-colors cursor-pointer"
          >
            {{ $t('valueBets.pagination.next') }}
          </button>
        </div>
      </div>
    </div>

    <!-- Educational Footer -->
    <div class="bg-slate-900/60 border border-slate-800/80 rounded-xl p-4 text-xs text-slate-400 leading-relaxed space-y-1">
      <div class="font-bold text-slate-200">{{ $t('valueBets.help.title') }}</div>
      <div>• <strong>{{ $t('valueBets.table.probPercent') }}</strong>: {{ $t('valueBets.help.prob') }}</div>
      <div>• <strong>{{ $t('valueBets.table.trueOdds') }}</strong>: {{ $t('valueBets.help.trueOdds') }}</div>
      <div>• <strong>{{ $t('valueBets.table.pinnacle') }}</strong>: {{ $t('valueBets.help.pinnacle') }}</div>
      <div>• <strong>{{ $t('valueBets.table.edge') }}</strong>: {{ $t('valueBets.help.edge') }}</div>
      <div>• <strong>{{ $t('valueBets.table.stakeKelly') }}</strong>: {{ $t('valueBets.help.stakeKelly') }}</div>
    </div>
  </div>
</template>
