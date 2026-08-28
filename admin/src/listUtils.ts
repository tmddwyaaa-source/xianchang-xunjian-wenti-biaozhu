import type { Issue, IssuePriority, IssueStatus, UserRole } from './api.ts'

export const DESC_MAX = 40

export function truncateDescription(
  text: string | undefined,
  max = DESC_MAX,
): string {
  const s = text ?? ''
  if (s.length <= max) return s
  return `${s.slice(0, max)}…`
}

export function formatDateTime(iso: string | undefined): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return d.toLocaleString()
}

export function formatCoord(n: number | undefined): string {
  if (typeof n !== 'number' || Number.isNaN(n)) return '—'
  return String(n)
}

export function filterIssues(
  issues: Issue[],
  status: '' | IssueStatus,
  priority: '' | IssuePriority,
  submitterQuery: string,
): Issue[] {
  const q = submitterQuery.trim().toLowerCase()
  return issues.filter((issue) => {
    if (status && issue.status !== status) return false
    if (priority && issue.priority !== priority) return false
    if (q && !(issue.submitterName ?? '').toLowerCase().includes(q)) {
      return false
    }
    return true
  })
}

export function canChangeStatus(role: UserRole): boolean {
  return role === 'admin'
}

export function canDeleteIssue(
  role: UserRole,
  userId: string,
  submitterId: string | undefined,
): boolean {
  if (role === 'admin') return true
  if (role === 'inspector') {
    return Boolean(submitterId) && submitterId === userId
  }
  return false
}
