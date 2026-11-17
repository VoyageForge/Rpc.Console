using System;

namespace VoyageForge.Terminal
{
    /// <summary>
    /// 兼容形式的子命令声明特性。新代码优先使用方法级 [ConsoleCommand("path", "desc")]。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ConsoleSubcommandAttribute : Attribute
    {
        /// <summary>子命令路径，可包含空格，例如 autostart on。</summary>
        public string Path { get; }
        /// <summary>帮助信息中的说明。</summary>
        public string Description { get; }

        /// <summary>声明子命令路径。</summary>
        public ConsoleSubcommandAttribute(string path, string description = null)
        {
            Path = path;
            Description = description;
        }
    }

    /// <summary>标记命令类根节点本身的处理方法，例如输入 listen 时执行 Status。</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class RootCommandAttribute : Attribute { }

    /// <summary>把位置参数关联到命令类中的动态补全方法。</summary>
    /// <remarks>目标方法必须是 public 实例方法，签名为 IEnumerable&lt;string&gt; Method(CompletionContext context)。</remarks>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class CompletionSourceAttribute : Attribute
    {
        /// <summary>候选提供方法的名称，推荐写 nameof(GetCandidates)。</summary>
        public string MethodName { get; }
        /// <summary>声明动态补全提供方法。</summary>
        public CompletionSourceAttribute(string methodName) { MethodName = methodName; }
    }
}
