import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError, type Info, type Me, type Note } from './lib/api'
import { useI18n, type Lang } from './lib/i18n'
import {
  Badge,
  Button,
  Card,
  ErrorBanner,
  Input,
  Spinner,
  Textarea,
} from './components/ui'
import { LanguageSwitcher } from './components/LanguageSwitcher'

type Filter = 'all' | 'pinned'

const colorStyles: Record<string, string> = {
  amber: 'border-amber-400/40 bg-amber-400/10',
  sky: 'border-sky-400/40 bg-sky-400/10',
  rose: 'border-rose-400/40 bg-rose-400/10',
  emerald: 'border-emerald-400/40 bg-emerald-400/10',
  violet: 'border-violet-400/40 bg-violet-400/10',
}

const colorSwatches: Record<string, string> = {
  amber: 'bg-amber-400',
  sky: 'bg-sky-400',
  rose: 'bg-rose-400',
  emerald: 'bg-emerald-400',
  violet: 'bg-violet-400',
}

const fallbackColors = ['amber', 'sky', 'rose', 'emerald', 'violet']

function cardColor(color: string) {
  return colorStyles[color] ?? 'border-white/10 bg-slate-900/50'
}

function swatchColor(color: string) {
  return colorSwatches[color] ?? 'bg-slate-400'
}

