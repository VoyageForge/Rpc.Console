using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace VoyageForge.Terminal.Logging
{
    /// <summary>
    /// 日志记录器：订阅 Unity 日志，按时间戳保存到 persistentDataPath/Console/logs。
    /// 由 Console 持有实例，log 命令负责开关；不做全局单例。
    /// </summary>
    public sealed class LogRecorder : IDisposable
    {
        /// <summary>日志目录（persistentDataPath 下）。</summary>
        private readonly string _directory;

        private readonly object _sync = new object();
        private StreamWriter _writer;
        private bool _recording;

        /// <summary>持久化开关状态的 PlayerPrefs 键。</summary>
        private const string RecordingKey = "VoyageForge.Terminal.LogRecording";

        /// <summary>上次日志文件路径的 PlayerPrefs 键。</summary>
        private const string PathKey = "VoyageForge.Terminal.LogFilePath";

        /// <summary>创建记录器。</summary>
        public LogRecorder(string directory = null)
        {
            _directory = string.IsNullOrEmpty(directory)
                ? Path.Combine(Application.persistentDataPath, "Console", "logs")
                : directory;
        }

        /// <summary>是否正在记录。</summary>
        public bool IsRecording { get { return _recording; } }

        /// <summary>当前日志文件路径；未记录时为 null。</summary>
        public string CurrentFilePath { get; private set; }

        /// <summary>开始记录；重复调用无副作用。</summary>
        public void Start()
        {
            lock (_sync)
            {
                if (_recording) return;

                Directory.CreateDirectory(_directory);
                string fileName = "console-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log";
                CurrentFilePath = Path.Combine(_directory, fileName);
                _writer = new StreamWriter(CurrentFilePath, false, new UTF8Encoding(false));
                _writer.AutoFlush = true;
                _recording = true;
                Application.logMessageReceived += OnLogMessage;
                PlayerPrefs.SetInt(RecordingKey, 1);
                PlayerPrefs.SetString(PathKey, CurrentFilePath);
                PlayerPrefs.Save();
            }
        }

        /// <summary>立即把缓冲写入磁盘（移动端切后台/低内存时调用）。</summary>
        public void Flush()
        {
            lock (_sync)
            {
                _writer?.Flush();
            }
        }

        /// <summary>停止记录并释放文件。</summary>
        public void Stop()
        {
            lock (_sync)
            {
                if (!_recording) return;
                Application.logMessageReceived -= OnLogMessage;
                _recording = false;
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
                PlayerPrefs.SetInt(RecordingKey, 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 与 Unity PC 日志（Player.log）相同的文本格式：
        /// [时间] [级别] 消息
        /// 堆栈（有则换行跟在后面）
        /// </summary>
        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            lock (_sync)
            {
                if (_writer == null) return;

                _writer.WriteLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "][" + type + "] " + condition);
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    _writer.WriteLine(stackTrace);
                }
            }
        }

        /// <summary>恢复上次的开关状态：上次开启则自动继续记录（新文件）。</summary>
        public void Restore()
        {
            if (PlayerPrefs.GetInt(RecordingKey, 0) == 1 && !_recording)
            {
                Start();
            }
        }

        /// <summary>上次日志文件路径（当前未记录时用于回显）。</summary>
        public string LastFilePath
        {
            get { return PlayerPrefs.GetString(PathKey, string.Empty); }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Stop();
        }
    }
}