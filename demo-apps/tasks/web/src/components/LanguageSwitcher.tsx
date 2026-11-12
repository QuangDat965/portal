import { useI18n, type Lang } from '../lib/i18n'

const options: { value: Lang; label: string }[] = [
  { value: 'vi', label: 'VI' },
  { value: 'en', label: 'EN' },
]

export function LanguageSwitcher() {
  const { lang, setLang } = useI18n()

  return (
    <div className="flex items-center gap-1 rounded-lg border border-white/10 bg-white/5 p-1 text-xs font-semibold">
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          onClick={() => setLang(option.value)}
          className={`rounded-md px-2.5 py-1 transition ${
            lang === option.value
              ? 'bg-emerald-400/20 text-emerald-200'
              : 'text-slate-400 hover:text-slate-200'
          }`}
        >
          {option.label}
        </button>
      ))}
    </div>
  )
}
