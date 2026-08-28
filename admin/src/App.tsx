import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import {
  clearSession,
  deleteIssue,
  fetchIssues,
  getToken,
  getUser,
  login,
  networkErrorMessage,
  patchIssueStatus,
  setUnauthorizedHandler,
  type AuthUser,
  type Issue,
  type IssuePriority,
  type IssueStatus,
} from './api.ts'
import {
  canChangeStatus,
  canDeleteIssue,
  filterIssues,
  formatCoord,
  formatDateTime,
  truncateDescription,
} from './listUtils.ts'
import { applyWsMessage, type WsEvent } from './issuesWs.ts'
import { useIssuesSocket } from './useIssuesSocket.ts'

const STATUSES: IssueStatus[] = ['open', 'in_progress', 'resolved']

const STATUS_LABEL: Record<IssueStatus, string> = {
  open: '待处理',
  in_progress: '进行中',
  resolved: '已解决',
}

const PRIORITY_LABEL: Record<IssuePriority, string> = {
  high: '高',
  medium: '中',
  low: '低',
}

const ROLE_LABEL: Record<AuthUser['role'], string> = {
  admin: '管理员',
  inspector: '巡检员',
  viewer: '只读',
}

function LoginPage({
  notice,
  onLoggedIn,
}: {
  notice: string | null
  onLoggedIn: (user: AuthUser) => void
}) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(notice)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    setError(notice)
  }, [notice])

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const { user } = await login(username.trim(), password)
      onLoggedIn(user)
    } catch (err) {
      setError(networkErrorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="login-main">
      <h1>现场巡检管理端</h1>
      <p className="muted">请登录后查看巡检列表</p>
      <form className="login-form" onSubmit={(e) => void onSubmit(e)}>
        {error ? (
          <p className="banner error" role="alert">
            {error}
          </p>
        ) : null}
        <label>
          用户名
          <input
            name="username"
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            required
          />
        </label>
        <label>
          密码
          <input
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </label>
        <button type="submit" disabled={busy || !username.trim()}>
          {busy ? '登录中…' : '登录'}
        </button>
      </form>
    </main>
  )
}

