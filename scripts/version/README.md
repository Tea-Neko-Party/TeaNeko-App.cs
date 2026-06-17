# 版本脚本

这里存放项目版本管理相关脚本。版本号统一由仓库根目录的 `Version.props` 和 `VERSION` 管理，所有 SDK 风格的 C# 子项目会通过根目录 `Directory.Build.props` 自动继承同一个发布版本。

- `Version.props` 存储 MSBuild 使用的项目版本属性。
- `VERSION` 同步保存纯文本版本号，方便快速查看。
- `Directory.Build.props` 会导入 `Version.props`，所以现有和后续新增的 `.csproj` 都会自动继承 `Version`、`PackageVersion`、`AssemblyVersion`、`FileVersion` 和 `InformationalVersion`。
- 发布标签使用 `vA.B.C` 格式，例如 `v5.0.0`。
- 版本日志会写入 `docs/version/A.B.C.md`。

## 版本号规则

版本号使用 `A.B.C` 格式：

- `A`：大版本。执行 `bump major` 时加 1，并将 `B`、`C` 清零。
- `B`：小版本。执行 `bump minor` 时加 1，并将 `C` 清零。
- `C`：补丁版本。执行 `bump patch` 时加 1。

## PowerShell 命令

在仓库根目录执行：

```powershell
.\scripts\version\version.ps1 show
.\scripts\version\version.ps1 bump patch
.\scripts\version\version.ps1 bump minor
.\scripts\version\version.ps1 bump major
.\scripts\version\version.ps1 set -Version 5.1.0
.\scripts\version\version.ps1 release current
.\scripts\version\version.ps1 release patch
```

## 双击脚本

可直接双击以下 `.cmd` 文件：

- `bump-patch.cmd`：补丁版本加 1，例如 `5.0.0` -> `5.0.1`。
- `bump-minor.cmd`：小版本加 1，并将补丁版本清零，例如 `5.0.1` -> `5.1.0`。
- `bump-major.cmd`：大版本加 1，并将小版本和补丁版本清零，例如 `5.1.2` -> `6.0.0`。
- `release-current.cmd`：使用当前版本生成版本日志、提交并创建 git tag。
- `release-patch.cmd`：先递增补丁版本，再发布。
- `release-minor.cmd`：先递增小版本，再发布。
- `release-major.cmd`：先递增大版本，再发布。

## 发布行为

`release` 命令会：

1. 检查工作区是否干净。
2. 按需递增版本号。
3. 根据上一个 `vA.B.C` git tag 生成 `docs/version/A.B.C.md`。
4. 提交 `VERSION`、`Version.props`、`Directory.Build.props` 和版本日志。
5. 创建 annotated git tag，例如 `v5.0.0`。

默认发布前必须保持工作区干净。如果确实需要在存在未提交改动时发布，可以增加 `-AllowDirty` 参数。

## 发布流程

1. 先提交正常的代码改动。
2. 运行一个发布命令，例如 `.\scripts\version\version.ps1 release patch`。
3. 脚本会按需递增版本号，生成 `docs/version/A.B.C.md`，创建发布提交，并创建 annotated git tag。

发布命令默认要求工作区干净。只有在明确知道自己要保留未提交改动并继续发布时，才使用 `-AllowDirty`。
