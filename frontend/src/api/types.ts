export interface LeagueDto {
  id: string
  name: string
  country: string
  code: string
  fdCsvCode: string | null
  understatName: string | null
  isActive: boolean
}

export interface UpdateLeagueDto {
  isActive: boolean
  understatName: string | null
}

export interface ValueBetDto {
  id: string
  fixtureId: string
  date: string
  league: string
  match: string
  market: string
  selection: string
  modelProb: number
  probPercent: string
  trueOdds: string
  pinnacle: string
  bookmaker: string
  odds: number
  edgeEv: number
  qKellyStake: string
}

export interface LadderCurrentDto {
  id: string
  challengeNumber: number
  currentBankroll: number
  targetAmount: number
  currentStep: number
  status: string
}

export interface LadderSafeLegDto {
  date: string
  fixtureId: string
  match: string
  market: string
  selection: string
  prob: number
  fairOdds?: number
  pinnacleOdds?: number
}

export interface FixtureDto {
  id: string
  league: string
  date: string
  time: string
  homeTeam: string
  awayTeam: string
  status: string
  homeGoals?: number
  awayGoals?: number
}

export interface PnlPointDto {
  date: string
  cumulativePnl: number
  market: string
  odds: number
  pnl: number
}

export interface SettledBetDto {
  id: string
  date: string
  market: string
  selection: string
  result: string
  odds: number
  stake: number
  pnl: number
}

export interface PerformanceDashboardDto {
  totalSettledBets: number
  winRate: number
  totalProfit: number
  roi: number
  bankrollEvolution: PnlPointDto[]
  history: SettledBetDto[]
}

export interface MarketStatDto {
  market: string
  bets: number
  winRate: number
  totalStaked: number
  totalProfit: number
  roi: number
}

export interface BacktestReportDto {
  totalSimulatedBets: number
  winRate: number
  overallRoi: number
  beatClosingRate: number
  avgClvEdge: number
  cumulativePnlEvolution: PnlPointDto[]
  marketStats: MarketStatDto[]
}

export interface SyncResultDto {
  success: boolean
  message: string
  fixturesUpdated: number
  oddsUpdated: number
  valueBetsGenerated: number
  betsSettled: number
}
