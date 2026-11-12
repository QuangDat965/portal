import { useTranslation } from 'react-i18next'
import { useMe } from '../components/auth'
import { Badge, Card, Spinner } from '../components/ui'

export function ProfilePage() {
  const { t } = useTranslation()
  const { data: me, isLoading } = useMe()

  if (isLoading || !me) {
    return <Spinner />
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-2xl font-bold text-white">{t('profile.title')}</h1>

      <Card>
        <div className="flex items-center gap-4">
          {me.avatarUrl ? (
            <img
              src={me.avatarUrl}
              alt=""
              className="h-16 w-16 rounded-2xl object-cover ring-1 ring-white/10"
              referrerPolicy="no-referrer"
            />
          ) : (
            <span className="grid h-16 w-16 place-items-center rounded-2xl bg-gradient-to-br from-sky-400/20 to-indigo-500/20 text-2xl font-black text-sky-200 ring-1 ring-white/10">
              {(me.name || me.email).slice(0, 1).toUpperCase()}
            </span>
          )}

          <div>
            <p className="text-lg font-semibold text-white">{me.name}</p>
            <p className="text-sm text-slate-400">{me.email}</p>
          </div>
        </div>

        <dl className="mt-6 space-y-4 border-t border-white/10 pt-5 text-sm">
          <div className="flex items-center justify-between gap-4">
            <dt className="text-slate-500">{t('profile.email')}</dt>
            <dd className="text-slate-200">{me.email}</dd>
          </div>
          <div className="flex items-center justify-between gap-4">
            <dt className="text-slate-500">{t('profile.name')}</dt>
            <dd className="text-slate-200">{me.name}</dd>
          </div>
          <div className="flex items-center justify-between gap-4">
            <dt className="text-slate-500">{t('profile.roles')}</dt>
            <dd className="flex flex-wrap justify-end gap-1.5">
              {me.roles.length === 0 ? (
                <span className="text-slate-500">{t('common.none')}</span>
              ) : (
                me.roles.map((role) => (
                  <Badge key={role} tone={role === 'admin' ? 'warning' : 'default'}>
                    {role}
                  </Badge>
                ))
              )}
            </dd>
          </div>
        </dl>
      </Card>
    </div>
  )
}
