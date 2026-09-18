import { createI18n } from 'vue-i18n'
import ptPT from './locales/pt-PT.json'
import en from './locales/en.json'

// Type-define 'en' as the master schema for the resource
type MessageSchema = typeof ptPT

export const i18n = createI18n<[MessageSchema], 'pt-PT' | 'en'>({
  legacy: false, // you must set `false`, to use Composition API
  locale: localStorage.getItem('betbuilder_locale') || 'pt-PT', // set locale
  fallbackLocale: 'en', // set fallback locale
  messages: {
    'pt-PT': ptPT,
    en: en
  }
})
