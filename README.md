# 现场巡检问题标注（原型）

园区巡检人员用手机在现场放置 AR 标记并上报；管理人员在网页查看并更新处理状态。

## 多窗口分工

| 文档 | 用途 |
|------|------|
| [docs/MODULE-REGISTRY.md](docs/MODULE-REGISTRY.md) | 模块清单与状态 |
| [docs/API.md](docs/API.md) | 三端接口 |
| [docs/TOOL-PATHS.md](docs/TOOL-PATHS.md) | 本机工具路径 |
| [docs/RECEIPT-LOG.md](docs/RECEIPT-LOG.md) | 查收记录 |

**作战条令**：M1 管大纲与 done；Mn 环内斥候→主力→搜剿；同卡点最多 4 次后硬停升级。

## 使用的版本

| 部分 | 版本 |
|------|------|
| Unity | 6000.3.23f1 LTS + AR Foundation / ARCore 6.3.x（工程由 M6 写入） |
| Go | 1.26.7 + go-sqlite3 |
| 管理端 | Node 24、React 19、TypeScript 5.5、Vite |
| 后端地址 | `http://<电脑局域网IP>:8080` |

## 如何启动

请**新开终端**（使 `GOROOT` / PATH 生效）。若 8080 已被其它程序占用，先关掉，或见下方「换端口」。

**1. Go 后端**（需 MinGW，`CGO_ENABLED=1`）：

```bat
cd /d D:\Cursor_projectt\测试考题\backend
set CGO_ENABLED=1
go run ./cmd/server
```

默认监听 `0.0.0.0:8080`，数据库 `backend/data/issues.db`。健康检查：`http://127.0.0.1:8080/health`。本机巡检当前为 **8080**（2026-08-28 起端口已空闲）。

**登录账号**（Phase 4）：`admin` / `admin123`（可改状态、可删）、`inspector` / `inspect123`（可提交、可删自己的）、`viewer` / `view123`（只看）。管理端打开 `http://localhost:5174` 先登录。Vite 开发默认连 `http://127.0.0.1:8080`。

**2. React 管理端**：

```bat
cd /d D:\Cursor_projectt\测试考题\admin
npm run dev
```

浏览器打开终端里提示的地址（本项目固定 **http://localhost:5174**）。

**3. Unity 真机**：Android 打包**不能**使用带中文的路径。请用编辑器打开纯英文目录：

`D:\Cursor_projectt\inspect-ar`

（由 `测试考题\mobile` 复制而来。）场景 `Assets/Scenes/InspectAR`，Build Profiles → Android → Build And Run。APK 保存到桌面，文件名例如 `InspectAR.apk`。

### 换端口

```bat
set LISTEN_ADDR=0.0.0.0:8081
```

管理端：`set VITE_API_BASE=http://127.0.0.1:8081` 后再 `npm run dev`。

## Unity 真机如何配置 Go 后端地址

手机与电脑同一 WiFi。在 AR 界面「后端地址」填 `http://<电脑局域网IP>:8080`（不要填 `localhost`），点「保存后端地址」。电脑防火墙需放行该端口。

## 已完成 / 未完成 / 已知问题

- **已完成**：工具链；三端骨架；Go API + SQLite + JWT 三角色 + PUT/DELETE + WebSocket；React 登录/筛选/三行坐标/实时列表；Unity AR（扫描、多标记、布局、锁地、关键词 High、世界锚、点云、临时平面、登录退出改密）。仓库 https://github.com/tmddwyaaa-source/xianchang-xunjian-wenti-biaozhu 。真机与电脑端演示视频已录。
- **进行中**：封装提交资料。
- **未完成（可改进，提交后下一轮）**：
  1. **AR 白墙白地**：ARCore 在低纹理地面仍可能不出真平面；点云引导和点击临时平面是兜底，不是同等精度的真实平面。工地可再引导用户扫砖缝/脚印，或外接视觉惯性更稳的方案。
  2. **标记坐标只在本机 AR 世界系**：换一部手机或重置 Session 后，无法用同一组 XYZ 对回现场同一点。后续可加 AR Geospatial / 云锚，或拍照+平面截图作为附件。
  3. **问题没有现场照片**：目前只有标题、描述、优先级和坐标。管理端很难只靠文字验收。可在放置时截一帧摄像头或允许相册上传。
  4. **双份 Unity 工程**：源码在 `测试考题/mobile`，出包必须用纯英文路径 `inspect-ar`，靠手工同步，容易漏文件（例如新脚本缺 `.meta`）。可改成单一英文仓库，或加同步脚本。
  5. **ARCore 资源文件未跟代码对齐**：`ARCoreSettings.asset` YAML 仍是 Requirement Optional / Depth Required；运行时 `EnsureArCoreSettings` 已写对，但未写回 YAML。个别机型可能因此起不来 AR。
  6. **账号体系偏演示**：只有三个种子用户，没有注册/邀请；改密后旧 JWT 在过期前仍有效；管理端网页没有改密入口（只有手机有）。登录无失败次数限制。
  7. **JWT / CORS 偏本机**：未设 `JWT_SECRET` 时用开发默认密钥；CORS 只放行 `localhost` / `127.0.0.1`。若要把管理端放到局域网 IP 或 HTTPS，要改来源白名单并换密钥。
  8. **SQLite 单文件**：适合原型，不适合多人同时重写。没有备份、没有分页。列表筛选只在 React 客户端做，数据变多后要改成服务端查询。
  9. **防火墙与地址**：手机不能填 `localhost`，必须电脑局域网 IP + 放行 8080。可做启动时探测本机 IP 并生成二维码，减少填错。
  10. **测试与出包**：Unity 无自动化真机测试；Go 改密等接口缺少固定集成测试文件。每次改 `mobile/` 都要再 Build And Run 才进手机。
- **已知问题**：Hub 附带 Visual Studio Community 安装失败（本机已有 VS 2026，不影响出包）；路径含中文时 Android 无法打包，必须用 `inspect-ar`；曾因占用改用 8081，**现已切回 8080**。
