# 修复方案

> 仍用 M1~Mn，不新增 F 编号。环内：斥候 → 主力 → 搜剿；同一卡点最多 4 次。
> 本机巡检 Go **8080**。管理端 **http://localhost:5174**。源码 `测试考题/mobile`；出包 `D:\Cursor_projectt\inspect-ar`。
> **M1 本窗口禁止改 `backend/` `admin/` `mobile/` 业务。** 子窗口未汇报「请查收」前不得标 done。

---

## Phase 9（已归档）— 工地白墙白地：敏感检测 + 特征点云 + 临时虚拟平面

磁盘核对（M1 斥候 2026-08-28）：

工地白粉墙 / 白地检不出蓝，**主要是 ARCore 本身**：没有纹理和对比度就几乎没有特征点，也就长不出 `ARPlane`。我们这边还叠了一层门槛，把仅有的小面也滤掉了。

| 现状 | 影响 |
|------|------|
| `IsStableFloor`：法线 ≥0.92、面积 ≥0.04/0.08、只要 `HorizontalUp`、只要 `Tracking` | 白地上偶发的碎面会被藏掉 |
| 射线只用 `PlaneWithinPolygon` | 没有多边形平面就完全点不了 |
| 没有 `ARPointCloudManager` | 用户看不见「现在有没有特征点」 |
| `ARCoreSettings`：`m_Requirement=0`（Optional）、`m_Depth=1`（Required） | Depth 强依赖对无纹理也帮不上忙，部分机型还可能更脆 |

ARCore **没有**「灵敏度滑条」。方案 1 能做的是：放宽我们的过滤、打开估计平面/特征点射线、Depth 改为 Optional。白漆地面仍可能零平面，所以必须有点云引导（方案 2）和点击即放的虚拟平面（方案 3）。

本环 **只开 M6**。不要改 `backend/` `admin/`。不要执行 `InspectAR/Setup Project`。保留世界锚 `TryAddAnchorAsync`、底栏、关键词 High、Horizontal 为主（可加 Vertical 作辅助，不要 `Everything`）。

| 模块 | 本环 | 职责 |
|------|------|------|
| M1 | 进行中 | 本文 / Registry、查收、同步 inspect-ar |
| M6 | done | 三方案：敏感过滤、点云 HUD、虚拟平面兜底 |

---

### 卡点 P — 低纹理工地三方案（仅 M6，`mobile/`）

禁止 Setup Project。源码 `测试考题/mobile`。运行时补组件即可。允许改：`InspectARApp.cs`、`InspectARSetup.cs`（只加 Verify / 运行时 prefab 工厂）、`InspectAR.unity`（可在 XR Origin 加 PointCloudManager）、`ARCoreSettings.asset`。不要新建第二套 Session/Origin。

#### 环 #1 — 让检测对低纹理更敏感

ARCore 不会因为改一个 float 就看清白墙。必须同时改 **我们的过滤** 和 **命中类型**：

1. 扫描中 `requestedDetectionMode = Horizontal`（地面）。可 **额外** `Horizontal \| Vertical`，但可视化仍优先水平面；禁止 `Everything`。
2. `IsStableFloor` 工地档（扫描期间一直用，不要 8 秒后收紧）：
   - 法线点积 ≥ **0.85**（允许略不平的水泥）
   - 面积 ≥ **0.01 m²**
   - 允许 `trackingState == Tracking \|\| Limited`
   - 仍丢 `subsumedBy != null`
3. `ARCoreSettings`：`requirement = Required`，`depth = Optional`（YAML 里不要再把 Depth 标成 Required）。
4. 放置射线按优先级（命中即停）：
   - `PlaneWithinPolygon`
   - `PlaneEstimated`
   - `FeaturePoint`
   - `Depth`（descriptor 支持才开）
5. 顶栏/Toast：扫描超过 **4 秒**仍没有任何合格平面时提示：「白墙白地特征点太少，请扫到砖缝、脚印、工具，或直接点屏幕用临时平面。」

不要自动 `ARSession.Reset`。

#### 环 #2 — 叠加特征点云 + 数量引导

1. XR Origin 上要有 `ARPointCloudManager`（场景或 `EnsurePointCloudManager` 运行时加）。
2. `pointCloudPrefab` 运行时生成即可：`ARPointCloud` + `ParticleSystem` + `ARPointCloudParticleVisualizer`。粒子小、暖黄/雾霾蓝、不挡操作。
3. **仅扫描中** `enabled = true`；暂停关掉点云 Manager，藏粒子。
4. 每帧（或 0.3s）统计所有 `ARPointCloud.positions` 数量，顶栏或 Toast 旁一行：
   - `< 15`：「特征点偏少，请对准裂缝/脚印/工具缓慢平移」
   - `≥ 15`：「特征点 n，可继续扫描」
5. 点云不要拦截点击（粒子 `raycast` 关）。

#### 环 #3 — 检不出平面时，点击即生成临时虚拟平面并放标记

`TryPlaceFromPress` 在环 #1 的四种射线都失败后：

1. 用主相机 `ScreenPointToRay` 与 **水平面** 求交：面法线 `Vector3.up`，面上一点为 `(0, camera.position.y - 1.2, 0)`（手持约 1.2m）。交不到则沿射线 **1.4m** 处再投影到该水平面。
2. 立刻在交点生成 **临时虚拟平面**（1.5m×1.5m 半透明蓝网格，与现有 Frozen 材质同类），父节点 XR Origin，不要挂 ARPlane。
3. `Pose`：位置=交点，旋转=`Quaternion.LookRotation(Vector3.forward, Vector3.up)`（水平）。
4. 同一套 `TryAddAnchorAsync` 在该 Pose 放立方体。Toast：「已用临时平面放置（现场缺少真实平面）」。
5. 每个标记可有自己的小网格；删本地标记时 Destroy 对应网格。虚拟平面 **不要**当成 ARCore 平面参与 `IsStableFloor`。
6. 仍要求：已登录、已新建任务、**正在扫描**。暂停时不可点放。

成功标准（代码）：过滤放宽；扫描中能看见点云和数量；无 AR 平面时点击 AR 窗仍能出临时蓝面+立方体。真机出包由用户做。

---

### 开工话术

```text
@multi-window_M
我是 M6 窗口。请读 D:\Cursor_projectt\测试考题\docs\MODULE-REGISTRY.md 中 M6，以及 docs/FIX-PLAN.md Phase 9 卡点 P。
按斥候 → 主力 → 搜剿执行；同一卡点最多 4 次。
只改 mobile/。不要执行 InspectAR/Setup Project。不要打开游戏。
环#1：放宽 IsStableFloor（法线 0.85、面积 0.01、允许 Limited）；射线依次 PlaneWithinPolygon / PlaneEstimated / FeaturePoint / Depth；ARCoreSettings 的 Depth 改为 Optional。
环#2：XR Origin 加 ARPointCloudManager；扫描中显示粒子点云；顶栏或状态显示特征点数量并引导去找纹理。
环#3：四种射线都没有命中时，点击生成 1.5m 水平临时虚拟平面并 TryAddAnchorAsync 放标记。
保留世界锚、底栏、关键词 High、ColorTint。不要自己把 Registry 标成 done。
完成后说：M6 已完成，请主导窗口查收。
```

---

## Phase 8（已归档）— 真机：出蓝慢、方块跟平面漂、点了不放

磁盘核对（M1 斥候 2026-08-28，对照 `InspectAR.unity` + `InspectARApp.cs`，AR Foundation **6.3.5**）：

三个现象是同一条链路，不是三个无关 Bug。

| 现象 | 磁盘根因（不是猜的） |
|------|----------------------|
| 蓝网格要很久才出、还乱跳 | 启动默认 **暂停**；`IsStableFloor` 法线点积 ≥**0.98**、面积 ≥**0.25 m²**；快扫窗只有 **1 秒**就收回 0.25。ARCore 其实可能已检出小面，全被藏掉。`RefreshFloorVisibility` 每帧换「最低那块」，活 `ARPlaneMeshVisualizer` 还在更新，所以跳。 |
| 立方体跟蓝面一起漂 | **已经用了 Anchor**，而且用的是 `AttachAnchor(plane, pose)`。6.3 文档写明：贴在平面上的锚会随平面法线/姿态更新。平面一改，方块必跟。场景 **XR Origin 上没有 ARAnchorManager**（运行时 `AddComponent`），主因仍是 AttachAnchor 语义，不是「完全没锚」。 |
| 看到蓝了点屏幕不放 | `Update` 要求 `m_Scanning && m_Task.CanPlace && m_FloorLocked`。锁地前就能看到活蓝网格，此时点击直接 return。候选面 ID 一直变则 **永远锁不上**。锁后把 `requestedDetectionMode = None`，ARCore 上 `PlaneWithinPolygon` 射线常失效。命中后还要求 `hit.trackableId == m_LockedFloor`，换面就全丢。底栏占屏幕高度 **22%** 且 `raycastTarget=true`，点靠下的地面会被当成点 UI。 |

