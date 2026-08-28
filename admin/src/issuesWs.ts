import type { Issue } from './api.ts'

export type WsEvent =
  | { type: 'issue.created'; issue: Issue }
  | { type: 'issue.updated'; issue: Issue }
  | { type: 'issue.deleted'; id: string }

export const WS_BACKOFF_START_MS = 1000
export const WS_BACKOFF_MAX_MS = 30000

export function wsUrlFromHttp(httpBase: string, token: string): string {
  const base = httpBase.replace(/\/$/, '')
  const wsBase = base
    .replace(/^http:\/\//i, 'ws://')
    .replace(/^https:\/\//i, 'wss://')
  return `${wsBase}/ws?token=${encodeURIComponent(token)}`
}

export function nextBackoff(ms: number): number {
  return Math.min(Math.max(ms, 1) * 2, WS_BACKOFF_MAX_MS)
}

export function parseWsPayload(raw: string): WsEvent | null {
  try {
    const data: unknown = JSON.parse(raw)
    if (!data || typeof data !== 'object' || !('type' in data)) return null
    const type = data.type
    if (
      (type === 'issue.created' || type === 'issue.updated') &&
      'issue' in data &&
      data.issue &&
      typeof data.issue === 'object' &&
      'id' in data.issue &&
      typeof data.issue.id === 'string'
    ) {
      return { type, issue: data.issue as Issue }
    }
    if (
      type === 'issue.deleted' &&
      'id' in data &&
      typeof data.id === 'string' &&
      data.id
    ) {
      return { type: 'issue.deleted', id: data.id }
    }
  } catch {
    /* 非 JSON */
  }
  return null
}

export function applyWsMessage(issues: Issue[], event: WsEvent): Issue[] {
  if (event.type === 'issue.created') {
    if (issues.some((item) => item.id === event.issue.id)) return issues
    return [event.issue, ...issues]
  }
  if (event.type === 'issue.updated') {
    return issues.map((item) =>
      item.id === event.issue.id ? event.issue : item,
    )
  }
  return issues.filter((item) => item.id !== event.id)
}
