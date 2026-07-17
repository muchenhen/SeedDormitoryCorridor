# 白荆科技宿舍走廊

`SeedDormitoryCorridor` 是面向 Windows 11 x64 的通用 2D 桌面宠物应用。它使用 WinForms + Win32 Layered Window，在不引入浏览器、游戏引擎或插件代码的前提下，提供逐像素透明、按 Alpha 命中、动画调度和本地宠物包管理。

## 当前功能

- 安装和切换多个宠物，兼容 ChatGPT/OpenAI Hatch Pet 的固定 8×9 `codex-pet-v2` Atlas。
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

未声明 profile 时，仅 1536×1872 PNG 会自动识别为 `codex-pet-v2`；程序不会猜测其他网格。完整动画表、可选字段和校验规则见 [资产格式](docs/asset-format.md)。

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
