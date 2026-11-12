import { test } from 'node:test'
import assert from 'node:assert/strict'
import { initialPresentation, newerView, presentationReducer, reconcileHand } from '../src/lib/presentation.ts'
import type { GameEvent, GameView } from '../src/lib/api.ts'

function view(revision = 0, recentEvents: GameEvent[] = [], ownHand = ['skip', 'skip', 'defuse']): GameView {
  return { revision, recentEvents, ownHand, roomId: 'room-a', name: 'Cats', inviteCode: 'ABC234',
    status: 'Playing', players: [], handCountByPlayer: {}, currentTurnUserId: 'a', pendingDraws: 1,
    deckRemaining: 10, winnerUserId: null, discardTop: null, discardCount: 0 }
}
function event(sequence: number): GameEvent {
  return { sequence, actorUserId: 'a', kind: 'drawn', cardType: null, targetUserId: null }
}

test('joining or reloading never replays old events', () => {
  const state = initialPresentation(view(4, [event(3), event(4)]))
  assert.equal(state.cursor, 4)
  assert.equal(state.queue.length, 0)
})

test('poll and mutation responses deduplicate events and reject old revisions', () => {
  let state = initialPresentation(view())
  state = presentationReducer(state, { type: 'receive', view: view(2, [event(1), event(2)]) })
  state = presentationReducer(state, { type: 'receive', view: view(1, [event(1)]) })
  state = presentationReducer(state, { type: 'receive', view: view(2, [event(1), event(2)]) })
  assert.deepEqual(state.queue.map(e => e.sequence), [1, 2])
  assert.equal(state.displayed.revision, 0)
  assert.equal(state.latest.revision, 2)
  assert.equal((newerView(view(2), view(1)) as GameView).revision, 2)
})

test('events finish in order and commit the newest hand only after the flight', () => {
  let state = initialPresentation(view())
  state = presentationReducer(state, { type: 'receive', view: view(1, [event(1)], ['skip', 'defuse']) })
  state = presentationReducer(state, { type: 'receive', view: view(2, [event(1), event(2)], ['skip', 'defuse', 'attack']) })
  state = presentationReducer(state, { type: 'complete', sequence: 2 })
  assert.equal(state.queue[0].sequence, 1)
  state = presentationReducer(state, { type: 'complete', sequence: 1 })
  assert.equal(state.displayed.revision, 0)
  state = presentationReducer(state, { type: 'complete', sequence: 2 })
  assert.equal(state.displayed.revision, 2)
  assert.deepEqual(state.hand.map(c => c.type), ['skip', 'defuse', 'attack'])
})

test('identical cards keep identity and remove the selected copy', () => {
  const old = [{ id: 'one', type: 'skip' }, { id: 'two', type: 'skip' }, { id: 'three', type: 'defuse' }]
  const next = reconcileHand(old, ['skip', 'defuse', 'favor'], 4, 'two')
  assert.deepEqual(next.hand.map(card => card.id), ['one', 'three', 'hand-4'])
  assert.equal(next.nextId, 5)
})

test('event gaps and long reconnect backlogs resync without inventing animations', () => {
  let state = initialPresentation(view())
  state = presentationReducer(state, { type: 'receive', view: view(40, [event(39), event(40)]) })
  assert.equal(state.queue.length, 0)
  assert.equal(state.displayed.revision, 40)
  state = presentationReducer(state, { type: 'receive', view: view(49, Array.from({ length: 9 }, (_, i) => event(i + 41))) })
  assert.equal(state.queue.length, 0)
  assert.equal(state.displayed.revision, 49)
})

test('switching rooms discards prior animation completion callbacks', () => {
  let state = initialPresentation(view())
  state = presentationReducer(state, { type: 'receive', view: view(1, [event(1)]) })
  state = presentationReducer(state, { type: 'receive', view: { ...view(), roomId: 'room-b' } })
  state = presentationReducer(state, { type: 'complete', sequence: 1 })
  assert.equal(state.displayed.roomId, 'room-b')
  assert.equal(state.queue.length, 0)
})
