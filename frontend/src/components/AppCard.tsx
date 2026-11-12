import { useTranslation } from 'react-i18next'
import type { PortalApp } from '../lib/api'
import { hostOf } from '../lib/format'
import { Badge } from './ui'

export function AppCard({
  app,
  busy,
  onOpen,
}: {
  app: PortalApp
  busy: boolean
  onOpen: (app: PortalApp) => void
}) {
  const { t } = useTranslation()

  return (
    <button
      type="button"
      onClick={() => onOpen(app)}
      disabled={busy}
      className="group flex h-full flex-col rounded-2xl border border-white/10 bg-slate-900/50 p-5 text-left shadow-xl shadow-black/20 backdrop-blur transition hover:-translate-y-0.5 hover:border-sky-400/40 hover:bg-slate-900/70 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-400 disabled:cursor-wait disabled:opacity-60"
    >
      <div className="flex items-start gap-3">
        {app.iconUrl ? (
          <img
            src={app.iconUrl}
            alt=""
            className="h-12 w-12 rounded-xl object-cover ring-1 ring-white/10"
          />
        ) : (
          <span className="grid h-12 w-12 place-items-center rounded-xl bg-gradient-to-br from-sky-400/20 to-indigo-500/20 text-lg font-black text-sky-200 ring-1 ring-white/10">
            {app.name.slice(0, 1).toUpperCase()}
          </span>
        )}
        <div className="min-w-0">
          <h3 className="truncate font-semibold text-white">{app.name}</h3>
          <p className="truncate text-xs text-slate-500">{hostOf(app.baseUrl)}</p>
        </div>
      </div>

      <p className="mt-4 line-clamp-2 min-h-10 text-sm text-slate-400">
        {app.description || ' '}
      </p>

      <div className="mt-auto flex items-center justify-between pt-4">
        <Badge tone="info">{busy ? t('apps.opening') : t('apps.open')}</Badge>
        <span className="text-slate-500 transition group-hover:translate-x-0.5 group-hover:text-sky-300">
          →
        </span>
      </div>
    </button>
  )
}
