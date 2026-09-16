import axios from 'axios'
import type {
  ValueBetDto,
  LadderCurrentDto,
  LadderSafeLegDto,
  FixtureDto,
  PerformanceDashboardDto,
  BacktestReportDto,
  SyncResultDto
} from './types'

const api = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json'
  }
})

export const ApiService = {
  // Value Bets
  async getValueBets(minEdge = 2.0, leagueId?: string, date?: string): Promise<ValueBetDto[]> {
    const params = new URLSearchParams()
    if (minEdge !== undefined) params.append('minEdge', minEdge.toString())
    if (leagueId) params.append('leagueId', leagueId)
    if (date) params.append('date', date)

    const res = await api.get<ValueBetDto[]>(`/value-bets?${params.toString()}`)
    return res.data
  },

  // Ladder
  async getLadderCurrent(): Promise<LadderCurrentDto> {
    const res = await api.get<LadderCurrentDto>('/ladder/current')
    return res.data
  },

  async getLadderSafeLegs(): Promise<LadderSafeLegDto[]> {
    const res = await api.get<LadderSafeLegDto[]>('/ladder/safe-legs')
    return res.data
  },

  async winLadderStep(odds: number): Promise<LadderCurrentDto> {
    const res = await api.post<LadderCurrentDto>('/ladder/step/win', { odds })
    return res.data
  },

  async resetLadder(): Promise<LadderCurrentDto> {
    const res = await api.post<LadderCurrentDto>('/ladder/reset')
    return res.data
  },

  // Fixtures
  async getFixtures(leagueIds?: string[]): Promise<FixtureDto[]> {
    const params = new URLSearchParams()
    if (leagueIds && leagueIds.length > 0) {
      params.append('leagueIds', leagueIds.join(','))
    }
    const res = await api.get<FixtureDto[]>(`/fixtures?${params.toString()}`)
    return res.data
  },

  // Performance
  async getPerformance(): Promise<PerformanceDashboardDto> {
    const res = await api.get<PerformanceDashboardDto>('/performance/dashboard')
    return res.data
  },

  // Backtest
  async getBacktestReport(): Promise<BacktestReportDto> {
    const res = await api.get<BacktestReportDto>('/backtest/report')
    return res.data
  },

  // Sync
  async syncLiveOdds(): Promise<SyncResultDto> {
    const res = await api.post<SyncResultDto>('/sync/live-odds')
    return res.data
  },

  async syncWeekendAndSettle(): Promise<SyncResultDto> {
    const res = await api.post<SyncResultDto>('/sync/settle')
    return res.data
  }
}
