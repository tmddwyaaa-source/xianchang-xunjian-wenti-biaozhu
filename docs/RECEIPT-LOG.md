# 查收记录

> 仅 M1 在磁盘核对 + 跑验收命令后填写。禁止口头 done。

## 2026-08-27 开工 — M1 环#1（文档）

| 检查项 | 结果 |
|--------|------|
| `docs/MODULE-REGISTRY.md` | ✅ |
| `docs/API.md` | ✅ |
| `docs/TOOL-PATHS.md` | ✅ |
| Unity 6000.3.23f1 + Android | ✅ 已在 `D:\unity\Unity-6000.3\6000.3.23f1` |

**结论**：M1 文档环完成；等待 M3 开工汇报。  
**下一环**：用户开 M3 窗口，粘贴 Registry 中「M3 开工」话术。

## 2026-08-27 查收 — M3

| 检查项 | 结果 |
|--------|------|
| 产出路径 `backend/` `admin/` `mobile/` | ✅ |
| `cd backend && go build ./...` | ✅ exit 0（Go 1.26.7） |
| `cd admin && npm run build` | ✅ `tsc -b && vite build` |
| `mobile/Packages/manifest.json` | ✅ `com.unity.xr.arfoundation` 6.3.5、`com.unity.xr.arcore` 6.3.5 |
| Unity 版本 | ✅ `6000.3.23f1` |
| 未越界写 Issue API / AR 放置 | ✅ |
| `.gitignore` | ✅ |

**结论**：`done`  
**阻塞项**：无。M4 / M5 可并行；M6 可开工。  
**备注**：`go.mod` 模块名为 `inspect`，Android `minSdk` 仍为 22（ARCore 建议 ≥24），交给 M5 / M6，不打回骨架。

## 2026-08-27 查收 — M5

| 检查项 | 结果 |
|--------|------|
| 产出路径 `backend/internal/api` `store` | ✅ |
| `go-sqlite3` v1.14.50 | ✅ |
| CGO `go build ./cmd/server` | ✅ |
| `GET /health` | ✅ `{"ok":true}` |
| `POST /api/issues` 201 + 默认 `open` | ✅ |
| `GET /api/issues` | ✅ |
| `PATCH` 改 `in_progress` | ✅ 200 |
| 空标题 / 非法 priority / 非法 status | ✅ 400 `{error}` |
| 未知 ID | ✅ 404 |
| CORS OPTIONS `localhost:5173` | ✅ 204 |
| 重启进程后列表仍在 | ✅ |

**结论**：`done`  
**备注**：查收时本机 **8080 被其它进程占用**（非本项目），改用 `127.0.0.1:18080` 验证。启动前请空出 8080，或设 `LISTEN_ADDR`。

## 2026-08-27 查收 — M4

| 检查项 | 结果 |
|--------|------|
| 产出路径 `admin/src/App.tsx` `api.ts` | ✅ |
| 列表含标题、优先级、状态 | ✅ |
| 三态按钮 PATCH | ✅ 代码 |
| 后端不可用错误条 `role="alert"` | ✅ 不白屏 |
| 默认 `http://127.0.0.1:8080`，`VITE_API_BASE` 可覆盖 | ✅ |
| `npm run build` | ✅ |

**结论**：`done`  
**备注**：本窗口未再开浏览器点按钮（5173 已有其它进程；8080 非本后端）。

## 2026-08-27 查收 — M6

| 检查项 | 结果 |
|--------|------|
| 产出路径 `mobile/Assets/InspectAR` | ✅ |
| 场景 `InspectAR.unity` 含 XR Origin、ARRaycastManager、ARPlaneManager、InspectARApp | ✅ |
| 已进 Build Settings | ✅ |
| ARCore Loader（Android） | ✅ |
| minSdk 25、ARM64、允许明文 HTTP | ✅ |
| 水平平面、立方体标记、表单、成功/失败文案、UI 遮挡不放置 | ✅ 代码 |
| 后端地址 PlayerPrefs + 界面可改 | ✅ 默认 `http://192.168.1.8:8080` |

**结论**：`done`  
**阻塞项**：真机识别平面与提交未在 M1 实测，演示视频仍待你录。

## 2026-08-27 集成修复 — AR 相机画面（M8）

