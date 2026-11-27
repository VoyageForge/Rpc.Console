using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VoyageForge.Terminal
{
    /// <summary>命令执行入口：接收一行文本、拆词、解析、调用 Handler，并把结果输出到 Renderer。</summary>
    /// <remarks>本类不处理输入框绘制、光标、历史记录和 Tab 按键；Unity 宿主自行管理这些 UI 交互。</remarks>
    public sealed class ConsoleExecutor
    {
        /// <summary>输出抽象；具体可以连接 UI Toolkit、UGUI、TextMeshPro 或终端。</summary>
        private readonly IConsoleRenderer _renderer;
        /// <summary>命令上下文使用的本地 Controller 执行器。</summary>
        private readonly VoyageForge.Rpc.Executor.LocalExecutor _controllers;
        /// <summary>供命令处理器解析可选依赖的服务容器。</summary>
        private readonly IServiceProvider _services;

        /// <summary>创建 Console 执行器。</summary>
        public ConsoleExecutor(IConsoleRenderer renderer, IServiceProvider services = null)
            : this(renderer, services, Commands.ConsolePanelController.CreateExecutor()) { }

        /// <summary>使用宿主提供的本地执行器，让命令处理器访问同一组 Controller 工厂。</summary>
        public ConsoleExecutor(IConsoleRenderer renderer, IServiceProvider services, VoyageForge.Rpc.Executor.LocalExecutor controllers)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            _services = services;
            _controllers = controllers ?? throw new ArgumentNullException(nameof(controllers));
        }

        /// <summary>执行一行命令。返回 true 表示处理器成功结束，false 表示解析失败、取消或处理异常。</summary>
        public async Task<bool> ExecuteAsync(ConsoleCommand root, string line,
            CancellationToken ct = default(CancellationToken))
        {
            // 第一步：拆词。格式错误应在解析命令树前显示给用户。
            IReadOnlyList<string> tokens;
            try
            {
                tokens = CommandParser.SplitArgs(line);
            }
            catch (FormatException exception)
            {
                _renderer.WriteError(exception.Message);
                return false;
            }

            // 第二步：只解析，不执行。ParseResult 还保存最终节点，便于构建 CommandContext。
            ParseResult parsed = CommandParser.Parse(root, tokens);
            if (!parsed.Success)
            {
                _renderer.WriteError(parsed.Error);
                return false;
            }

            // 第三步：为本次调用创建上下文。命令对象可能长期复用，所以不能把旧 Tokens 当作当前输入。
            CommandContext context = new CommandContext
            {
                Renderer = _renderer,
                Services = _services,
                Executor = _controllers,
                Command = parsed.Command,
                Tokens = tokens
            };

            try
            {
                ct.ThrowIfCancellationRequested();
                // 第四步：Handler 是 Source Generator 在编译期输出的强类型委托，运行时只需正常调用它。
                object result = await parsed.Command.Handler(new CommandInvocation
                {
                    Context = context,
                    Tokens = tokens,
                    Arguments = parsed.Arguments,
                    CancellationToken = ct
                }).ConfigureAwait(false);

                if (result != null) _renderer.WriteLine(FormatResult(result));
                return true;
            }
            catch (OperationCanceledException)
            {
                _renderer.WriteError("命令已取消。");
                return false;
            }
            catch (Exception exception)
            {
                _renderer.WriteError("命令执行失败：" + exception.Message);
                return false;
            }
        }

        /// <summary>默认结果格式：字符串原样输出，其他对象调用 ToString；复杂对象 JSON 格式化由宿主决定。</summary>
        private static string FormatResult(object result)
        {
            string text = result as string;
            if (text != null) return text;
            IFormattable formattable = result as IFormattable;
            return formattable != null
                ? formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture)
                : result.ToString();
        }
    }
}
