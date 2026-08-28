# API 约定（三端共用）

后端监听 **`0.0.0.0:8080`**（关机后本机 8080 已空闲，Phase 5 起不要再默认 8081；手机不能用 `localhost`）。  
数据文件：`backend/data/issues.db`  
时间字段 ISO-8601 UTC（例：`2026-08-27T07:00:00Z`）。

> Phase 4 起：**除 `GET /health` 与 `POST /api/auth/login` 外，所有 `/api/*` 必须带 JWT。**  
> Phase 5 起：WebSocket **`GET /ws?token=`** 用查询参数带同一 JWT（浏览器无法给 WS 设 Authorization 头）。  
> M4 / M6 对照本文实现，禁止自造字段名。

## 角色

| role | 含义 | 允许 | 禁止 |
|------|------|------|------|
| `admin` | 管理员 | 查看全部；改任意状态；删任意问题；提交新问题 | — |
| `inspector` | 巡检员 | 查看列表；提交新问题；删除**自己提交的**问题 | 改任何人的状态（含自己的）→ **403**；删别人的 → **403** |
| `viewer` | 只读 | 查看列表 | POST / PATCH / DELETE → **403** |

## 鉴权

请求头：

```http
Authorization: Bearer <jwt>
```

JWT（HS256）payload 至少含：

```json
{
  "sub": "用户id",
  "username": "alice",
  "role": "admin",
  "exp": 1770000000
}
```

密钥：环境变量 `JWT_SECRET`；未设置时用开发默认值（只允许本机，须写在启动日志里提醒）。有效期 **24h**。

未带 Token / Token 无效 / 过期 → **401** `{ "error": "unauthorized" }`。  
有 Token 但角色不够 → **403** `{ "error": "forbidden" }`。

中间件范围：拦截全部 `/api/`（登录除外）。`GET /health` 不经过鉴权。CORS 预检 `OPTIONS` 在鉴权之前放行。

CORS 允许方法：`GET, POST, PATCH, PUT, DELETE, OPTIONS`。  
允许头：`Content-Type, Authorization`。  
来源：任意 `http://localhost:*` 与 `http://127.0.0.1:*`（保持 Phase2）。  
WebSocket `CheckOrigin` 与上述来源规则相同。

## 种子用户（空 users 表时写入）

密码用 bcrypt 存储，禁止明文入库。

| username | password | role |
|----------|----------|------|
| `admin` | `admin123` | `admin` |
| `inspector` | `inspect123` | `inspector` |
| `viewer` | `view123` | `viewer` |

## 字段（Issue）

```json
{
  "id": "由后端生成",
  "title": "入口墙面破损",
  "description": "左侧墙体存在裂缝",
  "priority": "high",
  "status": "open",
  "position": { "x": 0.42, "y": 0.03, "z": 1.26 },
  "submitterId": "从 Token sub 写入，客户端不可伪造",
  "submitterName": "从 Token username 写入",
  "createdAt": "由后端生成",
  "updatedAt": "由后端生成"
}
```

| 字段 | 规则 |
|------|------|
| `priority` | `low` \| `medium` \| `high` |
| `status` | `open` \| `in_progress` \| `resolved`；新建默认 `open` |
| `title` | 必填，去空白后非空 |
| `description` | 可选，缺省 `""` |
| `position` | 上报必填；`x`、`y`、`z` **三个独立数字**，库表对应 `pos_x` / `pos_y` / `pos_z`。禁止合成一个字符串，禁止多个标记共用/覆盖同一组坐标 |
| `submitterId` | 创建时从 JWT `sub` 写入；旧数据可为空字符串 |
| `submitterName` | 创建时从 JWT `username` 写入；旧数据缺省 `"未知"` |
| `createdAt` | 即提交时间，列表必须展示 |

`POST` body **不要**接受 `submitterId` / `submitterName` / `status`；以后端 Token 为准。  
`PUT` body **不要**接受 `status` / `position` / `submitterId` / `submitterName`（改状态仍走 PATCH；坐标只在创建时写入）。

**坐标展示（M4 / M6 硬性）**：详情或展开区必须分成三行，例如：

```
X 坐标：0.42
Y 坐标：0.03
Z 坐标：1.26
```

禁止 `x=0.42，y=0.03，z=1.26` 挤在同一行。列表默认可以不显示坐标。

错误响应统一：

```json
{ "error": "title is required" }
```

| HTTP | 何时 |
|------|------|
| 400 | 参数错误 |
| 401 | 未登录 / Token 无效 |
| 403 | 已登录但无权限 |
| 404 | 找不到 ID |
| 500 | 其它 |

## 接口

### `GET /health`（公开）

```json
{ "ok": true }
```

### `POST /api/auth/login`（公开）

Body：`{ "username": "inspector", "password": "inspect123" }`  
成功 **200**：

```json
{
  "token": "<jwt>",
  "user": {
    "id": "…",
    "username": "inspector",
    "role": "inspector"
  }
}
```

用户名/密码错误 → **401** `{ "error": "invalid credentials" }`。  
缺字段 → **400**。

### `POST /api/auth/password`（已登录，改自己的密码）