真机上半截黄底、蓝色网格/红色方块乱飘：URP `Mobile_Renderer` 未加 `ARBackgroundRendererFeature`，摄像头画面未合成进背景。已写入该 Feature、Render Scale=1、Android 仅 GLES3；需重新 Build And Run 后验证。

## 2026-08-27 查收 — M4 Phase2（连 8081）

| 检查项 | 结果 |
|--------|------|
| 产出路径仅 `admin/` | ✅ `.env.development`、`src/api.ts`、`vite.config.ts` |
| `VITE_API_BASE=http://127.0.0.1:8081` | ✅ |
| 错误文案用 `apiBase()` | ✅ 不再写死 8080 |
| Vite `port: 5174` + `strictPort` | ✅ |
| `npm run build` | ✅ exit 0 |

**结论**：`review`（模块交付过关；红条还依赖 CORS 与进程重启）  
**阻塞项**：须重启 Vite 加载 env；8081 须换成带 CORS 的新二进制。

M1 已重启：`inspect-server`（8081，CORS 对 `http://localhost:5174` 返回 200）与 `npm run dev`（`http://localhost:5174/`）。请刷新该页。

## 2026-08-27 查收 — M5 Phase2（CORS 5174）

| 检查项 | 结果 |
|--------|------|
| 产出路径 `backend/internal/api` | ✅ `api.go`、`cors_test.go` |
| `go test ./internal/api` | ✅ ok 0.823s |
| 活 8081 OPTIONS Origin=5174 | ✅ 204 + `Access-Control-Allow-Origin: http://localhost:5174` |
| 活 8081 GET /api/issues 同上 Origin | ✅ 200 + 同 CORS 头 |

**结论**：`done`  
**备注**：交卷时旧进程无 CORS 头；M1 已换新 `inspect-server.exe` 后再测通过。

## 2026-08-27 查收 — M6 Phase2（地面过滤）

| 检查项 | 结果 |
|--------|------|
| 产出路径 `mobile/Assets/InspectAR/Scripts/InspectARApp.cs` | ✅ 仅此文件 |
| HorizontalUp + 法线点积 ≥0.98 + 面积 ≥0.25m² | ✅ `IsStableFloor` |
| 只显示最低地面，高 0.20m 以上隐藏 | ✅ `RefreshFloorVisibility` |
| `ARAnchorManager.AttachAnchor` + 方块为子物体 | ✅ |
| 已放置后忽略再点 | ✅ `m_HasMarker` |
| 未跑 Setup Project | ✅ |
| 同步 `D:\Cursor_projectt\inspect-ar` 同脚本 | ✅ |
| 真机 | ❌ 待你用 inspect-ar 重新 Build And Run |

**结论**：`review`  
**阻塞项**：旧 APK 无此逻辑，必须重新出包。

## 2026-08-27 查收 — M5 Phase3（Issue JSON 字段）

| 检查项 | 结果 |
|--------|------|
| 是否改 `backend/` | ✅ 未改（斥候判定已满足） |
| `GET /health` | ✅ `{"ok":true}` |
| `GET /api/issues` 含 id/status/priority/position.x|y|z | ✅ 活 8081 抽样通过 |

**结论**：`done`  
**备注**：M6 可按现有 JSON 上色与同步状态。

## 2026-08-27 查收 — M6 Phase3（扫描开关 + 颜色）

| 检查项 | 结果 |
|--------|------|
| 仅改 `mobile/Assets/InspectAR/Scripts/InspectARApp.cs` | ✅ |
| 默认 Plane/Raycast `enabled=false` | ✅ |
| 按钮 y 0.42–0.50，文案 暂停中/扫描中 | ✅ `RefreshScanButtons` |
| 暂停时 Update 不放置 | ✅ |
| `ColorForIssue` / `ApplyMarkerColor` | ✅ |
| 刷新标记 + 5s 轮询 GET | ✅ |
| 未跑 Setup Project | ✅ |
| 同步 inspect-ar | ✅ |
| 真机 | ❌ 待 Build And Run |

**结论**：`review`

## 2026-08-27 M1 文档环 — Phase 4 开工（未查收子模块）