组件现状（场景 YAML）：

- **AR Session**：有，同物体有 `ARInputManager`。`m_AttemptUpdate=1`，`m_TrackingMode=2`。挂载正确，不必再造一个。
- **XR Origin**：有 `ARPlaneManager`（`m_DetectionMode: 1` = **Horizontal**）+ `ARRaycastManager`。缺 **ARAnchorManager**。
- **Plane Detection Mode**：应保持 **Horizontal**。不要 `Everything`（柜子/腿会再铺蓝）。**禁止**锁地后改成 `None`（这是点了不放的主因之一）。

`ARSession.Reset()`：**不要**为了「出蓝慢」自动 Reset。Reset 会清掉全部平面和已有锚，方块会丢。只在用户点「重新对准」且 `ARSession.state` 为 `NeedsInstall` / 长时间 `SessionInitializing` / `Tracking` 丢失时手动调一次。

本环 **只开 M6**。不要改 `backend/` `admin/`。不要执行 `InspectAR/Setup Project`（会重建场景）。保留 Phase6 布局、任务 List、关键词 High、静态蓝网格（锁地后冻视觉，但检测保持 Horizontal）。

| 模块 | 本环 | 职责 |
|------|------|------|
| M1 | 进行中 | 本文 / Registry、查收、同步 inspect-ar |
| M6 | done | 放宽出蓝、世界锚、看到蓝就能点 |

---

### 卡点 N — 出蓝、锚定、点击（仅 M6，`mobile/`）

禁止 `InspectAR/Setup Project`。源码 `测试考题/mobile`。

#### 环 #1 — 组件 + 检测（出蓝慢 / 乱跳）

1. **ARAnchorManager 必须有**：在 XR Origin 上（与 Plane/Raycast 同物体）。`EnsureAnchorManager` 继续运行时补；`InspectARSetup.Verify` 增加检查项。不要新开第二套 XR Origin / AR Session。
2. **AR Session**：保持现有独立物体即可；不要 Reset 当默认流程。
3. **DetectionMode 始终 Horizontal**（扫描中）。暂停：`ARPlaneManager.enabled = false` 且 `ARRaycastManager.enabled = false`（恢复 Phase3 省电）。**锁定后不要 `None`**，只关活 visualizer、不再换最低平面。
4. 放宽 `IsStableFloor`（建议）：法线点积 ≥ **0.92**；面积快扫 **0.04**、正常 **0.08**；快扫窗 **8 秒**。仍只要 `HorizontalUp`，仍丢掉 `subsumedBy != null`。
5. 最低平面加滞回：已显示的面只要仍合格，不要因为另一块低 1cm 就换。锁地逻辑保留，但 **不再作为放置门闩**。

#### 环 #2 — 世界锚（方块跟平面漂）

不要再用 `AttachAnchor` 当标记锚（它就是设计成跟平面走的）。

AR Foundation 6.3.5 正确路径：`TryAddAnchorAsync(pose)`（Unity 世界坐标 Pose，来自射线命中）。Coroutine 包装 Awaitable，禁止每帧 `AddComponent<ARAnchor>`。

```csharp
IEnumerator PlaceAt(Pose pose)
{
    if (m_AnchorManager == null)
    {
        SetStatus("锚定失败：缺少 ARAnchorManager。", true);
        yield break;
    }
    var op = m_AnchorManager.TryAddAnchorAsync(pose);
    yield return new WaitUntil(() => op.IsCompleted);
    var result = op.GetResult(); // 若 API 为 await Result，用 result.status.IsSuccess()
    if (!result.status.IsSuccess() || result.value == null)
    {
        SetStatus("锚定失败，请再点一次地面。", true);
        yield break;
    }
    var anchor = result.value;
    var cube = CreateMarker();
    cube.transform.SetParent(anchor.transform, false);
    cube.transform.localPosition = Vector3.zero;
    cube.transform.localRotation = Quaternion.identity;
    cube.transform.localScale = Vector3.one * 0.12f;
    // 写入 DraftMarker.anchor / position = cube.transform.position
}
```

若 `GetResult` 名称与 6.3.5 不一致，以包内 `Awaitable<Result<ARAnchor>>` 为准，语义不变：成功才挂立方体。静态蓝网格父节点用 XR Origin，不要挂在 `ARPlane.transform` 上。已放置的 cube **禁止**每帧改 `transform.position`。

#### 环 #3 — 射线（点了不放）

放置条件改为：`m_Scanning && m_Task.CanPlace`。**不要**要求 `m_FloorLocked`。射线：`TrackableType.PlaneWithinPolygon`，命中后 `IsStableFloor(plane)` 即可，不要比对 `m_LockedFloor.trackableId`。

`IsPointerOverUi`：只拦截真正的按钮/输入框；顶栏/底栏/Toast/空玻璃若挡住中央 AR，把非按钮 Image 的 `raycastTarget` 关掉，或命中列表忽略 `TopBar`/`Toast`。底栏不要吞掉 AR 窗里的点击。

失败时 Toast 写明原因（便于真机）：未建任务 / 未开始扫描 / 点到 UI / 未命中平面 / 锚定失败。不要静默 return。

**不要**循环调用 `ARSession.Reset()`。可加次要按钮「重新对准」：确认后 `ARSession.Reset()`，并清未提交本地标记（Reset 会丢锚）。

环境（写进 Toast/状态，不必代码强制）：光线足、地面有纹理、避开纯色瓷砖/玻璃；缓慢平移不要只转手腕；Google Play 服务（AR）已装。

已知限制（6.3.5 / ARCore，规避而不是幻想修驱动）：

- `AttachAnchor` 跟平面走 → 改 `TryAddAnchorAsync`。
- 锁后 `DetectionMode.None` 导致射线失效 → 保持 Horizontal。
- 光滑无纹理地面 ARCore 本身慢且漂 → 换有纹地面测试；Reset 解决不了瓷砖。
- `AddComponent<ARAnchor>` 在 6.x 不可靠 → 不要用。
- Input System Only（工程 `activeInputHandler=1`）下用 `Touchscreen.current`；若真机完全点不了，再改 Both，本环先加 Toast 区分「没按下」和「按下但没命中」。

成功标准（代码）：扫描中看到蓝即可点放；标记走 `TryAddAnchorAsync`；锁地后仍 Horizontal；暂停关掉两个 Manager。真机出包仍由用户在 inspect-ar 做。

---

### 开工话术

```text
@multi-window_M
我是 M6 窗口。请读 D:\Cursor_projectt\测试考题\docs\MODULE-REGISTRY.md 中 M6，以及 docs/FIX-PLAN.md Phase 8 卡点 N。
按斥候 → 主力 → 搜剿执行；同一卡点最多 4 次。
只改 mobile/。不要执行 InspectAR/Setup Project。不要打开游戏。
环#1：XR Origin 补 ARAnchorManager（运行时+Verify）；检测保持 Horizontal，锁地后禁止 DetectionMode.None；暂停才关 Plane/Raycast；放宽面积/法线、快扫窗约 8 秒；最低平面加滞回。
环#2：放置改 TryAddAnchorAsync，立方体做世界锚子物体，不要 AttachAnchor；静态网格不要挂在 ARPlane 上。
环#3：看到蓝就能点（不要等 m_FloorLocked）；射线命中合格水平面即可；UI 别挡住 AR 窗；失败用 Toast 说明原因。不要自动 ARSession.Reset。
保留任务 List、底栏、关键词 High、ColorTint。不要自己把 Registry 标成 done。
完成后说：M6 已完成，请主导窗口查收。
```

---

## Phase 7（已归档）— 锁定地面网格 + 标题关键词自动 High

