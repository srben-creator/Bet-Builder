<script setup lang="ts">
import { useLadderStore } from '../../stores/useLadderStore'
import Badge from '../ui/Badge.vue'
import { AlertTriangle, Trash2, Trophy, XCircle } from 'lucide-vue-next'

const ladderStore = useLadderStore()
</script>

<template>
  <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-2xl flex flex-col h-full sticky top-6">
    <div class="flex items-center justify-between pb-4 border-b border-slate-800">
      <div class="flex items-center gap-2">
        <span class="text-xl">🎫</span>
        <h3 class="font-bold text-slate-100 text-lg">Bilhete / Slip</h3>
        <Badge v-if="ladderStore.slip.length > 0" variant="emerald">{{ ladderStore.slip.length }} pernas</Badge>
      </div>
      <button
        v-if="ladderStore.slip.length > 0"
        @click="ladderStore.clearSlip"
        class="text-xs text-slate-400 hover:text-rose-400 transition-colors"
      >
        Limpar tudo
      </button>
    </div>

    <!-- Empty State -->
    <div v-if="ladderStore.slip.length === 0" class="py-12 text-center text-slate-500">
      <p class="text-sm">Seu bilhete está vazio.</p>
      <p class="text-xs text-slate-600 mt-1">Clique em "Adicionar" nas seleções seguras para compor sua aposta.</p>
    </div>

    <!-- Legs List -->
    <div v-else class="flex-1 overflow-y-auto divide-y divide-slate-800/60 my-3">
      <!-- Correlation Alert -->
      <div v-if="ladderStore.hasCorrelation" class="p-3 mb-3 bg-amber-950/40 border border-amber-800/60 rounded-lg flex items-start gap-2 text-amber-300 text-xs">
        <AlertTriangle class="w-4 h-4 shrink-0 mt-0.5 text-amber-400" />
        <div>
          <span class="font-bold">Mesmo jogo detectado!</span> Eventos do mesmo jogo possuem correlação matemática. A probabilidade combinada é uma estimativa.
        </div>
      </div>

      <div
        v-for="(leg, idx) in ladderStore.slip"
        :key="`${leg.fixtureId}_${leg.market}`"
        class="py-3 flex items-start justify-between gap-2 text-sm"
      >
        <div>
          <div class="font-medium text-slate-200">{{ leg.match }}</div>
          <div class="text-xs text-emerald-400 font-semibold">{{ leg.market }}</div>
          <div class="text-xs text-slate-500">{{ (leg.prob * 100).toFixed(1) }}% de probabilidade</div>
        </div>
        <button
          @click="ladderStore.removeLeg(idx)"
          class="text-slate-500 hover:text-rose-400 p-1 transition-colors"
          title="Remover seleção"
        >
          <Trash2 class="w-4 h-4" />
        </button>
      </div>
    </div>

    <!-- Summary & Actions -->
    <div v-if="ladderStore.slip.length > 0" class="pt-4 border-t border-slate-800 space-y-3">
      <div class="flex justify-between text-xs text-slate-400">
        <span>Probabilidade Combinada:</span>
        <span class="font-bold text-slate-200 font-tabular">{{ (ladderStore.combinedProb * 100).toFixed(1) }}%</span>
      </div>

      <div class="flex justify-between text-xs text-slate-400">
        <span>Odd Justa (Sem Margem):</span>
        <span class="font-bold text-slate-200 font-tabular">{{ ladderStore.fairOdds.toFixed(2) }}</span>
      </div>

      <div>
        <label class="block text-xs font-medium text-slate-300 mb-1">Cotação da Casa (Odd Acumulada):</label>
        <input
          type="number"
          v-model.number="ladderStore.customOdds"
          step="0.05"
          min="1.01"
          class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-100 font-tabular text-sm focus:outline-none focus:border-emerald-500"
        />
      </div>

      <div
        class="p-2.5 rounded-lg text-xs font-semibold text-center"
        :class="ladderStore.edgeEv > 0 ? 'bg-emerald-950/60 text-emerald-300 border border-emerald-800' : 'bg-rose-950/60 text-rose-300 border border-rose-800'"
      >
        <span v-if="ladderStore.edgeEv > 0">✅ Aposta com +EV! Vantagem: {{ ladderStore.edgeEv.toFixed(1) }}%</span>
        <span v-else>❌ Aposta com -EV. Perda esperada: {{ Math.abs(ladderStore.edgeEv).toFixed(1) }}%</span>
      </div>

      <div class="flex gap-2">
        <button
          @click="ladderStore.loseStep"
          :disabled="ladderStore.loading"
          class="flex-1 py-3 bg-rose-900 hover:bg-rose-800 disabled:opacity-50 text-white font-bold rounded-xl flex items-center justify-center transition-all cursor-pointer border border-rose-800"
          title="Registrar derrota"
        >
          <XCircle class="w-4 h-4" />
        </button>
        <button
          @click="ladderStore.winStep"
          :disabled="ladderStore.loading || ladderStore.customOdds <= 1.0"
          class="flex-[4] py-3 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 disabled:opacity-50 text-white font-bold rounded-xl shadow-lg flex items-center justify-center gap-2 transition-all cursor-pointer"
        >
          <Trophy class="w-4 h-4" />
          Vencer Passo 🎉
        </button>
      </div>
    </div>
  </div>
</template>