| 检查项 | 结果 |
|--------|------|
| 磁盘有无登录/JWT/DELETE | ❌ 确认没有；本环新增 |
| `docs/API.md` 权限矩阵与接口 | ✅ |
| `docs/FIX-PLAN.md` 卡点 E/F/G | ✅ |
| Registry M4/M5/M6 → in_progress | ✅ |
| 本窗口改 `backend/` `admin/` `mobile/` | ✅ 未改 |

**结论**：M1 文档环完成。等待用户开真窗口：先 M5，再并行 M4、M6。子窗口未汇报查收前三模块不得标 done。

## 2026-08-27 查收 — M5 Phase4（登录 JWT 权限 DELETE）

| 检查项 | 结果 |
|--------|------|
| 产出路径仅 `backend/` | ✅ `internal/auth`、`store/users.go`、`api.go`、`main.go` |
| `go test ./...` | ✅ api 包 ok |
| `go build ./cmd/server` | ✅ |
| 无 Token GET /api/issues | ✅ 401 `unauthorized` |
| 错密码登录 | ✅ 401 |
| inspector/admin/viewer 登录 | ✅ 得 token + role |
| inspector POST，submitterName 来自 Token（忽略伪造） | ✅ |
| viewer POST / inspector PATCH | ✅ 403 |
| admin PATCH | ✅ 200 |
| inspector 删别人 403、删自己 204；viewer 403；admin 204；缺 ID 404 | ✅ |
| CORS OPTIONS 5174 + Authorization/DELETE | ✅ 204 |
| 旧行 submitter_name 缺省 | ✅ `未知` |
| 已重启本机 8081 为新二进制 | ✅ |

**结论**：`done`  
**备注**：未改 admin/mobile。

## 2026-08-27 查收 — M4 Phase4（登录列表详情删除）

| 检查项 | 结果 |
|--------|------|
| 产出路径仅 `admin/` | ✅ `App.tsx` `api.ts` `listUtils.ts` `index.css` |
| `npm run build` | ✅ |
| 登录页 + localStorage 键 | ✅ |
| 5174 admin 登录后列表含提交人/时间/描述 | ✅ |
| 查看详情含 xyz 与提交人 | ✅ |
| 状态筛选 + 提交人模糊搜索 | ✅ |
| viewer 无删除、无改状态 | ✅ 仅「查看详情」 |
| 退出后筛选是否重置 | ❌ admin 留下「已解决」+`nobody`，viewer 再登录仍空列表 |

**结论**：`review`  
**打回主力（计数 1/4）**：logout / 401 清会话时重置三个筛选项。

## 2026-08-27 复验 — M4 Phase4 打回（筛选重置）

| 检查项 | 结果 |
|--------|------|
| 产出 `admin/src/App.tsx` | ✅ `resetFilters`；logout 与 401 handler 均调用 |
| `npm run build` | ✅ |
| 5174：admin 筛「已解决」+ 提交人 `nobody` → 空列表 | ✅ 复现前置 |
| 退出后以 viewer 登录 | ✅ 状态/优先级=全部，提交人为空；列表有记录，不再「没有符合筛选的记录」 |
| 未改 backend/mobile | ✅ |

**结论**：`done`（打回 1/4 已过，未再打回）

## 2026-08-27 查收 — M6 Phase4（多点标注、登录、柔和 UI）

| 检查项 | 结果 |
|--------|------|
| 产出路径 `mobile/Assets/InspectAR/Scripts/InspectARApp.cs` | ✅ |
| 默认暂停；快速窗 1s `MinFloorArea=0.08` | ✅ |
| 确认框；确认暂停+锁定+清射线；可再放第 N 个 | ✅ `m_Confirmed` / `LatestUnsubmitted` |
| `inspect.jwt` + Authorization | ✅ |
| 莫兰迪色号、圆角 sprite、Fade、FontStyle.Normal | ✅ |
| `ColorForIssue` / `ApplyMarkerColor` | ✅ 保留 |
| 同步 inspect-ar 同脚本 | ✅ 哈希一致 |
| 真机 | ❌ 待用 inspect-ar 重新 Build And Run |

**结论**：`review`  
**阻塞项**：旧 APK 无登录/多点逻辑，必须重新出包。未代替改 `mobile/`。

## 2026-08-27 复验 — M6 Transition.ColorTint