磁盘核对（M1 斥候）：蓝网格仍绑在 **活的** `ARPlaneMeshVisualizer` 上，ARCore 每帧改平面姿态/边界，所以会漂。`RefreshFloorVisibility` 还可能换「最低那块」。立方体虽 `AttachAnchor`，跟踪一抖、平面一换，看起来就跟着晃。标题框目前只 `WriteEditorToSelected`，没有关键词规则。

本环 **只开 M6**。不要改 `backend/` `admin/`。保留 Phase6 布局、任务 List、Toast、历史 PUT。

| 模块 | 本环 | 职责 |
|------|------|------|
| M1 | 进行中 | 本文 / Registry、查收、同步 inspect-ar |
| M6 | done | 地面锁定冻结网格；标题关键词 → High + 描述追加 |

---

### 卡点 L — 蓝平面锁定、减轻立方体跟着漂（仅 M6，`mobile/`）

根因（给主力）：不是「锚点完全没做」，是 **视觉和检测还在用持续更新的平面**。

必须做：

1. **稳定后再锁**：同一 `ARPlane` 连续满足 `IsStableFloor` 约 **0.8～1.0 秒**（或连续 N 帧）才锁定。锁定后：
   - 记住 `m_LockedFloor`
   - **禁止**再把 `m_VisibleFloor` 换成另一块
   - 关掉这块的 `ARPlaneMeshVisualizer`（以及其它平面的可视化）
   - 复制一份 **静态网格**（`MeshFilter` + 半透明蓝材质）贴在锁定时的世界姿态上，之后 **不要**每帧跟 `ARPlane.center` 走
2. **锁后停止扩面**：`requestedDetectionMode = None` 或暂时 `ARPlaneManager.enabled = false`（以 6.3 仍能对已有平面 raycast 为准；若 raycast 失效则保持 manager 开、只关 visualizer + 不再 `RefreshFloorVisibility` 换块）。
3. 用户再点 **开始扫描**（为第二个位置）：解锁、毁掉静态网格、重新侦测，锁下一块后再放点。
4. **暂停扫描**：静态网格可留或藏，但不要让活 visualizer 再刷。
5. 立方体继续 `AttachAnchor` 并作为 Anchor 子物体；锁地后不要每帧改 cube 世界坐标。

成功标准（代码）：锁定后场景里不应再启用活的 `ARPlaneMeshVisualizer`；再扫才重新出活网格。

---

### 卡点 M — 标题关键词强制 High（仅 M6）

在标题 `InputField.onValueChanged` 里（可与 `WriteEditorToSelected` 同监听，先关键词再写回 Draft）：

关键词（包含即可，建议 `IndexOf` 忽略大小写对中文无影响）：`漏水` `裂缝` `冒烟` `异响` `脱落`。

命中时：

- 当前标记 `priority = "high"`，刷新优先级按钮为 High 选中
- **禁用**三个优先级按钮（`interactable = false`），不让改低
- 描述追加一行 `【系统提示】请尽快安排处理`：若描述里 **已经有这句** 则不重复；若用户已有其它文字，只在末尾换行追加，**禁止清空覆盖**

未命中（删光关键词）时：

- 三个按钮 `interactable = true`
- **不要**改回命中前的优先级（保持 High 或用户之后再改）
- 不必删除已追加的系统提示

换选中标记时按该条标题重新跑一遍规则。

---

### 开工话术

```text
@multi-window_M
我是 M6 窗口。请读 D:\Cursor_projectt\测试考题\docs\MODULE-REGISTRY.md 中 M6，以及 docs/FIX-PLAN.md Phase 7 卡点 L、M。
按斥候 → 主力 → 搜剿执行；同一卡点最多 4 次。
只改 mobile/。不要执行 InspectAR/Setup Project。不要打开游戏。
环#1：地面稳定约 1 秒后锁定；关掉活的 ARPlaneMeshVisualizer，改用静态蓝网格；禁止中途换「最低平面」；再点开始扫描才解锁重侦测。立方体保持 AttachAnchor 子物体。
环#2：标题 OnValueChanged：含 漏水/裂缝/冒烟/异响/脱落 则强制 high、禁用优先级按钮，描述不覆盖只追加「【系统提示】请尽快安排处理」（已有这句则不重复）；关键词删光后恢复按钮可点，不改回原优先级。
保留 Phase6 底栏/抽屉/Toast。不要自己把 Registry 标成 done。
完成后说：M6 已完成，请主导窗口查收。
```

---

## Phase 6（已归档）— Unity 让出 AR 中央画面 + 底栏按钮 + Toast

磁盘核对（M1 斥候 2026-08-28）：点「新建任务」后 `BuildTaskRoot` 把 **不透明** `MarkerListCard` 铺在 `anchorMin.y=0.40`～`0.82`（正中央），`EditorCard` 再占 `0～0.40`。`CreateCard` 用实心 `BgWarmGray`/`BgCoolGray`，Overlay 下完全挡住摄像头。扫描钮在顶部细条，底栏看起来空，中间几乎看不到蓝网格和立方体。

本环 **只开 M6**。不要改 `backend/` `admin/`。保留任务 List、逐条 POST、历史 PUT、主次危险色、`ColorTint`、地面过滤。

| 模块 | 本环 | 职责 |
|------|------|------|
| M1 | 进行中 | 本文 / Registry、查收、同步 inspect-ar |
| M6 | pending | 中央透明；底栏两行按钮；Toast 2–3s；横竖屏锚点 |

本任务按循环渐进执行：每环一个目标；斥候 → 主力 → 搜剿；同卡点最多 4 次。

---

### 卡点 K — AR 中央不被挡、底栏操作、Toast（仅 M6，`mobile/`）

禁止 `InspectAR/Setup Project`。不要打开游戏。源码 `测试考题/mobile`。

**Canvas（沿用）**

- `Screen Space Overlay`，`pixelPerfect = false`
- 竖屏：`referenceResolution = 1080×1920`，`matchWidthOrHeight = 1`
- 横屏：`referenceResolution = 1920×1080`，`matchWidthOrHeight = 0`（在 `OnRectTransformDimensionsChange` 或每帧根据 `Screen.width > Screen.height` 切换）
- 顶/底条相对 `Screen.safeArea` 留出刘海（至少顶底各加 safe padding，不要按钮钻到 Home 条下）

**竖屏分区（归一化锚点，高 = 1 为顶）**

| 区 | anchorMin.y | anchorMax.y | 内容 |
|----|-------------|-------------|------|
| 顶栏 | **0.92** | **1.00** | 半透明：用户名 + 任务状态（暂停中/扫描中/未建任务）。**不要**把「新建任务」放这里占中间 |
| AR 窗 | **0.22** | **0.92** | **禁止**全宽不透明 Image。扫描命中走这里。高度 ≥ **70%**（0.70）；有底栏时中央可见高度仍须 ≥ **60%** |
| 底栏 | **0.00** | **0.22** | 功能按钮，见下 |

横屏：顶栏改左上窄条或顶 10%；底栏改贴底全宽，高度约 0.18～0.22；**中央 60%+ 仍不准铺实心卡**。

**玻璃（假 Acrylic）**

Overlay 做不了真高斯模糊摄像头。硬性：所有挡画面的面板 `Image.color.a` **≤ 0.55**（透明 ≥ 45%，满足「至少 40%」）。建议两层：底层 `BgCoolGray` a=0.22，上层 `BgWarmGray` a=0.40。禁止 `#FFFFFF` 且 a=1。列表/编辑若必须出现：只能 **右侧抽屉**（例如 `anchorMin=(0.62,0.24)` `anchorMax=(0.98,0.90)`）或 **从底栏向上展开的 sheet**（展开后最高不超过 `y=0.50`，中央上半仍露摄像头）。默认收起，只留底栏一行「标记 n」。

**底栏按钮（紧凑，1～2 行，图标可省略但文字要短）**

建议两行，每行高约 72～88px，间距 8px：

1. `新建任务`（Primary）｜`开始扫描`（Primary）｜`暂停扫描`（Secondary）
2. `历史`（Secondary）｜`标记`（Secondary，开关列表抽屉/sheet）｜`提交`（Primary）

放弃任务放标记抽屉里（Danger），不要占中央。`raycastTarget` 仅按钮和抽屉；AR 窗空白处必须能点到平面。

**Toast**

