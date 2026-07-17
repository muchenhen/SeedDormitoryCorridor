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

`desktopPet` 是可选扩展，可声明 `profile`（当前仅 `codex-pet-v2`）、`defaultScale`、`renderMode`（`smooth`/`pixelated`）、`alphaThreshold` 与 `behavior` 映射。未知字段被忽略，原始 Manifest 永不被修改。未声明 profile 时，仅当 PNG 恰为 1536×1872 才自动选择 `codex-pet-v2`。

## codex-pet-v2

Atlas 固定 1536×1872、8 列×9 行、单格 192×208。

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