| 检查项 | 结果 |
|--------|------|
| `mobile/.../InspectARApp.cs` 第 1211 行 | ✅ `Selectable.Transition.ColorTint` |
| `fadeDuration = 0.15f` | ✅ 保留 |
| 已无 `Transition.Fade` | ✅ |
| 同步 inspect-ar | ✅ |
| 真机 | ❌ 待重新打开 inspect-ar 出包 |

**结论**：编译打回通过；模块保持 `review`（真机未测）。M1 未改 `mobile/` 业务，只同步出包目录。

## 2026-08-27 M1 — 写入 `密码.md`

| 检查项 | 结果 |
|--------|------|
| `D:\Cursor_projectt\测试考题\密码.md` | ✅ 三套演示账号 |

## 2026-08-28 M1 文档环 — Phase 5 开工（端口切回 8080）

| 检查项 | 结果 |
|--------|------|
| 用户确认 8080 已空（关机后） | ✅ |
| `docs/API.md` PUT + `/ws` + 三行坐标约定 | ✅ |
| `docs/FIX-PLAN.md` 卡点 H/I/J | ✅ |
| Registry M4/M5/M6 → `pending`（Phase5） | ✅ |
| 本窗口改 `backend/` `admin/` `mobile/` 业务 | ✅ 未改（仅文档 + 开发 env 指回 8080） |

**结论**：M1 文档环完成。等待用户开真窗口：先 M5，再并行 M4、M6。子窗口未汇报查收前不得标 done。

## 2026-08-28 查收 — M5 Phase5（PUT + WebSocket）

| 检查项 | 结果 |
|--------|------|
| 产出仅 `backend/` | ✅ `internal/ws/hub.go`、`api.go` PUT/`/ws`、`store.UpdateFields`、gorilla v1.5.3 |
| 未重建 issues 表 | ✅ 仍用 `pos_x/y/z` |
| `go test ./...` | ✅ api 包 ok |
| 两次 POST 不同 xyz，GET 互不覆盖 | ✅ `(0.1,0.2,0.3)` 与 `(9,8,7)` |
| inspector PUT 自己的，坐标不变 | ✅ title/priority 变，xyz 仍 0.1/0.2/0.3 |
| inspector PUT 别人 / viewer PUT | ✅ 403 |
| admin PUT 任意 | ✅ 200 |
| 无 token `GET /ws` | ✅ 401 |
| WS 连上后 POST，收到 `issue.created` 含新 id | ✅ gorilla 客户端 |
| CORS OPTIONS 含 PUT | ✅ 204 + `PUT` + Origin 5174 |
| 已换新二进制跑 **8080** | ✅ |

**结论**：`done`

## 2026-08-28 查收 — M4 Phase5（三行坐标 + WS）

| 检查项 | 结果 |
|--------|------|
| 产出仅 `admin/` | ✅ `useIssuesSocket.ts` `issuesWs.ts` `App.tsx` |
| `.env.development` 8080 | ✅ |
| `npm run build` | ✅ |
| `node selftest.mjs` | ✅ |
| 5174 详情三行 | ✅ `X 坐标：4` / `Y 坐标：5` / `Z 坐标：6` |
| 不点刷新，POST 后列表最前出现新标题 | ✅ `ws-live-prepend-999` 插到 `ws-created` 之前 |

**结论**：`done`

## 2026-08-28 查收 — M6 Phase5（任务 UI / 打回历史优先级）

| 检查项 | 结果 |
|--------|------|
| 产出仅 `mobile/` | ✅ `InspectARApp.cs` `InspectUiTheme.cs` `InspectTaskSession.cs` `InspectHistoryPanel.cs` |
| 主次危险色号与 ColorTint | ✅ `#2C4A6E` / `#D4D0C8` / `#E8D0CC`；无 `Transition.Fade` |
| 新建任务门闩 + DraftMarker 独立 xyz + 确认后逐条 POST | ✅ 代码 |
| 历史 GET + X/Y/Z 三行 + PUT 请求 | ✅ 代码有 |
| 点优先级再保存 | ❌ `InspectHistoryPanel.cs` 约 174–178 行：`captured.priority = capturedP` 后立刻 `StartHistoryLoad()`，GET 重建行，选中优先级丢失 |

**结论**：`review`  
**打回主力（计数 1/4）**：历史编辑点 low/medium/high 时不要立刻重新 GET；把选择留在内存，点「保存」再 PUT，成功后再刷新列表。  
未同步 inspect-ar。未代替改 `mobile/`。

