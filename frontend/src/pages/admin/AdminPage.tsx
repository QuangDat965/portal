import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AppsTab } from './AppsTab'
import { UsersTab } from './UsersTab'
import { AuditTab } from './AuditTab'

type Tab = 'apps' | 'users' | 'audit'

export function AdminPage() {
  const { t } = useTranslation()
  const [tab, setTab] = useState<Tab>('apps')

  const tabs: { id: Tab; label: string }[] = [
    { id: 'apps', label: t('admin.tabs.apps') },
    { id: 'users', label: t('admin.tabs.users') },
    { id: 'audit', label: t('admin.tabs.audit') },
  ]

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-white">{t('admin.title')}</h1>
        <p className="mt-1 text-sm text-slate-400">
          Quản lý ứng dụng, quyền truy cập và người dùng của portal.
        </p>
      </div>

      <div className="flex flex-wrap gap-1 rounded-xl border border-white/10 bg-slate-900/50 p-1">
        {tabs.map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => setTab(item.id)}
            className={`rounded-lg px-4 py-2 text-sm font-semibold transition ${
              tab === item.id
                ? 'bg-sky-500 text-slate-950'
                : 'text-slate-400 hover:bg-white/5 hover:text-slate-200'
            }`}
          >
            {item.label}
          </button>
        ))}
      </div>

      {tab === 'apps' ? <AppsTab /> : null}
      {tab === 'users' ? <UsersTab /> : null}
      {tab === 'audit' ? <AuditTab /> : null}
    </div>
  )
}
