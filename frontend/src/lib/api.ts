export type Me = {
  id: string
  email: string
  name: string
  avatarUrl: string | null
  roles: string[]
}

export type PortalApp = {
  id: string
  slug: string
  name: string
  description: string | null
  iconUrl: string | null
  baseUrl: string
  status: string
  sortOrder: number
}

export type LaunchResult = {
  redirectUrl: string
  expiresInSeconds: number
}

export type Role = {
  id: string
  name: string
}

export type AdminApp = PortalApp & {
  callbackPath: string
  roleIds: string[]
  clientId: string | null
}

export type AdminUser = {
  id: string
  email: string
  name: string
  avatarUrl: string | null
  roles: string[]
  createdAt: string
  lastLoginAt: string | null
}

export type AuditLog = {
  id: number
  userId: string | null
  appId: string | null
  action: string
  ipAddress: string | null
  createdAt: string
}

export type CreateAppResponse = {
  app: AdminApp
  clientSecret: string | null
}

export type RotateClientResponse = {
  clientId: string
  clientSecret: string
}

export type AppInput = {
  slug: string
  name: string
  description: string | null
  iconUrl: string | null
  baseUrl: string
  callbackPath: string | null
  status: string
  sortOrder: number
  roleIds: string[]
}

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

let csrfToken: string | null = null

async function getCsrfToken(): Promise<string> {
  if (csrfToken) return csrfToken

  const response = await fetch('/api/auth/csrf', { credentials: 'include' })
  if (!response.ok) {
    throw new ApiError(response.status, 'Không lấy được CSRF token.')
  }

  const data = (await response.json()) as { token: string }
  csrfToken = data.token
  return csrfToken
}

async function request<T>(
  path: string,
  init: RequestInit = {},
  allowCsrfRetry = true,
): Promise<T> {
  const method = (init.method ?? 'GET').toUpperCase()
  const headers = new Headers(init.headers)

  if (init.body) {
    headers.set('Content-Type', 'application/json')
  }

  if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
    headers.set('X-CSRF-TOKEN', await getCsrfToken())
  }

  const response = await fetch(path, {
    ...init,
    method,
    headers,
    credentials: 'include',
  })

  if (response.status === 401) {
    throw new ApiError(401, 'Chưa đăng nhập.')
  }

  if (!response.ok) {
    if (response.status === 400 && allowCsrfRetry) {
      csrfToken = null
      return request<T>(path, init, false)
    }

    const body = await response.json().catch(() => null)
    const message =
      body?.detail ??
      body?.title ??
      body?.error ??
      `Yêu cầu thất bại (${response.status}).`
    throw new ApiError(response.status, message)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export function getErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof Error) return error.message
  return fallback
}

export const api = {
  me: () => request<Me>('/api/me'),
  myApps: () => request<PortalApp[]>('/api/me/apps'),
  launch: (slug: string) =>
    request<LaunchResult>(`/api/apps/${encodeURIComponent(slug)}/launch`, {
      method: 'POST',
    }),
  csrf: () => request<{ token: string }>('/api/auth/csrf'),
  devLogin: (email: string, name?: string) =>
    request<{ ok: boolean }>('/api/auth/dev-login', {
      method: 'POST',
      body: JSON.stringify({ email, name: name || null }),
    }),
  passwordLogin: (email: string, password: string) =>
    request<{ ok: boolean }>('/api/auth/password-login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),
  logout: async () => {
    await request<void>('/api/auth/logout', { method: 'POST' })
    csrfToken = null
  },
  admin: {
    apps: () => request<AdminApp[]>('/api/admin/apps'),
    createApp: (input: AppInput) =>
      request<CreateAppResponse>('/api/admin/apps', {
        method: 'POST',
        body: JSON.stringify(input),
      }),
    updateApp: (id: string, input: AppInput) =>
      request<AdminApp>(`/api/admin/apps/${id}`, {
        method: 'PUT',
        body: JSON.stringify(input),
      }),
    deleteApp: (id: string) =>
      request<void>(`/api/admin/apps/${id}`, { method: 'DELETE' }),
    rotateClient: (id: string) =>
      request<RotateClientResponse>(`/api/admin/apps/${id}/client/rotate`, {
        method: 'POST',
      }),
    roles: () => request<Role[]>('/api/admin/roles'),
    users: (search: string) =>
      request<AdminUser[]>(
        `/api/admin/users${search ? `?search=${encodeURIComponent(search)}` : ''}`,
      ),
    updateUserRoles: (id: string, roles: string[]) =>
      request<AdminUser>(`/api/admin/users/${id}/roles`, {
        method: 'PUT',
        body: JSON.stringify({ roles }),
      }),
    setUserPassword: (id: string, password: string) =>
      request<void>(`/api/admin/users/${id}/password`, {
        method: 'PUT',
        body: JSON.stringify({ password }),
      }),
    audit: (take = 100) => request<AuditLog[]>(`/api/admin/audit?take=${take}`),
  },
}
