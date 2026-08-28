# 模块注册表（Module Registry）

> **主导窗口 M1** 维护。状态：`pending` | `in_progress` | `review` | `done` | `blocked`  
> 环内：斥候 → 主力 → 搜剿；同一卡点最多 4 次。**只有 M1 能标 done。**  
> 子窗口禁止打开游戏 / 禁止另开浏览器测管理端。  
> Phase 10 已查收：改密 API + Unity 登录会话。见 `docs/FIX-PLAN.md` 卡点 Q/R。本机 Go **8080**。

## 总览

| 编号 | 模块名 | 负责窗口 | 状态 | 依赖 |
|------|--------|----------|------|------|
| M1 | 主导 / 架构协调 | **本窗口** | in_progress | — |
| M3 | 三端骨架 | 窗口 3 | done | M1 |
| M5 | Go 后端 | 窗口 5 | done | Phase10 卡点 Q |
| M4 | React 管理端 | 窗口 4 | done | Phase5 卡点 I |
| M6 | Unity AR | 窗口 6 | done | Phase10 卡点 R |
| M8 | 集成查收 | **M1** | in_progress | Phase10 已同步 inspect-ar；真机待 Build And Run |

## 目录边界

```
backend/    仅 M3 骨架 + M5 业务
admin/       仅 M3 脚手架 + M4
mobile/      仅 M3 Unity 工程 + M6
docs/        仅 M1（含查收）
```

## M1 — 主导 / 架构协调

**路径**：`docs/`、根目录 `README.md`、`AI_LOG.md`

**交付物**：
- [x] `docs/MODULE-REGISTRY.md`
- [x] `docs/API.md`
- [x] `docs/TOOL-PATHS.md`
- [x] `docs/RECEIPT-LOG.md`
- [x] README / AI_LOG 初稿
- [x] M3 查收通过后标 M3 `done`
- [x] Phase 4 契约（登录/权限/DELETE/提交人、多点标注、管理端列表、Unity UI 色号）
- [x] Phase 4 子窗口查收（M6 真机仍欠出包）
- [x] Phase 5 契约（PUT、/ws、三行坐标、任务 List、主次按钮）
- [x] Phase 5 子窗口查收（M6 真机仍欠出包）
- [x] Phase 6 契约（中央透明、底栏、Toast）
- [x] Phase 6 M6 查收 + 同步 inspect-ar

**验收**：文档与 API 表可供子窗口照做；不实现 `backend`/`admin`/`mobile` 业务。

**状态**：`in_progress`

---

## M3 — 三端骨架

**路径**：`backend/`（仅空工程：`go.mod`、可编译的 `cmd/server` 占位）、`admin/`（Vite + React 19 + TS 5.5 空页）、`mobile/`（Unity 6000.3.23f1 工程 + AR Foundation / ARCore 包清单，可打开）

**交付物**：骨架已于前期 `done`。本环不再改。

**状态**：`done`

---

## M5 — Go 后端

**路径**：仅 `backend/`（不要改 `admin/`、`mobile/`）

**交付物（历史，已完成）**：
- [x] `GET /health`、`POST/GET /api/issues`、`PATCH /api/issues/:id`
- [x] SQLite 持久化；CORS 本机任意端口；Phase3 字段确认

**交付物（Phase 4，已归档）**：
- [x] `users` 表：`id`、`username`、bcrypt `password`、`role`（`admin`/`inspector`/`viewer`）
- [x] 空表种子：`admin/admin123`、`inspector/inspect123`、`viewer/view123`
- [x] `POST /api/auth/login` → `{ token, user: { id, username, role } }`
- [x] JWT 中间件：除 health/login 外 `/api/*` 必须 Bearer；无效 401
- [x] `GET /api/issues` 任意已登录；列表含 `submitterId`/`submitterName`
- [x] `POST /api/issues` 仅 admin/inspector；提交人从 Token 写入，忽略客户端伪造
- [x] `PATCH` 改状态仅 admin，否则 403
- [x] `DELETE /api/issues/:id`：admin 或提交人本人，成功 204
- [x] CORS：`DELETE` + `Authorization`；预检在鉴权前放行
- [x] 旧 issues 行迁移：`submitter_name` 缺省 `"未知"`

**交付物（Phase 5，本环）**：
- [x] 斥候确认 `pos_x`/`pos_y`/`pos_z` 已独立；已有则禁止重建表
- [x] `PUT /api/issues/:id`：admin 或本人改 title/description/priority；坐标与 status 不变
- [x] CORS 方法含 `PUT`
- [x] `GET /ws?token=` + gorilla Hub；非法 token 401
- [x] POST 201 → 广播 `issue.created`（完整 Issue）
- [x] PUT/PATCH → `issue.updated`；DELETE → `issue.deleted`
- [x] 两次不同 xyz 的 POST，GET 互不覆盖

