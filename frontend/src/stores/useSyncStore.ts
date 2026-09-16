import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiService } from '../api/client'
import type { SyncResultDto } from '../api/types'

export const useSyncStore = defineStore('sync', () => {
  const syncingOdds = ref(false)
  const syncingResults = ref(false)
  const lastResult = ref<SyncResultDto | null>(null)

  async function syncOdds(): Promise<SyncResultDto> {
    syncingOdds.value = true
    try {
      const res = await ApiService.syncLiveOdds()
      lastResult.value = res
      return res
    } catch (e: any) {
      const err = { success: false, message: e.response?.data?.detail || e.message || 'Falha ao sincronizar odds.', fixturesUpdated: 0, oddsUpdated: 0, valueBetsGenerated: 0, betsSettled: 0 }
      lastResult.value = err
      return err
    } finally {
      syncingOdds.value = false
    }
  }

  async function syncResults(): Promise<SyncResultDto> {
    syncingResults.value = true
    try {
      const res = await ApiService.syncWeekendAndSettle()
      lastResult.value = res
      return res
    } catch (e: any) {
      const err = { success: false, message: e.response?.data?.detail || e.message || 'Falha ao liquidar resultados.', fixturesUpdated: 0, oddsUpdated: 0, valueBetsGenerated: 0, betsSettled: 0 }
      lastResult.value = err
      return err
    } finally {
      syncingResults.value = false
    }
  }

  return {
    syncingOdds,
    syncingResults,
    lastResult,
    syncOdds,
    syncResults
  }
})
