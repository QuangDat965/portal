import { useTranslation } from 'react-i18next'
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { api } from '../lib/api'
import { useMe } from './auth'
import { LanguageSwitcher } from './LanguageSwitcher'
import { Button } from './ui'

function navClass({ isActive }: { isActive: boolean }) {
  return `rounded-lg px-3 py-2 text-sm font-medium transition ${
    isActive
      ? 'bg-white/10 text-white'
      : 'text-slate-400 hover:bg-white/5 hover:text-slate-200'
  }`
}

export function Layout() {
  const { t } = useTranslation()
  const { data: me } = useMe()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  async function handleLogout() {
    try {
      await api.logout()
    } finally {
      queryClient.clear()
      navigate('/login', { replace: true })
    }
  }

  return (
    <div className="flex min-h-screen flex-col">
      <header className="sticky top-0 z-20 border-b border-white/10 bg-slate-950/70 backdrop-blur">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-3 px-4 py-3">
          <Link to="/apps" className="flex items-center gap-2.5">
            <span className="grid h-9 w-9 place-items-center rounded-xl bg-gradient-to-br from-sky-400 to-indigo-500 text-sm font-black text-slate-950">
              P
            </span>
            <span className="text-sm font-bold tracking-wide text-white">
              {t('common.appName')}
            </span>
          </Link>

          <nav className="flex items-center gap-1 sm:ml-4">
            <NavLink to="/apps" className={navClass}>
              {t('admin.tabs.apps')}
            </NavLink>
            {me?.roles.includes('admin') ? (
              <NavLink to="/admin" className={navClass}>
                {t('common.admin')}
              </NavLink>
            ) : null}
            <NavLink to="/profile" className={navClass}>
              {t('common.profile')}
            </NavLink>
          </nav>

          <div className="ml-auto flex items-center gap-2">
            <LanguageSwitcher />
            {me ? (
              <div className="hidden items-center gap-2 rounded-lg border border-white/10 bg-white/5 px-2.5 py-1.5 sm:flex">
                {me.avatarUrl ? (
                  <img
                    src={me.avatarUrl}
                    alt=""
                    className="h-6 w-6 rounded-full object-cover"
                    referrerPolicy="no-referrer"
                  />
                ) : (
                  <span className="grid h-6 w-6 place-items-center rounded-full bg-sky-500/20 text-[10px] font-bold text-sky-200">
                    {(me.name || me.email).slice(0, 1).toUpperCase()}
                  </span>
                )}
                <span className="max-w-40 truncate text-xs text-slate-300">{me.name}</span>
              </div>
            ) : null}
            <Button variant="ghost" onClick={() => void handleLogout()} className="px-2.5">
              {t('common.logout')}
            </Button>
          </div>
        </div>
      </header>

      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-8">
        <Outlet />
      </main>

      <footer className="border-t border-white/5 py-5 text-center text-xs text-slate-600">
        Portal · SSO cho hệ sinh thái ứng dụng
      </footer>
    </div>
  )
}
