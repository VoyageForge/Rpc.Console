using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using VoyageForge.Terminal;
using VoyageForge.Terminal.Generated;

namespace VoyageForge.Terminal.Samples
{
    /// <summary>
    /// 源代码生成器示例：用 [ConsoleCommand] 标注命令类，生成器自动构建命令树。
    /// 输入 <c>demo2 add 1 2</c> 或 <c>demo2 hello</c>。
    /// </summary>
    public sealed class GeneratedCommandSample : MonoBehaviour
    {
        private IEnumerator Start()
        {
            ConsoleBootstrap.EnsureInitialized();

            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                Console console = Console.Instance;
                if (console != null && console.RootCommand != null)
                {
                    RegisterGeneratedCommands(console);
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning("[Console Sample] 控制台命令树未在 5 秒内就绪。");
        }

        private void RegisterGeneratedCommands(Console console)
        {
            // 生成器产物是实例类：不依赖静态入口，也不扫描反射。
            VoyageForge_Console_SamplesCommandCatalog catalog = new VoyageForge_Console_SamplesCommandCatalog();
            foreach (ConsoleCommand command in catalog.GetCommands())
            {
                console.RegisterCommand(command);
            }

            Debug.Log("[Console Sample] 生成器命令已注册：demo2 / demo2 add");
        }
    }

    /// <summary>生成器示例命令：根命令 demo2 + 子命令 demo2 add。</summary>
    [ConsoleCommand("demo2", "生成器示例命令")]
    public sealed class Demo2Command
    {
        /// <summary>demo2 hello：根节点执行方法。</summary>
        [RootCommand]
        public Task<object> Execute(CommandInvocation invocation)
        {
            invocation.Context.Renderer.WriteLine("demo2: hello");
            return Task.FromResult<object>(null);
        }

        /// <summary>demo2 add &lt;a&gt; &lt;b&gt;：参数类型由生成器转换为 int。</summary>
        [ConsoleCommand("add", "整数加法")]
        public Task<object> Add(int a, int b, CommandInvocation invocation)
        {
            invocation.Context.Renderer.WriteLine($"{a} + {b} = {a + b}");
            return Task.FromResult<object>(null);
        }
    }
}
