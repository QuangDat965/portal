export type Me = {
  id: string
  email: string
  name: string
  roles: string[]
}

export type Info = {
  app: string
  name: string
  description: string | null
  portalUrl: string
}

export type Player = { userId: string; name: string; isHost: boolean; isEliminated: boolean }
export type GameEvent = {
  sequence: number; actorUserId: string; kind: 'started' | 'drawn' | 'played' | 'defused' | 'exploded'
  cardType: string | null; targetUserId: string | null
}
export type GameView = {
  revision: number; recentEvents: GameEvent[]; discardTop: string | null; discardCount: number
  roomId: string; name: string; inviteCode: string; status: 'Waiting' | 'Playing' | 'Finished'
  players: Player[]; ownHand: string[]; handCountByPlayer: Record<string, number>
  currentTurnUserId: string; pendingDraws: number; deckRemaining: number; winnerUserId: string | null
}
export type ActionResult = { view: GameView; futureCards?: string[] | null; exploded?: boolean; defused?: boolean }

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(path, {
    ...init,
    headers,
    credentials: 'same-origin',
  })

  if (response.status === 401) {
    throw new ApiError(401, 'unauthorized')
  }

  if (!response.ok) {
    let message = `HTTP ${response.status}`
    try {
      const body = (await response.json()) as { error?: string; title?: string }
      message = body.error ?? body.title ?? message
    } catch {
      // Không phải JSON — giữ message mặc định.
    }
    throw new ApiError(response.status, message)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

let csrfPromise: Promise<string> | null = null

async function csrfToken(): Promise<string> {
  if (!csrfPromise) {
    csrfPromise = request<{ token: string }>('/api/auth/csrf')
      .then((data) => data.token)
      .catch((error: unknown) => {
        csrfPromise = null
        throw error
      })
  }
  return csrfPromise
}

async function mutate<T>(
  path: string,
  method: 'POST' | 'PUT' | 'DELETE',
  body?: unknown,
): Promise<T> {
  const token = await csrfToken()
  return request<T>(path, {
    method,
    headers: { 'X-CSRF-TOKEN': token },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
}

export const api = {
  info: () => request<Info>('/api/info'),
  me: () => request<Me>('/api/me'),
  logout: () => mutate<void>('/auth/logout', 'POST'),
  createRoom: (name: string) => mutate<GameView>('/api/rooms', 'POST', { name }),
  joinRoom: (inviteCode: string) => mutate<GameView>('/api/rooms/join', 'POST', { inviteCode }),
  room: (id: string, signal?: AbortSignal) => request<GameView>(`/api/rooms/${id}/state`, { signal }),
  start: (id: string) => mutate<GameView>(`/api/rooms/${id}/start`, 'POST'),
  draw: (id: string) => mutate<ActionResult>(`/api/games/${id}/draw`, 'POST'),
  play: (id: string, cardType: string, targetUserId?: string) =>
    mutate<ActionResult>(`/api/games/${id}/play`, 'POST', { cardType, targetUserId }),
}