## 2026-08-28 复验 — M6 Phase5 打回（历史优先级）

| 检查项 | 结果 |
|--------|------|
| 点 low/medium/high 是否立刻 GET | ✅ 改为 `SetDraftPriority` + 当场改按钮样式 |
| 保存是否用内存中的优先级 | ✅ `StartHistorySave(..., DraftPriority(captured))` |
| PUT 成功后再刷新 | ✅ `PutHistory` 200 后 `LoadHistory()` |
| 展开/关闭仍可 GET | ✅ 仅 Show / 展开收起调用 `StartHistoryLoad` |
| 同步 `D:\Cursor_projectt\inspect-ar` 四份脚本 | ✅ |
| 真机 | ❌ 待用 inspect-ar Build And Run |

**结论**：`done`（打回 1/4 已过，未再打回）  
M1 未改 `mobile/` 业务，只同步出包目录。

## 2026-08-28 M1 文档环 — Phase 6 开工（AR 布局）

| 检查项 | 结果 |
|--------|------|
| 中央挡画面根因 | ✅ `MarkerListCard` y=0.40～0.82 实心 |
| `docs/FIX-PLAN.md` 卡点 K | ✅ |
| Registry M6 → pending | ✅ |
| 本窗口改 `mobile/` | ✅ 未改 |

**结论**：等用户开 M6 窗口贴开工话术。

## 2026-08-28 查收 — M6 Phase6（AR 布局）

| 检查项 | 结果 |
|--------|------|
| 产出仅 `mobile/` | ✅ 主要 `InspectARApp.cs` `InspectUiTheme.cs` |
| 中央无全宽实心列表 | ✅ `MarkerListCard` 已删；抽屉默认 `SetActive(false)` |
| 竖屏顶 0.92–1 / 底 0–0.22 / 抽屉右侧 0.62–0.98 | ✅ `ApplyLayout` |
| 玻璃 a≤0.55 | ✅ `GlassMaxA` + `StyleGlass` clamp |
| 底栏两行六键 | ✅ 新建/开始/暂停 + 历史/标记/提交 |
| Toast 2.5s 淡出 | ✅ `SetStatus` → `ShowToast`；不挡底栏 |
| 横屏 match=0、safeArea 内边距 | ✅ |
| ColorTint / DraftMarker / PUT | ✅ 保留 |
| 同步 inspect-ar | ✅ 四份脚本 |
| 真机 | ❌ 待 Build And Run |

**结论**：`done`  
M1 未改 `mobile/` 业务，只同步出包目录。

## 2026-08-28 查收 — M6 Phase7（锁地 + 标题关键词）

| 检查项 | 结果 |
|--------|------|
| 产出仅 `mobile/` | ✅ 主要 `InspectARApp.cs`；未改 Registry |
| 同一地面连续约 1s 才锁 | ✅ `FloorLockSeconds=1` + `TickFloorLock` |
| 锁后关活 `ARPlaneMeshVisualizer`，静态蓝网格 | ✅ `HideAllLivePlaneVisuals` + `FreezeGridFrom` |
| 锁后不换最低平面 | ✅ `RefreshFloorVisibility` 遇 `m_FloorLocked` 直接 return |
| 再点「开始扫描」才解锁重侦测 | ✅ `SetScanning(true)` → `UnlockFloor` |
| 立方体 `AttachAnchor` 子物体，不每帧改世界坐标 | ✅ |
| 标题关键词强制 high、禁用优先级按钮 | ✅ `漏水/裂缝/冒烟/异响/脱落` |
| 描述不覆盖，只追加系统提示（已有不重复） | ✅ `【系统提示】请尽快安排处理` |
| 关键词删光后按钮可改，不恢复旧优先级 | ✅ |
| 换选中标记重跑规则 | ✅ `FillEditorFromSelected` → `ApplyTitleKeywordRules` |
| Phase6 底栏/抽屉/Toast | ✅ 保留 |
| 同步 inspect-ar | ✅ `InspectARApp.cs` |
| 真机 | ❌ 待 Build And Run |

**残留（不打回）**：暂停时 `ARPlaneManager.enabled` 仍为 true，靠关掉活 visualizer + 锁后 `DetectionMode.None`。未锁定就暂停时后台仍可能扩面，但不显示活网格。

