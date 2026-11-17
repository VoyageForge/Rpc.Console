using VoyageForge.Terminal.Commands;
using VoyageForge.Terminal.Generated;

namespace VoyageForge.Terminal
{
    /// <summary>
    /// 内置命令树：根节点 + 源生成器产出的命令目录（help/clear/exit/log/listen）。
    /// 命令节点由 [ConsoleCommand] 生成，不在本类手写。
    /// </summary>
    public sealed class BuiltinCommandTree
    {
        private readonly IConsoleRenderer _renderer;

        /// <summary>创建内置命令树构建器。</summary>
        public BuiltinCommandTree(IConsoleRenderer renderer)
        {
            _renderer = renderer;
        }

        /// <summary>构建内置命令树：根节点 + 所有程序集的生成器命令。</summary>
        public ConsoleCommand Build()
        {
            // 根节点保留 console；聚合器会收集 Runtime、示例、业务程序集里的 ICommandSource。
            ConsoleCommand root = new ConsoleCommand("console");
            ConsoleCommandCatalogAggregator aggregator = new ConsoleCommandCatalogAggregator();
            foreach (ConsoleCommand command in aggregator.Collect())
            {
                root.GetOrAddSubcommand(command.Name, command.Description);
                ConsoleCommand existing = root.FindSubcommand(command.Name);
                if (existing != null && ReferenceEquals(existing, command))
                {
                    continue;
                }

                if (existing != null)
                {
                    root.Subcommands.Remove(existing);
                }

                root.Subcommands.Add(command);
            }

            return root;
        }
    }
}