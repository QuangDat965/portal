import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { vi } from '../locales/vi'
import { en } from '../locales/en'

export type Lang = 'vi' | 'en'

type Vars = Record<string, string | number>

type I18nValue = {
  lang: Lang
  setLang: (lang: Lang) => void
  t: (key: string, vars?: Vars) => string
}

const dictionaries: Record<Lang, Record<string, string>> = { vi, en }
const storageKey = 'tasks.lang'

const I18nContext = createContext<I18nValue | null>(null)

export function I18nProvider({ children }: { children: ReactNode }) {
  const [lang, setLang] = useState<Lang>(() =>
    localStorage.getItem(storageKey) === 'en' ? 'en' : 'vi',
  )

  useEffect(() => {
    localStorage.setItem(storageKey, lang)
  }, [lang])

  const t = useCallback(
    (key: string, vars?: Vars) => {
      let text = dictionaries[lang][key] ?? dictionaries.vi[key] ?? key
      if (vars) {
        for (const [name, value] of Object.entries(vars)) {
          text = text.replaceAll(`{{${name}}}`, String(value))
        }
      }
      return text
    },
    [lang],
  )

  const value = useMemo(() => ({ lang, setLang, t }), [lang, t])

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}

export function useI18n() {
  const value = useContext(I18nContext)
  if (!value) {
    throw new Error('useI18n must be used inside I18nProvider')
  }
  return value
}
