using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VoyageForge.Terminal.Commands
{
    /// <summary>内置命令：help。</summary>
    [ConsoleCommand("help", "列出所有可用命令")]
    public sealed class HelpCommand
    {
        [RootCommand]
        public Task<object> Execute(CommandInvocation invocation)
        {
            ConsoleCommand root = Console.Instance != null ? Console.Instance.RootCommand : null;
            if (root == null) return Task.FromResult<object>(null);

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