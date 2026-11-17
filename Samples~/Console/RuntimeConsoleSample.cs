using UnityEngine;
using VoyageForge.Terminal;

namespace VoyageForge.Terminal.Samples
{
    /// <summary>
    /// 运行时控制台示例引导器。
    ///
    /// 只实例化 Sample 提供的 UIDocument 预制体（Console.prefab）：
    /// 预制体上已配置好 UIDocument（Console.uxml + Console.asset）与 Console，
    /// UXML 内部通过 &lt;Style src="Console.uss" /&gt; 引用样式，不需要任何代码加载配置。
    /// </summary>
    public static class ConsoleBootstrap
    {
        /// <summary>预制体在 Resources 下的路径。</summary>
        private const string PrefabResourcePath = "Console/Console";

        /// <summary>进入播放模式后初始化控制台（默认隐藏，连按 3 次 Tab 唤醒）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeConsole()
        {
            EnsureInitialized();
            Console.Log("VoyageForge 运行时控制台已初始化（隐藏中），连按 3 次 Tab 键唤醒。");
        }

        /// <summary>确保控制台已初始化：实例化 Sample 预制体。</summary>
        public static void EnsureInitialized()
        {
            if (Console.HasInstance)
            {
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError(
                    $"[Console Sample] 未找到预制体 Resources/{PrefabResourcePath}.prefab；请通过 Package Manager 导入 Console Sample。");
                return;
            }

            Object.Instantiate(prefab);
        }
    }
}