function formatDate(value: string, lang: Lang) {
  return new Intl.DateTimeFormat(lang === 'vi' ? 'vi-VN' : 'en-US', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

export function App() {
  const { t } = useI18n()

  const meQuery = useQuery<Me>({
    queryKey: ['me'],
    queryFn: api.me,
    retry: false,
  })

  const infoQuery = useQuery<Info>({
    queryKey: ['info'],
    queryFn: api.info,
    retry: false,
  })

  if (meQuery.isLoading) {
    return <Spinner label={t('common.loading')} />
  }

  if (meQuery.data) {
    return <Board me={meQuery.data} info={infoQuery.data} />
  }

  const unauthorized = meQuery.error instanceof ApiError && meQuery.error.status === 401
  if (!unauthorized && meQuery.error) {
    return <Spinner label={t('common.loading')} />
  }

  return <Landing info={infoQuery.data} />
}

function Landing({ info }: { info?: Info }) {
  const { t } = useI18n()
  const params = new URLSearchParams(window.location.search)
  const error = params.get('error')
  const errorMessage =
    error === 'missing_code'
      ? t('landing.errorMissing')
      : error
        ? t('landing.error')
        : null

  const portalUrl = info?.portalUrl ?? 'http://localhost:5173'

  return (
    <main className="mx-auto flex min-h-screen w-full max-w-2xl flex-col justify-center px-5 py-12">
      <div className="mb-5 flex justify-end">
        <LanguageSwitcher />
      </div>

      <Card className="text-center">
        <div className="mx-auto grid h-14 w-14 place-items-center rounded-2xl bg-amber-400 text-2xl font-black text-amber-950">
          ✎
        </div>
        <p className="mt-4 text-[11px] font-semibold tracking-[0.3em] text-amber-300/80">
          {t('landing.badge')}
        </p>
        <h1 className="mt-2 text-3xl font-bold text-white">
          {info?.name ?? t('landing.title')}
        </h1>
        <p className="mt-3 text-sm leading-relaxed text-slate-400">
          {t('landing.subtitle')}
        </p>

        {errorMessage ? (
          <div className="mt-5">
            <ErrorBanner message={errorMessage} />
          </div>
        ) : null}

        <div className="mt-6 rounded-xl border border-amber-400/20 bg-amber-400/5 p-4 text-left">
          <p className="text-sm leading-relaxed text-slate-300">{t('landing.hint')}</p>
          <p className="mt-2 text-xs leading-relaxed text-slate-500">{t('landing.note')}</p>
        </div>

        <a
          href={portalUrl}
          className="mt-6 inline-flex items-center justify-center gap-2 rounded-lg bg-amber-400 px-4 py-2 text-sm font-semibold text-amber-950 shadow-lg shadow-amber-400/20 transition hover:bg-amber-300"
        >
          {t('landing.openPortal')} →
        </a>
      </Card>
    </main>
  )
}

function Board({ me, info }: { me: Me; info?: Info }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [filter, setFilter] = useState<Filter>('all')

  const notesQuery = useQuery<Note[]>({
    queryKey: ['notes'],
    queryFn: api.notes.list,
  })

  const logout = useMutation({
    mutationFn: api.logout,
    onSuccess: () => {
      queryClient.clear()
      window.location.assign('/')
    },
  })

  const notes = notesQuery.data ?? []
  const pinnedCount = notes.filter((note) => note.pinned).length
  const visible = filter === 'pinned' ? notes.filter((note) => note.pinned) : notes
  const colors = info?.colors?.length ? info.colors : fallbackColors

  const filters: { value: Filter; label: string }[] = [
    { value: 'all', label: t('board.filterAll', { count: notes.length }) },
    { value: 'pinned', label: t('board.filterPinned', { count: pinnedCount }) },
  ]

  return (
    <main className="mx-auto w-full max-w-4xl px-5 py-8">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="grid h-11 w-11 place-items-center rounded-xl bg-amber-400 text-lg font-black text-amber-950">
            ✎
          </div>
          <div>
            <p className="text-[11px] font-semibold tracking-[0.25em] text-amber-300/80 uppercase">
              Portal SSO
            </p>
            <h1 className="text-xl font-bold text-white">
              {info?.name ?? t('landing.title')}
            </h1>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <LanguageSwitcher />
          <Button
            variant="secondary"
            onClick={() => logout.mutate()}
            disabled={logout.isPending}
          >
            {logout.isPending ? t('common.loading') : t('common.logout')}
          </Button>
        </div>
      </header>

      {logout.error ? (
        <div className="mt-4">
          <ErrorBanner message={t('common.error')} />
        </div>
      ) : null}

      <Card className="mt-5 flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-white">
            {t('board.greeting', { name: me.name || me.email })}
          </p>
          <p className="truncate text-xs text-slate-400">{me.email}</p>
        </div>
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="text-xs text-slate-500">{t('board.roles')}:</span>
          {me.roles.map((role) => (
            <Badge key={role} tone="info">
              {role}
            </Badge>
          ))}
          <Badge tone="success">{t('board.session')}</Badge>
        </div>
      </Card>

      <div className="mt-5">
        <AddNoteForm colors={colors} />
      </div>

      <div className="mt-6 flex flex-wrap items-center gap-2">
        {filters.map((item) => (
          <button
            key={item.value}
            type="button"
            onClick={() => setFilter(item.value)}
            className={`rounded-full border px-3.5 py-1.5 text-xs font-semibold transition ${
              filter === item.value
                ? 'border-amber-400/50 bg-amber-400/15 text-amber-200'
                : 'border-white/10 bg-white/5 text-slate-400 hover:bg-white/10 hover:text-slate-200'
            }`}
          >
            {item.label}
          </button>
        ))}
        <span className="ml-auto text-xs text-slate-500">{t('board.subtitle')}</span>
      </div>

      {notesQuery.isLoading ? (
        <Spinner label={t('common.loading')} />
      ) : notesQuery.error ? (
        <div className="mt-4">
          <ErrorBanner message={t('common.error')} />
        </div>
      ) : visible.length === 0 ? (
        <Card className="mt-4 text-center text-sm text-slate-400">
          {notes.length === 0 ? t('board.empty') : t('board.emptyFilter')}
        </Card>
      ) : (
        <div className="mt-4 grid gap-3 sm:grid-cols-2">
          {visible.map((note) => (
            <NoteCard key={note.id} note={note} colors={colors} />
          ))}
        </div>
      )}
    </main>
  )
}

function AddNoteForm({ colors }: { colors: string[] }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [color, setColor] = useState(colors[0] ?? 'amber')

  const create = useMutation({
    mutationFn: () => api.notes.create(title.trim(), body.trim(), color),
    onSuccess: async () => {
      setTitle('')
      setBody('')
      setColor(colors[0] ?? 'amber')
      await queryClient.invalidateQueries({ queryKey: ['notes'] })
    },
  })

  return (
    <Card>
      <h2 className="text-sm font-semibold text-white">{t('board.addTitle')}</h2>
      <form
        className="mt-3 space-y-3"
        onSubmit={(event) => {
          event.preventDefault()
          if (title.trim().length > 0) {
            create.mutate()
          }
        }}
      >
        <Input
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          placeholder={t('board.titlePlaceholder')}
          maxLength={160}
          required
        />
        <Textarea
          value={body}
          onChange={(event) => setBody(event.target.value)}
          placeholder={t('board.bodyPlaceholder')}
          maxLength={4000}
        />
        <div className="flex flex-wrap items-center justify-between gap-3">
          <ColorPicker
            colors={colors}
            value={color}
            onChange={setColor}
            label={t('board.color')}
          />
          <Button
            type="submit"
            disabled={create.isPending || title.trim().length === 0}
          >
            {create.isPending ? t('board.adding') : t('board.add')}
          </Button>
        </div>
        {create.error ? <ErrorBanner message={t('board.addError')} /> : null}
      </form>
    </Card>
  )
}

function ColorPicker({
  colors,
  value,
  onChange,
  label,
}: {
  colors: string[]
  value: string
  onChange: (color: string) => void
  label: string
}) {
  return (
    <div className="flex items-center gap-2">
      <span className="text-xs text-slate-500">{label}</span>
      <div className="flex items-center gap-1.5">
        {colors.map((color) => (
          <button
            key={color}
            type="button"
            aria-label={color}
            aria-pressed={value === color}
            onClick={() => onChange(color)}
            className={`h-6 w-6 rounded-full border-2 transition ${swatchColor(color)} ${
              value === color
                ? 'border-white scale-110'
                : 'border-transparent opacity-70 hover:opacity-100'
            }`}
          />
        ))}
      </div>
    </div>
  )
}

function NoteCard({ note, colors }: { note: Note; colors: string[] }) {
  const { t, lang } = useI18n()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState(false)
  const [title, setTitle] = useState(note.title)
  const [body, setBody] = useState(note.body)
  const [color, setColor] = useState(note.color)

  useEffect(() => {
    setTitle(note.title)
    setBody(note.body)
    setColor(note.color)
  }, [note.title, note.body, note.color])

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['notes'] })

  const pin = useMutation({
    mutationFn: () => api.notes.update(note.id, { pinned: !note.pinned }),
    onSuccess: refresh,
  })

  const save = useMutation({
    mutationFn: () =>
      api.notes.update(note.id, {
        title: title.trim(),
        body: body.trim(),
        color,
      }),
    onSuccess: async () => {
      setEditing(false)
      await refresh()
    },
  })

  const remove = useMutation({
    mutationFn: () => api.notes.remove(note.id),
    onSuccess: refresh,
  })

  const actionError = pin.error
    ? t('board.pinError')
    : save.error
      ? t('board.saveError')
      : remove.error
        ? t('board.deleteError')
        : null

  return (
    <Card className={`flex flex-col ${cardColor(note.color)}`}>
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          {note.pinned ? <Badge tone="warning">{t('board.pinned')}</Badge> : null}
        </div>
        <Button
          variant="ghost"
          className="px-2.5 py-1 text-xs"
          aria-pressed={note.pinned}
          disabled={pin.isPending}
          onClick={() => pin.mutate()}
        >
          {note.pinned ? t('board.unpin') : t('board.pin')}
        </Button>
      </div>

      {editing ? (
        <form
          className="mt-3 space-y-2"
          onSubmit={(event) => {
            event.preventDefault()
            if (title.trim().length > 0) {
              save.mutate()
            }
          }}
        >
          <Input
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            maxLength={160}
            required
            autoFocus
          />
          <Textarea
            value={body}
            onChange={(event) => setBody(event.target.value)}
            maxLength={4000}
            placeholder={t('board.bodyPlaceholder')}
          />
          <ColorPicker
            colors={colors}
            value={color}
            onChange={setColor}
            label={t('board.color')}
          />
          <div className="flex gap-2">
            <Button
              type="submit"
              className="px-3 py-1.5 text-xs"
              disabled={save.isPending || title.trim().length === 0}
            >
              {save.isPending ? t('common.loading') : t('common.save')}
            </Button>
            <Button
              type="button"
              variant="ghost"
              className="px-3 py-1.5 text-xs"
              onClick={() => {
                setEditing(false)
                setTitle(note.title)
                setBody(note.body)
                setColor(note.color)
              }}
            >
              {t('common.cancel')}
            </Button>
          </div>
        </form>
      ) : (
        <>
          <h3 className="mt-2 text-sm font-semibold text-slate-100">{note.title}</h3>
          {note.body ? (
            <p className="mt-1.5 flex-1 text-sm whitespace-pre-wrap text-slate-400">
              {note.body}
            </p>
          ) : (
            <div className="flex-1" />
          )}
          <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-slate-500">
            <span>{t('board.createdBy', { name: note.createdByName })}</span>
            <span>·</span>
            <span>{formatDate(note.updatedAt, lang)}</span>
          </div>
          <div className="mt-3 flex justify-end gap-1">
            <Button
              variant="ghost"
              className="px-2.5 py-1.5 text-xs"
              onClick={() => setEditing(true)}
            >
              {t('common.edit')}
            </Button>
            <Button
              variant="danger"
              className="px-2.5 py-1.5 text-xs"
              disabled={remove.isPending}
              onClick={() => {
                if (window.confirm(t('board.confirmDelete'))) {
                  remove.mutate()
                }
              }}
            >
              {t('common.delete')}
            </Button>
          </div>
        </>
      )}

      {actionError ? (
        <div className="mt-3">
          <ErrorBanner message={actionError} />
        </div>
      ) : null}
    </Card>
  )
}
