using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using VoyageForge.Terminal;

namespace VoyageForge.Terminal.Samples
{
    /// <summary>
    /// 创建自定义命令的示例：
    /// <list type="bullet">
    /// <item>用 <see cref="ConsoleCommand"/> 建树：命令 + 参数 + 子命令；</item>
    /// <item>用 <see cref="Console.Instance"/> 的 <c>RegisterCommand</c> 挂到控制台根节点；</item>
    /// <item>参数补全通过 <see cref="CompletionProvider"/> 动态提供。</item>
    /// </list>
    /// 运行后可在控制台输入 <c>demo hello world</c> 或 <c>demo greet Alice</c>。
    /// </summary>
    public sealed class CreateCommandSample : MonoBehaviour
    {
        /// <summary>演示命令名。</summary>
        private const string DemoCommandName = "demo";

        private IEnumerator Start()
        {
            // 1. 先确保控制台已初始化（预制体 + UI 构建）。
            ConsoleBootstrap.EnsureInitialized();

            // 2. 等待命令树就绪（OnInitialize 在 UIDocument 就绪后构建）。
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                Console console = Console.Instance;
                if (console != null && console.RootCommand != null)
                {
                    RegisterDemoCommand(console);
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning("[Console Sample] 控制台命令树未在 5 秒内就绪，示例命令未注册。");
        }

        /// <summary>把示例命令注册到控制台根节点。</summary>
        private void RegisterDemoCommand(Console console)
        {
            // 3. 创建命令节点：demo
            ConsoleCommand demo = new ConsoleCommand(DemoCommandName, "示例命令：回显文本");
            demo.Arguments.Add(new ConsoleArgument
            {
                Name = "text",
                Description = "要回显的文本（可省略）"
            });
            demo.Handler = invocation =>
            {
                string text = invocation.Arguments != null && invocation.Arguments.Length > 0
                    ? invocation.Arguments[0] as string
                    : string.Empty;
                invocation.Context.Renderer.WriteLine("demo: " + text);
                return Task.FromResult<object>(null);
            };

            // 4. 子命令：demo greet <name>，name 参数带动态补全候选。
            ConsoleCommand greet = demo.GetOrAddSubcommand("greet", "问候语");
            greet.Arguments.Add(new ConsoleArgument
            {
                Name = "name",
                Description = "名字",
                CompletionProvider = context => new[] { "Alice", "Bob", "Voyager" }
            });
            greet.Handler = invocation =>
            {
                string name = invocation.Arguments != null && invocation.Arguments.Length > 0
                    ? invocation.Arguments[0] as string
                    : "world";
                invocation.Context.Renderer.WriteLine("Hello, " + name + "!");
                return Task.FromResult<object>(null);
            };

            // 5. 注册到控制台根节点；同名命令会被忽略。
            bool registered = console.RegisterCommand(demo);
            Debug.Log(registered
                ? "示例命令已注册：demo / demo greet"
                : "示例命令已存在，跳过注册。");
        }
    }
}
