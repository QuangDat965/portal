import { motion } from 'motion/react'
import type { Player } from '../lib/api'
import { useI18n } from '../lib/i18n'
import { AvatarArt } from './CardArt'

export function PlayerSeat({ player, index, count, active, self = false, waiting = false }: {
  player: Player; index: number; count: number; active: boolean; self?: boolean; waiting?: boolean
}) {
  const { t } = useI18n()
  return <div className={`player-seat ${active ? 'seat-active' : ''} ${player.isEliminated ? 'seat-out' : ''} ${self ? 'self-seat' : ''}`} data-seat-user={player.userId}>
    <motion.div className="avatar-circle" animate={{ scale: active ? 1.07 : 1 }} transition={{ type: 'spring', stiffness: 330, damping: 26 }}>
      <AvatarArt index={index} />
      {active && <span className="seat-turn" aria-label={t('turn')}>✦</span>}
      {player.isEliminated && <span className="seat-eliminated" aria-label={t('eliminated')}>×</span>}
    </motion.div>
    <div className="seat-name"><strong>{player.name}</strong>{self && <span>{t('you')}</span>}</div>
    <span className="seat-count">{player.isEliminated ? t('eliminated') : waiting ? player.isHost ? t('host') : t('table.ready') : t('cards', { count })}</span>
    {!self && !player.isEliminated && count > 0 && <div className="opponent-hand" aria-hidden="true">{Array.from({ length: Math.min(6, count) }, (_, i) => <span key={i} style={{ transform: `translateX(${(i - (Math.min(6, count) - 1) / 2) * 15}px) rotate(${(i - 2) * 7}deg)` }}>✹</span>)}</div>}
  </div>
}
