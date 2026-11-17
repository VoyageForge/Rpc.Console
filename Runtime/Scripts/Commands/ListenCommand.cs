using System.Threading.Tasks;

namespace VoyageForge.Terminal.Commands
{
    /// <summary>内置命令：listen &lt;on/off&gt; 与 listen autostart &lt;on/off&gt;。</summary>
    [ConsoleCommand("listen", "控制日志监听开关与自启动")]
    public sealed class ListenCommand
    {
        [RootCommand]
        public Task<object> Execute(string mode, CommandInvocation invocation)
        {
            ListenLogBridge.HandleMode(mode);
            return Task.FromResult<object>(null);
        }

        [ConsoleCommand("autostart", "日志监听自启动")]
        public Task<object> AutoStart(string value, CommandInvocation invocation)
        {
            ListenLogBridge.HandleAutoStart(value);
            return Task.FromResult<object>(null);
        }
    }
}