**交付物（Phase 10，本环）**：
- [x] `POST /api/auth/password`：Bearer 必填；Body `oldPassword`/`newPassword`
- [x] 旧密码错 → 400 `invalid old password`（禁止 401）
- [x] 新密码 &lt;6 / 与旧相同 → 400；成功 200 `{ "ok": true }`，JWT 仍有效
- [x] `GetUserByID` + `UpdatePassword`；不重建 users 表
- [x] curl 自测后 **inspector 改回 inspect123**

允许本环文件：`internal/api/api.go`、`internal/store/users.go`（可加小测试）。不要改 `admin/`、`mobile/`。

**验收**：见 `docs/FIX-PLAN.md` 卡点 Q。打 **8080**。

**环内角色**：斥候 → 主力 → 搜剿；一环即可。

**开工话术**：见 `docs/FIX-PLAN.md` Phase 10「M5 — 改密接口」

**状态**：`done`

---

## M4 — React 管理端

**路径**：仅 `admin/`

**交付物（历史）**：
- [x] 列表标题/优先级/状态；改状态；错误提示；开发默认 8081

**交付物（Phase 4，已归档）**：
- [x] 登录页；`localStorage` 键 `inspect.token`、`inspect.user`
- [x] 所有 API 请求带 `Authorization: Bearer`
- [x] 401 清 Token 回登录
- [x] 列表展示提交人姓名、提交时间、描述（过长截断 + `…`）
- [x] 顶部：状态筛选、优先级筛选、提交人模糊搜索
- [x] 「查看详情」弹窗：完整描述、xyz、提交人完整信息
- [x] 「删除」确认 → `DELETE /api/issues/{id}` → 刷新
- [x] 按 `role` 隐藏改状态 / 删除（见 FIX-PLAN 卡点 F）
- [x] `npm run build` 通过
- [x] 退出 / 401 清会话时重置状态、优先级、提交人筛选

**交付物（Phase 5，本环）**：
- [x] `.env.development`：`VITE_API_BASE=http://127.0.0.1:8080`
- [x] 详情弹窗三行：`X 坐标` / `Y 坐标` / `Z 坐标`（禁止挤成一行）
- [x] `useIssuesSocket`：登录后连 `/ws?token=`
- [x] `issue.created` unshift 最前，不覆盖整表
- [x] `issue.updated` 按 id 替换；`issue.deleted` 移除
- [x] 断线指数退避重连（上限 30s）；登出关闭
- [x] `npm run build` 通过

**验收**：构建通过；对照 API.md。真浏览器由 M1 查收。禁止本窗口另开浏览器。

**开工话术**：见 `docs/FIX-PLAN.md` Phase 5「M4 — Phase5」

**状态**：`done`

---

## M6 — Unity AR 移动端

**路径**：仅 `mobile/`

**交付物（历史）**：
- [x] 水平地面过滤、ARAnchor、表单上报、扫描开关、优先级颜色、列表轮询变灰

**交付物（Phase 4）**：
- [x] 默认暂停；开始扫描立即激活 Plane/Raycast；1s 快速窗 `MinFloorArea=0.08`
- [x] 放置后确认框；确认则暂停、锁定标记、清空射线
- [x] 可再次扫描放置第 N 个标记，坐标彼此独立
- [x] 提交最近已确认未提交标记；成功后该标记保留，可继续标下一个
- [x] 登录面板；`PlayerPrefs` `inspect.jwt`；请求带 Authorization
- [x] UGUI 莫兰迪毛玻璃、圆角≥10、Fade、非粗体；色号见 FIX-PLAN 卡点 G
- [x] 保留 `ColorForIssue` / `ApplyMarkerColor`
- [x] `StyleFadeButton` 使用 `Selectable.Transition.ColorTint`（不要 `Fade`）

**交付物（Phase 5，已归档）**：
- [x] 浅灰蓝/暖灰底，禁止纯白铺满；Primary / Secondary / Danger 三套按钮
- [x] `StylePrimaryButton` / `StyleSecondaryButton` / `StyleDangerButton`；间距≥16；卡片分割
- [x] 「新建任务」后才进入扫描放置
- [x] `List<DraftMarker>`：每点独立 `position` xyz，可删、可选中改标题/描述/优先级（仅内存）
- [x] 「提交任务」确认框（数量+摘要）→ 确认发送才逐条 POST；取消继续改
- [x] 已提交锁定；未提交退出不持久化
- [x] 「历史记录」ScrollView：GET 列表；展开描述 + X/Y/Z 三行；编辑后 PUT
- [x] 401 / 无 Token 回登录；保留 ColorTint、地面过滤、优先级立方体色

