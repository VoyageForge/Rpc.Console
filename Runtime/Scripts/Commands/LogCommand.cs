using System.Threading.Tasks;
using VoyageForge.Terminal.Logging;

namespace VoyageForge.Terminal.Commands
{
    /// <summary>内置命令：log on/off，开启/关闭日志记录（persistentDataPath/Console/logs）。</summary>
    [ConsoleCommand("log", "开启/关闭日志记录（保存到 persistentDataPath）")]
    public sealed class LogCommand
    {
        /// <summary>参数补全候选：on/off。</summary>
        public System.Collections.Generic.IEnumerable<string> GetModes(CompletionContext context)
        {
            return new[] { "on", "off" };
        }

        [RootCommand]
        public Task<object> Execute(
            [CompletionSource("GetModes")] string mode, CommandInvocation invocation)
        {
            Console console = Console.HasInstance ? Console.Instance : null;
            LogRecorder recorder = console != null ? console.LogRecorder : null;
            if (recorder == null)
            {
                invocation.Context.Renderer.WriteError("日志记录器未初始化。");
                return Task.FromResult<object>(null);
            }

            if (string.Equals(mode, "on", System.StringComparison.OrdinalIgnoreCase))
            {
                recorder.Start();
                invocation.Context.Renderer.WriteLine("日志记录已开启：" + recorder.CurrentFilePath);
            }
            else if (string.Equals(mode, "off", System.StringComparison.OrdinalIgnoreCase))
            {
                string path = recorder.CurrentFilePath;
                recorder.Stop();
                invocation.Context.Renderer.WriteLine("日志记录已关闭：" + path);
            }
            else
            {
                invocation.Context.Renderer.WriteError("用法：log on|off");
            }

            return Task.FromResult<object>(null);
        }
    }
}