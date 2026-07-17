# AGENTS.md

## 项目目标

「白荆科技宿舍走廊」（`SeedDormitoryCorridor`）是可长期维护的 Windows 11 x64 通用 2D 桌面宠物应用。宠物由不可信的 PNG、`pet.json` 和文档资产包驱动；第一版只显示一个宠物，但可安装和切换多个宠物。

## 构建与测试

```powershell
dotnet restore SeedDormitoryCorridor.sln
dotnet build SeedDormitoryCorridor.sln -c Release
dotnet test SeedDormitoryCorridor.sln -c Release --no-build
./scripts/publish.ps1
```

## 目录职责

- `src/SeedDormitoryCorridor.App`：ApplicationContext、托盘、设置、单实例和应用编排。
- `src/SeedDormitoryCorridor.Platform.Windows`：WinForms/Win32 分层窗口、DPI、显示器和开机启动。
- `src/SeedDormitoryCorridor.Rendering`：spritesheet 解码、复用后备缓冲、预乘 Alpha 和命中换算。
- `src/SeedDormitoryCorridor.Runtime`：动画播放器、单调时钟、Idle 与交互状态，不依赖 WinForms/Win32。
- `src/SeedDormitoryCorridor.Assets`：强类型清单、Profile、校验、安全导入和原子安装。
- `src/SeedDormitoryCorridor.Configuration`：配置模型、路径和原子 JSON 持久化。
- `tests`：纯逻辑自动化测试；真实桌面行为不得放入脆弱 UI 测试。
- `docs`：架构、资产格式、安全、里程碑和人工验收记录。

## 技术约束

不得引入 WPF、WinUI、Avalonia、Qt、Electron、游戏引擎、DirectX/DirectComposition、数据库、浏览器内核、脚本执行、网络服务、账号、自动更新或插件代码加载。生产代码优先仅用 .NET 自带能力；新增依赖必须在 `docs/architecture.md` 说明。

## Layered Window Alpha 约束

- 只通过 `UpdateLayeredWindow` 提交 32-bit premultiplied BGRA。
- PNG 仅在加载资源时解码；每帧不得读文件、解码或创建新 Bitmap。
- 复用 Bitmap/HDC/HBITMAP/缓冲区，所有 GDI 和托管资源明确释放。
- `WM_NCHITTEST` 从源 spritesheet 的 CPU Alpha 读取；透明阈值默认 16。
- 完全穿透与逐像素穿透相互独立，完全穿透始终能从托盘关闭。

## 资产安全约束

资产包始终是不可信输入：只接受受限大小的 PNG 和数据/文档文件；拒绝绝对路径、`..`、重解析点和 ZIP 路径穿越；解压前后都验证路径位于 staging 根目录；绝不执行、反射加载或覆盖应用文件。替换安装必须先验证，再通过备份目录完成可回滚切换。

## 完成前验证

每次功能修改至少运行受影响测试；里程碑结束运行 Release build 和全部测试。发布相关修改还需执行 `scripts/publish.ps1`。真实透明、焦点、Alt+Tab、负坐标多屏、Per-Monitor DPI、Explorer 重启、睡眠唤醒和安装器行为记录到人工清单，不能伪称自动验证完成。
