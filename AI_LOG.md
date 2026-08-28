# AI 使用记录

按本题中发给 AI 的指令顺序记录。不含 IDE 自动补全。

## P-001

- 目标：以 M1 主导窗口开工，按给定技术栈准备环境并读题目

### Prompt 原文

> /multi-window_M 你是M1,你将作为项目负责人完成任务，这是项目的技术栈，你可以在路径D:\unity 创建对应的技术名称文件夹下载到里面。技术栈：……（Unity 6.3 LTS / Go 1.26 / Node 24 / React 19 / TS 5.5）。这是题目 c:\Users\user\Desktop\测试题目.txt 。我有node.js，你可以查看

### 结果

- 采用
- 核对本机 Node v24.16.0；在 `D:\unity` 安装 Go 1.26.7、MinGW、Unity Hub/CLI。项目实现按后续指示暂缓。

## P-002

- 目标：只装技术栈，先不要做项目

### Prompt 原文

> 先添加技术栈，项目实现等我指示

### 结果

- 采用
- 未写业务代码。Go/GCC/Hub 就绪；当时 6.3 编辑器因国内 CDN 404 未装完。

## P-003

- 目标：技术栈就绪后先出计划再动手

### Prompt 原文

> 安装已完成，请生成你的计划，让我过目

### 结果

- 修改后采用
- 盘上当时是误装的 6000.5.10f1。计划给出方案 A（用 6.5）与 B（改装 6.3 LTS），请用户拍板。

## P-004

- 目标：对齐题目技术栈，改用 Unity 6.3

### Prompt 原文

> 抱歉，是我疏忽了，请对齐技术栈，选择unity版本B

### 结果

- 采用
- 弃用方案 A。卸载 6.5 后由用户在 Hub 安装 6000.3.23f1 到 `D:\unity\Unity-6000.3`，并带 Android 模块。


## P-005

- 目标：按多窗口流程正式开工

### Prompt 原文

> 开工

### 结果

- 采用
- M1 写入 `docs/`、`README.md`、`AI_LOG.md`；下一步由用户开 M3 窗口粘贴开工话术。未在本窗口实现三端业务。

## P-006

- 目标：M1 查收 M3 骨架

### Prompt 原文

> M3已完成请查收

### 结果

- 采用
- 磁盘核对 + `go build ./...`、`npm run build`、AR 包清单均通过；M3 标 `done`。未代替 M3 改代码。

## P-010

- 目标：M1 查收 M4 / M5 / M6

### Prompt 原文

> M4M5M6已完成，请查收

### 结果

- 采用
- 跑通 Go 接口与重启持久化、`admin` 生产构建、核对 Unity AR 工程配置；三模块标 `done`。未代替子窗口改业务代码。真机画面未测。

## P-011

- 目标：Phase 4 由 M1 只定契约并开工，不改三端业务

### Prompt 原文

> 测试改进：1.提高 app 扫描速度与多次标记；2.React 列表展示提交人/时间/描述/详情/搜索；3.Unity 莫兰迪毛玻璃 UI。新增：登录 JWT 三角色；网页端提交人、详情、删除。

### 结果

- 采用（拆环、复用 M4/M5/M6，不新开 F 编号）
- 磁盘确认尚无登录系统；写入 `docs/API.md`、`docs/FIX-PLAN.md` Phase 4、`docs/MODULE-REGISTRY.md`。等待用户开 M5→M4/M6 真窗口。本窗口未改 `backend/` `admin/` `mobile/`。

## P-012

- 目标：M1 查收 Phase 4 三窗口

### Prompt 原文

> 所有窗口已完成，请查收

### 结果

- 部分采用（未口头 done）
- M5 磁盘 + curl 权限矩阵通过，标 `done`；8081 已换 JWT 二进制。
- M4 `npm run build` 与 5174 联调大部分通过，因退出后筛选残留打回，保持 `review`。
- M6 对照卡点 G 代码通过，同步 inspect-ar；真机未测，保持 `review`。
- 本窗口未改三端业务代码。

## P-013

- 目标：M1 复验 M4 筛选打回

### Prompt 原文

> M4已完成，请查收

### 结果

- 采用
- 磁盘有 `resetFilters`；5174 复测 admin 筛选后退出再 viewer 登录，筛选已清空且列表可见。M4 标 `done`。未改业务代码。

## P-014

- 目标：查收 M6 ColorTint；写入账号文件

### Prompt 原文

> M6已完成，请查收。请把你给我的密码封装到该项目路径下然后创建密码.md文件中，你来创建这个.md文件

### 结果

- 采用
- `InspectARApp.cs` 已改为 `ColorTint`；同步 inspect-ar。写入 `密码.md`。M6 编译打回通过，真机仍待出包。

## P-015

- 目标：Phase 5 由 M1 只定契约；端口切回 8080

### Prompt 原文

> 测试改正：1. 手机 UI 主次按钮；2. 三端独立 XYZ；3. 多标记任务本地 CRUD 与提交确认。新增：WebSocket 推送；Unity 历史与 PUT。随后：目前 8080 端口号已空，电脑关机过。

