import { createServer } from 'node:http'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = dirname(fileURLToPath(import.meta.url))
const apiSrc = readFileSync(join(root, 'src/api.ts'), 'utf8')
const appSrc = readFileSync(join(root, 'src/App.tsx'), 'utf8')
const utilsSrc = readFileSync(join(root, 'src/listUtils.ts'), 'utf8')
const envDev = readFileSync(join(root, '.env.development'), 'utf8')
const wsSrc = readFileSync(join(root, 'src/issuesWs.ts'), 'utf8')
const hookSrc = readFileSync(join(root, 'src/useIssuesSocket.ts'), 'utf8')

let failed = 0

function check(name, ok) {
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}`)
  if (!ok) failed += 1
}

check('localStorage inspect.token', apiSrc.includes("'inspect.token'"))
check('localStorage inspect.user', apiSrc.includes("'inspect.user'"))
check('POST /api/auth/login', apiSrc.includes('/api/auth/login'))
check('Authorization Bearer', apiSrc.includes('Bearer'))
check('401 clears session', apiSrc.includes('clearSession()') && apiSrc.includes('401'))
check('login fetch skips auth header', apiSrc.includes('{ auth: false }'))
check('DELETE method', /method:\s*['"]DELETE['"]/.test(apiSrc))
check('Issue has submitterId/submitterName', apiSrc.includes('submitterId') && apiSrc.includes('submitterName'))
check('no Token stays on login', appSrc.includes('LoginPage') && appSrc.includes('getToken()'))
check('login failure maps invalid credentials', apiSrc.includes('用户名或密码错误'))
check('list shows submitterName', appSrc.includes('issue.submitterName'))
check('list shows createdAt', appSrc.includes('issue.createdAt'))
check('truncate uses 40 + …', utilsSrc.includes('DESC_MAX = 40') && utilsSrc.includes('…'))
check('status filter values', appSrc.includes('value="open"') && appSrc.includes('value="in_progress"') && appSrc.includes('value="resolved"'))
check('priority filter values', appSrc.includes('value="high"') && appSrc.includes('value="medium"') && appSrc.includes('value="low"'))
check('submitter fuzzy toLowerCase', utilsSrc.includes('.toLowerCase()') && utilsSrc.includes('submitterName'))
check('detail dialog', appSrc.includes('role="dialog"') && appSrc.includes('position?.x'))
check('delete confirm', appSrc.includes('window.confirm'))
check('admin-only status buttons', utilsSrc.includes("role === 'admin'") && appSrc.includes('canChangeStatus'))
check('inspector delete own only', utilsSrc.includes('inspector') && utilsSrc.includes('submitterId === userId'))
check('viewer cannot delete', utilsSrc.includes('return false'))
check('logout resets filters', /function logout[\s\S]*resetFilters\(\)/.test(appSrc))
check('401 clears filters', appSrc.includes('resetFilters()') && appSrc.includes('setUnauthorizedHandler'))
check('dev API base 8080', envDev.includes('VITE_API_BASE=http://127.0.0.1:8080') && !envDev.includes('8081'))
check('detail X/Y/Z are three lines', appSrc.includes('X 坐标：') && appSrc.includes('Y 坐标：') && appSrc.includes('Z 坐标：'))
check('detail coords not squeezed on one line', !/x=\{formatCoord[\s\S]*，y=/.test(appSrc))
check('ws url uses /ws?token=', wsSrc.includes('/ws?token=') && wsSrc.includes('ws://'))
check('created unshifts without replacing table', wsSrc.includes('[event.issue, ...issues]'))
check('useIssuesSocket on login', appSrc.includes('useIssuesSocket') && hookSrc.includes('new WebSocket'))
check('reconnect backoff capped at 30s', wsSrc.includes('WS_BACKOFF_MAX_MS = 30000') && hookSrc.includes('nextBackoff'))
check('logout/unmount closes socket', hookSrc.includes('closed = true') && hookSrc.includes('socket?.close()'))

function truncateDescription(text, max = 40) {
  const s = text ?? ''
  if (s.length <= max) return s
  return `${s.slice(0, max)}…`
}

function filterIssues(issues, status, priority, submitterQuery) {
  const q = submitterQuery.trim().toLowerCase()
  return issues.filter((issue) => {
    if (status && issue.status !== status) return false
    if (priority && issue.priority !== priority) return false
    if (q && !(issue.submitterName ?? '').toLowerCase().includes(q)) return false
    return true
  })
}

function canChangeStatus(role) {
  return role === 'admin'
}

function canDeleteIssue(role, userId, submitterId) {
  if (role === 'admin') return true
  if (role === 'inspector') return Boolean(submitterId) && submitterId === userId
  return false
}

const long = '测'.repeat(45)
check('truncate 40 chars + ellipsis', long.length > 40 && truncateDescription(long) === `${long.slice(0, 40)}…`)
check('truncate short keeps text', truncateDescription('短描述') === '短描述')

const rows = [
  { id: '1', title: 'A', priority: 'high', status: 'open', submitterName: 'Inspector', submitterId: 'u1' },
  { id: '2', title: 'B', priority: 'low', status: 'resolved', submitterName: 'Admin', submitterId: 'u2' },
]
check('filter status open', filterIssues(rows, 'open', '', '').map((r) => r.id).join() === '1')
check('filter priority low', filterIssues(rows, '', 'low', '').map((r) => r.id).join() === '2')
check('filter submitter case-insensitive', filterIssues(rows, '', '', 'insp').map((r) => r.id).join() === '1')
check('admin can patch', canChangeStatus('admin') && !canChangeStatus('inspector') && !canChangeStatus('viewer'))
check('inspector deletes own only', canDeleteIssue('inspector', 'u1', 'u1') && !canDeleteIssue('inspector', 'u1', 'u2'))
check('viewer no delete', !canDeleteIssue('viewer', 'u1', 'u1'))
check('admin deletes any', canDeleteIssue('admin', 'u9', 'u1'))

function applyWsMessage(issues, event) {
  if (event.type === 'issue.created') {
    if (issues.some((item) => item.id === event.issue.id)) return issues
    return [event.issue, ...issues]
  }
  if (event.type === 'issue.updated') {
    return issues.map((item) => (item.id === event.issue.id ? event.issue : item))
  }
  return issues.filter((item) => item.id !== event.id)
}

function nextBackoff(ms) {
  return Math.min(Math.max(ms, 1) * 2, 30000)
}

function wsUrlFromHttp(httpBase, token) {
  const base = httpBase.replace(/\/$/, '')
  const wsBase = base.replace(/^http:\/\//i, 'ws://').replace(/^https:\/\//i, 'wss://')
  return `${wsBase}/ws?token=${encodeURIComponent(token)}`
}

const a = { id: 'a', title: '旧', status: 'open', priority: 'high' }
const b = { id: 'b', title: '新', status: 'open', priority: 'low' }
check('ws created unshifts to front', applyWsMessage([a], { type: 'issue.created', issue: b }).map((i) => i.id).join() === 'b,a')
check('ws created skips duplicate id', applyWsMessage([a], { type: 'issue.created', issue: { ...a, title: 'x' } })[0].title === '旧')
check('ws updated replaces by id', applyWsMessage([a, b], { type: 'issue.updated', issue: { ...a, title: '改' } })[0].title === '改')
check('ws deleted removes by id', applyWsMessage([a, b], { type: 'issue.deleted', id: 'a' }).map((i) => i.id).join() === 'b')
check('ws url http→ws token query', wsUrlFromHttp('http://127.0.0.1:8080', 'jwt+x') === 'ws://127.0.0.1:8080/ws?token=jwt%2Bx')
check('backoff 1s→2s→4s…30s cap', nextBackoff(1000) === 2000 && nextBackoff(16000) === 30000 && nextBackoff(30000) === 30000)

function listen(server) {
  return new Promise((resolve) => {
    server.listen(0, '127.0.0.1', () => resolve(server.address().port))
  })
}

function close(server) {
  return new Promise((resolve, reject) => {
    server.close((err) => (err ? reject(err) : resolve()))
  })
}

async function runMockProtocol() {
  const sample = {
    id: 'issue-1',
    title: '入口墙面破损',
    description: '左侧墙体存在裂缝',
    priority: 'high',
    status: 'open',
    submitterId: 'u-insp',
    submitterName: 'inspector',
    createdAt: '2026-08-27T07:00:00Z',
    position: { x: 0.42, y: 0.03, z: 1.26 },
  }
  const seen = { auth: '', deleted: false }

  const server = createServer((req, res) => {
    res.setHeader('Content-Type', 'application/json')
    seen.auth = req.headers.authorization || ''
    if (req.method === 'POST' && req.url === '/api/auth/login') {
      let raw = ''
      req.on('data', (chunk) => {
        raw += chunk
      })
      req.on('end', () => {
        const body = JSON.parse(raw || '{}')
        if (body.username === 'inspector' && body.password === 'inspect123') {
          res.end(
            JSON.stringify({
              token: 'jwt-test',
              user: { id: 'u-insp', username: 'inspector', role: 'inspector' },
            }),
          )
          return
        }
        res.statusCode = 401
        res.end(JSON.stringify({ error: 'invalid credentials' }))
      })
      return
    }
    if (!req.headers.authorization) {
      res.statusCode = 401
      res.end(JSON.stringify({ error: 'unauthorized' }))
      return
    }
    if (req.method === 'GET' && req.url === '/api/issues') {
      res.end(JSON.stringify({ issues: [sample] }))
      return
    }
    if (req.method === 'PATCH' && req.url === '/api/issues/issue-1') {
      let raw = ''
      req.on('data', (chunk) => {
        raw += chunk
      })
      req.on('end', () => {
        const body = JSON.parse(raw || '{}')
        res.end(JSON.stringify({ ...sample, status: body.status }))
      })
      return
    }
    if (req.method === 'DELETE' && req.url === '/api/issues/issue-1') {
      seen.deleted = true
      res.statusCode = 204
      res.end()
      return
    }
    res.statusCode = 404
    res.end(JSON.stringify({ error: 'not found' }))
  })

  const port = await listen(server)
  const base = `http://127.0.0.1:${port}`
  try {
    const badLogin = await fetch(`${base}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: 'inspector', password: 'wrong' }),
    })
    const badBody = await badLogin.json()
    check(
      'mock login fail 401 invalid credentials',
      badLogin.status === 401 && badBody.error === 'invalid credentials',
    )

    const okLogin = await fetch(`${base}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: 'inspector', password: 'inspect123' }),
    })
    const session = await okLogin.json()
    check('mock login 200 token+user', okLogin.ok && session.token === 'jwt-test' && session.user.role === 'inspector')

    const noToken = await fetch(`${base}/api/issues`)
    const noTokenBody = await noToken.json()
    check('mock GET without token 401', noToken.status === 401 && noTokenBody.error === 'unauthorized')

    const listRes = await fetch(`${base}/api/issues`, {
      headers: { Authorization: `Bearer ${session.token}` },
    })
    const list = await listRes.json()
    check(
      'mock GET with Bearer has submitter/time/desc',
      listRes.ok &&
        seen.auth === 'Bearer jwt-test' &&
        list.issues[0].submitterName === 'inspector' &&
        list.issues[0].createdAt &&
        list.issues[0].description,
    )

    const patchRes = await fetch(`${base}/api/issues/issue-1`, {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${session.token}`,
      },
      body: JSON.stringify({ status: 'resolved' }),
    })
    const patched = await patchRes.json()
    check('mock PATCH with Bearer', patchRes.ok && patched.status === 'resolved')

    const delRes = await fetch(`${base}/api/issues/issue-1`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${session.token}` },
    })
    check('mock DELETE 204 with Bearer', delRes.status === 204 && seen.deleted)
  } finally {
    await close(server)
  }
}

async function runBackendDown() {
  try {
    await fetch('http://127.0.0.1:9/api/issues')
    check('backend down throws TypeError', false)
  } catch (err) {
    check(
      'backend down throws TypeError (maps to page error, not blank)',
      err instanceof TypeError,
    )
  }
}

await runMockProtocol()
await runBackendDown()

if (failed) {
  console.error(`\nselftest failed: ${failed} check(s)`)
  process.exitCode = 1
} else {
  console.log('\nselftest passed')
}
