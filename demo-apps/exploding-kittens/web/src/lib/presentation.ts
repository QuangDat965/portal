import type { GameEvent, GameView } from './api'

export type HandCard = { id: string; type: string }
export type Presentation = {
  latest: GameView; displayed: GameView; hand: HandCard[]; nextId: number
  queue: GameEvent[]; cursor: number; lastEvent: GameEvent | null; removeId?: string
}

export function reconcileHand(previous: HandCard[], types: string[], nextId: number, removeId?: string) {
  const pool = previous.filter(card => card.id !== removeId)
  const hand = types.map(type => {
    const index = pool.findIndex(card => card.type === type)
    if (index >= 0) return pool.splice(index, 1)[0]
    return { id: `hand-${nextId++}`, type }
  })
  return { hand, nextId }
}

export function initialPresentation(view: GameView): Presentation {
  return { latest: view, displayed: view, ...reconcileHand([], view.ownHand, 1),
    queue: [], cursor: view.revision, lastEvent: null }
}

export type PresentationAction = { type: 'receive'; view: GameView } | { type: 'complete'; sequence: number } | { type: 'remove'; id: string }

export function presentationReducer(state: Presentation, action: PresentationAction): Presentation {
  if (action.type === 'remove') return { ...state, removeId: action.id }
  if (action.type === 'complete') {
    if (state.queue[0]?.sequence !== action.sequence) return state
    const queue = state.queue.slice(1)
    if (queue.length) return { ...state, queue, lastEvent: state.queue[0] }
    return { ...state, queue, lastEvent: state.queue[0], displayed: state.latest,
      ...reconcileHand(state.hand, state.latest.ownHand, state.nextId, state.removeId), removeId: undefined }
  }
  const view = action.view
  if (view.roomId !== state.latest.roomId) return initialPresentation(view)
  if (view.revision < state.latest.revision) return state
  const events = view.recentEvents.filter(event => event.sequence > state.cursor).sort((a, b) => a.sequence - b.sequence)
  const gap = view.revision > state.cursor && (events[0]?.sequence !== state.cursor + 1 || events.at(-1)?.sequence !== view.revision)
  if (gap || events.length + state.queue.length > 8) {
    return { ...state, latest: view, displayed: view, cursor: view.revision, queue: [], removeId: undefined,
      ...reconcileHand(state.hand, view.ownHand, state.nextId, state.removeId), lastEvent: events.at(-1) ?? null }
  }
  const queue = [...state.queue, ...events]
  return { ...state, latest: view, cursor: view.revision, queue,
    ...(queue.length === 0 ? { displayed: view, ...reconcileHand(state.hand, view.ownHand, state.nextId), removeId: undefined } : {}) }
}

export function newerView(previous: unknown, next: unknown): unknown {
  const oldView = previous as GameView | undefined
  const newView = next as GameView
  return oldView?.roomId === newView.roomId && oldView.revision > newView.revision ? previous : next
}
