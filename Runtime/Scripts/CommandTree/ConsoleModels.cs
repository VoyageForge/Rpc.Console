using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VoyageForge.Terminal
{
    /// <summary>一个位置参数的元数据。每次用户输入的实际值不保存在这里。</summary>
    public sealed class ConsoleArgument
    {
        /// <summary>参数名，直接取自 C# 方法参数名，例如 server。</summary>
        public string Name { get; set; }
        /// <summary>供 help UI 使用的说明文字。</summary>
        public string Description { get; set; }
        /// <summary>参数目标 CLR 类型；默认 string，生成器会从命令方法参数类型写入。</summary>
        public Type Type { get; set; } = typeof(string);
        /// <summary>动态候选提供委托。为 null 表示该参数没有补全提示。</summary>
        public CompletionProvider CompletionProvider { get; set; }
    }

    /// <summary>
    /// 一棵命令树中的节点。节点可以是可执行叶子、纯分组节点，或同时可执行并拥有子命令的混合节点。
    /// </summary>
    /// <remarks>
    /// 例如 listen 节点可以有 Status Handler，同时拥有 on/off/autostart 子节点。
    /// autostart 节点可以没有 Handler，只负责把 on/off 继续分组。
    /// </remarks>
    public sealed class ConsoleCommand
    {
        /// <summary>本节点的名称，只包含当前一段，不包含父级命令路径。</summary>
        public string Name { get; }
        /// <summary>节点描述。中间分组节点的描述可以为空。</summary>
        public string Description { get; private set; }
        /// <summary>本节点的位置参数定义列表，顺序与 C# 方法参数声明一致。</summary>
        public IList<ConsoleArgument> Arguments { get; } = new List<ConsoleArgument>();
        /// <summary>直接子节点集合，不包含更深层级的后代节点。</summary>
        public IList<ConsoleCommand> Subcommands { get; } = new List<ConsoleCommand>();
        /// <summary>执行本节点时调用的委托；没有处理器的纯分组节点为 null。</summary>
        public Func<CommandInvocation, Task<object>> Handler { get; set; }

        /// <summary>创建命令节点；命令名不能为空或只包含空白。</summary>
        public ConsoleCommand(string name, string description = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("命令名称不能为空。", nameof(name));
            Name = name;
            Description = description;
        }

        /// <summary>按 Ordinal 大小写敏感规则查找一个直接子节点。</summary>
        public ConsoleCommand FindSubcommand(string name)
        {
            return Subcommands.FirstOrDefault(command => string.Equals(command.Name, name, StringComparison.Ordinal));
        }

        /// <summary>
        /// 查找或创建一个直接子节点。生成器对路径的每一段都调用此方法。
        /// </summary>
        /// <remarks>
        /// 先生成 autostart on，再生成 autostart off 时，第二次会拿到之前创建的 autostart 节点，
        /// 两条路径共享中间节点。若不复用，就可能出现两个同名的 autostart 分组，解析和补全会变得不确定。
        /// </remarks>
        public ConsoleCommand GetOrAddSubcommand(string name, string description = null)
        {
            ConsoleCommand existing = FindSubcommand(name);
            if (existing != null)
            {
                // 中间节点第一次可能没有描述；之后某个命令把它作为最终路径段时，可以补上描述。
                if (string.IsNullOrWhiteSpace(existing.Description) && !string.IsNullOrWhiteSpace(description))
                    existing.Description = description;
                return existing;
            }

            ConsoleCommand created = new ConsoleCommand(name, description);
            Subcommands.Add(created);
            return created;
        }
    }

    /// <summary>宿主在 Build 阶段和每次执行阶段提供给命令对象的上下文。</summary>
    public sealed class CommandContext
    {
        /// <summary>输出目标。可由 Unity UI Toolkit、UGUI、终端或日志窗口实现。</summary>
        public IConsoleRenderer Renderer { get; set; }
        /// <summary>依赖服务容器；生成器可注入 IServiceProvider 和可选 ILogger。</summary>
        public IServiceProvider Services { get; set; }
        /// <summary>当前命令节点；执行时指向这一次命中的具体节点。</summary>
        public ConsoleCommand Command { get; set; }
        /// <summary>当前命令行拆分后的 token 快照。</summary>
        public IReadOnlyList<string> Tokens { get; set; }
    }

    /// <summary>每次调用 Handler 时传递的执行数据。</summary>
    public sealed class CommandInvocation
    {
        /// <summary>本次调用的上下文，包含 Renderer、Services、Command 和 Tokens。</summary>
        public CommandContext Context { get; set; }
        /// <summary>完整 token 列表，包含根命令名。</summary>
        public IReadOnlyList<string> Tokens { get; set; }
        /// <summary>位置参数原始值，生成代码会按目标参数类型逐项转换。</summary>
        public object[] Arguments { get; set; }
        /// <summary>当前命令的取消令牌，不占用位置参数槽位。</summary>
        public CancellationToken CancellationToken { get; set; }
    }

    /// <summary>调用动态补全提供方法时传入的上下文。</summary>
    public sealed class CompletionContext
    {
        /// <summary>已经完整输入的位置参数，不包含当前正在编辑的最后一个 token。</summary>
        public IReadOnlyList<string> PriorArguments { get; set; }
        /// <summary>光标当前 token 的前缀；行尾空格时为空字符串。</summary>
        public string CurrentPrefix { get; set; }
        /// <summary>完整输入 token 快照，provider 可以据此判断命令和前置参数。</summary>
        public IReadOnlyList<string> AllTokens { get; set; }
    }

    /// <summary>动态补全提供器委托。</summary>
    public delegate IEnumerable<string> CompletionProvider(CompletionContext context);

    /// <summary>命令解析结果；解析只定位命令和收集参数，不执行业务代码。</summary>
    public sealed class ParseResult
    {
        /// <summary>解析是否成功。</summary>
        public bool Success { get; set; }
        /// <summary>命中的最终节点。</summary>
        public ConsoleCommand Command { get; set; }
        /// <summary>尚未进行 CLR 类型转换的位置参数原始值。</summary>
        public object[] Arguments { get; set; } = Array.Empty<object>();
        /// <summary>解析失败时的提示文本。</summary>
        public string Error { get; set; }
    }
}
