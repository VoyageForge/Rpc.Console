using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VoyageForge.Terminal.Tests
{
    /// <summary>PlayMode 下的命令树补全规则。</summary>
    public sealed class ConsoleCompletionPlayModeTests
    {
        private sealed class NullRenderer : IConsoleRenderer
        {
            public void WriteLine(string text) { }
            public void WriteError(string text) { }
            public void WriteTable(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows) { }
        }

        private ConsoleCommand _root;

        [SetUp]
        public void SetUp()
        {
            _root = new BuiltinCommandTree(new NullRenderer()).Build();
        }

        [UnityTest]
        public IEnumerator 命令名补全()
        {
            IReadOnlyList<string> candidates = CommandCompletion.Get(_root, new[] { "cl" });
            CollectionAssert.Contains(candidates, "clear");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 参数值补全()
        {
            IReadOnlyList<string> candidates = CommandCompletion.Get(_root, new[] { "listen", "" });
            CollectionAssert.Contains(candidates, "on");
            CollectionAssert.Contains(candidates, "off");
            yield return null;
        }
    }
}