Phase 10。必须 `Authorization: Bearer <jwt>`。  
Body：

```json
{ "oldPassword": "inspect123", "newPassword": "inspect456" }
```

| 规则 | |
|------|--|
| 无 Token / Token 无效过期 | **401** `{ "error": "unauthorized" }` |
| 缺字段或空 | **400** |
| 新密码长度 &lt; 6 | **400** `{ "error": "password too short" }` |
| 新密码与旧密码相同 | **400** `{ "error": "password unchanged" }` |
| 旧密码不对 | **400** `{ "error": "invalid old password" }`（不要 401） |
| 成功 | **200** `{ "ok": true }`，bcrypt 覆盖库中密码；当前 JWT 仍有效 |

只能改 Token 对应的用户。不要提供改别人密码的接口。

### `GET /api/issues`（任意已登录角色）

返回 `{ "issues": [ ... ] }`，按 `createdAt` 新的在前。  
每条必须含 `submitterId`、`submitterName`、`createdAt`、`description`、`position`。  
本环**不做服务端筛选**；状态/优先级/提交人搜索由 React 客户端过滤。

### `POST /api/issues`（`admin` 或 `inspector`）

Unity 上报。Body：`title`, `description?`, `priority`, `position`（`x`/`y`/`z` 三个独立数字）。  
成功 **201**，返回完整 Issue（含提交人字段），并广播 `issue.created`。  
一条 Issue = 一个标记点；多标记任务由 Unity **连续多次 POST**，不要做嵌套 `markers[]`。  
`viewer` → **403**。

### `PATCH /api/issues/:id`（仅 `admin`）

Body：`{ "status": "in_progress" }`。成功 **200**，返回更新后的 Issue。  
`inspector` / `viewer` 即使带合法 Token 也 **403**（含改自己提交的记录）。  
成功后向 WebSocket 广播 `issue.updated`。

### `PUT /api/issues/:id`（`admin`，或 `inspector` 且该条 `submitterId` 等于当前 `sub`）

Phase 5 新增。用于 Unity 历史记录改标题/优先级。  
Body（至少一项）：

```json
{ "title": "入口墙面破损", "description": "可选", "priority": "high" }
```

| 规则 | |
|------|--|
| `title` | 若出现：去空白后非空，否则 400 |
| `priority` | 若出现：必须是 `low` \| `medium` \| `high` |
| `description` | 若出现：原样保存（可空字符串） |
| 未出现的字段 | 保持原值 |
| `viewer` 或 inspector 改别人的 | **403** |
| 找不到 | **404** |

成功 **200**，返回完整 Issue。成功后广播 `issue.updated`。  
`viewer` → **403**。不要用 PUT 改 `status`。

### `DELETE /api/issues/:id`（`admin`，或 `submitterId` 等于当前用户 `sub`）

成功 **204**，无 body。成功后广播 `issue.deleted`（payload 只含 `id`）。  
找不到 → **404**。  
其它已登录用户 → **403** `{ "error": "forbidden" }`。

### `GET /ws`（WebSocket，已登录）

升级协议。查询参数：

```
GET /ws?token=<jwt>
```

Token 无效/缺失：升级前 **401** JSON `{ "error": "unauthorized" }`（不要留下半开连接）。  
合法后加入 Hub 在线列表；断开时从列表删除。

服务端 → 客户端 JSON 文本帧（字段名固定）：

```json
{ "type": "issue.created", "issue": { "...完整 Issue..." } }
```

```json
{ "type": "issue.updated", "issue": { "...完整 Issue..." } }
```

```json
{ "type": "issue.deleted", "id": "问题id" }
```

依赖：`github.com/gorilla/websocket`。同一进程、同一端口（8080），不要另开端口。  
本机巡检：`ws://127.0.0.1:8080/ws?token=...`；真机网页若以后要用，把 http 基址换成电脑局域网 IP 再把 scheme 换成 `ws`。

## Unity 后端地址

工程内可配置（PlayerPrefs），真机填电脑局域网 IP，例如 `http://192.168.2.14:8080`。

登录成功后把 `token` 存 PlayerPrefs（键名 `inspect.jwt`），用户信息键 `inspect.userId` / `inspect.username` / `inspect.role`。之后所有 `/api/*`（含 `POST /api/auth/password`、`GET/POST/PUT /api/issues`）必须带 `Authorization: Bearer …`。  
401 或本地读到 JWT `exp` 已过：清上述键（保留 `inspect.backendBaseUrl`），回到登录面板，并提示「登录已过期，请重新登录。」  
退出登录：同样清 Token 键并回到登录面板。用户名输入框不要写死 `inspector`。

标记颜色（业务色，UI 面板不使用这些纯色）：

| 条件 | 色 |
|------|----|
| `status` 为 `in_progress` 或 `resolved` | 灰 `(0.55, 0.55, 0.55)` |
| 否则 `low` | 绿 `(0.20, 0.75, 0.30)` |
| 否则 `medium` | 黄 `(0.95, 0.80, 0.15)` |
| 否则 `high`（含默认） | 红 `(0.95, 0.25, 0.20)` |
