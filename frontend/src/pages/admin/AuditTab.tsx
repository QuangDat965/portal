import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api, getErrorMessage } from '../../lib/api'
import { formatDate } from '../../lib/format'
import { Badge, ErrorBanner, Spinner } from '../../components/ui'

export function AuditTab() {
  const { t } = useTranslation()
  const auditQuery = useQuery({
    queryKey: ['admin', 'audit'],
    queryFn: () => api.admin.audit(100),
  })

  return (
    <div className="space-y-4">
      <h2 className="text-lg font-semibold text-white">{t('admin.audit.title')}</h2>

      {auditQuery.isLoading ? <Spinner /> : null}

      {auditQuery.error ? (
        <ErrorBanner message={getErrorMessage(auditQuery.error, t('common.error'))} />
      ) : null}

      {auditQuery.data && auditQuery.data.length === 0 ? (
        <p className="rounded-xl border border-dashed border-white/15 px-4 py-10 text-center text-sm text-slate-500">
          {t('admin.audit.empty')}
        </p>
      ) : null}

      {auditQuery.data && auditQuery.data.length > 0 ? (
        <div className="overflow-x-auto rounded-2xl border border-white/10">
          <table className="w-full min-w-[640px] text-left text-sm">
            <thead className="bg-white/5 text-xs text-slate-400 uppercase">
              <tr>
                <th className="px-4 py-3 font-semibold">{t('admin.audit.time')}</th>
                <th className="px-4 py-3 font-semibold">{t('admin.audit.action')}</th>
                <th className="px-4 py-3 font-semibold">{t('admin.audit.user')}</th>
                <th className="px-4 py-3 font-semibold">{t('admin.audit.app')}</th>
                <th className="px-4 py-3 font-semibold">{t('admin.audit.ip')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/5">
              {auditQuery.data.map((log) => (
                <tr key={log.id} className="text-slate-300">
                  <td className="px-4 py-2.5 whitespace-nowrap text-slate-400">
                    {formatDate(log.createdAt)}
                  </td>
                  <td className="px-4 py-2.5">
                    <Badge tone={log.action.includes('launch') ? 'info' : 'default'}>
                      {log.action}
                    </Badge>
                  </td>
                  <td className="px-4 py-2.5 font-mono text-xs text-slate-500">
                    {log.userId?.slice(0, 8) ?? '—'}
                  </td>
                  <td className="px-4 py-2.5 font-mono text-xs text-slate-500">
                    {log.appId?.slice(0, 8) ?? '—'}
                  </td>
                  <td className="px-4 py-2.5 font-mono text-xs text-slate-500">
                    {log.ipAddress ?? '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  )
}
