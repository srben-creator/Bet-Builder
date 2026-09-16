import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  {
    path: '/',
    redirect: '/value-bets'
  },
  {
    path: '/value-bets',
    name: 'ValueBets',
    component: () => import('../views/ValueBetsView.vue')
  },
  {
    path: '/ladder',
    name: 'Ladder',
    component: () => import('../views/LadderView.vue')
  },
  {
    path: '/fixtures',
    name: 'Fixtures',
    component: () => import('../views/FixturesView.vue')
  },
  {
    path: '/performance',
    name: 'Performance',
    component: () => import('../views/PerformanceView.vue')
  },
  {
    path: '/backtest',
    name: 'Backtest',
    component: () => import('../views/BacktestView.vue')
  }
]

export const router = createRouter({
  history: createWebHistory(),
  routes
})
