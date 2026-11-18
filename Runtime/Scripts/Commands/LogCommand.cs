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
            if (string.Equals(mode, "on", System.StringComparison.OrdinalIgnoreCase))
            {
                if (LogRecorderBridge.IsRecording)
                {
                    invocation.Context.Renderer.WriteLine("日志记录已在开启状态：" + LogRecorderBridge.CurrentFilePath);
                    return Task.FromResult<object>(null);
                }

                LogRecorderBridge.Start();
                invocation.Context.Renderer.WriteLine("日志记录已开启：" + LogRecorderBridge.CurrentFilePath);
            }
            else if (string.Equals(mode, "off", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!LogRecorderBridge.IsRecording)
                {
                    invocation.Context.Renderer.WriteLine("日志记录未开启，上次文件：" + LogRecorderBridge.LastFilePath);
                    return Task.FromResult<object>(null);
                }

                string path = LogRecorderBridge.CurrentFilePath;
                LogRecorderBridge.Stop();
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