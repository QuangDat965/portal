import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { motion } from 'motion/react'
import { api, ApiError, type ActionResult, type GameView, type Me } from './lib/api'
import { newerView } from './lib/presentation'
import { useI18n } from './lib/i18n'
import { CardFace } from './components/Card'
import { CardArt } from './components/CardArt'
import { GameTable } from './components/GameTable'
import { Dialog } from './components/Dialog'

export function App() {
  const { t, lang, setLang } = useI18n()
  const client = useQueryClient()
  const [settings, setSettings] = useState(false)
  const [rules, setRules] = useState(false)
  const [exit, setExit] = useState(false)
  const [inRoom, setInRoom] = useState(false)
  const leaveRoom = useRef<(() => void) | null>(null)
  const info = useQuery({ queryKey: ['info'], queryFn: api.info })
  const me = useQuery({ queryKey: ['me'], queryFn: api.me, retry: false })
  const logout = useMutation({ mutationFn: api.logout, onSuccess: () => { client.clear(); location.reload() } })
  useEffect(() => { document.documentElement.lang = lang; document.title = t('title') }, [lang, t])
  return <div className={`game-app ${inRoom && me.data ? 'at-table' : 'at-lobby'}`}>
    <header className="floating-hud"><a className="game-brand" href="/" aria-label={t('title')}><span className="brand-icon">✹</span><span>{t('title')}<small>{t('table.brandTag')}</small></span></a>
      <div className="hud-center"><span className="status-dot" />{t('table.edition')}</div>
      <nav className="hud-controls"><button className="round-button" aria-label={t('rules')} onClick={() => setRules(true)}>?</button><button className="round-button" aria-label={t('table.settings')} onClick={() => setSettings(true)}><svg viewBox="0 0 24 24" aria-hidden="true"><path d="m10 3-1 3-3 1-3-1-2 4 3 2v3l-2 2 3 4 3-1 2 1 2 3 4-1v-3l2-2h3l1-5-3-1-1-3 1-3-4-2-2 2Z" /><circle cx="12" cy="12" r="3" /></svg></button>{inRoom && <button className="round-button exit-control" aria-label={t('table.exit')} onClick={() => setExit(true)}>↗</button>}</nav>
    </header>
    {logout.error && <p role="alert" className="floating-error">{t('error.generic')}</p>}
    {me.isPending ? <div className="loading-table" role="status"><span>✹</span>{t('loading')}</div> : me.data ? <Session key={me.data.id} me={me.data} onRoomChange={setInRoom} registerExit={leaveRoom} /> : <main className="login-scene"><div className="login-art"><CardArt /></div><span className="eyebrow">{t('table.brandTag')}</span><h1>{t('title')}<span>!</span></h1><p>{me.error instanceof ApiError && me.error.status === 401 ? t('login') : t('error.generic')}</p>{new URLSearchParams(location.search).has('error') && <p role="alert">{t('ssoError')}</p>}<div className="login-actions">{info.data && <a className="primary-button" href={info.data.portalUrl}>{t('portal')} ↗</a>}<button className="text-button" onClick={() => void me.refetch()}>{t('retry')}</button></div></main>}
    {settings && <Dialog title={t('table.settings')} onClose={() => setSettings(false)}><label className="settings-label">{t('language')}<select value={lang} onChange={e => setLang(e.target.value as 'vi' | 'en')}><option value="vi">{t('language.vi')}</option><option value="en">{t('language.en')}</option></select></label><p className="settings-note">{t('table.motionHint')}</p><div className="settings-links">{info.data && <a href={info.data.portalUrl}>{t('portal')} ↗</a>}{me.data && <button className="text-button" disabled={logout.isPending} onClick={() => logout.mutate()}>{t('logout')}</button>}</div></Dialog>}
    {rules && <Dialog title={t('rules')} onClose={() => setRules(false)}><CardArt className="rules-cat" type="defuse" /><p className="rules-copy">{t('rulesText')}</p><p className="settings-note">{t('table.mvpHint')}</p></Dialog>}
    {exit && <Dialog title={t('table.exit')} onClose={() => setExit(false)}><p>{t('table.exitHint')}</p><div className="dialog-actions"><button className="text-button" onClick={() => setExit(false)}>{t('table.stay')}</button><button className="primary-button" onClick={() => { leaveRoom.current?.(); setExit(false) }}>{t('back')} →</button></div></Dialog>}
  </div>
}

