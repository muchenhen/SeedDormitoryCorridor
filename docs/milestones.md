# 里程碑与验证记录

| 里程碑 | 范围 | 自动验收 | 尚需人工验收 |
| --- | --- | --- | --- |
| M0 | 解决方案、ApplicationContext、日志、文档 | Debug/Release build，测试发现 | 应用在通知区启动/退出 |
| M1 | 分层窗口、预乘 Alpha、资源释放 | 渲染与 Alpha 单测 | 真透明、无黑边、GDI 稳定 |
| M2 | 像素命中、拖拽、穿透、置顶、DPI/多屏 | 坐标/边界/DPI 单测 | 焦点、Alt+Tab、负坐标与混合 DPI |
| M3 | Codex V2、Manifest、校验、播放器、内置宠物 | Assets/Runtime 全部测试 | 九种动画观感与透明格 |
| M4 | 托盘、生命周期、单实例、保存 | 配置和 IPC 逻辑测试 | 第二次启动、关机/注销、Explorer 重启 |
| M5 | 目录/ZIP 导入、回滚、设置、删除 | 路径穿越/回滚测试 | 文件选择器和错误展示体验 |
| M6 | 自包含发布、Inno Setup、README | publish 成功；有 ISCC 时编译安装器 | 安装、覆盖、卸载与数据保留 |

每个里程碑完成后在本文件追加日期、命令结果和未完成人工项；自动化成功不代替 `docs/manual-test-checklist.md`。

## 2026-07-17 MVP 实施记录

- M0：完成 9 项目 solution、严格零警告构建、`PetApplicationContext`、轻量日志、项目规范与架构文档。
- M1/M2：完成 `UpdateLayeredWindow`、可复用 DIB/HDC/HBITMAP、PArgb 后备缓冲、Alpha 命中、拖拽、完全穿透、置顶、NOACTIVATE、DPI 与可见区域恢复。
- M3：完成 `codex-pet-v2` 九动画 Profile、强类型 Manifest、透明格校验、内置回退宠物和时长/命中测试。
- M4：完成托盘、显示/隐藏、暂停/恢复、动画/宠物菜单、状态保存、Mutex 单实例和白名单 named pipe。
- M5：完成目录/ZIP staging、安全解压、结构化问题、替换回滚、删除、设置界面和当前资源原子交换。
- M6：完成 win-x64 self-contained 发布、应用图标、Inno Setup 6 脚本、README、许可证和人工清单。

最终自动验证：

```text
dotnet build SeedDormitoryCorridor.sln -c Release
成功，0 warnings，0 errors

dotnet test SeedDormitoryCorridor.sln -c Release --no-build
成功，29 passed（Runtime 8 / Rendering 10 / Assets 11）

./scripts/publish.ps1
成功，产物位于 artifacts/publish

dotnet format SeedDormitoryCorridor.sln --verify-no-changes --no-restore
成功
```

自包含发布版受控启动成功，日志记录 `Loaded pet id=builtin-seed`；第二实例自动退出且只保留一个运行实例。启动完成后的 5 秒动画窗口 CPU 增量约 0.016 秒（单次短样本，不代替长期资源测试）。

当前机器没有安装或配置 `ISCC.exe`，因此 `.iss` 已实现但安装器 EXE 未在本环境编译。真实透明、鼠标落到后方、焦点/Alt+Tab、混合 DPI、多显示器、会话结束、Explorer 重启、托盘正常退出、安装/覆盖/卸载和长期 GDI 稳定性仍须按人工清单验证。受控冒烟进程由测试工具强制停止，不计作“托盘退出”验收。

## 2026-07-18 v0.1.0-alpha.1 发布准备

- 采用 Semantic Versioning，将首个粗糙公开版本定为 `v0.1.0-alpha.1`。
- 新增完全本地的正式双包流程，不使用 GitHub Actions；一次生成当前用户安装包、免安装 ZIP 和 SHA-256 校验文件。
- 发布输出使用隔离 staging 并在每次构建前清理，避免旧程序集或旧内置宠物混入新包。
- Release build 成功，0 warnings、0 errors；Runtime 11、Rendering 10、Assets 17，共 38 项测试全部通过。
- `publish.ps1` 成功；Inno Setup 6.7.3 编译成功。产物版本、ZIP 单一顶层目录、无 PDB、三个内置宠物和 SHA-256 重新计算均已检查。

安装包尚未进行 Authenticode 签名。真实桌面行为、交互式安装/覆盖/卸载、SmartScreen 和 GitHub 下载后哈希仍须按人工清单验证，不能由上述自动检查替代。
