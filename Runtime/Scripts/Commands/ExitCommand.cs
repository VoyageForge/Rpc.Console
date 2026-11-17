using System.Threading.Tasks;

namespace VoyageForge.Terminal.Commands
{
    /// <summary>内置命令：exit。</summary>
    [ConsoleCommand("exit", "退出（隐藏）控制台")]
    public sealed class ExitCommand
    {
        [RootCommand]
        public Task<object> Execute(CommandInvocation invocation)
        {
            Console.Instance?.Hide();
            return Task.FromResult<object>(null);
        }
    }
}