function Session({ me, onRoomChange, registerExit }: { me: Me; onRoomChange: (value: boolean) => void; registerExit: React.RefObject<(() => void) | null> }) {
  const { t } = useI18n()
  const client = useQueryClient()
  const storageKey = `ek.room.${me.id}`
  const [roomId, setRoomId] = useState(() => sessionStorage.getItem(storageKey))
  const [name, setName] = useState('')
  const [code, setCode] = useState('')
  const [future, setFuture] = useState<string[] | null>(null)
  const gate = useRef(false)
  const room = useQuery({ queryKey: ['room', me.id, roomId], queryFn: ({ signal }) => api.room(roomId!, signal), enabled: !!roomId, refetchInterval: 2000, retry: 1, structuralSharing: newerView })
  const mutation = useMutation({
    mutationFn: (action: () => Promise<GameView | ActionResult>) => action(),
    onMutate: async () => { await client.cancelQueries({ queryKey: ['room', me.id] }); setFuture(null) },
    onSuccess: async result => {
      await client.cancelQueries({ queryKey: ['room', me.id] })
      const view = 'view' in result ? result.view : result
      client.setQueryData<GameView>(['room', me.id, view.roomId], old => old && old.revision > view.revision ? old : view)
      if ('futureCards' in result) setFuture(result.futureCards ?? null)
      sessionStorage.setItem(storageKey, view.roomId)
      setRoomId(view.roomId)
    },
    onSettled: () => { void client.invalidateQueries({ queryKey: ['room', me.id] }) },
  })
  function run(action: () => Promise<GameView | ActionResult>) {
    if (gate.current) return
    gate.current = true
    void mutation.mutateAsync(action).catch(() => {}).finally(() => { gate.current = false })
  }
  const back = () => { if (gate.current) return; sessionStorage.removeItem(storageKey); setRoomId(null); mutation.reset(); setFuture(null) }
  useEffect(() => { onRoomChange(!!roomId); return () => onRoomChange(false) }, [roomId, onRoomChange])
  useEffect(() => { registerExit.current = back; return () => { registerExit.current = null } })
  const errorText = (error: Error) => {
    if (!(error instanceof ApiError)) return t('error.generic')
    const key = `error.${error.message}`
    const text = t(key)
    return text === key ? t('error.generic') : text
  }
  if (roomId) return room.data ? <GameTable key={roomId} view={room.data} me={me} pending={mutation.isPending} error={mutation.error ? errorText(mutation.error) : null} syncError={room.isError ? errorText(room.error) : null} future={future} onCloseFuture={() => setFuture(null)} onExit={back} onStart={() => run(() => api.start(roomId))} onDraw={() => run(() => api.draw(roomId))} onPlay={(type, target) => run(() => api.play(roomId, type, target))} onRetry={() => void room.refetch()} /> : <div className="loading-table" role="status">{room.isError ? <><p role="alert">{errorText(room.error)}</p><button onClick={() => void room.refetch()}>{t('retry')}</button><button onClick={back}>{t('back')}</button></> : <><span>✹</span>{t('loading')}</>}</div>
  return <main className="lobby-scene"><div className="lobby-decoration" aria-hidden="true">{['attack', 'exploding_kitten', 'defuse'].map((type, index) => <motion.div key={type} className="lobby-card" initial={{ opacity: 0, y: 25, rotate: (index - 1) * 15 }} animate={{ opacity: 1, y: 0, rotate: (index - 1) * 15 }} transition={{ type: 'spring', stiffness: 220, damping: 24, delay: index * .06 }}><CardFace type={type} /></motion.div>)}</div>
    <section className="lobby-intro"><span className="eyebrow">{t('table.pullUp')}</span><h1>{t('welcome')}</h1><p>{t('intro')}</p><span className="lobby-note">✦ {t('table.edition')} · {t('table.lastCat')}</span></section>
    {mutation.error && <p role="alert" className="lobby-error">{errorText(mutation.error)}</p>}
    <div className="lobby-tickets"><form onSubmit={e => { e.preventDefault(); run(() => api.createRoom(name.trim() || t('roomPlaceholder'))) }}><span className="ticket-number" aria-hidden="true">01</span><h2>{t('create')}</h2><label>{t('roomName')}<input value={name} onChange={e => setName(e.target.value)} maxLength={80} placeholder={t('roomPlaceholder')} /></label><button className="primary-button" disabled={mutation.isPending}>{t('create')} <span>→</span></button></form>
      <form onSubmit={e => { e.preventDefault(); run(() => api.joinRoom(code)) }}><span className="ticket-number" aria-hidden="true">02</span><h2>{t('join')}</h2><label>{t('invite')}<input className="code-input" value={code} onChange={e => setCode(e.target.value.toUpperCase().replace(/[^A-Z2-9]/g, ''))} minLength={6} maxLength={6} required placeholder={t('invitePlaceholder')} autoComplete="off" /></label><button className="primary-button secondary" disabled={mutation.isPending || code.length !== 6}>{t('join')} <span>↗</span></button></form></div>
  </main>
}
