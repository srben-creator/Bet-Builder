import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

export type Theme = 'dark' | 'light' | 'midnight' | 'dracula'

export const useSettingsStore = defineStore('settings', () => {
  const currentTheme = ref<Theme>((localStorage.getItem('betbuilder_theme') as Theme) || 'dark')
  
  const setTheme = (theme: Theme) => {
    currentTheme.value = theme
  }

  // Apply theme to HTML tag
  watch(currentTheme, (newTheme) => {
    localStorage.setItem('betbuilder_theme', newTheme)
    if (newTheme === 'dark') {
      document.documentElement.removeAttribute('data-theme')
    } else {
      document.documentElement.setAttribute('data-theme', newTheme)
    }
  }, { immediate: true })

  return {
    currentTheme,
    setTheme
  }
})