- 独立小条：竖屏锚在 AR 窗 **右上**（或底栏上方居中），`anchorMin≈(0.15,0.84)` `anchorMax≈(0.98,0.91)` 量级，不要盖住底栏按钮
- 文案：提交成功/失败、请先扫描平面、请先登录 等（接现有 `SetStatus`）
- 显示 **2.5 秒**（允许 2～3）后淡出关闭；新消息重置计时
- 错误可用 Danger 底 a=0.55；成功用 Primary 底 a=0.55；白字/深字保持可读

**环 #1**：拆掉中央 `MarkerListCard` 实心铺满；AR 窗无全宽实心板。  
**环 #2**：底栏两行 + Toast 自动消失。  
**环 #3**：横屏 match 切换 + safeArea。

**成功标准（代码）**：任务模式下中央 y=0.22～0.92 没有 a>0.55 的全宽 Image；底栏含新建/扫描/暂停/历史/提交；`SetStatus` 走 Toast 且 2～3s 消失。真机出包仍由用户在 inspect-ar 做。

---

### 开工话术（复制到新 Cursor 窗口；工作区 `D:\Cursor_projectt\测试考题`）

**M6 — Phase6 布局**

```text
@multi-window_M
我是 M6 窗口。请读 D:\Cursor_projectt\测试考题\docs\MODULE-REGISTRY.md 中 M6 章节，以及 docs/FIX-PLAN.md Phase 6 卡点 K。
按斥候 → 主力 → 搜剿执行；同一卡点最多 4 次。
只改 mobile/。不要执行 InspectAR/Setup Project。不要打开游戏。
环#1：点新建任务后禁止在屏幕中央铺不透明列表面板；列表/编辑改为右侧半透明抽屉或底部 sheet（Image.a≤0.55），中间 AR 可见高度≥60%。
环#2：顶栏只显示用户名和任务状态；底栏两行紧凑按钮（新建任务、开始/暂停扫描、历史、标记、提交）；SetStatus 改为 Toast，2～3 秒自动消失，不挡底栏。
环#3：竖屏 1080×1920 match=1，横屏 1920×1080 match=0；顶底避开 safeArea。
保留 DraftMarker、确认发送 POST、历史 PUT、主次危险色、ColorTint、地面过滤。
不要自己把 Registry 标成 done。
完成后说：M6 已完成，请主导窗口查收。
```

---

## Phase 5（已归档）— 按钮主次色、独立 XYZ、多标记任务、WS 推送、历史编辑

磁盘核对（M1 斥候 2026-08-28）：

| 现象 | 根因（已在盘上） |
|------|------------------|
| 手机 UI 太白、按钮分不清 | 面板 `MistWhite` α≈0.58；按钮几乎都是 Sage/FogBlue + 白色 ColorTint，无主次/危险档 |
| 多点后仍像只有一个坐标 | **库表已有独立 `pos_x/pos_y/pos_z`**，JSON 已是 `position.{x,y,z}`。Unity 提交走 `LatestUnsubmitted()` **一次只 POST 一条**；表单/状态区只跟最近一个点。React 详情把 xyz **挤成一行字符串** |
| 一次只能管一个点 | 没有「任务」容器；没有本地列表增删改；没有提交前总确认 |
| 网页要手动刷新 | **没有** `gorilla/websocket`、没有 `/ws` |
| 手机历史/编辑 | **没有** 历史面板；后端 **没有 PUT**（只有 PATCH 改 status） |

一条 Issue = 一个标记。多标记任务 = Unity 本地 List，确认发送后 **连续多次** `POST /api/issues`。不要给 Issue 加 `markers[]`。

| 模块 | 本环 | 职责 |
|------|------|------|
| M1 | 进行中 | 只写本文 / Registry / API、查收、联调、同步 inspect-ar、重启 **8080**/5174 |
| M5 | pending | PUT 改标题优先级；WS Hub；创建/更新/删除广播；CORS 加 PUT。坐标列已存在则 **禁止重建表** |
| M4 | pending | 详情三行 X/Y/Z；WebSocket 前置插入/更新/删除；断线重连；开发默认连 **8080** |
| M6 | pending | 主次危险按钮；新建任务 + 本地多标记 CRUD；提交确认后才 POST；历史面板 GET + PUT |

**推荐开窗顺序**：先开 **M5**。M5 主力已按 API.md 动手后，**M4 与 M6 可并行**。

本任务按循环渐进执行：
- 每环只做一个目标，必须有成功标准、证据、存档点
- 环内角色：斥候 → 主力 → 搜剿；fail 则主力↔搜剿
- 同一卡点签名最多 4 次；第 4 次仍 fail 必须硬停并写卡点升级报告
- 未升级前禁止继续对同一卡点盲改

---

### 卡点 H — 后端 PUT + 坐标列确认 + WebSocket（仅 M5，`backend/`）

允许超过 5 个文件（新 `internal/ws`）。建议：

- `internal/store`：`UpdateFields(id, title, desc, priority, updatedAt)`；**斥候先 `PRAGMA table_info`**：已有 `pos_x/y/z` 则不要 `ALTER`、不要删库
- `internal/ws`：Hub（register / unregister / broadcast）、Client 读泵可丢弃（本环服务端不收业务帧）
- `internal/api`：`PUT /api/issues/{id}`；`GET /ws`；CORS 方法含 `PUT`；`create`/`patch`/`put`/`delete` 成功后 `hub.Broadcast`
- `go.mod`：`github.com/gorilla/websocket`

`/ws` **不要**套现有 `requireAuth` HTTP 包装（升级要 Hijack）。在 Upgrade 前从 `?token=` 调 `auth.Parse`；失败 401 JSON。  
`CheckOrigin` 复用现有 localhost / 127.0.0.1 任意端口规则。

**环 #1 唯一目标**：PUT + CORS PUT；两次 POST 不同 xyz，GET 两条 `position.x/y/z` 互不覆盖。  
**环 #2 唯一目标**：`/ws` 鉴权 + POST 201 后所有连接收到 `issue.created`（完整 JSON）。顺带 PATCH/PUT → `issue.updated`，DELETE → `issue.deleted`。

**成功标准（须贴真实命令输出，打 8080）**：

1. inspector 两次 `POST`，body 的 `position` 分别为 `{0.1,0.2,0.3}` 与 `{9,8,7}` → 两次 201，GET 列表两条 xyz 都在且不相等
2. inspector `PUT` 自己的 `{ "title":"改过","priority":"low" }` → 200，坐标不变
3. inspector `PUT` 别人的 → 403；viewer PUT → 403；admin PUT 任意 → 200
4. 无 token `GET /ws` → 401
5. 合法 token 连上 `/ws`，再 POST 一条，WS 收到 `type=issue.created` 且含新 id
6. CORS OPTIONS 含 `PUT`（Origin `http://localhost:5174`）

无法重启已在跑的进程时，写明「请 M1 重启 8080」。不要改 `admin/` `mobile/`。

---

### 卡点 I — React 三行坐标 + WebSocket（仅 M4，`admin/`）

开发默认 **`VITE_API_BASE=http://127.0.0.1:8080`**（`.env.development`）。不要写死 8081。

**环 #1**：详情弹窗坐标改成三行独立：`X 坐标：…` / `Y 坐标：…` / `Z 坐标：…`（`formatCoord` 可沿用）。列表默认仍不展示坐标。`npm run build` 过。

**环 #2**：新增 hook（建议 `admin/src/useIssuesSocket.ts`）：

- `useEffect` 在已登录时连接 `ws://` + 由 `apiBase()` 把 `http`→`ws` + `/ws?token=`
- 收到 `issue.created`：若 id 不在列表则 **unshift 到最前**，禁止整表覆盖
- 收到 `issue.updated`：按 id 替换那一项
- 收到 `issue.deleted`：按 id 删掉
- 断线且仍登录：指数退避重连 1s → 2s → 4s … 上限 30s；`logout`/401 时关连接并取消重连
- 页面卸载 `close`

不要另开浏览器给用户看。不要改 `backend/`。

**成功标准**：`npm run build`；自测说明三行文案与重连策略。真浏览器由 M1 查收。

---

### 卡点 J — Unity 主次 UI + 任务多标记 + 历史 PUT（仅 M6，`mobile/`）

禁止 `InspectAR/Setup Project`。不要打开游戏。源码 `测试考题/mobile`。  
保留地面过滤、ARAnchor、快速扫描窗、`ColorForIssue` / `ApplyMarkerColor`、`inspect.jwt`、`ColorTint`（不要 `Transition.Fade`）。

