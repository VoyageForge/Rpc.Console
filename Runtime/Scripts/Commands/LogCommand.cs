using System.Threading.Tasks;

namespace VoyageForge.Terminal.Commands
{
    /// <summary>内置命令：log &lt;text&gt;。</summary>
    [ConsoleCommand("log", "打印一段文本")]
    public sealed class LogCommand
    {
        [RootCommand]
        public Task<object> Execute(string text, CommandInvocation invocation)
        {
            invocation.Context.Renderer.WriteLine(text ?? string.Empty);
            return Task.FromResult<object>(null);
        }
    }
}