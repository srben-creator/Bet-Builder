import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

export type Theme = 'dark' | 'light' | 'midnight' | 'dracula'

export const useSettingsStore = defineStore('settings', () => {
  const currentTheme = ref<Theme>((localStorage.getItem('betbuilder_theme') as Theme) || 'dark')
  
  const currentLocale = ref<string>(localStorage.getItem('betbuilder_locale') || 'pt-PT')

  const setTheme = (theme: Theme) => {
    currentTheme.value = theme
  }

  const setLocale = (locale: string) => {
    currentLocale.value = locale
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

  // Save locale to localStorage
  watch(currentLocale, (newLocale) => {
    localStorage.setItem('betbuilder_locale', newLocale)
  }, { immediate: true })

  return {
    currentTheme,
    currentLocale,
    setTheme,
    setLocale
  }
})
