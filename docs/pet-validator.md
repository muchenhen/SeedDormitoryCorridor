# 宠物包校验 CLI

`sdc-pet-validator` 为 SDCWeb 和其他自动化调用方提供无界面的宠物包校验入口。它复用桌面客户端的安全 staging、`PetPackageLoader`、Profile、Atlas、PNG 和 Alpha 校验，不维护第二套规则，也不会写入正式宠物目录。

## 构建与发布

需要 Windows x64 和 .NET SDK 10：

```powershell
dotnet build src/SeedDormitoryCorridor.PetValidator/SeedDormitoryCorridor.PetValidator.csproj -c Release
dotnet publish src/SeedDormitoryCorridor.PetValidator/SeedDormitoryCorridor.PetValidator.csproj `
  -c Release -r win-x64 --self-contained true `
  -o artifacts/pet-validator
```

CLI 是服务端工具，不会被 `scripts/publish.ps1` 放进桌面客户端安装包。部署 SDCWeb 时应单独固定并发布与客户端兼容的 validator 版本。

## 调用

目录和 ZIP 使用同一命令：

```powershell
sdc-pet-validator validate ./sample-pet --format json
sdc-pet-validator validate ./sample-pet.zip --format json
```

ZIP 可以在根目录直接包含 `pet.json`，也可以仅有一层包装目录。包内存在多个 `pet.json` 时拒绝校验。

退出码：

| 退出码 | 含义 |
| ---: | --- |
| `0` | 宠物包通过全部校验 |
| `1` | 输入可读取，但宠物包不符合格式或安全规则 |
| `2` | 参数错误、路径不存在、权限错误或程序运行失败 |

省略 `--format` 或使用 `--format text` 时输出适合人工阅读的摘要。服务端必须使用 `--format json`，并同时检查退出码与 `valid`。

## JSON 契约

成功示例：

```json
{
  "valid": true,
  "package": {
    "id": "sample-pet",
    "displayName": "Sample Pet",
    "description": "A sample desktop pet.",
    "spriteVersionNumber": 1,
    "profile": "codex-pet-v2",
    "width": 1536,
    "height": 1872
  },
  "issues": []
}
```

失败时 `package` 为 `null`，`issues` 中每项均包含 `severity`、`code`、`message`、`jsonPath` 和 `filePath`；无对应位置的字段值为 `null`。`filePath` 对包内文件使用 `/` 分隔的相对路径，不暴露随机 staging 路径。

```json
{
  "valid": false,
  "package": null,
  "issues": [
    {
      "severity": "error",
      "code": "manifest.id.required",
      "message": "id 不能为空。",
      "jsonPath": "$.id",
      "filePath": null
    }
  ]
}
```

错误码按所属层使用稳定前缀：

- `cli.*`：参数、路径或运行失败，对应退出码 `2`。
- `package.*`：目录/ZIP staging、路径、容量和包根结构错误。
- `manifest.*`、`spritesheet.*`、`profile.*`、`atlas.*`：桌面客户端正式资源校验错误。

## 服务端调用约束

- 将上传文件保存到服务端隔离的临时目录，再把该路径作为单个参数传给 CLI；不得拼接 shell 命令。
- 设置进程超时，并限制上传体积。CLI 内部限制为最多 256 个 ZIP 条目、单文件 64 MiB、总解压大小 128 MiB。
- 只在退出码为 `0` 且 JSON `valid` 为 `true` 时接受上传。
- 不信任 `displayName`、`description`、错误消息或包内文件名；日志和 UI 仍需正确转义。
- 调用完成后由服务端删除原始上传临时文件。CLI 自己创建的随机 staging 事务会在成功和失败路径中清理。

CLI 只验证格式、安全性和客户端兼容性，不判断角色素材版权，也不执行包内任何文件。
