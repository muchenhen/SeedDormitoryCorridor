# 白荆科技宿舍走廊

`SeedDormitoryCorridor` 是面向 Windows 11 x64 的通用 2D 桌面宠物应用。它使用 WinForms + Win32 Layered Window，在不引入浏览器、游戏引擎或插件代码的前提下，提供逐像素透明、按 Alpha 命中、动画调度和本地宠物包管理。

当前公开版本为 `v0.1.0-alpha.2`。安装包和免安装 ZIP 可从 [GitHub Releases](https://github.com/muchenhen/SeedDormitoryCorridor/releases) 下载；alpha 版本尚未进行 Authenticode 签名，Windows SmartScreen 可能提示未知发布者。

## 当前功能

- 默认显示内置苏筱桌宠（`spritesheet-chat-output.png`），并可切换到内置田偌或 Sweeper-EX；小豆人仅作为内部启动故障回退。
- 安装和切换多个宠物，兼容 ChatGPT/OpenAI 的 8×9 sprite v1 与 8×11 sprite v2 `codex-pet-v2` Atlas。
- CPU 侧一次解码 PNG，复用 32-bit premultiplied BGRA 后备缓冲、DIB 与 HDC，通过 `UpdateLayeredWindow` 提交。
- 透明像素穿透、身体拖拽、完全鼠标穿透、总在最前、负坐标多显示器和 Per-Monitor V2 DPI。
- 九种动画、优先级/中断/循环/完成跳转、按真实帧时长唤醒，以及带权重和冷却的特殊 Idle。
- 托盘生命周期、隐藏/恢复/暂停、设置、单实例 Mutex 和白名单 named-pipe IPC。
- 目录/ZIP staging、安全路径校验、Atlas/Alpha 校验、同 ID 可回滚替换和损坏资源回退。
- 可配置的 HTTPS 在线宠物目录，提供预览、兼容性状态、校验下载、安装、重新安装与删除。
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

自动化或服务端可使用 [`sdc-pet-validator`](docs/pet-validator.md) 校验目录/ZIP，并获得稳定 JSON 与退出码；该工具与桌面导入共用同一套 staging 和资源校验逻辑。

## 在线宠物

设置中的“在线宠物”页接受一个绝对 HTTPS 目录地址。客户端只在打开该页或点击“刷新”时访问目录和预览，不在后台轮询；目录不可用、超时或内容无效时，当前桌宠和本地宠物管理继续正常工作。

安装在线宠物前，客户端会核对最低客户端版本、声明大小和 SHA-256，将 ZIP 写入随机 staging，再调用与本地导入及 `sdc-pet-validator` 相同的正式验证器。通过全部校验后才以可回滚事务安装；同 ID 重新安装采用安全替换。任意非内置宠物都可从在线页删除，删除当前宠物时会先切换到默认内置宠物，内置 ID 始终受保护。目录接口字段和限制见 [在线宠物目录](docs/online-pet-library.md)。

## 制作与贡献宠物

新宠物建议使用 sprite v1：它已经覆盖当前会播放的全部动画，且不需要制作尚未启用的视线跟随帧。sprite v2 适合需要保留完整 ChatGPT/OpenAI 输出的素材；使用 v2 时必须在 `pet.json` 中明确填写 `"spriteVersionNumber": 2`。

### 图片规则

- sprite v1 必须是带 Alpha 通道的 1536×1872 PNG，按 8 列×9 行排列；sprite v2 为 1536×2288、8 列×11 行。两者的单格均为 192×208。
- 行列均从左上角的 0 开始计数，每行动画从左向右按时间顺序排列。
- 每个必要格必须包含非透明像素；未使用格必须完全透明，不能包含占位符、残影、格线或编号。
- 角色的造型、比例、尺寸、视觉中心和脚底基线应保持一致。所有像素必须留在当前格内，不得跨格或被裁切。
- 背景必须真实透明；不要添加底色、边框、地面、整图阴影、文字或水印。
- PNG 文件不得超过 64 MiB。当前不接受 WebP、JPG、GIF 或其他图片格式。

| 行 | 动画 | 使用列 | 内容建议 |
| ---: | --- | --- | --- |
| 0 | `idle` | 0–5 | 呼吸、眨眼等可平滑循环的待机动作 |
| 1 | `running-right` | 0–7 | 明确朝画面右侧跑动的完整循环 |
| 2 | `running-left` | 0–7 | 明确朝画面左侧跑动的完整循环 |
| 3 | `waving` | 0–3 | 从自然站立到挥手并收势 |
| 4 | `jumping` | 0–4 | 准备、起跳、最高点、落下和落地 |
| 5 | `failed` | 0–7 | 失败、受挫、惊讶或沮丧的动作过程 |
| 6 | `waiting` | 0–5 | 等待、无聊、打哈欠或轻微张望 |
| 7 | `running` | 0–5 | 正面或非方向性的原地跑动循环 |
| 8 | `review` | 0–5 | 查看、检查、阅读或思考 |

sprite v1 的未使用格为 row 0 的 column 6–7、row 3 的 column 4–7、row 4 的 column 5–7，以及 row 6–8 各自的 column 6–7。sprite v2 还要求 row 0 column 6 和 row 9–10 的全部 16 格包含视线跟随帧；row 0 column 7 仍须完全透明。当前版本只校验并保留这些视线帧，不主动播放它们。

### ChatGPT 图像生成提示词

使用生成式图像工具时，建议上传一张自己有权使用的角色参考图，并将下面的占位内容替换为实际角色信息：

```text
请根据我上传的角色参考图，为 SeedDormitoryCorridor 制作一张 2D 桌面宠物动画 spritesheet。

角色要求：
- 角色：[角色名称或外观描述]
- 画风：[例如 Q 版二头身、日系插画或像素风]
- 严格保持参考图中的发型、发色、瞳色、服装、配饰、主色和角色比例。
- 整张图只出现这一个角色。所有帧的脸型、身体比例、服装细节、配色和画风必须一致。

硬性输出规格：
- 输出一张带真实透明 Alpha 通道的 PNG，画布精确为 1536×1872 像素。
- 画布划分为 8 列×9 行，每格精确为 192×208 像素；坐标从左上角开始。
- 每一帧的全部可见像素必须位于自己的格子内，不得跨格、重叠或被裁切。
- 各帧保持一致的角色尺寸、视觉中心和脚底基线；每行动画从左向右按时间顺序排列。
- 不要绘制背景、底色、格线、编号、文字、边框、地面、整图阴影或水印。
- 未使用格必须保持 100% 透明，不能放置占位符、草稿、残影或标记。

逐行内容：
- row 0，column 0–5：idle，共 6 帧。轻微呼吸、眨眼或身体起伏，首尾平滑循环；column 6–7 完全透明。
- row 1，column 0–7：running-right，共 8 帧。角色明确面向并跑向画面右侧，组成完整循环。
- row 2，column 0–7：running-left，共 8 帧。角色明确面向并跑向画面左侧，组成完整循环。
- row 3，column 0–3：waving，共 4 帧。从自然站立到挥手，再自然收势；column 4–7 完全透明。
- row 4，column 0–4：jumping，共 5 帧。准备、起跳、最高点、落下、落地；column 5–7 完全透明。
- row 5，column 0–7：failed，共 8 帧。表现失败、受挫、惊讶或沮丧，并形成连贯动作。
- row 6，column 0–5：waiting，共 6 帧。表现等待、无聊、打哈欠或轻微张望，可循环；column 6–7 完全透明。
- row 7，column 0–5：running，共 6 帧。正面或非方向性的原地跑动循环；column 6–7 完全透明。
- row 8，column 0–5：review，共 6 帧。表现查看、检查、阅读或思考；column 6–7 完全透明。

动作质量要求：
- 跑步、待机和原地跑动的首尾帧应平滑衔接。
- 挥手和跳跃的最后一帧应便于回到 idle。
- 相邻帧应有清晰但连续的变化，不要简单复制完全相同的帧。
- 不要改变镜头、缩放、光源或角色朝向规则。
- 左右跑动时正确保留服装和配饰的不对称细节。

请只返回最终 spritesheet，不要添加说明文字或预览边框。
```

图像模型不能保证严格遵守像素尺寸、网格和 Alpha 要求。生成后仍需使用图像编辑工具检查、裁切、对齐并清除未使用格，不能将未经检查的输出直接提交。

### 推荐的 pet.json

```json
{
  "id": "contributor-pet-name",
  "displayName": "宠物显示名称",
  "description": "宠物和作者说明。",
  "spriteVersionNumber": 1,
  "spritesheetPath": "spritesheet.png",
  "desktopPet": {
    "profile": "codex-pet-v2",
    "defaultScale": 1.0,
    "renderMode": "smooth",
    "alphaThreshold": 16,
    "behavior": {
      "onShow": "waving",
      "onSingleClick": "jumping",
      "onDoubleClick": "waving",
      "onDragLeft": "running-left",
      "onDragRight": "running-right",
      "afterInteraction": "idle"
    }
  }
}
```

插画素材使用 `smooth`；像素画建议使用 `pixelated`。`id` 最长 80 字符，只能包含 ASCII 字母、数字、点、短横线和下划线。

提交前请通过应用的“导入宠物”功能实际加载素材，逐一播放九种动画，并检查透明背景、左右方向、跨格、裁切和动画衔接。宠物包还应附带 README 和授权说明，注明作者、制作或生成方式、参考来源及允许的使用范围。贡献者必须确保有权使用角色、参考图和最终素材；本项目的许可证不会自动授予第三方角色或素材的版权。

可导入宠物包与程序内置宠物是两个不同概念：仅将目录添加到 `assets` 不会自动把宠物加入发行版。申请成为内置宠物时，还需登记应用资源和内置列表、添加资产加载测试，并完成 Release 构建与完整测试。

## 数据位置

- 配置：`%AppData%\SeedDormitoryCorridor\settings.json`
- 宠物：`%LocalAppData%\SeedDormitoryCorridor\Pets\`
- 日志：`%LocalAppData%\SeedDormitoryCorridor\Logs\`

应用没有账号、脚本执行、自动更新或插件代码加载。仅在线宠物页按用户操作访问所配置的 HTTPS 目录、预览和宠物 ZIP。

## 文档

- [架构](docs/architecture.md)
- [安全模型](docs/security.md)
- [资产格式](docs/asset-format.md)
- [在线宠物目录](docs/online-pet-library.md)
- [里程碑](docs/milestones.md)
- [Windows 人工验收](docs/manual-test-checklist.md)

## 已知限制

- 第一版只支持 PNG，不支持 WebP。
- 同时只显示一个宠物。
- 真实透明、焦点、混合 DPI、Explorer 重启、关机/注销和安装/卸载必须在交互式 Windows 桌面按清单人工验证。
- 在线目录地址尚未内置，需由用户或发行渠道提供兼容的 SDCWeb API 地址。
- 没有自动更新或可执行插件。

## 许可证

本项目采用 [PolyForm Noncommercial License 1.0.0](LICENSE)：源码可查看、修改和在许可规定的非商业目的下使用，但禁止未经授权的商业使用。由于包含商业用途限制，它是 source-available 许可证，而不是 OSI 定义的开源许可证。
