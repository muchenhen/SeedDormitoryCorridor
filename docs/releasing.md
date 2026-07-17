# 发布流程

发布完全在受信任的 Windows 11 x64 本机执行，不使用 GitHub Actions。正式版本同时提供当前用户安装包和免安装 ZIP，并附带 SHA-256 校验文件。

## 版本策略

项目使用 Semantic Versioning：

- `0.x`：1.0 前的快速迭代，功能和资产格式仍可能调整。
- `-alpha.N`：粗糙的早期公开版本。
- `-beta.N`：主要功能已确定，集中修复兼容性和体验。
- `-rc.N`：候选正式版，只接受发布阻断修复。
- `1.0.0` 及以后：面向长期维护的稳定版本；补丁、兼容功能和破坏性变更分别递增 PATCH、MINOR 和 MAJOR。

版本的唯一默认来源是仓库根目录的 `Directory.Build.props`。底层 `publish.ps1` 接受显式 `-Version` 用于开发验证；正式 `release.ps1` 只接受与项目版本相同的值，并要求当前处于干净的 `main` 分支。

Windows 文件版本的第四段按渠道映射：`alpha.N` 使用 `N`，`beta.N` 使用 `10000 + N`，`rc.N` 使用 `20000 + N`，稳定版使用 `30000`。这让同一个 MAJOR.MINOR.PATCH 下的安装器升级顺序与预发布阶段一致。

## 前置条件

- Windows 11 x64
- .NET SDK 10
- Inno Setup 6，且 `ISCC.exe` 位于 PATH 或默认安装目录
- GitHub CLI，仅在创建 GitHub Release 时需要

安装包和 ZIP 当前未进行 Authenticode 代码签名。获得证书后应在本地构建流程中签名，并在上传前验证签名和哈希。

## 构建与验证

从干净且已同步的 `main` 分支执行：

```powershell
dotnet restore SeedDormitoryCorridor.sln
dotnet build SeedDormitoryCorridor.sln -c Release
dotnet test SeedDormitoryCorridor.sln -c Release --no-build
./scripts/release.ps1
```

产物位于 `artifacts/release/<version>`：

```text
SeedDormitoryCorridor-<version>-win-x64-setup.exe
SeedDormitoryCorridor-<version>-win-x64-portable.zip
SHA256SUMS.txt
```

发布前必须检查 ZIP 只包含本次全新 publish 的文件、三个内置宠物都存在、安装器和主程序版本正确，并按 `docs/manual-test-checklist.md` 完成人工验收。自动化测试不能代替真实桌面、安装和卸载测试。

## 创建 GitHub Release

验证提交已经位于 `main` 后创建带 `v` 前缀的标签。`0.x` 的 alpha/beta/rc 版本必须标为 prerelease：

```powershell
git tag -a v0.1.0-alpha.1 -m "SeedDormitoryCorridor v0.1.0-alpha.1"
git push origin v0.1.0-alpha.1
gh release create v0.1.0-alpha.1 `
  artifacts/release/0.1.0-alpha.1/* `
  --repo muchenhen/SeedDormitoryCorridor `
  --title "白荆科技宿舍走廊 v0.1.0-alpha.1" `
  --notes-file docs/releases/v0.1.0-alpha.1.md `
  --prerelease `
  --verify-tag
```

创建后从 GitHub 下载全部三个文件，重新计算 SHA-256，并确认与 `SHA256SUMS.txt` 一致。
