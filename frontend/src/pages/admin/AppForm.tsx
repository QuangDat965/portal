import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { AdminApp, AppInput, Role } from '../../lib/api'
import { Button, ErrorBanner, Field, Input, Select } from '../../components/ui'

export function AppForm({
  initial,
  roles,
  busy,
  error,
  onSubmit,
  onCancel,
}: {
  initial?: AdminApp
  roles: Role[]
  busy: boolean
  error?: string | null
  onSubmit: (input: AppInput) => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  const [slug, setSlug] = useState(initial?.slug ?? '')
  const [name, setName] = useState(initial?.name ?? '')
  const [description, setDescription] = useState(initial?.description ?? '')
  const [iconUrl, setIconUrl] = useState(initial?.iconUrl ?? '')
  const [baseUrl, setBaseUrl] = useState(initial?.baseUrl ?? '')
  const [callbackPath, setCallbackPath] = useState(
    initial?.callbackPath ?? '/auth/callback',
  )
  const [status, setStatus] = useState(initial?.status ?? 'Active')
  const [sortOrder, setSortOrder] = useState(initial?.sortOrder ?? 0)
  const [roleIds, setRoleIds] = useState<string[]>(initial?.roleIds ?? [])

  function toggleRole(roleId: string) {
    setRoleIds((current) =>
      current.includes(roleId)
        ? current.filter((id) => id !== roleId)
        : [...current, roleId],
    )
  }

  return (
    <form
      className="space-y-4 rounded-2xl border border-white/10 bg-slate-900/70 p-5"
      onSubmit={(event) => {
        event.preventDefault()
        onSubmit({
          slug: slug.trim(),
          name: name.trim(),
          description: description.trim() || null,
          iconUrl: iconUrl.trim() || null,
          baseUrl: baseUrl.trim(),
          callbackPath: callbackPath.trim() || null,
          status,
          sortOrder: Number(sortOrder) || 0,
          roleIds,
        })
      }}
    >
      <h3 className="font-semibold text-white">
        {initial ? t('admin.apps.form.editTitle') : t('admin.apps.form.createTitle')}
      </h3>

      {error ? <ErrorBanner message={error} /> : null}

      <div className="grid gap-4 sm:grid-cols-2">
        <Field label={t('admin.apps.form.slug')}>
          <Input
            value={slug}
            onChange={(event) => setSlug(event.target.value)}
            placeholder="my-app"
            pattern="[a-z0-9][a-z0-9-]*[a-z0-9]"
            required
          />
        </Field>
        <Field label={t('admin.apps.form.name')}>
          <Input
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="My App"
            required
          />
        </Field>
      </div>

      <Field label={t('admin.apps.form.description')}>
        <Input
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          placeholder="Mô tả ngắn về ứng dụng"
        />
      </Field>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field label={t('admin.apps.form.baseUrl')}>
          <Input
            type="url"
            value={baseUrl}
            onChange={(event) => setBaseUrl(event.target.value)}
            placeholder="https://app1.example.com"
            required
          />
        </Field>
        <Field label={t('admin.apps.form.callbackPath')}>
          <Input
            value={callbackPath}
            onChange={(event) => setCallbackPath(event.target.value)}
            placeholder="/auth/callback"
          />
        </Field>
      </div>

      <Field label={t('admin.apps.form.iconUrl')}>
        <Input
          type="url"
          value={iconUrl}
          onChange={(event) => setIconUrl(event.target.value)}
          placeholder="https://..."
        />
      </Field>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field label={t('admin.apps.form.status')}>
          <Select value={status} onChange={(event) => setStatus(event.target.value)}>
            <option value="Active">{t('admin.apps.form.active')}</option>
            <option value="Disabled">{t('admin.apps.form.disabled')}</option>
          </Select>
        </Field>
        <Field label={t('admin.apps.form.sortOrder')}>
          <Input
            type="number"
            value={sortOrder}
            onChange={(event) => setSortOrder(Number(event.target.value))}
          />
        </Field>
      </div>

      <Field label={t('admin.apps.form.roles')}>
        <div className="flex flex-wrap gap-2">
          {roles.map((role) => (
            <label
              key={role.id}
              className="flex cursor-pointer items-center gap-2 rounded-lg border border-white/10 bg-slate-950/50 px-3 py-2 text-sm text-slate-200"
            >
              <input
                type="checkbox"
                checked={roleIds.includes(role.id)}
                onChange={() => toggleRole(role.id)}
                className="h-4 w-4 rounded border-white/20 bg-slate-900 accent-sky-500"
              />
              {role.name}
            </label>
          ))}
        </div>
      </Field>

      <div className="flex justify-end gap-2 pt-2">
        <Button type="button" variant="ghost" onClick={onCancel} disabled={busy}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" disabled={busy}>
          {busy ? t('common.loading') : t('common.save')}
        </Button>
      </div>
    </form>
  )
}
