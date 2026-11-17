using System;

namespace VoyageForge.Terminal
{
    /// <summary>可选命令基类。命令类型不继承本类也可以被生成器识别和执行。</summary>
    /// <remarks>
    /// 继承后获得当前上下文、输出助手、依赖获取助手和生命周期钩子。
    /// 不依赖 Microsoft.Extensions.DependencyInjection 或 Microsoft.Extensions.Logging，避免 Runtime 绑定第三方库。
    /// </remarks>
    public abstract class CommandBase
    {
        /// <summary>
        /// 当前命令上下文。Builder 创建命令实例后注入一次；每次实际执行 Handler 时，再替换为本次 invocation 的上下文。
        /// setter 保持 internal，避免业务代码随意替换框架上下文；生成代码通过下方 __GeneratedSetContext 桥接入口注入。
        /// </summary>
        public CommandContext Context { get; internal set; }

        /// <summary>便捷获取当前输出渲染器；未注入 Context 时返回 null。</summary>
        protected IConsoleRenderer Console => Context == null ? null : Context.Renderer;
        /// <summary>便捷获取当前服务容器；未注入 Context 时返回 null。</summary>
        protected IServiceProvider Services => Context == null ? null : Context.Services;

        /// <summary>输出普通文本；Renderer 为空时不操作。</summary>
        protected void Print(string message) { if (Console != null) Console.WriteLine(message); }
        /// <summary>输出错误文本；Renderer 为空时不操作。</summary>
        protected void PrintError(string message) { if (Console != null) Console.WriteError(message); }

        /// <summary>获取服务容器中的依赖；如果没有注册则抛出明确异常。</summary>
        protected T GetService<T>()
        {
            object service = Services == null ? null : Services.GetService(typeof(T));
            if (service == null) throw new InvalidOperationException("未注册服务：" + typeof(T).FullName);
            return (T)service;
        }

        /// <summary>每次处理方法开始之前调用。派生类可重写以准备状态、开始计时或记录审计信息。</summary>
        protected virtual void OnCommandEntering() { }
        /// <summary>每次处理方法正常或异常结束后调用。生成代码在 finally 中确保调用。</summary>
        protected virtual void OnCommandExiting() { }
        /// <summary>处理方法抛出异常时调用，生成代码随后重新抛出原异常给 ConsoleExecutor。</summary>
        protected virtual void OnCommandFailed(Exception exception) { }

        // 生成 Builder 是独立静态类，不是 CommandBase 的派生类，不能直接访问 protected 生命周期方法。
        // 下面三个 public 桥接入口只供生成代码调用；业务命令正常只需要 override 上面的钩子。
        /// <summary>生成代码调用的上下文注入桥接方法；Context 的 setter 对业务程序集保持 internal。</summary>
        public void __GeneratedSetContext(CommandContext context) { Context = context; }
        /// <summary>生成代码调用的进入钩子桥接方法。</summary>
        public void __GeneratedOnEntering() { OnCommandEntering(); }
        /// <summary>生成代码调用的退出钩子桥接方法。</summary>
        public void __GeneratedOnExiting() { OnCommandExiting(); }
        /// <summary>生成代码调用的失败钩子桥接方法。</summary>
        public void __GeneratedOnFailed(Exception exception) { OnCommandFailed(exception); }
    }
}
