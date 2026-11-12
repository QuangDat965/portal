import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api, getErrorMessage, type AdminApp, type AppInput } from '../../lib/api'
import { hostOf } from '../../lib/format'
import { Badge, Button, ErrorBanner, Spinner, SuccessBanner } from '../../components/ui'
import { AppForm } from './AppForm'

type SecretInfo = { clientId: string; secret: string }

export function AppsTab() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<AdminApp | null>(null)
  const [secret, setSecret] = useState<SecretInfo | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  const appsQuery = useQuery({ queryKey: ['admin', 'apps'], queryFn: api.admin.apps })
  const rolesQuery = useQuery({ queryKey: ['admin', 'roles'], queryFn: api.admin.roles })

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['admin', 'apps'] })

  const closeForm = () => {
    setFormOpen(false)
    setEditing(null)
  }

  const createMutation = useMutation({
    mutationFn: api.admin.createApp,
    onSuccess: async (result) => {
      await invalidate()
      closeForm()
      setNotice(t('admin.apps.created', { name: result.app.name }))
      if (result.clientSecret) {
        setSecret({ clientId: result.app.clientId ?? result.app.slug, secret: result.clientSecret })
      }
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, input }: { id: string; input: AppInput }) =>
      api.admin.updateApp(id, input),
    onSuccess: async () => {
      await invalidate()
      closeForm()
    },
  })

  const deleteMutation = useMutation({
    mutationFn: api.admin.deleteApp,
    onSuccess: async () => {
      await invalidate()
    },
  })

  const rotateMutation = useMutation({
    mutationFn: api.admin.rotateClient,
    onSuccess: async (result) => {
      await invalidate()
      setSecret({ clientId: result.clientId, secret: result.clientSecret })
      setNotice(t('admin.apps.rotated', { clientId: result.clientId }))
    },
  })

  const saving = createMutation.isPending || updateMutation.isPending
  const mutationError =
    createMutation.error ??
    updateMutation.error ??
    deleteMutation.error ??
    rotateMutation.error

  const roles = rolesQuery.data ?? []
  const roleName = (id: string) => roles.find((role) => role.id === id)?.name ?? id

  async function copySecret() {
    if (!secret) return
    try {
      await navigator.clipboard.writeText(secret.secret)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 1500)
    } catch {
      // Clipboard có thể bị chặn; người dùng vẫn copy tay được.
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-semibold text-white">{t('admin.apps.title')}</h2>
        <Button
          onClick={() => {
            setEditing(null)
            setFormOpen(true)
          }}
        >
          + {t('admin.apps.newApp')}
        </Button>
      </div>

      {notice ? <SuccessBanner message={notice} /> : null}
      {mutationError ? (
        <ErrorBanner message={getErrorMessage(mutationError, t('common.error'))} />
      ) : null}

      {secret ? (
        <div className="rounded-2xl border border-amber-400/30 bg-amber-400/10 p-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <p className="text-sm font-semibold text-amber-200">
                {t('admin.apps.secretTitle')}
              </p>
              <p className="mt-1 text-xs text-amber-200/70">
                {t('admin.apps.secretHint')}
              </p>
            </div>
            <Button variant="ghost" onClick={() => setSecret(null)} className="px-2 py-1">
              ✕
            </Button>
          </div>
          <div className="mt-3 space-y-2 text-sm">
            <p className="text-amber-100/80">
              {t('admin.apps.clientId')}:{' '}
              <code className="rounded bg-slate-950/50 px-1.5 py-0.5 text-amber-100">
                {secret.clientId}
              </code>
            </p>
            <div className="flex flex-wrap items-center gap-2">
              <code className="max-w-full overflow-x-auto rounded bg-slate-950/60 px-3 py-2 text-amber-100">
                {secret.secret}
              </code>
              <Button variant="secondary" onClick={() => void copySecret()}>
                {copied ? t('common.copied') : 'Copy'}
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {formOpen ? (
        <AppForm
          key={editing?.id ?? 'new'}
          initial={editing ?? undefined}
          roles={roles}
          busy={saving}
          error={mutationError ? getErrorMessage(mutationError, t('common.error')) : null}
          onCancel={closeForm}
          onSubmit={(input) => {
            if (editing) {
              updateMutation.mutate({ id: editing.id, input })
            } else {
              createMutation.mutate(input)
            }
          }}
        />
      ) : null}

      {appsQuery.isLoading || rolesQuery.isLoading ? <Spinner /> : null}

      {appsQuery.error ? (
        <ErrorBanner message={getErrorMessage(appsQuery.error, t('common.error'))} />
      ) : null}

      {appsQuery.data && appsQuery.data.length === 0 ? (
        <p className="rounded-xl border border-dashed border-white/15 px-4 py-10 text-center text-sm text-slate-500">
          {t('admin.apps.empty')}
        </p>
      ) : null}

      <div className="space-y-3">
        {appsQuery.data?.map((app) => (
          <div
            key={app.id}
            className="rounded-2xl border border-white/10 bg-slate-900/50 p-4"
          >
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="flex min-w-0 items-start gap-3">
                {app.iconUrl ? (
                  <img
                    src={app.iconUrl}
                    alt=""
                    className="h-10 w-10 rounded-lg object-cover ring-1 ring-white/10"
                  />
                ) : (
                  <span className="grid h-10 w-10 place-items-center rounded-lg bg-sky-500/15 font-bold text-sky-200">
                    {app.name.slice(0, 1).toUpperCase()}
                  </span>
                )}
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-semibold text-white">{app.name}</p>
                    <Badge tone={app.status === 'Active' ? 'success' : 'warning'}>
                      {app.status}
                    </Badge>
                  </div>
                  <p className="text-xs text-slate-500">
                    <code>{app.slug}</code> · {hostOf(app.baseUrl)}
                    {app.clientId ? (
                      <>
                        {' '}
                        · client <code>{app.clientId}</code>
                      </>
                    ) : null}
                  </p>
                </div>
              </div>

              <div className="flex flex-wrap gap-2">
                <Button
                  variant="secondary"
                  className="px-3 py-1.5"
                  onClick={() => {
                    setEditing(app)
                    setFormOpen(true)
                  }}
                >
                  {t('common.edit')}
                </Button>
                <Button
                  variant="secondary"
                  className="px-3 py-1.5"
                  disabled={rotateMutation.isPending}
                  onClick={() => {
                    if (
                      window.confirm(
                        `${t('admin.apps.rotate')} · ${app.clientId ?? app.slug}?`,
                      )
                    ) {
                      rotateMutation.mutate(app.id)
                    }
                  }}
                >
                  {t('admin.apps.rotate')}
                </Button>
                <Button
                  variant="danger"
                  className="px-3 py-1.5"
                  disabled={deleteMutation.isPending}
                  onClick={() => {
                    if (window.confirm(t('admin.apps.deleteConfirm', { name: app.name }))) {
                      deleteMutation.mutate(app.id)
                    }
                  }}
                >
                  {t('common.delete')}
                </Button>
              </div>
            </div>

            <div className="mt-3 flex flex-wrap items-center gap-1.5 border-t border-white/5 pt-3">
              <span className="mr-1 text-xs text-slate-500">
                {t('admin.apps.roleAccess')}:
              </span>
              {app.roleIds.length === 0 ? (
                <span className="text-xs text-slate-500">{t('common.none')}</span>
              ) : (
                app.roleIds.map((id) => (
                  <Badge key={id} tone="info">
                    {roleName(id)}
                  </Badge>
                ))
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
