import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiService } from '../api/client'
import type { SyncResultDto } from '../api/types'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

export const useSyncStore = defineStore('sync', () => {
  const syncingOdds = ref(false)
  const syncingResults = ref(false)
  const lastResult = ref<SyncResultDto | null>(null)
  const syncProgress = ref<string>('')

  // Initialize SignalR
  const connection = new HubConnectionBuilder()
    .withUrl('/api/hubs/sync')
    .configureLogging(LogLevel.Information)
    .build()

  connection.on('ReceiveSyncProgress', (message: string) => {
    syncProgress.value = message
  })

  // Start connection
  connection.start().catch(err => console.error('SignalR error:', err))

  async function syncOdds(): Promise<SyncResultDto> {
    syncingOdds.value = true
    syncProgress.value = 'Iniciando requisição...'
    lastResult.value = null
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
      syncProgress.value = ''
    }
  }

  async function syncResults(): Promise<SyncResultDto> {
    syncingResults.value = true
    syncProgress.value = 'Iniciando liquidação...'
    lastResult.value = null
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
      syncProgress.value = ''
    }
  }

  return {
    syncingOdds,
    syncingResults,
    lastResult,
    syncProgress,
    syncOdds,
    syncResults
  }
})
