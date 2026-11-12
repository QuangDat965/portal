import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError, type Info, type Me, type Task } from './lib/api'
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

type Filter = 'all' | 'open' | 'done'

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
        <div className="mx-auto grid h-14 w-14 place-items-center rounded-2xl bg-emerald-500 text-2xl font-black text-emerald-950">
          ✓
        </div>
        <p className="mt-4 text-[11px] font-semibold tracking-[0.3em] text-emerald-300/80">
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

        <div className="mt-6 rounded-xl border border-emerald-400/20 bg-emerald-400/5 p-4 text-left">
          <p className="text-sm leading-relaxed text-slate-300">{t('landing.hint')}</p>
          <p className="mt-2 text-xs leading-relaxed text-slate-500">{t('landing.note')}</p>
        </div>

        <a
          href={portalUrl}
          className="mt-6 inline-flex items-center justify-center gap-2 rounded-lg bg-emerald-500 px-4 py-2 text-sm font-semibold text-emerald-950 shadow-lg shadow-emerald-500/20 transition hover:bg-emerald-400"
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

  const tasksQuery = useQuery<Task[]>({
    queryKey: ['tasks'],
    queryFn: api.tasks.list,
  })

  const logout = useMutation({
    mutationFn: api.logout,
    onSuccess: () => {
      queryClient.clear()
      window.location.assign('/')
    },
  })

  const tasks = tasksQuery.data ?? []
  const openCount = tasks.filter((task) => !task.done).length
  const doneCount = tasks.length - openCount
  const visible =
    filter === 'all'
      ? tasks
      : tasks.filter((task) => (filter === 'done' ? task.done : !task.done))

  const filters: { value: Filter; label: string }[] = [
    { value: 'all', label: t('board.filterAll', { count: tasks.length }) },
    { value: 'open', label: t('board.filterOpen', { count: openCount }) },
    { value: 'done', label: t('board.filterDone', { count: doneCount }) },
  ]

  return (
    <main className="mx-auto w-full max-w-3xl px-5 py-8">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="grid h-11 w-11 place-items-center rounded-xl bg-emerald-500 text-lg font-black text-emerald-950">
            ✓
          </div>
          <div>
            <p className="text-[11px] font-semibold tracking-[0.25em] text-emerald-300/80 uppercase">
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
        <AddTaskForm />
      </div>

      <div className="mt-6 flex flex-wrap items-center gap-2">
        {filters.map((item) => (
          <button
            key={item.value}
            type="button"
            onClick={() => setFilter(item.value)}
            className={`rounded-full border px-3.5 py-1.5 text-xs font-semibold transition ${
              filter === item.value
                ? 'border-emerald-400/50 bg-emerald-400/15 text-emerald-200'
                : 'border-white/10 bg-white/5 text-slate-400 hover:bg-white/10 hover:text-slate-200'
            }`}
          >
            {item.label}
          </button>
        ))}
        <span className="ml-auto text-xs text-slate-500">{t('board.subtitle')}</span>
      </div>

      {tasksQuery.isLoading ? (
        <Spinner label={t('common.loading')} />
      ) : tasksQuery.error ? (
        <div className="mt-4">
          <ErrorBanner message={t('common.error')} />
        </div>
      ) : visible.length === 0 ? (
        <Card className="mt-4 text-center text-sm text-slate-400">
          {tasks.length === 0 ? t('board.empty') : t('board.emptyFilter')}
        </Card>
      ) : (
        <div className="mt-4 space-y-3">
          {visible.map((task) => (
            <TaskRow key={task.id} task={task} />
          ))}
        </div>
      )}
    </main>
  )
}

function AddTaskForm() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [title, setTitle] = useState('')
  const [notes, setNotes] = useState('')

  const create = useMutation({
    mutationFn: () =>
      api.tasks.create(title.trim(), notes.trim().length > 0 ? notes.trim() : null),
    onSuccess: async () => {
      setTitle('')
      setNotes('')
      await queryClient.invalidateQueries({ queryKey: ['tasks'] })
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
          maxLength={200}
          required
        />
        <Textarea
          value={notes}
          onChange={(event) => setNotes(event.target.value)}
          placeholder={t('board.notesPlaceholder')}
          maxLength={2000}
        />
        <div className="flex justify-end">
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

function TaskRow({ task }: { task: Task }) {
  const { t, lang } = useI18n()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState(false)
  const [title, setTitle] = useState(task.title)
  const [notes, setNotes] = useState(task.notes ?? '')

  useEffect(() => {
    setTitle(task.title)
    setNotes(task.notes ?? '')
  }, [task.title, task.notes])

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['tasks'] })

  const toggle = useMutation({
    mutationFn: () => api.tasks.update(task.id, { done: !task.done }),
    onSuccess: refresh,
  })

  const save = useMutation({
    mutationFn: () =>
      api.tasks.update(task.id, {
        title: title.trim(),
        notes: notes.trim().length > 0 ? notes.trim() : null,
      }),
    onSuccess: async () => {
      setEditing(false)
      await refresh()
    },
  })

  const remove = useMutation({
    mutationFn: () => api.tasks.remove(task.id),
    onSuccess: refresh,
  })

  const actionError = toggle.error
    ? t('board.saveError')
    : save.error
      ? t('board.saveError')
      : remove.error
        ? t('board.deleteError')
        : null

  return (
    <Card className={task.done ? 'opacity-75' : ''}>
      <div className="flex items-start gap-3">
        <button
          type="button"
          role="checkbox"
          aria-checked={task.done}
          aria-label={task.done ? t('board.uncheck') : t('board.check')}
          onClick={() => toggle.mutate()}
          disabled={toggle.isPending}
          className={`mt-0.5 grid h-5 w-5 shrink-0 place-items-center rounded-md border transition ${
            task.done
              ? 'border-emerald-400 bg-emerald-400 text-emerald-950'
              : 'border-white/25 bg-slate-950/60 text-transparent hover:border-emerald-400/60'
          }`}
        >
          <span className="text-[11px] font-black">✓</span>
        </button>

        <div className="min-w-0 flex-1">
          {editing ? (
            <form
              className="space-y-2"
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
                maxLength={200}
                required
                autoFocus
              />
              <Textarea
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
                maxLength={2000}
                placeholder={t('board.notesPlaceholder')}
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
                    setTitle(task.title)
                    setNotes(task.notes ?? '')
                  }}
                >
                  {t('common.cancel')}
                </Button>
              </div>
            </form>
          ) : (
            <>
              <h3
                className={`text-sm font-semibold ${
                  task.done ? 'text-slate-500 line-through' : 'text-slate-100'
                }`}
              >
                {task.title}
              </h3>
              {task.notes ? (
                <p className="mt-1.5 text-sm whitespace-pre-wrap text-slate-400">
                  {task.notes}
                </p>
              ) : null}
              <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-slate-500">
                <Badge tone={task.done ? 'success' : 'info'}>
                  {task.done ? t('board.done') : t('board.open')}
                </Badge>
                <span>{t('board.createdBy', { name: task.createdByName })}</span>
                <span>·</span>
                <span>{formatDate(task.createdAt, lang)}</span>
              </div>
            </>
          )}
        </div>

        {!editing ? (
          <div className="flex shrink-0 items-center gap-1">
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
        ) : null}
      </div>

      {actionError ? (
        <div className="mt-3">
          <ErrorBanner message={actionError} />
        </div>
      ) : null}
    </Card>
  )
}