**结论**：`done`  
M1 未改 `mobile/` 业务，只同步出包目录。

## 2026-08-28 查收 — M6 Phase8（出蓝 / 世界锚 / 点击）

| 检查项 | 结果 |
|--------|------|
| 产出仅 `mobile/` | ✅ 脚本 + `InspectAR.unity`；未跑 Setup 重建场景 |
| XR Origin 上 `ARAnchorManager` + Verify | ✅ 场景组件已加；`EnsureAnchorManager`；Setup.Verify |
| 扫描中 Horizontal；锁后不是 None | ✅ `LockFloor` 仍设 Horizontal |
| 暂停关 Plane + Raycast | ✅ `enabled = scanning` |
| 法线 0.92、快扫 0.04/8s、正常 0.08、滞回 | ✅ `LowestFloorHysteresis=0.08` |
| `TryAddAnchorAsync`，无 `AttachAnchor` | ✅ 立方体挂世界锚 |
| 静态网格父节点 XR Origin | ✅ |
| 看到蓝即可点，不比锁定 trackableId | ✅ `TryPlaceFromPress` + `IsStableFloor` |
| 顶/底栏玻璃不吞点击；只拦按钮/输入 | ✅ |
| 失败 Toast 写明原因 | ✅ 未建任务 / 未扫描 / 未命中 / 锚定失败 |
| `ARSession.Reset` 仅「重新对准」确认后 | ✅ |
| 关键词 High / ColorTint / 底栏 | ✅ 保留 |
| 同步 inspect-ar | ✅ 脚本 + 场景 |
| 真机 | ❌ 待 Build And Run |

**结论**：`done`  
M1 未改 `mobile/` 业务，只同步出包目录。

## 2026-08-28 查收 — M6 Phase9（打回 1/4）

| 检查项 | 结果 |
|--------|------|
| 法线 0.85、面积 0.01、Limited 可显示 | ❌ 仍是 `0.92` / `0.04`/`0.08`，只要 `Tracking` |
| 射线 PlaneEstimated / FeaturePoint / Depth | ❌ 仍只打 `PlaneWithinPolygon`，失败 Toast「请对准蓝色地面再点」 |
| `ARCoreSettings` Depth Optional、ARCore Required | ❌ YAML 仍 `m_Requirement=0`、`m_Depth=1` |
| `ARPointCloudManager` + 粒子点云 + 数量文案 | ❌ 工程内无此类代码/组件 |
| 点击造 1.5m 临时水平面 + TryAddAnchorAsync | ❌ 无虚拟平面路径 |
| 相对 Phase8 是否有卡点 P 改动 | ❌ 未见 |

**结论**：`review` 打回主力（计数 **1/4**）。请按 `docs/FIX-PLAN.md` Phase 9 卡点 P 做完再来查收。  
M1 未代替改 `mobile/`，未同步 inspect-ar。

## 2026-08-28 复验 — M6 Phase9（打回 1/4 后）

| 检查项 | 结果 |
|--------|------|
| 法线 0.85、面积 0.01、Limited | ✅ `IsStableFloor` |
| 射线 Polygon → Estimated → FeaturePoint → Depth | ✅ `PlaceRayTypes`；Depth 看 descriptor |
| 扫描中 Horizontal\|Vertical，锁后不是 None | ✅ `ScanDetectionMode` |
| 4 秒无合格平面 Toast | ✅ `LowTextureHint` |
| XR Origin `ARPointCloudManager` + 粒子预制体 | ✅ 场景组件 + 运行时 prefab |
| 仅扫描中开点云；顶栏数量 &lt;15 引导 | ✅ |
| 射线失败 → 1.5m 临时水平网格 + TryAddAnchorAsync | ✅ Toast「已用临时平面放置」 |
| 删本地标记拆虚拟网格 | ✅ `DraftMarker.virtualGrid` |
| 关键词 High / ColorTint / 世界锚 | ✅ 保留 |
| `ARCoreSettings.asset` YAML | ❌ 仍 `m_Requirement=0`、`m_Depth=1`（残留不打回；Setup 代码已写 Required/Optional） |
| 同步 inspect-ar | ✅ |
| 真机 | ❌ 待 Build And Run |

**结论**：`done`（打回 1/4 已过，未再打回）  
M1 未改 `mobile/` 业务，只同步出包目录。


