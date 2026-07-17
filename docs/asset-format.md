# 宠物资产格式

## 最小目录

```text
sample-pet/
  pet.json
  spritesheet.png
  preview.png       # 可选
  icon.png          # 可选
  LICENSE.txt       # 可选
  README.md         # 可选
```

最小 `pet.json`：

```json
{
  "id": "sample-pet",
  "displayName": "Sample Pet",
  "description": "A sample desktop pet.",
  "spritesheetPath": "spritesheet.png"
}
```

`spriteVersionNumber` 可为 1 或 2，省略时按 1 处理。`desktopPet` 是可选扩展，可声明 `profile`（当前仅 `codex-pet-v2`）、`defaultScale`、`renderMode`（`smooth`/`pixelated`）、`alphaThreshold` 与 `behavior` 映射。未知字段被忽略，原始 Manifest 永不被修改。未声明 profile 时，1536×1872 或 1536×2288 PNG 会自动选择 `codex-pet-v2`，随后仍会核对版本与尺寸是否一致。

## codex-pet-v2

单格固定为 192×208、每行 8 列。`spriteVersionNumber: 1` 使用 1536×1872（9 行）；`spriteVersionNumber: 2` 使用 1536×2288（11 行）。v2 的第 0 行第 6 格以及第 9、10 行（从 0 开始）共同构成 17 个视线跟随帧；当前桌面窗口不主动播放它们，但会完整保留和校验。

| 行 | 动画 | 使用列 | 帧时长（ms） |
| ---: | --- | --- | --- |
| 0 | idle | 0–5 | 280,110,110,140,140,320 |
| 1 | running-right | 0–7 | 120×7,220 |
| 2 | running-left | 0–7 | 120×7,220 |
| 3 | waving | 0–3 | 140×3,280 |
| 4 | jumping | 0–4 | 140×4,280 |
| 5 | failed | 0–7 | 140×7,240 |
| 6 | waiting | 0–5 | 150×5,260 |
| 7 | running | 0–5 | 120×5,220 |
| 8 | review | 0–5 | 150×5,280 |

所有必要格至少包含一个非零 Alpha 像素；所有未使用格必须完全透明。第一版只支持 PNG，WebP 会得到明确的“不支持”校验消息。
