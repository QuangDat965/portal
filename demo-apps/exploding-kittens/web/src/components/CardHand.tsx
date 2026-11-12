import { useEffect, useRef, useState, type CSSProperties } from 'react'
import { AnimatePresence, motion } from 'motion/react'
import { Card } from './Card'
import type { HandCard } from '../lib/presentation'
import { useI18n } from '../lib/i18n'

export function CardHand({ cards, selectedId, myTurn, busy, departingId, onSelect, onPlay }: {
  cards: HandCard[]; selectedId: string | null; myTurn: boolean; busy: boolean; departingId?: string
  onSelect: (id: string | null) => void; onPlay: (card: HandCard) => void
}) {
  const { t } = useI18n()
  const container = useRef<HTMLDivElement>(null)
  const suppressClick = useRef(0)
  const [width, setWidth] = useState(800)
  const [start, setStart] = useState(0)
  useEffect(() => {
    if (!container.current) return
    const observer = new ResizeObserver(([entry]) => setWidth(entry.contentRect.width))
    observer.observe(container.current)
    return () => observer.disconnect()
  }, [])
  const capacity = width < 600 ? 5 : 10
  const offset = Math.min(start, Math.max(0, cards.length - capacity))
  const visible = cards.slice(offset, offset + capacity)
  const cardWidth = width < 600 ? 118 : 158
  const spacing = Math.min(width < 600 ? 64 : 102, (width - cardWidth - 32) / Math.max(1, visible.length - 1))
  const page = (direction: number) => {
    onSelect(null)
    setStart(Math.max(0, Math.min(cards.length - capacity, offset + direction * Math.max(1, capacity - 1))))
  }
  useEffect(() => {
    if (selectedId && !visible.some(card => card.id === selectedId)) onSelect(null)
  }, [selectedId, visible, onSelect])
  const select = (id: string) => { if (performance.now() > suppressClick.current) onSelect(id) }
  return <div className="hand-region" ref={container} style={{ '--card-width': `${cardWidth}px` } as CSSProperties}>
    {cards.length > capacity && <div className="hand-pagination"><button className="round-button" aria-label={t('table.previousCards')} disabled={offset === 0 || busy} onClick={() => page(-1)}>←</button><span>{t('table.handPage', { from: offset + 1, to: offset + visible.length, count: cards.length })}</span><button className="round-button" aria-label={t('table.nextCards')} disabled={offset + capacity >= cards.length || busy} onClick={() => page(1)}>→</button></div>}
    <motion.div className="card-fan" aria-label={t('hand')} onPanStart={() => { suppressClick.current = performance.now() + 600 }} onPanEnd={(_, info) => { if (Math.abs(info.offset.x) > 35 && cards.length > capacity) page(info.offset.x < 0 ? 1 : -1) }}>
      <AnimatePresence initial={false}>
        {visible.map((card, index) => {
          const position = index - (visible.length - 1) / 2
          const ratio = visible.length > 1 ? position / ((visible.length - 1) / 2) : 0
          const playable = myTurn && !['defuse', 'exploding_kitten', 'nope'].includes(card.type)
          return <Card key={card.id} {...card} index={index} x={position * spacing} y={Math.pow(Math.abs(ratio), 2) * 30} angle={ratio * 13} departing={departingId === card.id} zoom={width < 600 ? 1.18 : 1.06} selected={selectedId === card.id} playable={playable} busy={busy}
            onSelect={() => select(card.id)} onPlay={() => { if (performance.now() > suppressClick.current) onPlay(card) }} />
        })}
      </AnimatePresence>
    </motion.div>
    {cards.length === 0 && <p className="empty-hand">{t('empty')}</p>}
  </div>
}