### 结果

- 采用（复用 M4/M5/M6，不新开 F 编号）
- 磁盘确认：`pos_x/y/z` 已独立；缺 PUT、/ws、任务 List、主次按钮、详情三行坐标。
- 写入 Phase 5 契约。本机默认端口改回 **8080**。本窗口未改三端业务代码。

## P-016

- 目标：M1 查收 Phase 5 三窗口

### Prompt 原文

> 所有窗口已完成，请查收

### 结果

- 部分采用（未口头 done）
- M5 活测 PUT/xyz/WS/CORS 通过，标 `done`；8080 已换新二进制。
- M4 构建 + 5174 三行坐标 + WS 前置插入通过，标 `done`。
- M6 环#1/#2 代码通过；历史优先级按钮会立刻 GET 冲掉选择，打回 1/4，保持 `review`。未改三端业务，未同步 inspect-ar。

## P-017

- 目标：复验 M6 历史优先级打回

### Prompt 原文

> M6已完成请查收

### 结果

- 采用
- `InspectHistoryPanel` 用 `m_DraftPriority` 记住选择，点优先级不再立刻 GET；保存才 PUT，成功后再刷新。标 `done`。已同步 inspect-ar。未改业务代码。真机仍待出包。

## P-018

- 目标：Phase 6 只定 Unity 布局契约

### Prompt 原文

> 新建任务后中央白色面板挡住 AR；底栏空白、中间扫描区太小；要毛玻璃/靠边、底栏两行按钮、Toast 2～3 秒。

### 结果

- 采用（只开 M6）
- 根因：`MarkerListCard` 不透明铺在 y=0.40～0.82。写入卡点 K。本窗口未改 `mobile/`。

## P-020

- 目标：说明平面抖动原因，并开 Phase7（锁地 + 标题关键词）

### Prompt 原文

> 标记和蓝平面仍会抖、会漂；讲如何优化。新功能：标题含漏水等则强制 High 并追加描述提示。告诉我你的理解。

### 结果

- 采用。蓝网格仍跟活 ARPlane 走。写入卡点 L/M。只开 M6。未改 `mobile/`。


## P-019

- 目标：查收 M6 Phase6 布局

### Prompt 原文

> M6已完成，请查收

### 结果

- 采用
- 中央实心列表已改为默认收起的右侧玻璃抽屉；底栏两行按钮；Toast 2.5s。标 `done`。已同步 inspect-ar。未改业务代码。真机仍待出包。

## P-021

- 目标：查收 M6 Phase7 锁地 + 标题关键词

### Prompt 原文

> M6已完成，请查收

### 结果

- 采用
- 地面稳定 1s 后锁静态蓝网格；标题关键词强制 High 并追加系统提示。标 `done`。已同步 inspect-ar。未改业务代码。真机仍待出包。

## P-022

- 目标：真机三连问题定 Phase8 契约（只开 M6）

### Prompt 原文

> 真机：出蓝超过 5 分钟且跳动；立方体随平面偏移（怀疑没 ARAnchor）；偶尔点了不放。要完整方案：Anchor Manager / Session / Horizontal、AttachAnchor 代码、Reset、射线排查、已知 Bug。

### 结果

- 采用（只开 M6）
- 根因：过滤过严 + `AttachAnchor` 跟平面走 + 未锁地/DetectionMode.None/UI 挡点击。写入卡点 N。本窗口未改 `mobile/`。

## P-023

- 目标：查收 M6 Phase8

### Prompt 原文

> M6已完成，请查收

### 结果

- 采用
- 放宽出蓝、`TryAddAnchorAsync` 世界锚、看到蓝即可点放。标 `done`。已同步 inspect-ar（含场景 ARAnchorManager）。未改业务代码。真机仍待出包。

## P-024

- 目标：工地白墙白地定 Phase9 三方案（只开 M6）

### Prompt 原文

> 白色粉刷墙和白色地面完全无法生成蓝色平面。要三个方案：调检测参数、叠加特征点云、检不出时点击造临时平面。请优化。

### 结果

- 采用（只开 M6）
- ARCore 无灵敏度滑条；写入卡点 P（放宽过滤、点云 HUD、虚拟水平面兜底）。本窗口未改 `mobile/`。

## P-025

- 目标：查收 M6 Phase9

### Prompt 原文

> M6已完成，请查收

### 结果

- 未采用（打回 1/4）
- `mobile/` 仍是 Phase8：过滤未放宽、无点云、无临时平面、ARCoreSettings 未改。未改业务、未同步出包目录。

## P-026

- 目标：复验 M6 Phase9

### Prompt 原文

> M6已完成，请查收

### 结果

- 采用（打回 1/4 已过）
- 过滤、点云、临时平面已落地。`ARCoreSettings.asset` YAML 仍 Depth Required，作残留。已同步 inspect-ar。未改业务代码。真机仍待出包。