允许拆文件（仍只在 `mobile/`），建议不超过：`InspectARApp.cs`、`InspectUiTheme.cs`、`InspectTaskSession.cs`、`InspectHistoryPanel.cs`。超过 5 个文件先停下说明。

#### 环 #1 — 视觉层次（替换「全白同款按钮」）

AR 相机画面保持透明 Overlay，**不要**用纯白铺满屏幕。表单/列表卡片用浅灰蓝或暖灰，禁止面板填 `#FFFFFF`。

| 档 | 用途 | 底 | 字 |
|----|------|----|----|
| Primary | 新建任务、开始扫描、提交任务、确认发送、保存 | `#2C4A6E` 深蓝填充 | `#F7F5F2` |
| Secondary | 取消、返回、暂停扫描、历史记录、关闭 | `#D4D0C8` 浅灰填充 | `#3F3E3C`；可加 1px `#B8B4AC` 软边 |
| Danger | 删除标记、放弃任务 | `#E8D0CC` 浅红底（禁止纯红填充） | `#8B3A3A` |

```csharp
ColorUtility.TryParseHtmlString("#D6DCE4", out var BgCoolGray);   // 浅灰蓝底
ColorUtility.TryParseHtmlString("#E6E2DC", out var BgWarmGray);   // 暖灰卡片
ColorUtility.TryParseHtmlString("#2C4A6E", out var Primary);
ColorUtility.TryParseHtmlString("#F7F5F2", out var OnPrimary);
ColorUtility.TryParseHtmlString("#D4D0C8", out var Secondary);
ColorUtility.TryParseHtmlString("#3F3E3C", out var OnSecondary);
ColorUtility.TryParseHtmlString("#E8D0CC", out var Danger);
ColorUtility.TryParseHtmlString("#8B3A3A", out var OnDanger);
ColorUtility.TryParseHtmlString("#C5C1BA", out var Divider);
```

必须实现（名称可同可近）：`StylePrimaryButton` / `StyleSecondaryButton` / `StyleDangerButton`。  
按钮间距 ≥ **16px**；`Shadow` distance `(0,-3)` alpha ≈0.22；圆角 ≥10（沿用九宫）。  
不同功能区用 Divider 或独立卡片（登录卡 / 扫描卡 / 标记列表卡 / 提交卡）。  
立方体业务色仍按 API.md 绿黄红灰，不要改成按钮主题色。  
运行时生成即可；不必强求落盘 Prefab（`Style*` 即预设）。`Button.transition = ColorTint`，`fadeDuration = 0.15f`。

#### 环 #2 — 新建任务、本地多标记、提交前确认

数据结构（字段名可近，语义必须有）：

```csharp
sealed class DraftMarker {
    public string localId;      // Guid
    public GameObject cube;
    public ARAnchor anchor;
    public Vector3 position;    // 放置时立刻 cube.transform.position，xyz 分开存，禁止覆盖别人
    public string title, description, priority;
    public bool submitted;
    public string issueId;      // POST 成功后才有
}
// 当前未提交任务：List<DraftMarker> 仅内存；退出 App 不写 PlayerPrefs
```

流程：

1. 启动默认暂停。主界面有 **新建任务**（Primary）。未建任务时不能扫描放置。
2. 点新建任务进入任务模式，才出现开始/暂停扫描。可在不同平面 **连续放置** 多个标记；每放一个就把 `transform.position` 写入该 `DraftMarker`，列表增加一行（标题可先默认「标记 n」）。
3. 每行有 Danger **删除**（小叉或「删除」）：只删本地 List + Destroy 对应物体，不请求后端。
4. 点列表项选中，编辑标题/描述/优先级（提交前任意改）。改优先级同步该立方体 `ApplyMarkerColor`。
5. 所有增删改只在内存。**提交任务**（Primary）弹出确认：标记数量 + 每条标题和 `X/Y/Z` 三行摘要。**确认发送** 才对未提交项依次 `POST`（每条独立 `position.x/y/z`）。**取消** 退回继续改。
6. 全部 POST 成功：本任务标记锁定，不可再改；可留在本次进程的本地「已提交」查看。中途失败：已成功的锁定，失败及未发的仍可改，提示哪一条失败。
7. 删掉「只提交 LatestUnsubmitted 一条、全局一个表单绑死一个点」的路径。

#### 环 #3 — 历史记录 + PUT

左上或底部 **历史记录**（Secondary）：滑出 ScrollView，`GET /api/issues`，每行标题 + 优先级色标签 + 状态。  
点一项展开：完整描述 + **X/Y/Z 三行**。  
**编辑**：改标题 + 优先级下拉 `low/medium/high`，保存 `PUT /api/issues/{id}`，成功刷新列表并提示。  
无 Token 或 401：提示并回到登录。viewer 调 PUT 会 403，按钮按角色隐藏或提示无权限（inspector 只能改自己的）。

**成功标准（代码）**：主次危险三套按钮色；任务 List 多点 xyz 不互相覆盖；确认后才 POST；历史 GET/PUT。真机出包由用户在 inspect-ar 做。

---

### 开工话术（复制到新 Cursor 窗口；工作区 `D:\Cursor_projectt\测试考题`）

**M5 — Phase5 PUT 与 WebSocket（先开）**

```text
@multi-window_M
我是 M5 窗口。请读 D:\Cursor_projectt\测试考题\docs\MODULE-REGISTRY.md 中 M5 章节，以及 docs/API.md、docs/FIX-PLAN.md Phase 5 卡点 H。
按斥候 → 主力 → 搜剿执行；同一卡点最多 4 次。
只改 backend/。不要打开游戏。
环#1：斥候确认 issues 表已有 pos_x/pos_y/pos_z 则不要改表结构；实现 PUT /api/issues/{id}（标题/描述/优先级）；CORS 允许 PUT。两次 POST 不同 xyz 必须都能在 GET 里独立读到。
环#2：gorilla/websocket；GET /ws?token=；Hub 在线列表；POST 成功广播 issue.created（完整 Issue）；PUT/PATCH 广播 issue.updated；DELETE 广播 issue.deleted。CheckOrigin 与现有 localhost 规则一致。
本机端口 8080。结束必须交关键 diff + 真实 curl/WS 输出。
不要自己把 Registry 标成 done。
完成后说：M5 已完成，请主导窗口查收。
```

**M4 — Phase5 详情坐标与 WS（M5 主力开工后可并行）**

```text
@multi-window_M
我是 M4 窗口。请读 MODULE-REGISTRY.md 中 M4，以及 docs/API.md、docs/FIX-PLAN.md Phase 5 卡点 I。
按斥候 → 主力 → 搜剿执行；同一卡点最多 4 次。
只改 admin/。不要打开游戏，不要另开浏览器给用户看。
环#1：查看详情把坐标改成三行：「X 坐标：」「Y 坐标：」「Z 坐标：」。.env.development 为 VITE_API_BASE=http://127.0.0.1:8080。
环#2：useEffect 连 ws://{apiBase 改 ws}/ws?token=；issue.created 插入列表最前（不要整表覆盖）；updated 替换；deleted 移除；断线指数退避重连，登出取消。
对照 API.md，禁止自造字段。npm run build 必须通过。
不要自己把 Registry 标成 done。
完成后说：M4 已完成，请主导窗口查收。
```

**M6 — Phase5 任务管理与历史（可与 M4 并行）**

```text
@multi-window_M
我是 M6 窗口。请读 MODULE-REGISTRY.md 中 M6，以及 docs/API.md、docs/FIX-PLAN.md Phase 5 卡点 J。
按斥候 → 主力 → 搜剿执行；同一卡点最多 4 次。
只改 mobile/。不要执行 InspectAR/Setup Project。不要打开游戏。
环#1：主次危险三套按钮色（深蓝填充白字 / 浅灰 / 浅红字），浅灰蓝或暖灰底，禁止纯白铺满；按钮间距与卡片分割。ColorTint 不要 Transition.Fade。
环#2：新建任务后才能扫描放置；List<DraftMarker> 每点独立 xyz；列表可删、可选中编辑标题描述优先级；全部仅内存；提交任务弹出数量和摘要，确认发送才逐条 POST。
环#3：历史记录 ScrollView，GET /api/issues；展开描述与 X/Y/Z 三行；编辑后 PUT /api/issues/{id}；401 回登录。
保留 ColorForIssue / ApplyMarkerColor 与地面过滤。
不要自己把 Registry 标成 done。
完成后说：M6 已完成，请主导窗口查收。
```

---

