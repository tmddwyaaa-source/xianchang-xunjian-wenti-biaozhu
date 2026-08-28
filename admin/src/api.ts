export type IssuePriority = 'low' | 'medium' | 'high'
export type IssueStatus = 'open' | 'in_progress' | 'resolved'
export type UserRole = 'admin' | 'inspector' | 'viewer'

export type AuthUser = {
  id: string
  username: string
  role: UserRole
}

export type Issue = {
  id: string
  title: string
  description?: string
  priority: IssuePriority
  status: IssueStatus
  position?: { x: number; y: number; z: number }
  submitterId?: string
  submitterName?: string
  createdAt?: string
  updatedAt?: string
}

export const TOKEN_KEY = 'inspect.token'
export const USER_KEY = 'inspect.user'

export function apiBase(): string {
  const raw = import.meta.env.VITE_API_BASE
  if (typeof raw === 'string' && raw.trim()) {
    return raw.trim().replace(/\/$/, '')
  }
  return 'http://127.0.0.1:8080'
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function getUser(): AuthUser | null {
  const raw = localStorage.getItem(USER_KEY)
  if (!raw) return null
  try {
    const data: unknown = JSON.parse(raw)
    if (
      data &&
      typeof data === 'object' &&
      'id' in data &&
      'username' in data &&
      'role' in data &&
      typeof data.id === 'string' &&
      typeof data.username === 'string' &&
      (data.role === 'admin' ||
        data.role === 'inspector' ||
        data.role === 'viewer')
    ) {
      return { id: data.id, username: data.username, role: data.role }
    }
  } catch {
    /* 损坏的会话 */
  }
  return null
}

export function setSession(token: string, user: AuthUser): void {
  localStorage.setItem(TOKEN_KEY, token)
  localStorage.setItem(USER_KEY, JSON.stringify(user))
}

export function clearSession(): void {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(USER_KEY)
}

let onUnauthorized: (() => void) | null = null

export function setUnauthorizedHandler(handler: (() => void) | null): void {
  onUnauthorized = handler
}

export function networkErrorMessage(err: unknown): string {
  const msg = err instanceof Error ? err.message : ''
  if (msg === 'invalid credentials' || msg.includes('invalid credentials')) {
    return '用户名或密码错误'
  }
  if (msg === 'unauthorized' || msg.includes('unauthorized')) {
    return '登录已过期，请重新登录'
  }
  if (err instanceof TypeError) {
    return `无法连接后端，请确认服务已在 ${apiBase()} 运行`
  }
  if (msg.trim()) {
    return msg
  }
  return '加载失败，请稍后重试'
}

async function readError(res: Response): Promise<string> {
  try {
    const data: unknown = await res.json()
    if (
      data &&
      typeof data === 'object' &&
      'error' in data &&
      typeof data.error === 'string' &&
      data.error.trim()
    ) {
      return data.error
    }
  } catch {
    /* 非 JSON 响应 */
  }
  return `请求失败（${res.status}）`
}

async function parseJson(res: Response): Promise<unknown> {
  if (!res.ok) {
    throw new Error(await readError(res))
  }
  try {
    return await res.json()
  } catch {
    throw new Error('服务器返回了无法解析的数据')
  }
}

type FetchOpts = {
  auth?: boolean
}

async function apiFetch(
  path: string,
  init: RequestInit = {},
  opts: FetchOpts = {},
): Promise<Response> {
  const headers = new Headers(init.headers)
  const needAuth = opts.auth !== false
  if (needAuth) {
    const token = getToken()
    if (token) {
      headers.set('Authorization', `Bearer ${token}`)
    }
  }
  const res = await fetch(`${apiBase()}${path}`, { ...init, headers })
  if (needAuth && res.status === 401) {
    clearSession()
    onUnauthorized?.()
    throw new Error(await readError(res))
  }
  return res
}

export async function login(
  username: string,
  password: string,
): Promise<{ token: string; user: AuthUser }> {
  const res = await apiFetch(
    '/api/auth/login',
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    },
    { auth: false },
  )
  const data = await parseJson(res)
  if (
    !data ||
    typeof data !== 'object' ||
    !('token' in data) ||
    !('user' in data) ||
    typeof data.token !== 'string' ||
    !data.token ||
    !data.user ||
    typeof data.user !== 'object' ||
    !('id' in data.user) ||
    !('username' in data.user) ||
    !('role' in data.user) ||
    typeof data.user.id !== 'string' ||
    typeof data.user.username !== 'string' ||
    (data.user.role !== 'admin' &&
      data.user.role !== 'inspector' &&
      data.user.role !== 'viewer')
  ) {
    throw new Error('登录响应格式不正确')
  }
  const user: AuthUser = {
    id: data.user.id,
    username: data.user.username,
    role: data.user.role,
  }
  setSession(data.token, user)
  return { token: data.token, user }
}

export async function fetchIssues(): Promise<Issue[]> {
  const res = await apiFetch('/api/issues')
  const data = await parseJson(res)
  if (
    !data ||
    typeof data !== 'object' ||
    !('issues' in data) ||
    !Array.isArray(data.issues)
  ) {
    throw new Error('列表数据格式不正确')
  }
  return data.issues as Issue[]
}

export async function patchIssueStatus(
  id: string,
  status: IssueStatus,
): Promise<Issue> {
  const res = await apiFetch(`/api/issues/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ status }),
  })
  return (await parseJson(res)) as Issue
}

export async function deleteIssue(id: string): Promise<void> {
  const res = await apiFetch(`/api/issues/${encodeURIComponent(id)}`, {
    method: 'DELETE',
  })
  if (res.status === 204) return
  if (!res.ok) {
    throw new Error(await readError(res))
  }
}
