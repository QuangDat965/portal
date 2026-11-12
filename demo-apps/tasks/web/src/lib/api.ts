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

export type Task = {
  id: string
  title: string
  notes: string | null
  done: boolean
  createdBy: string
  createdByName: string
  createdAt: string
  updatedAt: string
}

export type TaskPatch = {
  title?: string
  notes?: string | null
  done?: boolean
}

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
  tasks: {
    list: () => request<Task[]>('/api/tasks'),
    create: (title: string, notes: string | null) =>
      mutate<Task>('/api/tasks', 'POST', { title, notes }),
    update: (id: string, patch: TaskPatch) =>
      mutate<Task>(`/api/tasks/${id}`, 'PUT', patch),
    remove: (id: string) => mutate<void>(`/api/tasks/${id}`, 'DELETE'),
  },
}
