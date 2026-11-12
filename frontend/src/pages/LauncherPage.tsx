import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api, getErrorMessage, type PortalApp } from '../lib/api'
import { useMe } from '../components/auth'
import { AppCard } from '../components/AppCard'
import { ErrorBanner, Spinner } from '../components/ui'

export function LauncherPage() {
  const { t } = useTranslation()
  const { data: me } = useMe()
  const appsQuery = useQuery({
    queryKey: ['me', 'apps'],
    queryFn: api.myApps,
  })

  const launchMutation = useMutation({
    mutationFn: (app: PortalApp) => api.launch(app.slug),
    onSuccess: (result) => {
      window.location.assign(result.redirectUrl)
    },
  })

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-white">
          {t('apps.welcome', { name: me?.name ?? '' })}
        </h1>
        <p className="mt-1 text-sm text-slate-400">{t('apps.subtitle')}</p>
      </div>

      {launchMutation.error ? (
        <ErrorBanner message={getErrorMessage(launchMutation.error, t('common.error'))} />
      ) : null}

      {appsQuery.isLoading ? <Spinner /> : null}

      {appsQuery.error ? (
        <ErrorBanner message={getErrorMessage(appsQuery.error, t('common.error'))} />
      ) : null}

      {appsQuery.data && appsQuery.data.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-white/15 bg-slate-900/30 px-6 py-16 text-center">
          <p className="text-base font-semibold text-slate-200">{t('apps.emptyTitle')}</p>
          <p className="mt-1 text-sm text-slate-500">{t('apps.emptyText')}</p>
        </div>
      ) : null}

      {appsQuery.data && appsQuery.data.length > 0 ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {appsQuery.data.map((app) => (
            <AppCard
              key={app.id}
              app={app}
              busy={
                launchMutation.isPending && launchMutation.variables?.id === app.id
              }
              onOpen={(selected) => launchMutation.mutate(selected)}
            />
          ))}
        </div>
      ) : null}
    </div>
  )
}
