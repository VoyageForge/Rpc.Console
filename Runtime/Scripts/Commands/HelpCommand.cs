using System.Text;
using System.Threading.Tasks;

namespace VoyageForge.Terminal.Commands
{
    /// <summary>内置命令：help（列出所有已注册命令）。</summary>
    [ConsoleCommand("help", "列出所有可用命令")]
    public sealed class HelpCommand
    {
        [RootCommand]
        public Task<object> Execute(CommandInvocation invocation)
        {
            // 不依赖父链：直接从聚合器重建根命令列表，测试/宿主都能拿到完整命令集。
            ConsoleCommand root = new ConsoleCommand("console");
            ConsoleCommandCatalogAggregator aggregator = new ConsoleCommandCatalogAggregator();
            foreach (ConsoleCommand command in aggregator.Collect())
            {
                root.Subcommands.Add(command);
            }

            StringBuilder builder = new StringBuilder("[Console] 可用命令：");
            foreach (ConsoleCommand command in root.Subcommands)
            {
                builder.Append('\n').Append("  ").Append(command.Name);
                if (!string.IsNullOrWhiteSpace(command.Description))
                {
                    builder.Append("    ").Append(command.Description);
                }
            }

            invocation.Context.Renderer.WriteLine(builder.ToString());
            return Task.FromResult<object>(null);
        }
    }
}