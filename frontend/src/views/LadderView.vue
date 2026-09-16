<script setup lang="ts">
import { onMounted, ref, computed } from 'vue'
import { useLadderStore } from '../stores/useLadderStore'
import MetricCard from '../components/ui/MetricCard.vue'
import SlipDrawer from '../components/ladder/SlipDrawer.vue'
import Badge from '../components/ui/Badge.vue'
import { Plus, RotateCcw, ShieldCheck } from 'lucide-vue-next'

const ladderStore = useLadderStore()
const selectedDate = ref('Todas')

onMounted(() => {
  ladderStore.loadChallenge()
})

const availableDates = computed(() => {
  const dates = new Set(ladderStore.safeLegs.map(l => l.date))
  return ['Todas', ...Array.from(dates).sort()]
})

const filteredLegs = computed(() => {
  if (selectedDate.value === 'Todas') return ladderStore.safeLegs
  return ladderStore.safeLegs.filter(l => l.date === selectedDate.value)
})

function isAlreadyInSlip(leg: any) {
  return ladderStore.slip.some(l => l.fixtureId === leg.fixtureId && l.market === leg.market)
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header Page Info -->
    <div class="flex items-center justify-between">
      <div>
        <h2 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
          <span>🪜</span>
          <span>The Honest Ladder Challenge</span>
        </h2>
        <p class="text-sm text-slate-400 mt-1">
          Estratégia de crescimento composto de banca através de seleções de altíssima probabilidade com proteção contra correlação.
        </p>
      </div>
    </div>

    <!-- Top Survival Dashboard -->
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      <MetricCard
        title="Banca Atual"
        :value="`€${ladderStore.challenge?.currentBankroll.toFixed(2) || '5.00'}`"
        subtitle="Saldo composto acumulado"
        trend="up"
      />
      <MetricCard
        title="Meta Final"
        :value="`€${ladderStore.challenge?.targetAmount.toFixed(2) || '50.00'}`"
        subtitle="Objetivo do desafio"
      />
      <MetricCard
        title="Passo Atual"
        :value="`Passo ${ladderStore.challenge?.currentStep || 1}`"
        subtitle="Etapa em andamento"
      />

      <!-- Reset Action Card -->
      <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg flex flex-col justify-between">
        <div class="text-xs font-medium uppercase tracking-wider text-slate-400 mb-1">Ação de Banca</div>
        <button
          @click="ladderStore.resetChallenge"
          class="w-full py-2.5 px-4 bg-slate-800 hover:bg-slate-700 text-slate-200 text-sm font-semibold rounded-lg border border-slate-700 flex items-center justify-center gap-2 transition-all cursor-pointer"
        >
          <RotateCcw class="w-4 h-4 text-amber-400" />
          <span>Reiniciar Ladder</span>
        </button>
        <div class="text-xs text-slate-500 mt-2">Volta ao Passo 1 com €5.00</div>
      </div>
    </div>

    <!-- Main Content: Safe Legs (Left) + Slip Drawer (Right) -->
    <div class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
      <!-- Left Column: Safe Legs -->
      <div class="lg:col-span-7 space-y-4">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <ShieldCheck class="w-5 h-5 text-emerald-400" />
            <h3 class="font-bold text-slate-100 text-lg">Seleções Seguras (>75% Prob)</h3>
          </div>

          <!-- Date Filter -->
          <div class="flex items-center gap-2">
            <span class="text-xs text-slate-400">Data:</span>
            <select
              v-model="selectedDate"
              class="bg-slate-900 border border-slate-700 rounded-lg px-2.5 py-1 text-xs text-slate-200 focus:outline-none"
            >
              <option v-for="d in availableDates" :key="d" :value="d">{{ d }}</option>
            </select>
          </div>
        </div>

        <div v-if="ladderStore.loading" class="py-12 text-center text-slate-400">
          <div class="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-400 mb-3"></div>
          <p class="text-sm">Carregando pernas seguras...</p>
        </div>

        <div v-else-if="filteredLegs.length === 0" class="bg-slate-900 border border-slate-800 rounded-2xl p-8 text-center text-slate-400">
          <p class="text-sm">Nenhuma seleção de alta probabilidade disponível no momento.</p>
          <p class="text-xs text-slate-500 mt-1">Execute o sincronismo de odds pelo botão "Sync Odds" no cabeçalho.</p>
        </div>

        <div v-else class="space-y-2.5">
          <div
            v-for="leg in filteredLegs"
            :key="`${leg.fixtureId}_${leg.market}`"
            class="bg-slate-900 border border-slate-800 hover:border-slate-700 rounded-xl p-4 flex items-center justify-between gap-4 transition-all"
          >
            <div class="space-y-1">
              <div class="text-xs text-slate-500">{{ leg.date }}</div>
              <div class="font-semibold text-slate-100 text-sm">{{ leg.match }}</div>
              <div class="flex items-center gap-2">
                <Badge variant="blue">{{ leg.market }}</Badge>
                <span class="text-xs font-bold text-emerald-400 font-tabular">🎯 {{ (leg.prob * 100).toFixed(1) }}% prob</span>
              </div>
            </div>

            <button
              @click="ladderStore.addLeg(leg)"
              :disabled="isAlreadyInSlip(leg)"
              class="px-3 py-1.5 rounded-lg text-xs font-bold flex items-center gap-1.5 transition-all cursor-pointer"
              :class="isAlreadyInSlip(leg) ? 'bg-slate-800 text-slate-500 cursor-not-allowed' : 'bg-emerald-600 hover:bg-emerald-500 text-white shadow'"
            >
              <Plus class="w-3.5 h-3.5" />
              <span>{{ isAlreadyInSlip(leg) ? 'Adicionado' : 'Adicionar' }}</span>
            </button>
          </div>
        </div>
      </div>

      <!-- Right Column: Slip Drawer -->
      <div class="lg:col-span-5">
        <SlipDrawer />
      </div>
    </div>
  </div>
</template>
