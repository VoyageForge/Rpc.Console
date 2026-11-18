using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;
using VoyageForge.Terminal.Logging;

namespace VoyageForge.Terminal.Tests
{
    /// <summary>日志记录器测试：三种日志（Log/Warning/Error）都应写入文件。</summary>
    public sealed class LogRecorderTests
    {
        [Test]
        public void 三种日志各写入一行()
        {
            string directory = Path.Combine(Application.persistentDataPath, "ConsoleTests");
            if (Directory.Exists(directory)) Directory.Delete(directory, true);

            LogRecorder recorder = new LogRecorder(directory);
            recorder.Start();
            Assert.IsTrue(recorder.IsRecording);

            Debug.Log("logrecorder-info");
            // 多行消息：原有的换行必须原样保留（不能被压成一行）。
            Debug.Log("multi-line-1\nmulti-line-2\nmulti-line-3");
            Debug.LogWarning("logrecorder-warning");
            LogAssert.Expect(LogType.Error, "logrecorder-error");
            Debug.LogError("logrecorder-error");

            recorder.Stop();
            Assert.IsFalse(recorder.IsRecording);
            Assert.IsNotNull(recorder.CurrentFilePath);
            Assert.IsTrue(File.Exists(recorder.CurrentFilePath));

            // Unity Player.log 风格：[时间][级别] 消息，堆栈跟一行。
            string content = File.ReadAllText(recorder.CurrentFilePath);
            StringAssert.Contains("[Log] logrecorder-info", content);
            StringAssert.Contains("multi-line-1", content);
            StringAssert.Contains("multi-line-2", content);
            StringAssert.Contains("multi-line-3", content);
            // 三行应各自成行（允许首行带 [时间][级别] 前缀）。
            Assert.IsTrue(content.Split('\n').Select(line => line.Trim()).Any(line =>
                line.EndsWith("multi-line-2")), "multi-line-2 应独占一行。");
            Assert.IsTrue(content.Split('\n').Select(line => line.Trim()).Any(line =>
                line.EndsWith("multi-line-3")), "multi-line-3 应独占一行。");
            StringAssert.Contains("[Warning] logrecorder-warning", content);
            StringAssert.Contains("[Error] logrecorder-error", content);
            StringAssert.Contains("UnityEngine.Debug:LogError", content);
        }
    }
}