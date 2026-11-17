using System.Threading.Tasks;

namespace VoyageForge.Terminal.Commands
{
    /// <summary>内置命令：clear。</summary>
    [ConsoleCommand("clear", "清空控制台日志")]
    public sealed class ClearCommand
    {
        [RootCommand]
        public Task<object> Execute(CommandInvocation invocation)
        {
            Console.Instance?.Clear();
            return Task.FromResult<object>(null);
        }
    }
}