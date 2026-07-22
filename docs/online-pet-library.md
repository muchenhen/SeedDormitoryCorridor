# 在线宠物目录

设置的“在线宠物”页从兼容的 SDCWeb API 读取目录。仓库当前不硬编码生产 URL；目录地址保存在用户 `settings.json` 的 `onlineCatalogUrl`，必须是无凭据的绝对 HTTPS URL。

## 响应格式

响应可以是条目数组，也可以是包含 `pets` 数组的对象：

```json
{
  "pets": [
    {
      "id": "sample-pet",
      "displayName": "Sample Pet",
      "description": "A sample desktop pet.",
      "author": "Example Author",
      "version": "1.0.0",
      "previewUrl": "https://cdn.example/pets/sample-pet.png",
      "packageUrl": "https://cdn.example/pets/sample-pet.zip",
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "packageSize": 123456,
      "spriteVersionNumber": 1,
      "minimumClientVersion": "0.1.0",
      "updatedAt": "2026-07-22T00:00:00Z"
    }
  ]
}
```

`author` 可省略；其余字段必需。`id` 遵循宠物 Manifest 的 80 字符 ASCII 规则，目录内不允许重复（不区分大小写）。`spriteVersionNumber` 只能是 1 或 2。`minimumClientVersion` 接受 .NET 数字版本形式，也允许忽略 `-prerelease` 或 `+build` 后缀进行最低版本比较。

## 客户端状态

- `未安装`：兼容且正式宠物目录中不存在该 id。
- `正在下载`：正在下载、计算 SHA-256、验证或提交安装。
- `已安装`：正式宠物目录中已存在该 id；可重新安装或删除。
- `不兼容`：最低客户端版本过高，或目录 id 与受保护的内置宠物冲突。
- `失败`：网络、大小、哈希、包校验或提交失败；原有安装和当前宠物保持不变。

## 服务端要求

SDCWeb 应在发布条目前调用 `sdc-pet-validator --format json <package>`，只发布退出码为 0 的 ZIP，并根据最终 ZIP 字节计算 `packageSize` 和小写或大写十六进制 `sha256`。预览必须是 PNG，且不应复用包内任意不可信 HTML。目录、预览和 ZIP 均应使用 HTTPS，客户端不跟随重定向。

客户端限制目录响应为 2 MiB/500 项，单张预览为 4 MiB/2048×2048，单个 ZIP 为 128 MiB。下载完成后客户端仍会独立重复大小、SHA-256、staging 和正式资产校验，不能用服务端验证替代客户端信任边界。
