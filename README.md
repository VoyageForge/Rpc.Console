# VoyageForge RPC Console

基于命令树的运行时控制台（UI Toolkit）：UI 负责展示，命令层由 `ConsoleCommand` 树 + `ConsoleExecutor` 执行，补全由 `CommandCompletion` 计算。

## 架构

```
Runtime/Scripts/
  Console.cs                控制台单例（MonoSingleton），IConsoleRenderer
  ConsoleCommandLine.cs     输入行：按键、历史、补全、执行与结果展示
  ConsoleLogList.cs         日志列表
  ConsoleLogEntry.cs        日志条目
  BuiltinCommandTree.cs     内置命令树：help / clear / exit / log / listen
  CommandTree/
    CommandBase.cs              命令基类（可选）
    ConsoleCommand.cs           命令节点：名称、描述、参数、子命令、处理委托
    ConsoleCommandAttribute.cs  [ConsoleCommand("路径", "描述")]
    ConsoleCommandLine.cs       输入解析（CommandParser）
    ConsoleCommandLine 相关：
    CommandParser.cs            分词（引号、转义）
    CommandCompletion.cs        补全（命令/子命令/参数级中文提示）
    CommandArgumentConverter.cs 字符串 → 参数类型
    ConsoleExecutor.cs          执行器（实例类，非静态/非单例）
    ConsoleModels.cs            CommandInvocation / CompletionItem
    ConsoleContracts.cs         IConsoleRenderer / ICommandArgumentCompletionProvider
  Commands/
    ListenLogBridge.cs          listen 命令与日志监听的桥接
  Resources/Console/           RuntimeConsole.uxml / .uss / ConsoleSettings.asset
```

分层：

- **UI**：`RuntimeConsole`（面板、日志、输入行、补全列表）；
- **命令树**：`ConsoleCommand` 描述命令/子命令/参数；`ConsoleExecutor` 执行；`CommandCompletion` 补全；
- **宿主**：在 `RuntimeConsole` 构建时创建根命令并注入 `ConsoleExecutor`。

## 命令树 API

```csharp
// 根命令
ConsoleCommand root = new ConsoleCommand("console", "控制台命令");

// 子命令 + 参数
ConsoleCommand listen = root.Add("listen", "控制日志监听开关");
listen.AddArgument("mode", "日志监听开关（on/off）", completion: new[] { "on", "off" });

// 执行器（实例，不是静态类/单例）
ConsoleExecutor executor = new ConsoleExecutor(renderer);
RpcResponse result = await executor.ExecuteAsync(root, "listen on", ct);

// 补全（命令/子命令/参数）
IReadOnlyList<CompletionItem> items = CommandCompletion.Get(root, new[] { "listen", "" });
```

`[ConsoleCommand]` 标注的命令类由源生成器（`CodeGen~/VoyageForge.Terminal.SourceGen`）收集，未标注时也可手动建树。

## 源代码生成器（CodeGen~）

`CodeGen~/VoyageForge.Console.CodeGen` 扫描 `[ConsoleCommand]`，生成**实例类** `ConsoleCommandCatalog : ICommandSource`（不生成静态入口）。

```csharp
[ConsoleCommand("demo2", "生成器示例命令")]
public sealed class Demo2Command
{
    [RootCommand]
    public Task<object> Execute(CommandInvocation invocation) { ... }

    [ConsoleCommand("add", "整数加法")]
    public Task<object> Add(int a, int b, CommandInvocation invocation) { ... }
}
```

注册：

```csharp
ConsoleCommandCatalog catalog = new ConsoleCommandCatalog();
foreach (ConsoleCommand command in catalog.GetCommands())
    console.RegisterCommand(command);
```

生成内容：

- 每个根命令类的实例化 + `ConsoleCommand` 节点（路径按空格拆分，复用 `GetOrAddSubcommand`）；
- 方法参数 → `ConsoleArgument`（Name / `Type` / Description），Handler 用 `CommandArgumentConverter.Convert<T>` 转换；
- `[RootCommand]` → 根节点 Handler；`[ConsoleCommand("sub path")]` / `[ConsoleSubcommand("sub path")]` → 子命令 Handler；
- 返回类型支持 `void` / `Task` / `Task<object>` / `object`。

构建：

```powershell
.\Build-CodeGen.ps1
```

DLL 部署到 `Runtime/Analyzers/VoyageForge.Console.CodeGen.dll`，meta 由模板生成（Asset Label `RoslynAnalyzer`）。

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
