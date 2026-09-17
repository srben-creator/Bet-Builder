import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { ApiService } from '../api/client'
import type { LadderCurrentDto, LadderSafeLegDto } from '../api/types'

export const useLadderStore = defineStore('ladder', () => {
  const challenge = ref<LadderCurrentDto | null>(null)
  const safeLegs = ref<LadderSafeLegDto[]>([])
  const slip = ref<LadderSafeLegDto[]>([])
  const customOdds = ref<number>(1.50)
  const loading = ref(false)

  const hasCorrelation = computed(() => {
    const fixtureIds = slip.value.map(l => l.fixtureId)
    return fixtureIds.length !== new Set(fixtureIds).size
  })

  const combinedProb = computed(() => {
    if (slip.value.length === 0) return 0
    return slip.value.reduce((acc, leg) => acc * leg.prob, 1.0)
  })

  const fairOdds = computed(() => {
    if (combinedProb.value <= 0) return 0
    return Number((1.0 / combinedProb.value).toFixed(2))
  })

  const edgeEv = computed(() => {
    if (combinedProb.value <= 0 || customOdds.value <= 1.0) return 0
    return ((combinedProb.value * customOdds.value) - 1.0) * 100.0
  })

  async function loadChallenge() {
    loading.value = true
    try {
      challenge.value = await ApiService.getLadderCurrent()
      safeLegs.value = await ApiService.getLadderSafeLegs()
    } finally {
      loading.value = false
    }
  }

  function addLeg(leg: LadderSafeLegDto) {
    const exists = slip.value.some(l => l.fixtureId === leg.fixtureId && l.market === leg.market)
    if (!exists) {
      slip.value.push(leg)
      if (fairOdds.value > 1.0) {
        customOdds.value = Number(fairOdds.value.toFixed(2))
      }
    }
  }

  function removeLeg(index: number) {
    slip.value.splice(index, 1)
    if (slip.value.length > 0 && fairOdds.value > 1.0) {
      customOdds.value = Number(fairOdds.value.toFixed(2))
    }
  }

  function clearSlip() {
    slip.value = []
  }

  async function winStep() {
    if (customOdds.value <= 1.0) return
    loading.value = true
    try {
      challenge.value = await ApiService.winLadderStep(customOdds.value)
      clearSlip()
    } finally {
      loading.value = false
    }
  }

  async function loseStep() {
    loading.value = true
    try {
      challenge.value = await ApiService.loseLadderStep()
      clearSlip()
    } finally {
      loading.value = false
    }
  }

  async function resetChallenge() {
    loading.value = true
    try {
      challenge.value = await ApiService.resetLadder()
      clearSlip()
    } finally {
      loading.value = false
    }
  }

  return {
    challenge,
    safeLegs,
    slip,
    customOdds,
    loading,
    hasCorrelation,
    combinedProb,
    fairOdds,
    edgeEv,
    loadChallenge,
    addLeg,
    removeLeg,
    clearSlip,
    winStep,
    loseStep,
    resetChallenge
  }
})
