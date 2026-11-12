import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api, getErrorMessage, type AdminUser, type Role } from '../../lib/api'
import { formatDate } from '../../lib/format'
import { Badge, Button, ErrorBanner, Input, Spinner, SuccessBanner } from '../../components/ui'

function UserRow({
  user,
  roles,
  onSaved,
}: {
  user: AdminUser
  roles: Role[]
  onSaved: (message: string) => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [selected, setSelected] = useState<string[]>(user.roles)

  useEffect(() => {
    setSelected(user.roles)
  }, [user.roles])

  const mutation = useMutation({
    mutationFn: (nextRoles: string[]) => api.admin.updateUserRoles(user.id, nextRoles),
    onSuccess: async (updated) => {
      await queryClient.invalidateQueries({ queryKey: ['admin', 'users'] })
      onSaved(t('admin.users.saved', { email: updated.email }))
    },
  })

  const dirty = [...selected].sort().join(',') !== [...user.roles].sort().join(',')

  function toggle(roleName: string) {
    setSelected((current) =>
      current.includes(roleName)
        ? current.filter((name) => name !== roleName)
        : [...current, roleName],
    )
  }

  return (
    <div className="rounded-2xl border border-white/10 bg-slate-900/50 p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <p className="font-semibold text-white">{user.name || user.email}</p>
            {user.roles.map((role) => (
              <Badge key={role} tone={role === 'admin' ? 'warning' : 'default'}>
                {role}
              </Badge>
            ))}
          </div>
          <p className="text-sm text-slate-400">{user.email}</p>
          <p className="mt-1 text-xs text-slate-600">
            {t('profile.joined')}: {formatDate(user.createdAt)} ·{' '}
            {t('profile.lastLogin')}: {formatDate(user.lastLoginAt)}
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {roles.map((role) => (
            <label
              key={role.id}
              className="flex cursor-pointer items-center gap-2 rounded-lg border border-white/10 bg-slate-950/50 px-2.5 py-1.5 text-xs text-slate-200"
            >
              <input
                type="checkbox"
                checked={selected.includes(role.name)}
                onChange={() => toggle(role.name)}
                className="h-3.5 w-3.5 rounded border-white/20 bg-slate-900 accent-sky-500"
              />
              {role.name}
            </label>
          ))}
          <Button
            className="px-3 py-1.5 text-xs"
            disabled={!dirty || mutation.isPending}
            onClick={() => mutation.mutate(selected)}
          >
            {mutation.isPending ? t('common.loading') : t('admin.users.save')}
          </Button>
        </div>
      </div>

      {mutation.error ? (
        <div className="mt-3">
          <ErrorBanner message={getErrorMessage(mutation.error, t('common.error'))} />
        </div>
      ) : null}
    </div>
  )
}

export function UsersTab() {
  const { t } = useTranslation()
  const [search, setSearch] = useState('')
  const [debounced, setDebounced] = useState('')
  const [notice, setNotice] = useState<string | null>(null)

  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(search.trim()), 300)
    return () => window.clearTimeout(timer)
  }, [search])

  const usersQuery = useQuery({
    queryKey: ['admin', 'users', debounced],
    queryFn: () => api.admin.users(debounced),
  })

  const rolesQuery = useQuery({ queryKey: ['admin', 'roles'], queryFn: api.admin.roles })

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-semibold text-white">{t('admin.users.title')}</h2>
        <Input
          value={search}
          onChange={(event) => {
            setSearch(event.target.value)
            setNotice(null)
          }}
          placeholder={t('admin.users.search')}
          className="max-w-xs"
        />
      </div>

      {notice ? <SuccessBanner message={notice} /> : null}

      {usersQuery.isLoading || rolesQuery.isLoading ? <Spinner /> : null}

      {usersQuery.error ? (
        <ErrorBanner message={getErrorMessage(usersQuery.error, t('common.error'))} />
      ) : null}

      {usersQuery.data && usersQuery.data.length === 0 ? (
        <p className="rounded-xl border border-dashed border-white/15 px-4 py-10 text-center text-sm text-slate-500">
          {t('admin.users.empty')}
        </p>
      ) : null}

      <div className="space-y-3">
        {usersQuery.data?.map((user) => (
          <UserRow
            key={user.id}
            user={user}
            roles={rolesQuery.data ?? []}
            onSaved={setNotice}
          />
        ))}
      </div>
    </div>
  )
}
