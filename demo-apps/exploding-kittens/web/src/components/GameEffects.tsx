import { useCallback, useEffect, useRef, useState } from 'react'
import { animate, motion, useAnimate, useReducedMotion } from 'motion/react'
import type { GameEvent, GameView } from '../lib/api'
import { useI18n } from '../lib/i18n'
import { CardFace } from './Card'

export function eventText(event: GameEvent, view: GameView, t: (key: string, vars?: Record<string, string | number>) => string) {
  const name = view.players.find(player => player.userId === event.actorUserId)?.name ?? ''
  return t(`event.${event.kind}`, { name, card: event.cardType ? t(`card.${event.cardType}`) : '' })
}

type Point = { x: number; y: number; scale: number }
export function GameEffects({ event, view, meId, sourceCardId, onComplete, onImpact }: { event: GameEvent; view: GameView; meId: string; sourceCardId?: string; onComplete: (sequence: number) => void; onImpact: (sequence: number) => void }) {
  const { t } = useI18n()
  const reduce = useReducedMotion()
  const [scope, run] = useAnimate<HTMLDivElement>()
  const [impact, setImpact] = useState(false)
  const completion = useRef(onComplete)
  completion.current = onComplete
  const done = useCallback(() => completion.current(event.sequence), [event.sequence])
  const special = event.kind === 'defused' || event.kind === 'exploded'
  const type = special ? 'exploding_kitten' : event.cardType ?? undefined
  useEffect(() => {
    let cancelled = false
    const controls: ReturnType<typeof animate>[] = []
    const root = document.querySelector('.game-world')
    function point(selector: string, fallback: Point): Point {
      const bounds = root?.querySelector(selector)?.getBoundingClientRect()
      return bounds ? { x: bounds.left + bounds.width / 2 - 65, y: bounds.top + bounds.height / 2 - 92, scale: Math.min(1.2, bounds.width / 130) } : fallback
    }
    const center = { x: innerWidth / 2 - 65, y: innerHeight * .45 - 92, scale: 1 }
    const actor = point(event.actorUserId === meId ? '[data-hand-anchor]' : `[data-seat-user="${event.actorUserId}"] .avatar-circle`, center)
    const deck = point('[data-pile="deck"]', center)
    const discard = point('[data-pile="discard"]', center)
    const selected = point(sourceCardId ? `[data-card-id="${sourceCardId}"]` : `[data-card-type="${event.cardType}"]`, actor)
    const from = event.kind === 'played' ? event.actorUserId === meId ? selected : actor : deck
    const to = event.kind === 'played' ? discard : special ? center : actor
    const flight = scope.current?.querySelector<HTMLElement>('.flying-card')
    async function sequence() {
      if (event.kind !== 'started' && flight) {
        const position = run(flight, { x: from.x, y: from.y, scale: from.scale, rotate: 0 }, { duration: 0 }); controls.push(position); await position
        if (cancelled) return
        if (reduce) {
          const control = run(flight, { opacity: [0, 1, 0] }, { duration: .12 }); controls.push(control); await control
        } else {
          const lift = run(flight, { x: from.x + 8, y: from.y - 28, scale: from.scale * 1.08, rotate: -9, opacity: 1 }, { duration: .12 }); controls.push(lift); await lift
          if (cancelled) return
          const travel = run(flight, { x: to.x, y: to.y, rotate: event.kind === 'played' ? 9 : 0, scale: special ? 1.15 : to.scale }, { type: 'spring', duration: .38, bounce: .15 }); controls.push(travel); await travel
        }
      }
      if (cancelled) return
      setImpact(true)
      onImpact(event.sequence)
      const transfer = scope.current?.querySelector<HTMLElement>('.favor-transfer')
      if (event.cardType === 'favor' && event.targetUserId && transfer && !reduce) {
        const target = point(`[data-seat-user="${event.targetUserId}"] .avatar-circle`, center)
        const position = run(transfer, { x: target.x, y: target.y, scale: .4, opacity: 1 }, { duration: 0 }); controls.push(position); await position
        const movement = run(transfer, { x: actor.x, y: actor.y, scale: actor.scale, opacity: [1, 1, 0], rotate: -8 }, { duration: .28, ease: 'easeOut' }); controls.push(movement); await movement
      }
      if (cancelled) return
      const duration = reduce ? .12 : special ? .5 : event.kind === 'started' ? .25 : .22
      const hold = animate(0, 1, { duration }); controls.push(hold); await hold
      if (!cancelled) done()
    }
    void sequence().catch(() => { if (!cancelled) done() })
    return () => { cancelled = true; for (const control of controls) control.stop() }
  }, [event, meId, sourceCardId, reduce, run, scope, done, onImpact])
  return <div className={`game-effects effect-${event.cardType ?? event.kind} ${impact ? 'is-impact' : ''}`} ref={scope} aria-hidden="true">
    {event.cardType === 'favor' && <div className="flying-card favor-transfer"><CardFace back /></div>}
    {event.kind !== 'started' && <div className={`flying-card ${impact ? 'flight-landed' : ''}`}><CardFace type={type} back={!type} /></div>}
    {impact && <>
      {event.kind === 'exploded' && <div className="explosion"><motion.div className="explosion-cloud" initial={{ scale: .35, opacity: 0, rotate: -12 }} animate={{ scale: reduce ? 1 : 1.15, opacity: [0, 1, 1, 0], rotate: 7 }} transition={{ duration: reduce ? .12 : .5 }}>{t('table.boom')}</motion.div>
        {!reduce && Array.from({ length: 12 }, (_, i) => <motion.i key={i} className={i % 3 === 0 ? 'smoke-puff' : 'spark'} initial={{ x: 0, y: 0, opacity: 1, scale: .5 }} animate={{ x: Math.cos(i * Math.PI / 6) * (100 + i % 3 * 30), y: Math.sin(i * Math.PI / 6) * 110, opacity: 0, rotate: i * 35, scale: i % 3 === 0 ? 2.5 : 1 }} transition={{ duration: .5 }} />)}
      </div>}
      {event.kind === 'defused' && <motion.div className="defuse-stamp" initial={{ scale: .6, rotate: -15 }} animate={{ scale: 1, rotate: -6 }} transition={{ type: 'spring', stiffness: 420, damping: 21 }}><span>✂</span>{t('table.safe')}</motion.div>}
      {event.kind === 'played' && <div className="action-flourish">{event.cardType === 'shuffle' ? <div className="shuffle-flourish">{[-1, 1].map(side => <motion.span key={side} initial={{ x: side * 12, rotate: side * 3 }} animate={{ x: [side * 12, side * 42, 0], rotate: [side * 3, side * 18, 0], opacity: [1, 1, 0] }} transition={{ duration: reduce ? .1 : .28 }} />)}</div> : <motion.span className={`action-symbol symbol-${event.cardType}`} initial={{ scale: .5, opacity: 0, rotate: -15 }} animate={{ scale: reduce ? 1 : [1, 1.3, 1.2], opacity: [0, 1, 0], rotate: 0 }} transition={{ duration: reduce ? .1 : .3 }}>{({ attack: 'ϟ', skip: '↷', see_the_future: '◉', favor: '♡', nope: '⊘' } as Record<string, string>)[event.cardType ?? '']}</motion.span>}</div>}
    </>}
    <motion.div className="event-ribbon" initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }}>{eventText(event, view, t)}</motion.div>
  </div>
}
