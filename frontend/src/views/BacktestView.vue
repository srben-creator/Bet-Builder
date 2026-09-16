<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { ApiService } from '../api/client'
import type { BacktestReportDto } from '../api/types'
import MetricCard from '../components/ui/MetricCard.vue'
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

const report = ref<BacktestReportDto | null>(null)
const loading = ref(true)

onMounted(async () => {
  loading.value = true
  try {
    report.value = await ApiService.getBacktestReport()
  } finally {
    loading.value = false
  }
})

const chartData = computed(() => {
  if (!report.value || report.value.cumulativePnlEvolution.length === 0) {
    return { labels: [], datasets: [] }
  }

  const labels = report.value.cumulativePnlEvolution.map(p => p.date)
  const data = report.value.cumulativePnlEvolution.map(p => p.cumulativePnl)

  return {
    labels,
    datasets: [
      {
        label: 'P&L Simulado (Unidades)',
        data,
        borderColor: '#38bdf8',
        backgroundColor: 'rgba(56, 189, 248, 0.1)',
        fill: true,
        tension: 0.2,
        pointRadius: 2,
        pointBackgroundColor: '#38bdf8'
      }
    ]
  }
})

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { display: false },
    tooltip: {
      backgroundColor: '#0f172a',
      titleColor: '#f1f5f9',
      bodyColor: '#38bdf8',
      borderColor: '#334155',
      borderWidth: 1
    }
  },
  scales: {
    x: {
      grid: { color: 'rgba(51, 65, 85, 0.3)' },
      ticks: { color: '#64748b', maxTicksLimit: 8 }
    },
    y: {
      grid: { color: 'rgba(51, 65, 85, 0.3)' },
      ticks: { color: '#64748b' }
    }
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header Page Info -->
    <div>
      <h2 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
        <span>🔬</span>
        <span>Relatório de Backtest & Closing Line Value (CLV)</span>
      </h2>
      <p class="text-sm text-slate-400 mt-1">
        Avaliação retrospectiva do modelo Dixon-Coles simulando temporadas anteriores sem vazamento de dados futuros.
      </p>
    </div>

    <!-- Top Metric Cards -->
    <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-4">
      <MetricCard
        title="Apostas Simuladas"
        :value="report?.totalSimulatedBets || 0"
      />
      <MetricCard
        title="Taxa de Acerto"
        :value="`${report?.winRate.toFixed(1) || '0.0'}%`"
      />
      <MetricCard
        title="ROI Geral"
        :value="`${(report?.overallRoi || 0) >= 0 ? '+' : ''}${report?.overallRoi.toFixed(2) || '0.00'}%`"
        :trend="(report?.overallRoi || 0) >= 0 ? 'up' : 'down'"
      />
      <MetricCard
        title="Superou Fechamento"
        :value="`${report?.beatClosingRate.toFixed(1) || '0.0'}%`"
        subtitle="Beat Closing Line Rate"
        trend="up"
      />
      <MetricCard
        title="Edge Médio CLV"
        :value="`${(report?.avgClvEdge || 0) >= 0 ? '+' : ''}${report?.avgClvEdge.toFixed(2) || '0.00'}%`"
        subtitle="Vantagem média sobre Pinnacle"
      />
    </div>

    <!-- Chart: Cumulative Backtest Curve -->
    <div class="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
      <h3 class="font-bold text-slate-100 text-base">Curva de P&L Simulado (Walk-Forward)</h3>

      <div v-if="loading" class="h-64 flex items-center justify-center text-slate-400">
        <div class="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-sky-400"></div>
      </div>
      <div v-else-if="!report || report.cumulativePnlEvolution.length === 0" class="h-64 flex items-center justify-center text-slate-500 text-sm">
        Nenhum dado de backtest encontrado no banco de dados.
      </div>
      <div v-else class="h-72">
        <Line :data="chartData" :options="chartOptions" />
      </div>
    </div>

    <!-- Performance by Market Table -->
    <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
      <h3 class="font-bold text-slate-100 text-base">Performance por Mercado</h3>

      <div v-if="!report || report.marketStats.length === 0" class="py-8 text-center text-slate-500 text-sm">
        Nenhum registro por mercado disponível.
      </div>

      <div v-else class="overflow-x-auto">
        <table class="w-full text-left text-sm">
          <thead class="bg-slate-950 text-slate-400 text-xs uppercase font-semibold border-b border-slate-800">
            <tr>
              <th class="py-3 px-4">Mercado</th>
              <th class="py-3 px-4 text-right">Apostas</th>
              <th class="py-3 px-4 text-right">Taxa de Acerto</th>
              <th class="py-3 px-4 text-right">Total Apostado</th>
              <th class="py-3 px-4 text-right">Lucro (P&L)</th>
              <th class="py-3 px-4 text-right font-bold text-emerald-400">ROI (%)</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-800/60 font-tabular">
            <tr v-for="stat in report.marketStats" :key="stat.market" class="hover:bg-slate-800/40">
              <td class="py-3 px-4 font-semibold text-slate-200">
                <span class="px-2 py-0.5 bg-slate-800 rounded text-xs">{{ stat.market }}</span>
              </td>
              <td class="py-3 px-4 text-right text-slate-300">{{ stat.bets }}</td>
              <td class="py-3 px-4 text-right text-slate-300">{{ stat.winRate.toFixed(1) }}%</td>
              <td class="py-3 px-4 text-right text-slate-400">{{ stat.totalStaked.toFixed(2) }} u</td>
              <td
                class="py-3 px-4 text-right font-medium"
                :class="stat.totalProfit >= 0 ? 'text-emerald-400' : 'text-rose-400'"
              >
                {{ stat.totalProfit >= 0 ? '+' : '' }}{{ stat.totalProfit.toFixed(2) }} u
              </td>
              <td
                class="py-3 px-4 text-right font-bold"
                :class="stat.roi >= 0 ? 'text-emerald-400' : 'text-rose-400'"
              >
                {{ stat.roi >= 0 ? '+' : '' }}{{ stat.roi.toFixed(2) }}%
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
