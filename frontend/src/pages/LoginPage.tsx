import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Navigate, useSearchParams } from 'react-router-dom'
import { api, getErrorMessage } from '../lib/api'
import { useMe } from '../components/auth'
import { LanguageSwitcher } from '../components/LanguageSwitcher'
import { Button, Card, ErrorBanner, Field, Input } from '../components/ui'

export function LoginPage() {
  const { t } = useTranslation()
  const { data: me, isLoading } = useMe()
  const [searchParams] = useSearchParams()
  const [email, setEmail] = useState('admin@example.com')
  const [name, setName] = useState('')
  const queryClient = useQueryClient()

  const loginMutation = useMutation({
    mutationFn: () => api.devLogin(email, name),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['me'] })
    },
  })

  if (isLoading) return null
  if (me) return <Navigate to="/apps" replace />

  const googleError = searchParams.get('error') === 'google'
  const showDevLogin = import.meta.env.DEV || import.meta.env.VITE_SHOW_DEV_LOGIN === 'true'

  return (
    <div className="relative flex min-h-screen items-center justify-center px-4 py-10">
      <div className="absolute top-4 right-4">
        <LanguageSwitcher />
      </div>

      <div className="w-full max-w-md">
        <div className="mb-6 flex items-center justify-center gap-3">
          <span className="grid h-12 w-12 place-items-center rounded-2xl bg-gradient-to-br from-sky-400 to-indigo-500 text-lg font-black text-slate-950 shadow-lg shadow-sky-500/20">
            P
          </span>
          <div>
            <p className="text-lg font-bold text-white">{t('common.appName')}</p>
            <p className="text-xs text-slate-500">Hệ sinh thái ứng dụng</p>
          </div>
        </div>

        <Card className="p-7">
          <h1 className="text-xl font-bold text-white">{t('login.title')}</h1>
          <p className="mt-1.5 text-sm text-slate-400">{t('login.subtitle')}</p>

          {googleError ? (
            <div className="mt-4">
              <ErrorBanner message={t('login.failed')} />
            </div>
          ) : null}

          <button
            type="button"
            onClick={() => {
              window.location.href = '/api/auth/google'
            }}
            className="mt-6 flex w-full items-center justify-center gap-3 rounded-lg bg-white px-4 py-2.5 text-sm font-semibold text-slate-800 transition hover:bg-slate-100"
          >
            <svg viewBox="0 0 24 24" className="h-4 w-4" aria-hidden="true">
              <path
                fill="#4285F4"
                d="M23.49 12.27c0-.79-.07-1.54-.2-2.27H12v4.51h6.47a5.54 5.54 0 0 1-2.4 3.64v3h3.86c2.26-2.09 3.56-5.17 3.56-8.88Z"
              />
              <path
                fill="#34A853"
                d="M12 24c3.24 0 5.95-1.08 7.93-2.91l-3.86-3c-1.07.72-2.45 1.15-4.07 1.15-3.13 0-5.78-2.11-6.73-4.96H1.29v3.09A12 12 0 0 0 12 24Z"
              />
              <path
                fill="#FBBC05"
                d="M5.27 14.28A7.2 7.2 0 0 1 4.9 12c0-.79.14-1.56.37-2.28V6.63H1.29a12 12 0 0 0 0 10.74l3.98-3.09Z"
              />
              <path
                fill="#EA4335"
                d="M12 4.75c1.76 0 3.35.61 4.6 1.8l3.42-3.42A11.97 11.97 0 0 0 12 0 12 12 0 0 0 1.29 6.63l3.98 3.09C6.22 6.86 8.87 4.75 12 4.75Z"
              />
            </svg>
            {t('login.google')}
          </button>

          {showDevLogin ? (
            <div className="mt-6 border-t border-white/10 pt-5">
              <p className="text-xs font-semibold tracking-wide text-slate-400 uppercase">
                {t('login.devTitle')}
              </p>
              <p className="mt-1 text-xs text-slate-500">{t('login.devHint')}</p>

              <form
                className="mt-4 space-y-3"
                onSubmit={(event) => {
                  event.preventDefault()
                  loginMutation.mutate()
                }}
              >
                <Field label={t('login.email')}>
                  <Input
                    type="email"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    required
                  />
                </Field>
                <Field label={t('login.name')}>
                  <Input
                    value={name}
                    onChange={(event) => setName(event.target.value)}
                    placeholder="Nguyễn Văn A"
                  />
                </Field>
                {loginMutation.error ? (
                  <ErrorBanner
                    message={getErrorMessage(loginMutation.error, t('login.failed'))}
                  />
                ) : null}
                <Button
                  type="submit"
                  variant="secondary"
                  className="w-full"
                  disabled={loginMutation.isPending}
                >
                  {loginMutation.isPending ? t('common.loading') : t('login.submit')}
                </Button>
              </form>
            </div>
          ) : null}
        </Card>
      </div>
    </div>
  )
}