## Phase 4（已归档）— 登录权限 + 多点标注 + 管理端详情删除 + Unity 柔和 UI

磁盘核对（M1 斥候 2026-08-27）：**没有 users 表、没有 JWT、没有登录页、没有 DELETE。**  
需求里写的「配合已实现的登录系统」= 本环 M5 先落地契约，M4/M6 按 `docs/API.md` 接入，不是盘上已有功能。

需求 2 与「功能 2」合并为 **M4 一个模块**（同一套列表，不要做两份 UI）。

| 模块 | 本环 | 职责 |
|------|------|------|
| M1 | 进行中 | 只写本文 / Registry / API、查收、联调、同步 inspect-ar |
| M5 | in_progress | users + 登录 + JWT 中间件 + Issue 提交人字段 + DELETE + 权限 |
| M4 | in_progress | 登录页、Token、列表展示提交人/时间/描述、筛选搜索、详情弹窗、删除 |
| M6 | in_progress | 加速扫描、确认框、多点标注、登录带 Token、莫兰迪毛玻璃 UI |

**推荐开窗顺序**：先开 **M5**（契约源头）。M5 主力已按 API.md 动手后，**M4 与 M6 可并行**（各守目录，对照 API.md，禁止猜字段）。

本任务按循环渐进执行：
- 每环只做一个目标，必须有成功标准、证据、存档点
- 环内角色：斥候 → 主力 → 搜剿；fail 则主力↔搜剿
- 同一卡点签名最多 4 次；第 4 次仍 fail 必须硬停并写卡点升级报告
- 未升级前禁止继续对同一卡点盲改

---

### 卡点 E — 后端登录、JWT、提交人、删除（仅 M5，`backend/`）

允许超过 5 个文件：这是新子系统。建议拆分：

- `internal/store`：`users` 表；`issues` 增加 `submitter_id` / `submitter_name`（旧行迁移：空 id，`submitter_name='未知'`）
- `internal/auth`：bcrypt、签发/校验 JWT、从 `Authorization` 取 claims
- `internal/api`：登录路由、中间件、DELETE、创建时写入提交人；CORS 增加 `DELETE` 与 `Authorization`

**环 #1 唯一目标**：登录 + 中间件挡住无 Token 的 `/api/issues`。  
**环 #2 唯一目标**：创建写入提交人；PATCH 仅 admin；DELETE 仅 admin 或本人。

种子用户与错误码见 `docs/API.md`。依赖可用 `golang-jwt/jwt/v5` 与 `golang.org/x/crypto/bcrypt`。

**成功标准（须贴真实命令输出）**：

1. `POST /api/auth/login` inspector 得 token
2. 无 Token `GET /api/issues` → 401
3. inspector `POST /api/issues` → 201，body 含 `submitterName=inspector`
4. inspector `PATCH` 改状态 → 403
5. admin `PATCH` → 200
6. viewer `POST` → 403
7. inspector 删自己的 → 204；删别人的 → 403
8. admin 删任意 → 204
9. CORS：`Origin: http://localhost:5174` + 带 `Authorization` 的 OPTIONS 含允许头

不要改 `admin/` `mobile/`。无法重启已在跑的 8081 时，报告写明「请 M1 重启」。

---

### 卡点 F — React 登录、列表信息、筛选、详情、删除（仅 M4，`admin/`）

无路由库也可以：未登录只渲染登录页；`localStorage` 键 **`inspect.token`** 与 **`inspect.user`**（JSON：`id/username/role`）。

所有 `fetch`（含 PATCH/DELETE/GET）必须带 `Authorization: Bearer ${token}`。401 清存储并回到登录页。

**环 #1**：登录页 + 带 Token 拉列表。  
**环 #2**：列表列与筛选、详情弹窗、删除。

列表每行必须有（不要藏）：

- 标题、优先级、状态（保留）
- **提交人姓名**（`submitterName`）
- **提交时间**（`createdAt`，本地可读格式即可）
- **描述**：直接显示；超过约 40 字截断加 `…`

顶部筛选（客户端过滤即可，不必等后端 query）：

- 状态下拉：全部 / 待处理 / 进行中 / 已解决（值 `""` / `open` / `in_progress` / `resolved`）
- 优先级下拉：全部 / 高 / 中 / 低
- 提交人姓名输入：模糊包含（大小写不敏感）

每行：

- **查看详情**：弹窗展示完整描述、精确 `position.x/y/z`、`submitterId`、`submitterName`、`createdAt`/`updatedAt`
- **删除**：确认框后 `DELETE /api/issues/{id}`，成功刷新列表
- 改状态按钮：**仅 `role === "admin"` 显示**；inspector/viewer 不显示（避免点出 403）
- 删除按钮：`admin` 全部可见；`inspector` 仅当 `submitterId === 当前 user.id`；`viewer` 不显示

`api.ts` 增加 `login` / `deleteIssue`；`Issue` 类型补 `submitterId` `submitterName`。`npm run build` 必须过。不要改 `backend/`。不要另开浏览器给用户看。

**成功标准**：`npm run build`；自测说明登录失败文案、无 Token 不进列表、筛选与截断逻辑。真浏览器点选留给 M1 查收。

---

### 卡点 G — Unity：加速扫描、确认框、多点标注、登录、柔和 UI（仅 M6，`mobile/`）

禁止执行会重建场景的 `InspectAR/Setup Project`。不要打开游戏。源码以 `测试考题/mobile` 为准（inspect-ar 由 M1 查收后同步）。

保留 Phase2 地面过滤与 ARAnchor、Phase3 颜色方法 `ColorForIssue` / `ApplyMarkerColor`。

#### 环 #1 — 扫描速度 + 多点标注

根因（磁盘）：`Update` 里 `if (m_HasMarker) return`，所以一次只能放一个；确认扫描后没有「确认锁定」流程。

必须改成：

1. **默认暂停**（已有，勿倒退）。
2. **开始扫描**：立刻 `ARPlaneManager` + `ARRaycastManager` `enabled = true`；`requestedDetectionMode = Horizontal`。进入 **1.0 秒快速窗**：`MinFloorArea` 临时降为 **0.08**（法线点积与 HorizontalUp 不变），让蓝网格尽快出现；1 秒后恢复 **0.25**。开始时 `s_Hits.Clear()`。
3. **仅扫描中**可点击平面放置；暂停时点击无效。
4. 放置后弹出 **确认框**（确认 / 取消）。取消：销毁刚放的未锁定立方体，不暂停。确认：`SetScanning(false)`、锁定该标记（加入已锁定列表）、`s_Hits.Clear()`、不要再把「已有标记」当成禁止下一次放置的条件。
5. 用户可再次「开始扫描」打 **第二、第三…** 个平面。每个标记独立坐标、独立 GameObject / Anchor。提交时使用 **最近一次已确认且尚未提交** 的标记 xyz；提交成功后记住该 id，其它已确认未提交的标记仍留在场景，可继续填表提交下一条。
6. 删掉「全局只能有一个 `m_Marker` 且有标记就不再射线」的逻辑。

#### 环 #2 — 登录（依赖 API.md）

启动先出登录面板（用户名、密码、登录按钮）。`POST {base}/api/auth/login`，成功把 token 存 `PlayerPrefs` 键 `inspect.jwt`。之后 GET/POST 带 `Authorization`。401 清 token 回登录。无 token 禁止上报。

#### 环 #3 — 柔和 UI（UGUI，代码生成即可，不必新场景资源也可运行时画圆角贴图）

视觉：毛玻璃假 Acrylic、大圆角 ≥10px、莫兰迪低饱和、柔和投影、细体/常规体、按钮 Fade 过渡。

**Canvas**

- `Screen Space Overlay`；`CanvasScaler`：`Scale With Screen Size`，参考分辨率 `1080×1920`，`matchWidthOrHeight = 1`
- 不要 `pixelPerfect`
- 面板/按钮 `Image.raycastTarget` 该挡点击的才开

**切图建议**（若做 Sprite；也允许运行时 `Texture2D` 画圆角再 `Sprite.Create`）

- 一张白底圆角矩形 **64×64**，圆角 **16px**，九宫 `border = 16,16,16,16`
- Import：Sprite (2D and UI)，Mesh Type Full Rect，Pixels Per Unit 100
- 真高斯模糊 AR 画面在 Overlay 下不做；用半透明叠层假 Acrylic（见下）

**ColorUtility / Color（面板，禁止按钮用纯红纯绿）**

