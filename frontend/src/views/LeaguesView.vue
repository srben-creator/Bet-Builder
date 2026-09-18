<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { ApiService } from '../api/client'
import type { LeagueDto, UpdateLeagueDto } from '../api/types'
import { useSyncStore } from '../stores/useSyncStore'
import { RefreshCw, CheckCircle2 } from 'lucide-vue-next'

const syncStore = useSyncStore()

const leagues = ref<LeagueDto[]>([])
const loading = ref(false)
const seeding = ref(false)
const searchQuery = ref('')
const statusFilter = ref('all') // 'all', 'active', 'inactive'

const fetchLeagues = async () => {
  loading.value = true
  try {
    leagues.value = await ApiService.getLeaguesManage()
  } catch (err) {
    console.error('Failed to fetch leagues:', err)
  } finally {
    loading.value = false
  }
}

const seedLeagues = async () => {
  seeding.value = true
  try {
    await ApiService.seedLeagues()
    await fetchLeagues()
  } catch (err) {
    console.error('Failed to seed leagues:', err)
  } finally {
    seeding.value = false
  }
}

const toggleLeague = async (league: LeagueDto) => {
  try {
    const dto: UpdateLeagueDto = {
      isActive: !league.isActive,
      understatName: league.understatName
    }
    const updated = await ApiService.updateLeague(league.id, dto)
    const index = leagues.value.findIndex((l) => l.id === league.id)
    if (index !== -1) {
      leagues.value[index] = updated
    }
  } catch (err) {
    console.error('Failed to update league:', err)
  }
}

const filteredLeagues = computed(() => {
  let result = leagues.value

  if (statusFilter.value === 'active') {
    result = result.filter(l => l.isActive)
  } else if (statusFilter.value === 'inactive') {
    result = result.filter(l => !l.isActive)
  }

  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(
      (l) =>
        l.name.toLowerCase().includes(query) ||
        l.country.toLowerCase().includes(query) ||
        (l.fdCsvCode && l.fdCsvCode.toLowerCase().includes(query))
    )
  }

  return result
})

onMounted(() => {
  fetchLeagues()
})
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between">
      <h1 class="text-3xl font-bold tracking-tight text-white">Leagues Management</h1>
      <div class="flex items-center space-x-4">
        <button
          @click="syncStore.syncOdds"
          :disabled="syncStore.syncingOdds"
          class="rounded-md bg-indigo-500/20 px-3 py-2 text-sm font-semibold text-indigo-400 shadow-sm ring-1 ring-inset ring-indigo-500/50 hover:bg-indigo-500/30 flex items-center gap-2 disabled:opacity-50"
        >
          <RefreshCw class="w-4 h-4" :class="{ 'animate-spin': syncStore.syncingOdds }" />
          {{ syncStore.syncingOdds ? 'Syncing Odds...' : 'Sync Odds' }}
        </button>
        <button
          @click="syncStore.syncResults"
          :disabled="syncStore.syncingResults"
          class="rounded-md bg-emerald-500/20 px-3 py-2 text-sm font-semibold text-emerald-400 shadow-sm ring-1 ring-inset ring-emerald-500/50 hover:bg-emerald-500/30 flex items-center gap-2 disabled:opacity-50"
        >
          <CheckCircle2 class="w-4 h-4" :class="{ 'animate-spin': syncStore.syncingResults }" />
          {{ syncStore.syncingResults ? 'Settling...' : 'Settle Bets' }}
        </button>
        <button
          @click="seedLeagues"
          :disabled="seeding"
          class="rounded-md bg-white/10 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-white/20"
        >
          {{ seeding ? 'Seeding...' : 'Load Standard Leagues' }}
        </button>
      </div>
    </div>
    
    <div class="flex items-center gap-4 bg-gray-900 p-4 rounded-xl shadow ring-1 ring-white/10">
      <div class="relative flex-1 max-w-sm">
        <input
          v-model="searchQuery"
          type="text"
          placeholder="Search leagues..."
          class="block w-full rounded-md border-0 bg-white/5 py-1.5 px-3 text-white shadow-sm ring-1 ring-inset ring-white/10 focus:ring-2 focus:ring-inset focus:ring-indigo-500 sm:text-sm sm:leading-6"
        />
      </div>
      <div>
        <select
          v-model="statusFilter"
          class="block w-full rounded-md border-0 bg-white/5 py-1.5 pl-3 pr-10 text-white shadow-sm ring-1 ring-inset ring-white/10 focus:ring-2 focus:ring-inset focus:ring-indigo-500 sm:text-sm sm:leading-6"
        >
          <option value="all" class="bg-slate-900 text-white">All Status</option>
          <option value="active" class="bg-slate-900 text-white">Active Only</option>
          <option value="inactive" class="bg-slate-900 text-white">Inactive Only</option>
        </select>
      </div>
    </div>

    <div v-if="loading" class="text-center py-12">
      <div class="inline-block h-8 w-8 animate-spin rounded-full border-4 border-solid border-indigo-500 border-r-transparent align-[-0.125em] text-indigo-500" role="status">
        <span class="!absolute !-m-px !h-px !w-px !overflow-hidden !whitespace-nowrap !border-0 !p-0 ![clip:rect(0,0,0,0)]">Loading...</span>
      </div>
    </div>
    
    <div v-else-if="leagues.length === 0" class="text-center py-12 text-gray-400">
      No leagues found. Click "Load Standard Leagues" to populate from football-data.co.uk presets.
    </div>

    <div v-else class="overflow-hidden rounded-xl bg-gray-900 shadow ring-1 ring-white/10">
      <table class="min-w-full divide-y divide-gray-800">
        <thead class="bg-gray-800/50">
          <tr>
            <th scope="col" class="py-3.5 pl-4 pr-3 text-left text-sm font-semibold text-white sm:pl-6">Country</th>
            <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-white">Name</th>
            <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-white">System Code</th>
            <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-white">FD Code</th>
            <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-white">Understat</th>
            <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-white">Status</th>
            <th scope="col" class="px-3 py-3.5 text-right text-sm font-semibold text-white">Actions</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-800 bg-gray-900">
          <tr v-for="league in filteredLeagues" :key="league.id">
            <td class="whitespace-nowrap py-4 pl-4 pr-3 text-sm font-medium text-white sm:pl-6">
              {{ league.country }}
            </td>
            <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-300">{{ league.name }}</td>
            <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-400 font-mono text-xs">{{ league.code }}</td>
            <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-300">
              <span v-if="league.fdCsvCode" class="inline-flex items-center rounded-md bg-blue-400/10 px-2 py-1 text-xs font-medium text-blue-400 ring-1 ring-inset ring-blue-400/30">
                {{ league.fdCsvCode }}
              </span>
              <span v-else class="text-gray-500">-</span>
            </td>
            <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-300">{{ league.understatName || '-' }}</td>
            <td class="whitespace-nowrap px-3 py-4 text-sm">
              <span
                class="inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ring-1 ring-inset"
                :class="[
                  league.isActive 
                    ? 'bg-green-400/10 text-green-400 ring-green-400/20' 
                    : 'bg-gray-400/10 text-gray-400 ring-gray-400/20'
                ]"
              >
                {{ league.isActive ? 'Active' : 'Inactive' }}
              </span>
            </td>
            <td class="whitespace-nowrap px-3 py-4 text-right text-sm font-medium">
              <button
                @click="toggleLeague(league)"
                class="text-indigo-400 hover:text-indigo-300"
              >
                {{ league.isActive ? 'Disable' : 'Enable' }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
