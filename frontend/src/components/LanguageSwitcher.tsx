import { useTranslation } from 'react-i18next'

export function LanguageSwitcher() {
  const { i18n } = useTranslation()
  const current = i18n.resolvedLanguage?.startsWith('vi') ? 'vi' : 'en'

  return (
    <select
      aria-label="Language"
      value={current}
      onChange={(event) => void i18n.changeLanguage(event.target.value)}
      className="rounded-lg border border-white/10 bg-slate-900/60 px-2 py-1.5 text-xs font-semibold text-slate-300 focus:outline-none"
    >
      <option value="vi">VI</option>
      <option value="en">EN</option>
    </select>
  )
}
