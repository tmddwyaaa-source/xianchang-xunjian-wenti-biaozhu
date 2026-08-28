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

- **已完成**：工具链；M3 骨架；M5 API+SQLite+登录权限+PUT+WS；M4 列表登录筛选+三行坐标+WS；M6 AR（扫描、多标记、布局、锁地、关键词 High、世界锚、点云、临时平面兜底）。
- **进行中**：真机用 `inspect-ar` 重新 Build And Run，在白墙白地验证点云数量与点击即放。
- **未完成**：真机演示视频、电脑端演示视频、对外分享仓库/网盘。
- **已知问题**：Hub 附带 Visual Studio Community 安装失败（本机已有 VS 2026）；曾因占用改用 8081，**现已切回 8080**。
