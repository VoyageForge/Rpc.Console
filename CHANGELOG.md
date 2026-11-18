# Changelog

## 0.2.0

- 版本统一为 0.2.0。
- 命名空间 `VoyageForge.Terminal`，程序集名同步为 `VoyageForge.Terminal`（原 `VoyageForge.Console`）。
- 删除 `BuiltinCommandTree`：内置命令改为 `[ConsoleCommand]` 生成器命令（`help`/`clear`/`exit`/`log`/`listen`）。
- `log` 命令改为日志系统开关（`log on/off`，写入 `persistentDataPath/Console/logs`）。
- `Console` 增加 `RootCommand`/`RegisterCommand`，`ConsoleCommandCatalogAggregator` 自动聚合所有程序集命令。
- 示例增加场景 + UI 按钮（`Canvas`/`EventSystem`/`Camera`）。
- 测试：`VoyageForge.Terminal.Tests`（补全/执行/log 开关）。

## 0.2.1

- 命名空间/类名同步（`VoyageForge.Terminal` / `Console`）。