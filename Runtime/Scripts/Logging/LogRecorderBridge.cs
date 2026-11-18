using UnityEngine;

namespace VoyageForge.Terminal.Logging
{
    /// <summary>
    /// log 命令的日志记录桥接（静态、无 Console 依赖）：
    /// 持有 LogRecorder 实例，负责状态恢复；Console 不感知记录器。
    /// </summary>
    public static class LogRecorderBridge
    {
        private static readonly LogRecorder Recorder = new LogRecorder();

        /// <summary>是否正在记录。</summary>
        public static bool IsRecording { get { return Recorder.IsRecording; } }

        /// <summary>当前日志文件路径；未记录时为 null。</summary>
        public static string CurrentFilePath { get { return Recorder.CurrentFilePath; } }

        /// <summary>上次日志文件路径（未记录时回显）。</summary>
        public static string LastFilePath { get { return Recorder.LastFilePath; } }

        /// <summary>开始记录。</summary>
        public static void Start() { Recorder.Start(); }

        /// <summary>停止记录。</summary>
        public static void Stop() { Recorder.Stop(); }

        /// <summary>启动时恢复上次的开关状态（与 listen 一致，独立于 Console 初始化）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EarlyInit()
        {
            Recorder.Restore();

            // 游戏退出/编辑器停止播放时释放文件句柄（避免 StreamWriter 未 flush/关闭）。
            Application.quitting -= Recorder.Stop;
            Application.quitting += Recorder.Stop;

            // Android/iOS 切后台不一定触发 quitting：失焦时立即 flush，避免被系统杀死时丢日志。
            Application.focusChanged -= OnFocusChanged;
            Application.focusChanged += OnFocusChanged;
        }

        /// <summary>移动端切后台（失焦）时落盘；重新获得焦点后继续记录。</summary>
        private static void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                Recorder.Flush();
            }
        }
    }
}