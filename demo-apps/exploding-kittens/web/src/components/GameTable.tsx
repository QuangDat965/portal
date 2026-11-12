import { useCallback, useEffect, useReducer, useState } from 'react'
import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import type { GameView, Me } from '../lib/api'
import { initialPresentation, presentationReducer, type HandCard } from '../lib/presentation'
import { useI18n } from '../lib/i18n'
import { CardHand } from './CardHand'
import { CardStack } from './CardStack'
import { PlayerSeat } from './PlayerSeat'
import { CardArt } from './CardArt'
import { CardFace } from './Card'
import { GameEffects, eventText } from './GameEffects'
import { Dialog } from './Dialog'

export function GameTable({ view, me, pending, error, syncError, future, onCloseFuture, onStart, onDraw, onPlay, onExit, onRetry }: {
  view: GameView; me: Me; pending: boolean; error: string | null; syncError: string | null; future: string[] | null
  onCloseFuture: () => void; onStart: () => void; onDraw: () => void; onPlay: (type: string, target?: string) => void; onExit: () => void; onRetry: () => void
}) {
  const { t } = useI18n()
  const reduce = useReducedMotion()
  const [presentation, dispatch] = useReducer(presentationReducer, view, initialPresentation)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [favor, setFavor] = useState<HandCard | null>(null)
  const [history, setHistory] = useState(false)
  const [copied, setCopied] = useState(false)
  const [impactSequence, setImpactSequence] = useState(0)
  useEffect(() => { dispatch({ type: 'receive', view }) }, [view])
  const complete = useCallback((sequence: number) => dispatch({ type: 'complete', sequence }), [])
  const shown = presentation.displayed
  const event = presentation.queue[0]
  const transitioning = !!event || view.revision > shown.revision
  const busy = pending || transitioning || !!syncError
  const self = shown.players.find(player => player.userId === me.id)!
  const myTurn = shown.status === 'Playing' && shown.currentTurnUserId === me.id && !self.isEliminated
  const opponents = shown.players.filter(player => player.userId !== me.id)
  const selected = presentation.hand.find(card => card.id === selectedId)
  const canPlay = myTurn && !!selected && !['defuse', 'exploding_kitten', 'nope'].includes(selected.type)
  const playCard = (card: HandCard, target?: string) => {
    if (busy || !myTurn) return
    if (card.type === 'favor' && !target) { setSelectedId(card.id); setFavor(card); return }
    dispatch({ type: 'remove', id: card.id })
    setFavor(null)
    onPlay(card.type, target)
  }
  const winner = shown.players.find(player => player.userId === shown.winnerUserId)
  const activeName = shown.players.find(player => player.userId === shown.currentTurnUserId)?.name ?? ''
  return <>
    <motion.main className={`game-world ${shown.status === 'Waiting' ? 'is-waiting' : ''} ${myTurn ? 'is-your-turn' : ''}`} data-revision={shown.revision} data-transitioning={transitioning}
      animate={event?.kind === 'exploded' && impactSequence === event.sequence && !reduce ? { x: [0, -5, 5, -3, 0], y: [0, 2, -2, 0] } : { x: 0, y: 0 }} transition={{ duration: .28 }}>
      <button className="room-chip" aria-label={t('table.copyCode')} onClick={() => { void navigator.clipboard?.writeText(shown.inviteCode).then(() => setCopied(true)).catch(() => setCopied(false)) }}><span>{t('invite')}</span><strong>{shown.inviteCode}</strong><small>{copied ? '✓' : '⧉'}</small></button>
      <div className="ambient-light" aria-hidden="true" />
      <div className="table-perspective" aria-hidden="true"><div className="table-surface"><div className="table-inlay"><span>✦</span></div></div></div>
      <div className="opponents" style={{ '--opponent-count': opponents.length } as React.CSSProperties}>
        {opponents.map(player => <PlayerSeat key={player.userId} player={player} index={shown.players.findIndex(p => p.userId === player.userId)} count={shown.handCountByPlayer[player.userId] ?? 0} active={shown.currentTurnUserId === player.userId} waiting={shown.status === 'Waiting'} />)}
        {shown.status === 'Waiting' && Array.from({ length: Math.max(0, 2 - opponents.length) }, (_, i) => <div className="empty-seat" key={i}><span>+</span><small>{t('table.emptySeat')}</small></div>)}
      </div>
      <div className="table-center">
        {shown.status === 'Playing' && <motion.div className={`turn-token ${myTurn ? 'your-turn-token' : ''}`} key={shown.currentTurnUserId} initial={{ opacity: 0, y: 5 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: .18 }} role="status"><span className="status-dot" /><strong>{myTurn ? t('yourTurn') : t('otherTurn', { name: activeName })}</strong><span className="draw-debt">{t('debt', { count: shown.pendingDraws })}</span></motion.div>}
        <div className="pile-area"><CardStack kind="deck" count={shown.deckRemaining} disabled={busy || !myTurn} onDraw={onDraw} /><span className="pile-divider" aria-hidden="true">✧</span><CardStack kind="discard" count={shown.discardCount} top={shown.discardTop} /></div>
        {shown.status === 'Playing' && <div className="context-hint" role="status">{self.isEliminated ? t('spectator') : myTurn ? t('table.turnHint') : t('table.waitingTurn')}{presentation.lastEvent && !event && <small>{eventText(presentation.lastEvent, shown, t)}</small>}</div>}
      </div>
      {shown.status === 'Waiting' && <section className="waiting-note"><span className="eyebrow">{t('table.gather')}</span><h1>{t('waiting')}</h1><p>{t('waitingHint')}</p><div className="invite-sticker"><small>{t('invite')}</small><strong>{shown.inviteCode}</strong></div>
        <p className="waiting-count">{t('players', { count: shown.players.length })}</p>{self.isHost ? <button className="primary-button" disabled={busy || shown.players.length < 2} onClick={onStart}>{t('start')} <span>→</span></button> : <p className="wait-host">{t('waitHost')}</p>}
      </section>}
      <div className="table-side-label" aria-hidden="true">{t('table.club')}</div>
      <div className="hand-dock" data-hand-anchor>
        <div className="self-info"><PlayerSeat player={self} self index={shown.players.findIndex(player => player.userId === me.id)} count={shown.ownHand.length} active={myTurn} waiting={shown.status === 'Waiting'} /></div>
        {shown.status !== 'Waiting' && <>
          <div className="hand-caption"><strong>{t('hand')} <span>{shown.ownHand.length}</span></strong><small>{t('table.handHint')}</small></div>
          <CardHand departingId={event?.kind === 'played' && event.actorUserId === me.id ? presentation.removeId ?? presentation.hand.find(card => card.type === event.cardType)?.id : undefined} cards={presentation.hand} selectedId={selected?.id ?? null} myTurn={myTurn} busy={busy || shown.status === 'Finished'} onSelect={id => setSelectedId(current => current === id ? null : id)} onPlay={card => playCard(card)} />
          <div className="hand-actions"><AnimatePresence initial={false} mode="wait">{selected ? <motion.div key={selected.id} className="selected-action" initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }} transition={{ duration: .12 }}>
            <button className="text-button" aria-label={t('table.deselect')} onClick={() => setSelectedId(null)}>×</button><span>{t(`card.${selected.type}`)}</span><button className="primary-button small" disabled={busy || !canPlay} onClick={() => playCard(selected)}>{t(canPlay ? 'table.playCard' : selected.type === 'defuse' ? 'table.automatic' : 'table.waitTurn')} <span>↗</span></button>
          </motion.div> : <motion.span key="hint" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>{t('table.selectHint')}</motion.span>}</AnimatePresence></div>
        </>}
      </div>
      <button className="history-button round-button" aria-label={t('table.history')} onClick={() => setHistory(true)}>≋</button>
      <div className="game-notices" aria-live="polite">{error && <p className="game-error" role="alert">{error}</p>}{syncError && <p className="game-error" role="alert">{syncError}<button onClick={onRetry}>{t('retry')}</button></p>}</div>
      {event && <GameEffects key={`${shown.roomId}-${event.sequence}`} event={event} view={view} meId={me.id} sourceCardId={presentation.removeId} onComplete={complete} onImpact={setImpactSequence} />}
      {shown.status === 'Finished' && !event && <div className="winner-overlay"><motion.section className="winner-card" initial={{ opacity: 0, scale: .85, y: 25 }} animate={{ opacity: 1, scale: 1, y: 0 }} transition={{ type: 'spring', stiffness: 300, damping: 24 }}><span className="winner-crown">♛</span><CardArt type="defuse" /><span className="eyebrow">{t('table.lastCat')}</span><h1>{t('won', { name: winner?.name ?? '' })}</h1><p>{t('gameOver')}</p><button className="primary-button" onClick={onExit}>{t('back')} →</button></motion.section></div>}
    </motion.main>
    {favor && <Dialog title={t('target')} onClose={() => setFavor(null)}><p>{t('table.favorHint')}</p><div className="target-list">{view.players.filter(player => player.userId !== me.id && !player.isEliminated && view.handCountByPlayer[player.userId] > 0).map(player => <button key={player.userId} disabled={busy || !myTurn} onClick={() => playCard(favor, player.userId)}><span>{player.name}</span><small>{t('cards', { count: view.handCountByPlayer[player.userId] })}</small><span>↗</span></button>)}</div>{!view.players.some(player => player.userId !== me.id && !player.isEliminated && view.handCountByPlayer[player.userId] > 0) && <p>{t('table.noTargets')}</p>}</Dialog>}
    {future && !transitioning && <Dialog title={t('future')} onClose={onCloseFuture} className="future-dialog"><p>{t('table.futureHint')}</p><ol className="future-cards">{future.map((type, i) => <li key={i}><span>{i + 1}</span><CardFace type={type} /></li>)}</ol><button className="primary-button" onClick={onCloseFuture}>{t('close')} ✓</button></Dialog>}
    {history && <Dialog title={t('table.history')} onClose={() => setHistory(false)}><ol className="history-list">{view.recentEvents.slice(-10).reverse().map(action => <li key={action.sequence}><span>{action.sequence}</span>{eventText(action, view, t)}</li>)}</ol>{view.recentEvents.length === 0 && <p>{t('table.noHistory')}</p>}</Dialog>}
  </>
}
