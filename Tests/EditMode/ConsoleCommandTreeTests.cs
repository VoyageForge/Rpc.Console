using System.Collections.Generic;
using NUnit.Framework;

namespace VoyageForge.Terminal.Tests
{
    /// <summary>命令树补全与执行测试（不依赖 UI）。</summary>
    public sealed class ConsoleCommandTreeTests
    {
        private static ConsoleCommand BuildRoot(IConsoleRenderer renderer)
        {
            ConsoleCommand root = new ConsoleCommand("console");
            ConsoleCommandCatalogAggregator aggregator = new ConsoleCommandCatalogAggregator();
            int count = 0;
            foreach (ConsoleCommand command in aggregator.Collect())
            {
                root.Subcommands.Add(command);
                count++;
            }

            UnityEngine.Debug.Log("[Test] collected commands: " + count);
            foreach (ConsoleCommand command in root.Subcommands)
            {
                UnityEngine.Debug.Log("[Test] command " + command.Name + " args=" + command.Arguments.Count + " subs=" + command.Subcommands.Count + " handler=" + (command.Handler != null));
            }
            return root;
        }

        /// <summary>捕获输出的测试渲染器。</summary>
        private sealed class CaptureRenderer : IConsoleRenderer
        {
            public readonly List<string> Lines = new List<string>();
            public readonly List<string> Errors = new List<string>();

            public void WriteLine(string text) => Lines.Add(text);
            public void WriteError(string text) => Errors.Add(text);
            public void WriteTable(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows) { }
        }

        private CaptureRenderer _renderer;
        private ConsoleCommand _root;

        [SetUp]
        public void SetUp()
        {
            _renderer = new CaptureRenderer();
            _root = BuildRoot(_renderer);
        }

        [Test]
        public void 补全_第一层命令()
        {
            IReadOnlyList<string> candidates = CommandCompletion.Get(_root, new[] { "he" });
            CollectionAssert.Contains(candidates, "help");
        }

        [Test]
        public void 补全_子命令与参数值()
        {
            IReadOnlyList<string> candidates = CommandCompletion.Get(_root, new[] { "listen", "" });
            CollectionAssert.Contains(candidates, "autostart");
            CollectionAssert.Contains(candidates, "on");
            CollectionAssert.Contains(candidates, "off");
        }

        [Test]
        public void 补全_子命令参数()
        {
            IReadOnlyList<string> candidates = CommandCompletion.Get(_root, new[] { "listen", "autostart", "" });
            CollectionAssert.Contains(candidates, "on");
            CollectionAssert.Contains(candidates, "off");
        }

        [Test]
        public void 执行_log_开关()
        {
            // 测试环境没有 Console 实例，log 命令应通过错误输出提示记录器未初始化。
            bool ok = new ConsoleExecutor(_renderer).ExecuteAsync(_root, "log on").GetAwaiter().GetResult();
            Assert.IsTrue(ok);
            Assert.IsTrue(_renderer.Errors.Exists(line => line.Contains("未初始化")),
                "期望提示日志记录器未初始化。");
        }

        [Test]
        public void 执行_help_列出命令()
        {
            bool ok = new ConsoleExecutor(_renderer).ExecuteAsync(_root, "help").GetAwaiter().GetResult();
            Assert.IsTrue(ok);
            StringAssert.Contains("help", _renderer.Lines[0]);
        }

        [Test]
        public void 执行_未知命令_返回失败()
        {
            bool ok = new ConsoleExecutor(_renderer).ExecuteAsync(_root, "nope").GetAwaiter().GetResult();
            Assert.IsFalse(ok);
            Assert.Greater(_renderer.Errors.Count, 0);
        }
    }
}