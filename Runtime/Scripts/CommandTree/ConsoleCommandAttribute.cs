using System;

namespace VoyageForge.Terminal
{
    /// <summary>
    /// 声明一个 Console 根命令，或声明该根命令下的路径式子命令。
    /// </summary>
    /// <remarks>
    /// 本特性同时允许出现在 class 和 method 上：
    /// <code>
    /// [ConsoleCommand("listen", "监听器控制")]
    /// public sealed class ListenCommand
    /// {
    ///     [ConsoleCommand("autostart on", "开启自启动")]
    ///     public void EnableAutoStart() { }
    /// }
    /// </code>
    /// class 上的 NameOrPath 是根命令名称；method 上的 NameOrPath 是相对于根节点的路径。
    /// 统一使用这一份 Attribute 类型，避免 ConsoleCommandAttribute 在多个 .cs 文件中重复定义。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ConsoleCommandAttribute : Attribute
    {
        /// <summary>类级用法是根命令名；方法级用法是一个或多个空格分隔的路径片段。</summary>
        public string NameOrPath { get; }
        /// <summary>显示在 help 输出中的说明文本。</summary>
        public string Description { get; }

        /// <summary>声明根命令或子命令。空路径会由生成器报告 CONSOLE001。</summary>
        public ConsoleCommandAttribute(string nameOrPath, string description = null)
        {
            NameOrPath = nameOrPath;
            Description = description;
        }
    }
}