**交付物（Phase 6，已归档）**：
- [x] 新建任务后中央 AR 窗（约 y=0.22～0.92）无全宽不透明面板；可见高度 ≥60%
- [x] 列表/编辑：右侧抽屉或底部 sheet，`Image.a ≤ 0.55`，默认收起
- [x] 顶栏：用户名 + 任务状态
- [x] 底栏两行：新建任务、开始/暂停扫描、历史、标记、提交
- [x] Toast：`SetStatus` 2～3 秒消失，不挡底栏
- [x] 竖屏 1080×1920 match=1；横屏 1920×1080 match=0；避开 safeArea
- [x] 保留 DraftMarker / POST / 历史 PUT / ColorTint / 地面过滤

**交付物（Phase 7，已归档）**：
- [x] 地面稳定约 1s 后锁定；活 visualizer 关闭；静态蓝网格
- [x] 锁定后不换最低平面；再次「开始扫描」才解锁
- [x] 立方体仍为 Anchor 子物体，不每帧改世界坐标
- [x] 标题含漏水/裂缝/冒烟/异响/脱落 → priority=high 且禁用优先级按钮
- [x] 描述不覆盖，只追加 `【系统提示】请尽快安排处理`（已有则不重复）
- [x] 关键词删光后按钮可改，不恢复旧优先级

**验收**：代码对照卡点 L/M 已过。禁止打开游戏 / 出包。真机仍待 inspect-ar Build And Run。

**交付物（Phase 8，已归档）**：
- [x] XR Origin 有 ARAnchorManager（运行时补 + Verify）；不新建第二套 Session/Origin
- [x] 扫描中 DetectionMode 保持 Horizontal；锁地后禁止 None；暂停才关 Plane/Raycast
- [x] 放宽出蓝：法线 ≥0.92、快扫面积 0.04 / 8s、正常 0.08；最低平面滞回
- [x] 放置用 `TryAddAnchorAsync`，禁止 `AttachAnchor`；立方体为世界锚子物体
- [x] 看到蓝即可点放（不要 `m_FloorLocked` 门闩）；射线命中合格水平面即可
- [x] 底栏/玻璃不吞 AR 点击；失败 Toast 写明原因；不要自动 `ARSession.Reset`

**验收**：代码对照卡点 N 已过。禁止打开游戏 / 出包。真机仍待 inspect-ar Build And Run。

**交付物（Phase 9，已归档）**：
- [x] 低纹理：法线 0.85、面积 0.01、Limited 也显示；射线 PlaneWithinPolygon → Estimated → FeaturePoint → Depth
- [ ] ARCoreSettings YAML 仍 `m_Requirement=0` / `m_Depth=1`（残留；`EnsureArCoreSettings` 代码已写对）
- [x] 扫描中粒子点云 + 特征点数量（&lt;15 引导去扫砖缝/脚印/工具）
- [x] 四种射线都失败时点击仍放置：1.5m 临时水平蓝网格 + TryAddAnchorAsync
- [x] 不要自动 ARSession.Reset

**交付物（Phase 10，本环）**：
- [x] `InspectAuthSession`：jwt 存取、`AttachAuth`、`Clear`（保留 URL）、读 JWT `exp`
- [x] 启动不自动登录；无 Token / 过期 / 401 → 登录卡 +「登录已过期，请重新登录。」
- [x] 登录卡居中、半透明（遮罩 a≈0.18，卡 a≤0.50），可见摄像头；用户名不写死 inspector
- [x] 顶栏右上角小尺寸用户芯片（Button）；点开下拉：修改密码、退出登录
- [x] 退出：清 Token，回登录卡，可换账号
- [x] 改密弹层：`POST /api/auth/password`；错旧密码不登出；401 才登出
- [x] 登录后所有 `/api/*` 带 Bearer；保留扫描/锚点/点云/临时平面/ColorTint

**验收**：代码对照卡点 R。禁止打开游戏 / 出包。

**开工话术**：见 `docs/FIX-PLAN.md` Phase 10「M6 — Unity 登录会话」

**状态**：`done`

---

## M8 — 集成（仅 M1）

**路径**：跨模块配置、`README.md` 启动与真机地址说明；不代替子窗口改业务。

**验收**：登录 → 手机多点任务确认发送（带 Token，每点独立 xyz）→ React 无需手动刷新即出现新问题 → 详情三行坐标 → 历史 PUT / admin 改状态。端口 **8080** + 5174。

**状态**：`in_progress`（Phase10 源码已同步 inspect-ar；真机待出包）

---

## 子窗口开工话术（Phase 10，复制到新 Cursor 窗口）

工作区路径：`D:\Cursor_projectt\测试考题`

完整话术以 **`docs/FIX-PLAN.md` Phase 10** 为准。本环开 **M5** 与 **M6**（可同时）。

完工说：`M5 已完成，请主导窗口查收。` 或 `M6 已完成，请主导窗口查收。`