```csharp
ColorUtility.TryParseHtmlString("#8BA7B8", out var FogBlue);     // 雾霾蓝  (0.545f, 0.655f, 0.722f)
ColorUtility.TryParseHtmlString("#F3F0EA", out var MistWhite);   // 米白    (0.953f, 0.941f, 0.918f)
ColorUtility.TryParseHtmlString("#C8C4BE", out var WarmGray);    // 浅灰    (0.784f, 0.769f, 0.745f)
ColorUtility.TryParseHtmlString("#D7C2BE", out var DustyRose);   // 淡藕粉  (0.843f, 0.761f, 0.745f)
ColorUtility.TryParseHtmlString("#4A4A48", out var Ink);         // 正文
ColorUtility.TryParseHtmlString("#7A7874", out var InkMuted);    // 次要文字
ColorUtility.TryParseHtmlString("#A3B4A8", out var Sage);        // 主按钮
```

- 毛玻璃面板：`MistWhite` **alpha 0.55~0.62**，底再叠一层 `FogBlue` alpha **0.18**
- 按钮底：`Sage` 或 `FogBlue`，alpha **0.88**；禁用 `WarmGray` alpha 0.45
- 文字：`Ink`，`FontStyle.Normal`（禁止 Bold）
- 投影：`Shadow` 或模拟，`effectColor = (0.29, 0.29, 0.28, 0.18)`，distance `(0, -3)`，不要 1px 硬边框（`Outline` 关掉）
- `Button.transition = Fade`，`colors.fadeDuration = 0.15f`；Normal 1、Highlighted 0.92、Pressed 0.80、Disabled 0.4（改 alpha，不要瞬间换纯色）
- 圆角：Image sprite 九宫或运行时圆角，视觉半径 **≥ 10px**（建议 16）

立方体业务色（优先级/状态）仍用 API.md 的绿/黄/红/灰，不要改成莫兰迪以免管理端对不上。

**成功标准（代码）**：默认暂停；开始后 1s 内面积阈值放宽；确认后暂停且可再扫第二个点；登录后请求带 Token；UI 无粗体硬边框。真机出包由用户在 inspect-ar 做。

---

### 开工话术（复制到新 Cursor 窗口；工作区 `D:\Cursor_projectt\测试考题`）

**M5 — Phase4 登录与权限（先开）**

```text
@multi-window_M
我是 M5 窗口。请读 D:\Cursor_projectt\测试考题\docs\MODULE-REGISTRY.md 中 M5 章节，以及 docs/API.md、docs/FIX-PLAN.md Phase 4 卡点 E。
按斥候 → 主力 → 搜剿执行；同一卡点最多 4 次。
只改 backend/。不要打开游戏。
环#1：users + POST /api/auth/login + JWT 中间件（无 Token 访问 /api/issues 必须 401）。
环#2：Issue 增加 submitterId/submitterName；PATCH 仅 admin；DELETE 仅 admin 或提交人本人；CORS 允许 DELETE 与 Authorization。
种子用户见 API.md。结束必须交关键 diff + 真实 curl 输出。
不要自己把 Registry 标成 done。
完成后说：M5 已完成，请主导窗口查收。
```

**M4 — Phase4 管理端（M5 主力开工后可并行）**

```text
@multi-window_M
我是 M4 窗口。请读 MODULE-REGISTRY.md 中 M4，以及 docs/API.md、docs/FIX-PLAN.md Phase 4 卡点 F。
按斥候 → 主力 → 搜剿执行；同一卡点最多 4 次。
只改 admin/。不要打开游戏，不要另开浏览器给用户看。
环#1：登录页；localStorage inspect.token / inspect.user；所有请求带 Authorization。
环#2：列表展示提交人姓名、提交时间、描述（过长截断…）；顶部状态/优先级下拉 + 提交人模糊搜索；查看详情弹窗（完整描述 + xyz + 提交人）；删除确认后 DELETE 并刷新；按 role 隐藏改状态/删除。
对照 API.md，禁止自造字段。npm run build 必须通过。
不要自己把 Registry 标成 done。
完成后说：M4 已完成，请主导窗口查收。
```

**M6 — Phase4 AR 多点与 UI（可与 M4 并行）**

```text
@multi-window_M
我是 M6 窗口。请读 MODULE-REGISTRY.md 中 M6，以及 docs/API.md、docs/FIX-PLAN.md Phase 4 卡点 G。
按斥候 → 主力 → 搜剿执行；同一卡点最多 4 次。
只改 mobile/。不要执行 InspectAR/Setup Project。不要打开游戏。
环#1：开始扫描强制激活 Plane/Raycast；快速窗 1s 内 MinFloorArea=0.08；放置后确认框；确认则暂停并锁定，清射线；可再扫放第二个标记，各坐标独立。
环#2：登录面板 + PlayerPrefs inspect.jwt；GET/POST 带 Authorization。
环#3：UGUI 莫兰迪毛玻璃、圆角≥10、Fade、细体；色号按 FIX-PLAN 卡点 G。保留 ColorForIssue / ApplyMarkerColor。
不要自己把 Registry 标成 done。
完成后说：M6 已完成，请主导窗口查收。
```

---

## Phase 3（已归档）— 扫描开关 + 标记颜色/状态同步

| 模块 | 本环 | 职责 |
|------|------|------|
| M1 | 进行中 | 只写本表/Registry、查收、同步 inspect-ar、不改 admin/backend/mobile 业务 |
| M4 | 本环跳过 | 5174 改状态已可用；Unity 用 GET 拉状态，不必改 React |
| M5 | in_progress | 斥候确认 `GET/POST /api/issues` 已含 `id/status/priority/position`；**已满足则禁止改代码**，只交 curl 证据 |
| M6 | in_progress | 暂停/开始扫描；按优先级上色；GET 列表把进行中/已解决标成灰 |

### 卡点 C — 扫描卡顿：底部「暂停扫描 / 开始扫描」

只改 `mobile/Assets/InspectAR/Scripts/InspectARApp.cs`（可顺带平面材质，禁止 `InspectAR/Setup Project`）。

1. **默认暂停**：`Start` 里在找到 `ARPlaneManager` / `ARRaycastManager` 之后立刻 `enabled = false`（不要侦测平面）。AR Session / 摄像头保持开。
2. **两个按钮**放在 AR 画面底部、表单上方（建议 Rect 锚点 y=`0.42`～`0.50`，左右各一），不要挡表单输入。
3. 点 **开始扫描**：两组件 `enabled = true`，走现有地面过滤，合格地面半透明蓝网格。
4. 点 **暂停扫描**：两组件 `enabled = false`，隐藏已有平面网格（visualizer/renderer/line），停止侦测。
5. **文案**：暂停时开始按钮=`开始扫描`、暂停按钮=`暂停中`（可禁用暂停钮）；扫描时暂停按钮=`暂停扫描`、开始按钮=`扫描中`（可禁用开始钮）。状态区可同步「暂停中」/「扫描中」。
6. **放置**：仅 `扫描中` 且现有 `IsStableFloor` 逻辑允许时，点击平面才 `AttachAnchor`；暂停时 `Update` 里直接 return，屏幕点击不放方块。点 UI 仍不放置。

### 卡点 D — 标记颜色：优先级 + 管理端状态

颜色常量（URP Unlit `_BaseColor`，否则 `material.color`）：

| 条件 | 色 |
|------|----|
| `status` 为 `in_progress` 或 `resolved` | 灰 `(0.55, 0.55, 0.55)` |
| 否则 `low` | 绿 `(0.20, 0.75, 0.30)` |
| 否则 `medium` | 黄 `(0.95, 0.80, 0.15)` |
| 否则 `high`（含默认） | 红 `(0.95, 0.25, 0.20)` |

**必须实现两个方法（名称可同可近）：**

```csharp
static Color ColorForIssue(string priority, string status)
void ApplyMarkerColor(GameObject marker, string priority, string status)
```

生成标记：`CreateMarker` / 放置后立刻 `ApplyMarkerColor(go, m_Priority, "open")`。表单改优先级且尚未提交时，同步当前方块颜色。

**状态同步（打开 App 或加载列表）：**

