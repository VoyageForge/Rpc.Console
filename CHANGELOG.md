# Changelog

## 0.2.0

- Console 迁移到 `VoyageForge-RPC/Rpc.Console`（包名 `com.voyageforge.rpc-console`）。
- 命令层替换为**命令树**：`ConsoleCommand` / `ConsoleExecutor` / `CommandCompletion` / `CommandParser`。
- 删除旧命令组/请求/`ICommandBehavior`/`CompletionItem` 模型；`RuntimeConsole` 作为 `IConsoleRenderer` + 命令树宿主。
- 内置命令树：`help` / `clear` / `exit` / `log` / `listen` / `listen autostart`。
- 补全改为 `CompletionItem` + 参数级中文提示（动态，不写死）。
- 保留 UI：`Console.uxml` / `Console.uss` / `ConsoleSettings.asset` / `Console.prefab`。
- 测试改为 EditMode（命令树补全/执行）+ PlayMode（真实 UI 补全）。

## 0.1.0

- 从 Depot 包提取为独立的 `com.voyageforge.console` 包，依赖 `com.voyageforge.rpc` 与 `com.voyageforge.depot`。
- 内置命令改为 Rpc 命令模型并拆分：`help` / `clear` / `exit` / `log` / `listen`。
- `listen` 改为命令组：默认行为 `listen on|off`，子命令 `listen autostart on|off`。
- 输入执行改用 `CommandRegistry.TryExecuteLine`（多 token 命令名最长匹配）。
- 补全生成器迁入本包（`CodeGen~/VoyageForge.Console.SourceGen`）。
- 示例新增 `GroupedCommandSample`（`[DefaultCommand]` + 子命令 + 位置参数）。
