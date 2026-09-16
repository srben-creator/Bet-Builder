<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { ApiService } from '../api/client'
import type { PerformanceDashboardDto } from '../api/types'
import MetricCard from '../components/ui/MetricCard.vue'
import Badge from '../components/ui/Badge.vue'
import { Line } from 'vue-chartjs'
import {
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  LineElement,
  LinearScale,
  PointElement,
  CategoryScale,
  Filler
} from 'chart.js'

ChartJS.register(
  Title,
  Tooltip,
  Legend,
  LineElement,
  LinearScale,
  PointElement,
  CategoryScale,
  Filler
)

const dashboard = ref<PerformanceDashboardDto | null>(null)
const loading = ref(true)

onMounted(async () => {
  loading.value = true
  try {
    dashboard.value = await ApiService.getPerformance()
  } finally {
    loading.value = false
  }
})

const chartData = computed(() => {
  if (!dashboard.value || dashboard.value.bankrollEvolution.length === 0) {
    return { labels: [], datasets: [] }
  }

  const labels = dashboard.value.bankrollEvolution.map(p => p.date)
  const data = dashboard.value.bankrollEvolution.map(p => p.cumulativePnl)

  return {
    labels,
    datasets: [
      {
        label: 'P&L Acumulado (Unidades)',
        data,
        borderColor: '#10b981',
        backgroundColor: 'rgba(16, 185, 129, 0.1)',
        fill: true,
        tension: 0.3,
        pointRadius: 3,
        pointBackgroundColor: '#10b981'
      }
    ]
  }
})

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      display: false
    },
    tooltip: {
      backgroundColor: '#0f172a',
      titleColor: '#f1f5f9',
      bodyColor: '#10b981',
      borderColor: '#334155',
      borderWidth: 1
    }
  },
  scales: {
    x: {
      grid: {
        color: 'rgba(51, 65, 85, 0.3)'
      },
      ticks: {
        color: '#64748b',
        maxTicksLimit: 8
      }
    },
    y: {
      grid: {
        color: 'rgba(51, 65, 85, 0.3)'
      },
      ticks: {
        color: '#64748b'
      }
    }
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header Page Info -->
    <div>
      <h2 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
        <span>📈</span>
        <span>Performance & Evolução de Banca</span>
      </h2>
      <p class="text-sm text-slate-400 mt-1">
        Acompanhe o desempenho no mundo real da estratégia de apostas com valor esperado positivo (+EV).
      </p>
    </div>

    <!-- KPI Metric Cards -->
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      <MetricCard
        title="Apostas Liquidadas"
        :value="dashboard?.totalSettledBets || 0"
        subtitle="Total de apostas finalizadas"
      />
      <MetricCard
        title="Taxa de Acerto"
        :value="`${dashboard?.winRate.toFixed(1) || '0.0'}%`"
        subtitle="Win Rate global"
        :trend="(dashboard?.winRate || 0) >= 50 ? 'up' : 'down'"
      />
      <MetricCard
        title="P&L Total (Unidades)"
        :value="`${(dashboard?.totalProfit || 0) >= 0 ? '+' : ''}${dashboard?.totalProfit.toFixed(2) || '0.00'}`"
        subtitle="Lucro acumulado em unidades"
        :trend="(dashboard?.totalProfit || 0) >= 0 ? 'up' : 'down'"
      />
      <MetricCard
        title="ROI Geral"
        :value="`${(dashboard?.roi || 0) >= 0 ? '+' : ''}${dashboard?.roi.toFixed(1) || '0.0'}%`"
        subtitle="Retorno sobre investimento"
        :trend="(dashboard?.roi || 0) >= 0 ? 'up' : 'down'"
      />
    </div>

    <!-- Chart: Bankroll Evolution -->
    <div class="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
      <div class="flex items-center justify-between">
        <div>
          <h3 class="font-bold text-slate-100 text-base">Evolução do Saldo (Curva P&L)</h3>
          <p class="text-xs text-slate-500">Unidades de lucro acumuladas ao longo do tempo</p>
        </div>
      </div>

      <div v-if="loading" class="h-64 flex items-center justify-center text-slate-400">
        <div class="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-400"></div>
      </div>
      <div v-else-if="!dashboard || dashboard.bankrollEvolution.length === 0" class="h-64 flex items-center justify-center text-slate-500 text-sm">
        Nenhum dado de P&L registrado até o momento.
      </div>
      <div v-else class="h-72">
        <Line :data="chartData" :options="chartOptions" />
      </div>
    </div>

    <!-- Settled Bets History Table -->
    <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
      <h3 class="font-bold text-slate-100 text-base">Histórico de Apostas Finalizadas</h3>

      <div v-if="!dashboard || dashboard.history.length === 0" class="py-8 text-center text-slate-500 text-sm">
        Nenhuma aposta liquidada ainda.
      </div>

      <div v-else class="overflow-x-auto">
        <table class="w-full text-left text-sm">
          <thead class="bg-slate-950 text-slate-400 text-xs uppercase font-semibold border-b border-slate-800">
            <tr>
              <th class="py-3 px-4">Data / Hora</th>
              <th class="py-3 px-4">Mercado</th>
              <th class="py-3 px-4">Seleção</th>
              <th class="py-3 px-4 text-center">Resultado</th>
              <th class="py-3 px-4 text-right">Cotação</th>
              <th class="py-3 px-4 text-right">Stake</th>
              <th class="py-3 px-4 text-right">P&L</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-800/60 font-tabular">
            <tr v-for="bet in dashboard.history" :key="bet.id" class="hover:bg-slate-800/40">
              <td class="py-3 px-4 text-xs text-slate-400">{{ bet.date }}</td>
              <td class="py-3 px-4 text-xs text-slate-300">{{ bet.market }}</td>
              <td class="py-3 px-4 font-semibold text-slate-200">{{ bet.selection }}</td>
              <td class="py-3 px-4 text-center">
                <Badge :variant="bet.result === 'won' ? 'emerald' : bet.result === 'void' ? 'amber' : 'rose'">
                  {{ bet.result.toUpperCase() }}
                </Badge>
              </td>
              <td class="py-3 px-4 text-right font-medium text-slate-200">{{ bet.odds.toFixed(2) }}</td>
              <td class="py-3 px-4 text-right text-slate-400">{{ bet.stake.toFixed(2) }} u</td>
              <td
                class="py-3 px-4 text-right font-bold"
                :class="bet.pnl >= 0 ? 'text-emerald-400' : 'text-rose-400'"
              >
                {{ bet.pnl >= 0 ? '+' : '' }}{{ bet.pnl.toFixed(2) }} u
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
