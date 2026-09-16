<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { ApiService } from '../api/client'
import type { FixtureDto } from '../api/types'
import Badge from '../components/ui/Badge.vue'
import { Calendar, Clock, Trophy } from 'lucide-vue-next'

const fixtures = ref<FixtureDto[]>([])
const loading = ref(true)

onMounted(async () => {
  loading.value = true
  try {
    fixtures.value = await ApiService.getFixtures()
  } finally {
    loading.value = false
  }
})

// Group fixtures by date
const groupedFixtures = computed(() => {
  const groups: Record<string, FixtureDto[]> = {}
  for (const f of fixtures.value) {
    if (!groups[f.date]) {
      groups[f.date] = []
    }
    groups[f.date].push(f)
  }
  return groups
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header Page Info -->
    <div class="flex items-center justify-between">
      <div>
        <h2 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
          <span>📅</span>
          <span>Jogos Agendados</span>
        </h2>
        <p class="text-sm text-slate-400 mt-1">
          Calendário completo de partidas futuras monitoradas pelo sistema nas principais ligas europeias.
        </p>
      </div>

      <Badge variant="blue">{{ fixtures.length }} jogos agendados</Badge>
    </div>

    <!-- Loading Skeleton -->
    <div v-if="loading" class="py-16 text-center text-slate-400">
      <div class="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-400 mb-3"></div>
      <p class="text-sm">Carregando calendário de partidas...</p>
    </div>

    <!-- Empty State -->
    <div v-else-if="fixtures.length === 0" class="bg-slate-900 border border-slate-800 rounded-2xl p-12 text-center text-slate-400">
      <Calendar class="w-10 h-10 mx-auto text-slate-600 mb-3" />
      <h3 class="text-lg font-bold text-slate-200">Nenhum jogo agendado encontrado</h3>
      <p class="text-sm text-slate-500 mt-1">Clique em "Sync Odds" no cabeçalho para carregar as próximas rodadas.</p>
    </div>

    <!-- Grouped Match Cards -->
    <div v-else class="space-y-8">
      <div v-for="(matchList, dateKey) in groupedFixtures" :key="dateKey" class="space-y-3">
        <!-- Date Header -->
        <div class="flex items-center gap-2 text-sm font-bold text-emerald-400 pb-1 border-b border-slate-800">
          <Calendar class="w-4 h-4" />
          <span>{{ dateKey }}</span>
          <span class="text-xs font-normal text-slate-500">({{ matchList.length }} jogos)</span>
        </div>

        <!-- Matches Grid -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
          <div
            v-for="fix in matchList"
            :key="fix.id"
            class="bg-slate-900 border border-slate-800 hover:border-slate-700 rounded-xl p-4 flex items-center justify-between gap-4 transition-colors"
          >
            <div class="space-y-1">
              <div class="flex items-center gap-2">
                <Trophy class="w-3.5 h-3.5 text-slate-500" />
                <span class="text-xs text-slate-400">{{ fix.league }}</span>
              </div>
              <div class="font-bold text-slate-100 text-sm">
                {{ fix.homeTeam }} <span class="text-slate-500 font-normal">vs</span> {{ fix.awayTeam }}
              </div>
            </div>

            <div class="text-right space-y-1">
              <div class="flex items-center gap-1 text-xs text-slate-400 justify-end">
                <Clock class="w-3 h-3 text-slate-500" />
                <span class="font-tabular font-medium">{{ fix.time }} UTC</span>
              </div>
              <Badge variant="slate">{{ fix.status }}</Badge>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