function App() {
  const [user, setUser] = useState<AuthUser | null>(() =>
    getToken() ? getUser() : null,
  )
  const [authNotice, setAuthNotice] = useState<string | null>(null)
  const [issues, setIssues] = useState<Issue[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [busyId, setBusyId] = useState<string | null>(null)
  const [statusFilter, setStatusFilter] = useState<'' | IssueStatus>('')
  const [priorityFilter, setPriorityFilter] = useState<'' | IssuePriority>('')
  const [submitterQuery, setSubmitterQuery] = useState('')
  const [detail, setDetail] = useState<Issue | null>(null)

  const resetFilters = useCallback(() => {
    setStatusFilter('')
    setPriorityFilter('')
    setSubmitterQuery('')
  }, [])

  useEffect(() => {
    setUnauthorizedHandler(() => {
      setUser(null)
      setIssues([])
      setDetail(null)
      resetFilters()
      setAuthNotice('登录已过期，请重新登录')
    })
    return () => setUnauthorizedHandler(null)
  }, [resetFilters])

  const load = useCallback(async () => {
    if (!getToken() || !user) return
    setLoading(true)
    setError(null)
    try {
      setIssues(await fetchIssues())
    } catch (err) {
      if (!getToken()) return
      setIssues([])
      setError(networkErrorMessage(err))
    } finally {
      setLoading(false)
    }
  }, [user])

  useEffect(() => {
    if (user && getToken()) {
      void load()
    }
  }, [user, load])

  const token = user ? getToken() : null

  const onWsEvent = useCallback((event: WsEvent) => {
    setIssues((prev) => applyWsMessage(prev, event))
    setDetail((cur) => {
      if (!cur) return cur
      if (event.type === 'issue.deleted' && event.id === cur.id) return null
      if (
        (event.type === 'issue.updated' || event.type === 'issue.created') &&
        event.issue.id === cur.id
      ) {
        return event.issue
      }
      return cur
    })
  }, [])

  useIssuesSocket(Boolean(user && token), token, onWsEvent)

  const visible = useMemo(
    () => filterIssues(issues, statusFilter, priorityFilter, submitterQuery),
    [issues, statusFilter, priorityFilter, submitterQuery],
  )

  async function changeStatus(id: string, status: IssueStatus) {
    setBusyId(id)
    setError(null)
    try {
      const updated = await patchIssueStatus(id, status)
      setIssues((prev) =>
        prev.map((item) => (item.id === id ? { ...item, ...updated } : item)),
      )
    } catch (err) {
      if (!getToken()) return
      setError(networkErrorMessage(err))
    } finally {
      setBusyId(null)
    }
  }

  async function onDelete(issue: Issue) {
    if (!window.confirm(`确定删除「${issue.title}」？`)) return
    setBusyId(issue.id)
    setError(null)
    try {
      await deleteIssue(issue.id)
      setDetail((cur) => (cur?.id === issue.id ? null : cur))
      await load()
    } catch (err) {
      if (!getToken()) return
      setError(networkErrorMessage(err))
    } finally {
      setBusyId(null)
    }
  }

  function logout() {
    clearSession()
    setUser(null)
    setIssues([])
    setDetail(null)
    resetFilters()
    setAuthNotice(null)
    setError(null)
  }

  if (!user || !getToken()) {
    return (
      <LoginPage
        notice={authNotice}
        onLoggedIn={(next) => {
          setAuthNotice(null)
          setUser(next)
        }}
      />
    )
  }

  return (
    <main>
      <header className="page-head">
        <div>
          <h1>现场巡检管理端</h1>
          <p className="muted user-meta">
            {user.username}（{ROLE_LABEL[user.role]}）
          </p>
        </div>
        <div className="head-actions">
          <button type="button" onClick={() => void load()} disabled={loading}>
            {loading ? '加载中…' : '刷新'}
          </button>
          <button type="button" onClick={logout}>
            退出
          </button>
        </div>
      </header>

      <div className="filters">
        <label>
          状态
          <select
            value={statusFilter}
            onChange={(e) =>
              setStatusFilter(e.target.value as '' | IssueStatus)
            }
          >
            <option value="">全部</option>
            <option value="open">待处理</option>
            <option value="in_progress">进行中</option>
            <option value="resolved">已解决</option>
          </select>
        </label>
        <label>
          优先级
          <select
            value={priorityFilter}
            onChange={(e) =>
              setPriorityFilter(e.target.value as '' | IssuePriority)
            }
          >
            <option value="">全部</option>
            <option value="high">高</option>
            <option value="medium">中</option>
            <option value="low">低</option>
          </select>
        </label>
        <label className="grow">
          提交人
          <input
            value={submitterQuery}
            onChange={(e) => setSubmitterQuery(e.target.value)}
            placeholder="模糊搜索姓名"
          />
        </label>
      </div>

      {error ? (
        <p className="banner error" role="alert">
          {error}
        </p>
      ) : null}

      {loading && issues.length === 0 && !error ? (
        <p className="muted">正在拉取巡检列表…</p>
      ) : null}

      {!loading && !error && issues.length === 0 ? (
        <p className="muted">暂无巡检记录</p>
      ) : null}

      {!loading && issues.length > 0 && visible.length === 0 ? (
        <p className="muted">没有符合筛选的记录</p>
      ) : null}

      {visible.length > 0 ? (
        <table>
          <thead>
            <tr>
              <th>标题</th>
              <th>优先级</th>
              <th>状态</th>
              <th>提交人</th>
              <th>提交时间</th>
              <th>描述</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            {visible.map((issue) => (
              <tr key={issue.id}>
                <td>{issue.title}</td>
                <td>{PRIORITY_LABEL[issue.priority] ?? issue.priority}</td>
                <td>{STATUS_LABEL[issue.status] ?? issue.status}</td>
                <td>{issue.submitterName || '未知'}</td>
                <td>{formatDateTime(issue.createdAt)}</td>
                <td className="desc-cell">
                  {truncateDescription(issue.description)}
                </td>
                <td>
                  <div className="status-actions">
                    <button type="button" onClick={() => setDetail(issue)}>
                      查看详情
                    </button>
                    {canDeleteIssue(user.role, user.id, issue.submitterId) ? (
                      <button
                        type="button"
                        disabled={busyId === issue.id}
                        onClick={() => void onDelete(issue)}
                      >
                        删除
                      </button>
                    ) : null}
                    {canChangeStatus(user.role)
                      ? STATUSES.map((status) => (
                          <button
                            key={status}
                            type="button"
                            disabled={
                              busyId === issue.id || issue.status === status
                            }
                            onClick={() => void changeStatus(issue.id, status)}
                          >
                            {STATUS_LABEL[status]}
                          </button>
                        ))
                      : null}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}

      {detail ? (
        <div
          className="modal-backdrop"
          role="presentation"
          onClick={() => setDetail(null)}
        >
          <div
            className="modal"
            role="dialog"
            aria-labelledby="issue-detail-title"
            onClick={(e) => e.stopPropagation()}
          >
            <h2 id="issue-detail-title">问题详情</h2>
            <dl>
              <dt>标题</dt>
              <dd>{detail.title}</dd>
              <dt>描述</dt>
              <dd className="prewrap">{detail.description || '—'}</dd>
              <dt>优先级</dt>
              <dd>{PRIORITY_LABEL[detail.priority] ?? detail.priority}</dd>
              <dt>状态</dt>
              <dd>{STATUS_LABEL[detail.status] ?? detail.status}</dd>
              <dt>坐标</dt>
              <dd className="coord-lines">
                <div>X 坐标：{formatCoord(detail.position?.x)}</div>
                <div>Y 坐标：{formatCoord(detail.position?.y)}</div>
                <div>Z 坐标：{formatCoord(detail.position?.z)}</div>
              </dd>
              <dt>提交人</dt>
              <dd>
                {detail.submitterName || '未知'}（id:{' '}
                {detail.submitterId || '—'}）
              </dd>
              <dt>提交时间</dt>
              <dd>{formatDateTime(detail.createdAt)}</dd>
              <dt>更新时间</dt>
              <dd>{formatDateTime(detail.updatedAt)}</dd>
            </dl>
            <button type="button" onClick={() => setDetail(null)}>
              关闭
            </button>
          </div>
        </div>
      ) : null}
    </main>
  )
}

export default App
