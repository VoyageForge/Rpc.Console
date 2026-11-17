using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VoyageForge.Terminal
{
    /// <summary>控制台输出抽象。运行时核心不绑定 Unity UI、终端或某个 UI 框架。</summary>
    public interface IConsoleRenderer
    {
        /// <summary>输出普通信息。</summary>
        void WriteLine(string text);
        /// <summary>输出错误信息，宿主可使用不同颜色或图标。</summary>
        void WriteError(string text);
        /// <summary>输出二维表格，具体样式由宿主决定。</summary>
        void WriteTable(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows);
    }

    /// <summary>命令来源接口，可把一个生成式 Builder 包装成插件式命令来源。</summary>
    public interface ICommandSource
    {
        /// <summary>返回该来源提供的根命令节点。</summary>
        IEnumerable<ConsoleCommand> GetCommands();
    }

    /// <summary>行编辑器接口；Unity 宿主可使用自己的输入框，不必支持 Console.ReadKey。</summary>
    public interface ILineEditor
    {
        /// <summary>读取一行输入；返回 null 表示输入结束。</summary>
        string ReadLine(string prompt);
        /// <summary>显示补全候选。</summary>
        void ShowCompletions(IReadOnlyList<string> candidates);
    }

    /// <summary>命令执行拦截器预留接口，例如权限检查、审计、计时或短路缓存。</summary>
    public interface IConsoleInterceptor
    {
        /// <summary>调用 next 继续处理；不调用 next 可直接拒绝执行。</summary>
        Task<object> InvokeAsync(ParseResult result, Func<Task<object>> next);
    }

    /// <summary>参数校验扩展点；可以在转换或业务调用前检查原始字符串规则。</summary>
    public interface IArgumentValidator
    {
        /// <summary>校验通过返回 true；失败返回 false 并填写用户可理解的错误说明。</summary>
        bool Validate(string name, string rawValue, out string error);
    }
}
