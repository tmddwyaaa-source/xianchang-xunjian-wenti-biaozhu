# 工具路径约定

> 2026-08-27 对齐。运行时工具在 `D:\unity`，考题源码在本仓库。

| 工具 | 版本 | 路径 |
|------|------|------|
| Node.js | v24.16.0 LTS | `C:\Program Files\nodejs` |
| npm | 11.13.0 | 同上 |
| Go | 1.26.7 | `D:\unity\Go-1.26.7`（`GOROOT`） |
| GOPATH | — | `D:\unity\Go-gopath` |
| GCC / MinGW-w64 | 16.1.0 UCRT | `D:\unity\MinGW-w64\mingw64` |
| Unity Hub | 3.21.0 | MSIX（设置 → 安装量 → 安装位置） |
| Unity Editor | **6000.3.23f1** LTS | `D:\unity\Unity-6000.3\6000.3.23f1\Editor\Unity.exe` |
| Android / OpenJDK | 随编辑器 | 已随 6000.3.23f1 安装 |
| Visual Studio | 2026（本机已有） | `D:\vs2026`；Hub 附带 Community **不必再装** |

环境变量（用户级）：`GOROOT`、`GOPATH`；PATH 含 Go `bin`、GOPATH `bin`、MinGW `bin`。

新终端验证：

```bat
go version
gcc --version
node -v
npm -v
```