- `POST` 成功后解析 201 JSON 的 `id`（`JsonUtility` 包一层 DTO，字段 `id/status/priority/position`）。
- 用 `Dictionary<string, GameObject>`（或 List）记住已提交标记。
- 表单加按钮 **刷新标记**：`GET {base}/api/issues`，解析 `{ "issues": [ ... ] }`。
- 对已有 id：`ApplyMarkerColor`；`in_progress`/`resolved` → 灰。
- 列表里有 `position` 但场景还没有的条目：在 `(x,y,z)` 生成立方体并上色（跨会话坐标系不保证对齐真实家具，允许）。
- 启动后自动拉一次列表；之后每 **5 秒**轮询一次（失败只打日志，不打断扫描）。
- 未提交的本地标记没有 id，轮询不要删掉它。

保留 Phase2 地面过滤与 Anchor，不要倒退。

**成功标准（代码）：** 默认不侦测；开始扫描才出蓝；暂停不放置；low/medium/high 三色；GET 后非 open 变灰。真机由用户出包。

### 开工话术

工作区：`D:\Cursor_projectt\测试考题`

**M5 — Phase3 接口确认**

```text
@multi-window_M
我是 M5。当前角色：斥候；仅当接口缺字段时才转主力。
请读 docs/FIX-PLAN.md Phase 3 卡点 D 对 GET/POST 的字段要求。
只动 backend/。用 curl 打本机 8081（若无进程不要编造）。
已满足则禁止改代码。完成后说：M5 已完成，请主导窗口查收。
```

**M6 — Phase3 扫描开关与颜色**

```text
@multi-window_M
我是 M6。当前角色：主力。请读 docs/FIX-PLAN.md Phase 3 卡点 C 和 D。
只改 mobile/。不要执行 InspectAR/Setup Project。不要打开游戏。
必须实现 ApplyMarkerColor / ColorForIssue。完成后说：M6 已完成，请主导窗口查收。
```

---

# Phase 2（已归档）

> 本机巡检 Go 在 **8081**。管理端 **http://localhost:5174**。

| 模块 | 本阶段 | 职责 |
|------|--------|------|
| M1 | 查收归档 | 同步 inspect-ar、重启 Vite |
| M4 | done | 开发默认 8081 |
| M5 | done | CORS 本机任意端口 |
| M6 | review | 地面过滤 + ARAnchor |

## 卡点 A — 管理端「无法连接后端 8080」


**现象**：浏览器 `localhost:5174` 红条「无法连接后端，请确认服务已在 8080 端口运行」。`http://192.168.2.14:8081/health` 已是 `{"ok":true}`。

**已核实根因（M1 斥候，磁盘）**：

1. `admin/src/api.ts` 默认 `http://127.0.0.1:8080`；`networkErrorMessage` **写死 8080**，连 CORS 失败也会显示这句。
2. `backend/internal/api/api.go` 的 `allowedOrigins` **只有** `http://localhost:5173` 与 `http://127.0.0.1:5173`。用户开的是 **5174**，浏览器会 CORS 拦截，表现为 TypeError。
3. 本机 5173 已被其它项目占用，巡检管理端必须用 5174。

**M5 最小改动（只改 `backend/`）**

- 放行来源：任意 `http://localhost:*` 与 `http://127.0.0.1:*`（解析 Origin 的 host+协议，端口不限）。
- 不要用 `*` 配 `Allow-Credentials`；当前未带 cookie，按现有头继续即可。
- 自测：`OPTIONS`/`GET` 带 `Origin: http://localhost:5174` 时响应含 `Access-Control-Allow-Origin: http://localhost:5174`。
- 改完后无法替用户重启已在跑的 `inspect-server` 时，在报告里写明「请 M1 重启 8081」。

**M4 最小改动（只改 `admin/`）**

- 开发环境默认连 `http://127.0.0.1:8081`：提交 `admin/.env.development`（内容一行 `VITE_API_BASE=http://127.0.0.1:8081`）。
- `networkErrorMessage` 使用 `apiBase()`，禁止写死 8080。
- 可选：`vite.config.ts` 固定 `server.port = 5174`，避免再抢 5173。
- 不要改 `backend/`。Vite 必须重启后 env 才生效（M1 查收时重启）。

**成功标准**：浏览器打开 `http://localhost:5174`，无红条，能看到列表或空列表；刷新不报 8080。

## 卡点 B — 真机平面乱生、标记跟着飘

**现象**：摄像头已出画面，但柜子侧面、人腿都会立刻铺蓝色网格，多层穿插；点下去的红方块跟着蓝面抖动、飘走。

**已核实根因（M1 斥候）**

- `InspectARApp` 只设置了 `PlaneDetectionMode.Horizontal`，**没有**在 `planesChanged` 里丢掉斜面/高处/小面。
- ARCore 会把桌面、柜门、腿等近似水平的小面也当成 plane。
- 标记是普通 Cube，未 `ARAnchor` 贴到命中平面；平面网格每帧更新时视觉上像整块「屏幕」在飞。

**M6 最小改动（只改 `mobile/`，主要 `Assets/InspectAR/Scripts/InspectARApp.cs`；必要时加组件到现有场景物体，不要跑会清空场景的 Setup Project）**

1. 运行时确保 XR Origin 上有 `ARAnchorManager`（没有就 `AddComponent`）。
2. 订阅 `ARPlaneManager.trackablesChanged` / `planesChanged`（以 6.3.5 API 为准）：对每个平面若 **不是稳定水平地面** 则关闭其 `ARPlaneMeshVisualizer`、`MeshRenderer`、`LineRenderer`，并尽量 `Remove`/`SetVisible(false)`。
   - 水平：`alignment == PlaneAlignment.HorizontalUp`（或 Horizontal），法线与世界 up 点积 ≥ **0.98**。
   - 面积过小（例如 &lt; 0.25 m²）先隐藏，长大后再显示。
   - 只保留 **最低** 的那块地面：其它平面 center.y 比最低地面高超过 **0.20 m** 的一律隐藏（柜子、腿）。
   - 同时可见水平地面最多 **1** 块。
3. 点击放置：只对通过上述过滤的平面做 raycast；用 `ARAnchorManager.AttachAnchor(plane, pose)`，红方块设为该 Anchor 子物体，之后 **不要**每帧改世界坐标。
4. 已放置后：忽略新的点击改位，或仅允许点「同一块地面」更新一次；禁止跟新出现的斜面走。
5. 状态文案：未找到合格地面时提示「请对准地面缓慢扫描，避开家具和身体」。

**成功标准（真机，M1/用户）**：对准地面才出一块蓝；柜子/腿不再铺多层斜蓝；红方块贴地，转手机不明显飞走。

**出包路径**：源码以 `测试考题/mobile` 为准。M1 查收后同步到 `D:\Cursor_projectt\inspect-ar` 再 Build And Run（中文路径不能打包）。

## 多方验证（本环）

| 角色 | 做什么 |
|------|--------|
| M4 主力 | 改 admin，交 diff + `npm run build` |
| M5 主力 | 改 CORS，交 curl 带 Origin:5174 的响应头 |
| M6 主力 | 改 InspectARApp，交关键逻辑说明（无法本窗口真机） |
| M1 搜剿 | 磁盘路径、build、curl CORS、重启 Vite 5174、同步 inspect-ar |
| 用户 | 新 APK 真机扫地面；网页刷新看列表 |

## 子窗口开工话术

工作区：`D:\Cursor_projectt\测试考题`

### M5 开工 — Phase2 CORS

```text
@multi-window_M
我是 M5 窗口。当前角色：主力。请读 docs/FIX-PLAN.md 卡点 A 的 M5 段，以及 docs/MODULE-REGISTRY.md。
只改 backend/。CORS 必须放行 http://localhost:5174 与任意本机 Vite 端口。
不要打开游戏。完成后说：M5 已完成，请主导窗口查收。
```

### M4 开工 — Phase2 连 8081

```text
@multi-window_M
我是 M4 窗口。当前角色：主力。请读 docs/FIX-PLAN.md 卡点 A 的 M4 段。
只改 admin/。开发默认 VITE_API_BASE=http://127.0.0.1:8081；错误文案不要写死 8080。
不要打开游戏，不要另开浏览器。完成后说：M4 已完成，请主导窗口查收。
```

### M6 开工 — Phase2 地面过滤

```text
@multi-window_M
我是 M6 窗口。当前角色：主力。请读 docs/FIX-PLAN.md 卡点 B。
只改 mobile/。不要执行会重建场景的 InspectAR/Setup Project。
过滤斜面/高处/小面；标记用 ARAnchor。不要打开游戏。
完成后说：M6 已完成，请主导窗口查收。
```
