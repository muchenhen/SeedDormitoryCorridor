# 白荆科技宿舍走廊

`SeedDormitoryCorridor` 是面向 Windows 11 x64 的通用 2D 桌面宠物应用。它使用 WinForms + Win32 Layered Window，在不引入浏览器、游戏引擎或插件代码的前提下，提供逐像素透明、按 Alpha 命中、动画调度和本地宠物包管理。

当前公开版本为 `v0.1.0-alpha.1`。安装包和免安装 ZIP 可从 [GitHub Releases](https://github.com/muchenhen/SeedDormitoryCorridor/releases) 下载；alpha 版本尚未进行 Authenticode 签名，Windows SmartScreen 可能提示未知发布者。

## 当前功能

- 默认显示内置苏筱桌宠（`spritesheet-chat-output.png`），并可切换到内置田偌；小豆人仅作为内部启动故障回退。
- 安装和切换多个宠物，兼容 ChatGPT/OpenAI 的 8×9 sprite v1 与 8×11 sprite v2 `codex-pet-v2` Atlas。
- CPU 侧一次解码 PNG，复用 32-bit premultiplied BGRA 后备缓冲、DIB 与 HDC，通过 `UpdateLayeredWindow` 提交。
- 透明像素穿透、身体拖拽、完全鼠标穿透、总在最前、负坐标多显示器和 Per-Monitor V2 DPI。
- 九种动画、优先级/中断/循环/完成跳转、按真实帧时长唤醒，以及带权重和冷却的特殊 Idle。
- 托盘生命周期、隐藏/恢复/暂停、设置、单实例 Mutex 和白名单 named-pipe IPC。
- 目录/ZIP staging、安全路径校验、Atlas/Alpha 校验、同 ID 可回滚替换和损坏资源回退。
- 原子配置写入、损坏配置备份、当前用户开机启动、轻量本地日志。
- 当前用户级 Inno Setup 安装器脚本，默认保留宠物和配置。

## 构建

需要 Windows x64 和 .NET SDK 10。

```powershell
dotnet restore SeedDormitoryCorridor.sln
dotnet build SeedDormitoryCorridor.sln -c Release
dotnet test SeedDormitoryCorridor.sln -c Release --no-build
./scripts/publish.ps1
```

安装包还需要 Inno Setup 6：

```powershell
./scripts/publish.ps1 -BuildInstaller
```

自包含产物默认位于 `artifacts/publish`，安装包位于 `installer/Output`。

正式双包发布在本机执行，不使用 GitHub Actions：

```powershell
./scripts/release.ps1
```

该命令生成安装包、免安装 ZIP 和 `SHA256SUMS.txt`；完整版本策略和发布步骤见 [`docs/releasing.md`](docs/releasing.md)。

## 动画与交互

内置宠物采用以下固定触发逻辑；外部宠物可通过 `desktopPet.behavior` 改写显示、单击、双击和拖拽方向对应的动画。

| 触发 | 默认动画/行为 |
| --- | --- |
| 启动、切换宠物或重新显示 | `waving`，完成后回到 `idle` |
| 无其他动作 | `idle` 循环 |
| 左键单击身体 | `jumping` |
| 左键双击身体 | `waving` |
| 向左/向右拖拽超过 4 px | `running-left` / `running-right`；松开后回到 `idle` |
| 右键身体 | 打开托盘菜单，不切换动画 |
| 鼠标悬停（hover） | 当前没有动画逻辑 |
| 托盘“播放动画”菜单 | 可手动播放资产声明的全部九种动画 |

特殊待机会在没有交互且没有高优先级动画时，从 `waving`、`jumping`、`waiting`、`review` 中按 3:2:3:2 权重随机选择；不会连续重复同一种。低频、正常和高频的触发间隔分别为 60、30、15 秒；关闭后只保留普通 `idle`。`waving`/`jumping` 冷却 120 秒，`waiting`/`review` 冷却 180 秒。`failed` 和非方向性的 `running` 当前没有固定业务状态触发，只能从菜单手动播放。

启用“鼠标完全穿透”后，桌宠身体不会接收单击、双击、拖拽、右键或 hover；仍可从系统托盘关闭穿透。

## 导入宠物

托盘菜单选择“导入宠物”，可选择 ZIP 或包含 `pet.json` 的目录。最小包只需：

```text
sample-pet/
  pet.json
  spritesheet.png
```

```json
{
  "id": "sample-pet",
  "displayName": "Sample Pet",
  "description": "A sample desktop pet.",
  "spritesheetPath": "spritesheet.png"
}
```

未声明 profile 时，1536×1872（sprite v1）或 1536×2288（sprite v2）PNG 会自动识别为 `codex-pet-v2`；程序不会猜测其他网格。完整动画表、可选字段和校验规则见 [资产格式](docs/asset-format.md)。

## 数据位置

- 配置：`%AppData%\SeedDormitoryCorridor\settings.json`
- 宠物：`%LocalAppData%\SeedDormitoryCorridor\Pets\`
- 日志：`%LocalAppData%\SeedDormitoryCorridor\Logs\`

应用没有网络服务、账号、脚本执行、自动更新或插件代码加载。

## 文档

- [架构](docs/architecture.md)
- [安全模型](docs/security.md)
- [资产格式](docs/asset-format.md)
- [里程碑](docs/milestones.md)
- [Windows 人工验收](docs/manual-test-checklist.md)

## 已知限制

- 第一版只支持 PNG，不支持 WebP。
- 同时只显示一个宠物。
- 真实透明、焦点、混合 DPI、Explorer 重启、关机/注销和安装/卸载必须在交互式 Windows 桌面按清单人工验证。
- 没有自动更新、在线宠物商店或可执行插件。

## 许可证

本项目采用 [PolyForm Noncommercial License 1.0.0](LICENSE)：源码可查看、修改和在许可规定的非商业目的下使用，但禁止未经授权的商业使用。由于包含商业用途限制，它是 source-available 许可证，而不是 OSI 定义的开源许可证。
