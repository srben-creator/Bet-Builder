<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { ApiService } from '../api/client'
import type { ValueBetDto } from '../api/types'
import Badge from '../components/ui/Badge.vue'
import { Filter, Search, TrendingUp } from 'lucide-vue-next'

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

watch([searchQuery, minEdge, selectedLeagueId], () => {
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
          <span>+EV Value Bets Tracker</span>
        </h2>
        <p class="text-sm text-slate-400 mt-1">
          Apostas com vantagem estatística matemática calculadas pelo modelo Dixon-Coles contra a linha de corte da Pinnacle.
        </p>
      </div>

      <!-- Live Count Badge -->
      <div class="flex items-center gap-2">
        <Badge variant="emerald">
          <TrendingUp class="w-3.5 h-3.5 mr-1" />
          {{ filteredBets.length }} apostas de valor encontradas
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
          placeholder="Buscar time ou seleção..."
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
          <option value="">Todas</option>
          <option v-for="l in availableLeagues" :key="l.id" :value="l.id">{{ l.name }}</option>
        </select>
      </div>

      <!-- Min Edge Slider -->
      <div class="flex items-center gap-3 min-w-[200px]">
        <label class="text-xs font-semibold text-slate-400 whitespace-nowrap">
          Edge Mínimo: <span class="text-emerald-400 font-tabular">{{ minEdge.toFixed(1) }}%</span>
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
      <p class="text-sm">Carregando apostas de valor...</p>
    </div>

    <!-- Empty State -->
    <div v-else-if="filteredBets.length === 0" class="bg-slate-900 border border-slate-800 rounded-2xl p-12 text-center">
      <span class="text-4xl">🔍</span>
      <h3 class="text-lg font-bold text-slate-200 mt-3">Nenhuma aposta +EV encontrada</h3>
      <p class="text-sm text-slate-400 mt-1">Tente reduzir o Edge mínimo ou clique no botão "Sync Odds" no cabeçalho para baixar novas cotações.</p>
    </div>

    <!-- Data Table -->
    <div v-else class="bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden shadow-xl flex flex-col">
      <div class="overflow-auto max-h-[600px] relative">
        <table class="w-full text-left text-sm relative">
          <thead class="sticky top-0 z-10 bg-slate-950 text-slate-400 text-xs uppercase font-semibold shadow-[0_1px_0_0_#1e293b]">
            <tr>
              <th class="py-3.5 px-4 bg-slate-950">Data</th>
              <th class="py-3.5 px-4 bg-slate-950">Liga</th>
              <th class="py-3.5 px-4 bg-slate-950">Partida</th>
              <th class="py-3.5 px-4 bg-slate-950">Mercado</th>
              <th class="py-3.5 px-4 bg-slate-950">Seleção</th>
              <th class="py-3.5 px-4 bg-slate-950 text-right">Prob %</th>
              <th class="py-3.5 px-4 bg-slate-950 text-right">True Odds</th>
              <th class="py-3.5 px-4 bg-slate-950 text-right">Pinnacle</th>
              <th class="py-3.5 px-4 bg-slate-950">Casa</th>
              <th class="py-3.5 px-4 bg-slate-950 text-right">Odd</th>
              <th class="py-3.5 px-4 bg-slate-950 text-right font-bold text-emerald-400">Edge (+EV)</th>
              <th class="py-3.5 px-4 bg-slate-950 text-right">Stake Kelly</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-800/60 font-tabular">
            <tr
              v-for="bet in paginatedBets"
              :key="bet.id"
              class="hover:bg-slate-800/40 transition-colors"
            >
              <td class="py-3 px-4 text-slate-300 text-xs whitespace-nowrap">{{ bet.date }}</td>
              <td class="py-3 px-4 text-slate-400 text-xs whitespace-nowrap">{{ bet.league }}</td>
              <td class="py-3 px-4 font-medium text-slate-100 whitespace-nowrap">{{ bet.match }}</td>
              <td class="py-3 px-4 text-slate-300">
                <span class="px-2 py-0.5 bg-slate-800 rounded text-xs">{{ bet.market }}</span>
              </td>
              <td class="py-3 px-4 font-semibold text-emerald-300">{{ bet.selection }}</td>
              <td class="py-3 px-4 text-right text-slate-300">{{ bet.probPercent }}</td>
              <td class="py-3 px-4 text-right text-slate-400">{{ bet.trueOdds }}</td>
              <td class="py-3 px-4 text-right text-slate-400 font-medium">{{ bet.pinnacle }}</td>
              <td class="py-3 px-4 text-slate-300 text-xs">{{ bet.bookmaker }}</td>
              <td class="py-3 px-4 text-right font-bold text-white">{{ bet.odds.toFixed(2) }}</td>
              <td class="py-3 px-4 text-right">
                <span class="font-bold text-emerald-400 bg-emerald-950/70 px-2 py-0.5 rounded border border-emerald-800/60">
                  +{{ bet.edgeEv.toFixed(2) }}%
                </span>
              </td>
              <td class="py-3 px-4 text-right text-slate-300">{{ bet.qKellyStake }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      
      <!-- Pagination Controls -->
      <div class="bg-slate-950/50 border-t border-slate-800 p-4 flex flex-col sm:flex-row items-center justify-between gap-4">
        <div class="text-xs text-slate-400">
          Mostrando <span class="font-bold text-slate-200">{{ ((currentPage - 1) * itemsPerPage) + 1 }}</span> a 
          <span class="font-bold text-slate-200">{{ Math.min(currentPage * itemsPerPage, filteredBets.length) }}</span> de 
          <span class="font-bold text-slate-200">{{ filteredBets.length }}</span> resultados
        </div>
        
        <div class="flex items-center gap-2">
          <button 
            @click="currentPage > 1 && currentPage--"
            :disabled="currentPage === 1"
            class="px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800 disabled:opacity-50 disabled:cursor-not-allowed transition-colors cursor-pointer"
          >
            Anterior
          </button>
          <div class="text-xs font-semibold text-slate-400 px-2">
            Pág {{ currentPage }} / {{ totalPages || 1 }}
          </div>
          <button 
            @click="currentPage < totalPages && currentPage++"
            :disabled="currentPage >= totalPages"
            class="px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800 disabled:opacity-50 disabled:cursor-not-allowed transition-colors cursor-pointer"
          >
            Próxima
          </button>
        </div>
      </div>
    </div>

    <!-- Educational Footer -->
    <div class="bg-slate-900/60 border border-slate-800/80 rounded-xl p-4 text-xs text-slate-400 leading-relaxed space-y-1">
      <div class="font-bold text-slate-200">💡 Como interpretar esta tabela:</div>
      <div>• <strong>Prob %</strong>: Probabilidade matemática calculada pelo motor Dixon-Coles com base no xG histórico.</div>
      <div>• <strong>True Odds</strong>: A cotação justa teórica (1 / Probabilidade).</div>
      <div>• <strong>Pinnacle</strong>: A linha da casa mais afiada do mundo. Usada como filtro de segurança (a aposta só é exibida se a cotação da casa recreativa for superior à da Pinnacle).</div>
      <div>• <strong>Edge (+EV)</strong>: Sua vantagem percentual estimada sobre a margem da casa.</div>
      <div>• <strong>Stake Kelly</strong>: Fração conservadora recomendada da sua banca total (estratégia Quarter-Kelly).</div>
    </div>
  </div>
</template>
