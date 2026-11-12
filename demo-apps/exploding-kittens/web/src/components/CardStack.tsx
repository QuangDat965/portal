import { motion } from 'motion/react'
import { CardFace } from './Card'
import { useI18n } from '../lib/i18n'

export function CardStack({ kind, count, top, disabled, onDraw }: {
  kind: 'deck' | 'discard'; count: number; top?: string | null; disabled?: boolean; onDraw?: () => void
}) {
  const { t } = useI18n()
  const deck = kind === 'deck'
  return <div className={`pile pile-${kind}`}>
    <motion.button className={`card-stack ${deck ? 'draw-stack' : ''}`} data-pile={kind} disabled={deck ? disabled : true}
      aria-label={deck ? `${t('draw')} · ${t('remaining', { count })}` : t('table.discard')}
      initial={false} animate={{ rotateZ: deck ? -8 : 8 }}
      onClick={onDraw} whileHover={deck && !disabled ? { y: -7, rotateZ: -11 } : {}} whileTap={deck && !disabled ? { scale: .97 } : {}}
      transition={{ type: 'spring', stiffness: 400, damping: 28 }}>
      {Array.from({ length: Math.min(4, Math.max(0, count - 1)) }, (_, index) => <div className="stack-edge" key={index} style={{ transform: `translate(${(index + 1) * 2}px, ${(index + 1) * 3}px) rotate(${index % 2 ? 1 : -1}deg)` }} />)}
      {deck || top ? <CardFace back={deck} type={top ?? undefined} /> : <div className="discard-placeholder"><span aria-hidden="true">✦</span><small>{t('table.discard')}</small></div>}
      {deck && <span className="deck-count">{count}</span>}
    </motion.button>
    <span className="pile-label">{deck ? t('table.drawPile') : t('table.discard')}<small>{deck ? t('remaining', { count }) : count > 0 ? t('cards', { count }) : t('table.noDiscard')}</small></span>
  </div>
}
