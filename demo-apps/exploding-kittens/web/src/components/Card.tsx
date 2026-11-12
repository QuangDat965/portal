import { useRef, useState, type CSSProperties, type PointerEvent } from 'react'
import { motion, useIsPresent, useReducedMotion, useSpring } from 'motion/react'
import { useI18n } from '../lib/i18n'
import { CardArt, cardColors } from './CardArt'

export function CardFace({ type, back = false }: { type?: string; back?: boolean }) {
  const { t } = useI18n()
  if (back) return <div className="card-face card-back"><div className="back-frame"><span className="back-star">✹</span><CardArt /><strong>{t('title')}</strong><span className="back-paws">✦ · ✦</span></div></div>
  return <div className={`card-face face-${type}`} style={{ '--card-color': cardColors[type ?? ''] ?? '#f57558' } as CSSProperties}>
    <div className="card-corner"><span>{t(`card.${type}`)}</span><span aria-hidden="true">✦</span></div>
    <div className="card-illustration"><CardArt type={type} /></div>
    <div className="card-copy"><strong>{t(`card.${type}`)}</strong><p>{t(`hint.${type}`)}</p></div>
    <span className="card-edition" aria-hidden="true">✦ ✦ ✦</span>
  </div>
}

type Props = {
  id: string; type: string; x: number; y: number; angle: number; index: number
  selected: boolean; playable: boolean; busy: boolean; zoom: number; departing: boolean
  onSelect: () => void; onPlay: () => void
}

export function Card({ id, type, x, y, angle, index, selected, playable, busy, zoom, departing, onSelect, onPlay }: Props) {
  const { t } = useI18n()
  const reduce = useReducedMotion()
  const present = useIsPresent()
  const [hovered, setHovered] = useState(false)
  const [focused, setFocused] = useState(false)
  const bounds = useRef<DOMRect | null>(null)
  const rotateX = useSpring(0, { stiffness: 380, damping: 28 })
  const rotateY = useSpring(0, { stiffness: 380, damping: 28 })
  const lifted = selected || hovered || focused
  function tilt(event: PointerEvent<HTMLButtonElement>) {
    if (reduce || event.pointerType !== 'mouse' || !bounds.current) return
    rotateY.set((event.clientX - bounds.current.left - bounds.current.width / 2) / bounds.current.width * 12)
    rotateX.set(-(event.clientY - bounds.current.top - bounds.current.height / 2) / bounds.current.height * 10)
  }
  return <motion.div className={`hand-card ${playable ? 'is-playable' : ''} ${selected ? 'is-selected' : ''}`}
    data-card-id={id} data-card-type={type} data-selected={selected} data-departing={departing} style={{ zIndex: lifted ? 100 : index + 1, pointerEvents: present ? 'auto' : 'none' }}
    initial={{ opacity: 0, y: reduce ? y : y + 70, rotate: angle }}
    animate={{ opacity: 1, x: lifted ? Math.max(-220, Math.min(220, x)) : x, y: lifted ? zoom > 1.1 ? -60 : -48 : y, rotate: lifted ? 0 : angle, scale: lifted ? zoom : 1 }}
    exit={{ opacity: 0, scale: reduce ? 1 : .8, y: reduce ? y : y - 45 }}
    transition={{ type: 'spring', stiffness: 390, damping: 32, mass: .8 }}>
    <motion.button className="physical-card" type="button" aria-label={`${t(`card.${type}`)} · ${t(playable ? 'table.playable' : 'table.inspect')}`}
      aria-pressed={selected} data-playable={playable} disabled={busy || !present}
      style={{ rotateX, rotateY }}
      onPointerEnter={event => { if (event.pointerType === 'mouse') { bounds.current = event.currentTarget.getBoundingClientRect(); setHovered(true) } }}
      onPointerMove={tilt} onPointerLeave={() => { setHovered(false); rotateX.set(0); rotateY.set(0) }}
      onFocus={event => setFocused(event.currentTarget.matches(':focus-visible'))} onBlur={() => setFocused(false)} onClick={() => {
        if (busy) return
        if (matchMedia('(hover: hover) and (pointer: fine)').matches && playable && type !== 'favor') onPlay()
        else onSelect()
      }}>
      <CardFace type={type} />
    </motion.button>
  </motion.div>
}
