# VoyageForge RPC Console

基于命令树的运行时控制台（UI Toolkit）：UI 负责展示，命令层由 `ConsoleCommand` 树 + `ConsoleExecutor` 执行，补全由 `CommandCompletion` 计算。

## 执行路径

`ConsoleCommandLine` 收集输入，`CommandParser` 将文本解析为命令和参数，`ConsoleExecutor` 调用命令处理器并把结果写入 `IConsoleRenderer`。内置的 `clear` 命令通过 `invocation.Context.Executor.Local<ConsolePanelController>().Clear()` 操作本地控制台。命令层依赖 Rpc.Executor，运行命令无需创建网络 Session。

```csharp
var executor = new ConsoleExecutor(renderer);
bool succeeded = await executor.ExecuteAsync(root, "listen on", ct);
IReadOnlyList<string> suggestions = CommandCompletion.Get(root, new[] { "listen", "" });
```

## 声明与注册命令

在命令类上使用 `[ConsoleCommand]`，在入口方法上使用 `[RootCommand]`。方法上的 `[ConsoleCommand("子命令路径")]` 可声明多级子命令。命令参数由生成的处理器转换。[内置命令示例](Runtime/Scripts/Commands/ClearCommand.cs)

生成器为各业务程序集生成实现 `ICommandSource` 的目录。控制台初始化时，`ConsoleCommandCatalogAggregator` 收集这些目录中的命令。修改生成器本身后，在本包根目录运行 `Build-CodeGen.ps1`，将 DLL 部署至 `Runtime/Analyzers/VoyageForge.Console.CodeGen.dll`。[命令生成器源码](CodeGen~/VoyageForge.Console.CodeGen/ConsoleCommandGenerator.cs)

## 内置命令

| 命令 | 说明 |
|---|---|
| `help` | 列出当前命令/子命令 |
| `clear` | 清空日志 |
| `exit` | 退出播放（Editor）/退出应用 |
| `log <text>` | 输出一条日志 |
| `listen <on/off>` | 日志监听开关 |
| `listen autostart <on/off>` | 自启动开关 |

## 测试

- `Tests/EditMode/ConsoleCommandTreeTests.cs`：补全与执行（help/log/未知命令/子命令/参数补全）；
- `Tests/PlayMode/ConsoleCompletionPlayModeTests.cs`：`[UnityTest]` 补全行为。

## 示例

`Samples~/Console`：预制体引导（`Resources/Console/Console.prefab`）+ `RuntimeConsoleSample`。
