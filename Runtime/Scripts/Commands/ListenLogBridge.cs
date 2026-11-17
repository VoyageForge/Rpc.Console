using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoyageForge.Terminal.Commands
{
    /// <summary>
    /// listen 的日志桥接（静态、无命令依赖）：
    /// 负责 Unity 日志 → Console 的订阅/取消、启动窗口缓存、PlayerPrefs 持久化。
    /// </summary>
    public static class ListenLogBridge
    {
        private const string ListenLogsPrefKey = "VoyageForge.Terminal.ListenLogs";
        private const string AutoStartListeningPrefKey = "VoyageForge.Terminal.AutoStartListening";
        private const int MaxPendingLogs = 300;

        private static readonly List<ConsoleLogEntry> PendingLogs = new List<ConsoleLogEntry>();
        private static bool _listening;

        private static bool AutoStartListening
        {
            get => PlayerPrefs.GetInt(AutoStartListeningPrefKey, 0) == 1;
            set => PlayerPrefs.SetInt(AutoStartListeningPrefKey, value ? 1 : 0);
        }

        /// <summary>处理 listen on|off（空 = 查看状态）。</summary>
        public static bool HandleMode(string mode)
        {
            if (string.IsNullOrEmpty(mode))
            {
                Console.WriteDirect(
                    $"[Console] 日志监听：{(_listening ? "开启" : "关闭")}，自启动：{(AutoStartListening ? "开启" : "关闭")}");
                return true;
            }

            switch (mode.ToLowerInvariant())
            {
                case "on":
                    SetListening(true, true);
                    Console.WriteDirect("[Console] 日志监听已开启。");
                    break;
                case "off":
                    SetListening(false, true);
                    PendingLogs.Clear();
                    Console.WriteDirect("[Console] 日志监听已关闭。");
                    break;
                default:
                    Console.WriteDirect($"[Console] 未知参数：{mode}。用法：listen on/off", LogType.Warning);
                    break;
            }

            return true;
        }

        /// <summary>处理 listen autostart on|off（空 = 查看状态）。</summary>
        public static bool HandleAutoStart(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Console.WriteDirect($"[Console] 自启动：{(AutoStartListening ? "开启" : "关闭")}");
                return true;
            }

            switch (value.ToLowerInvariant())
            {
                case "on":
                    AutoStartListening = true;
                    Console.WriteDirect("[Console] 自启动监听已开启（下次初始化时自动开始监听）。");
                    break;
                case "off":
                    AutoStartListening = false;
                    Console.WriteDirect("[Console] 自启动监听已关闭（下次初始化时不再自动监听）。");
                    break;
                default:
                    Console.WriteDirect($"[Console] 未知参数：{value}。用法：listen autostart on/off", LogType.Warning);
                    break;
            }

            return true;
        }

        /// <summary>控制台初始化时：把启动窗口缓存填入控制台并切到实时转发。</summary>
        public static void FlushPending()
        {
            if (Console.HasInstance)
            {
                for (int i = 0; i < PendingLogs.Count; i++)
                {
                    Console.Instance.WriteLogEntry(PendingLogs[i]);
                }
            }

            PendingLogs.Clear();
            Application.logMessageReceived -= CacheLog;

            if (AutoStartListening && !_listening)
            {
                SetListening(true, false);
            }
        }

        /// <summary>取消订阅、清空缓存（控制台销毁时调用）。</summary>
        public static void Shutdown()
        {
            Application.logMessageReceived -= CacheLog;
            Application.logMessageReceived -= HandleUnityLog;
            _listening = false;
            PendingLogs.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EarlyInit()
        {
            PendingLogs.Clear();
            if (AutoStartListening)
            {
                Application.logMessageReceived += CacheLog;
            }
        }

        private static void SetListening(bool listen, bool persist)
        {
            if (persist)
            {
                PlayerPrefs.SetInt(ListenLogsPrefKey, listen ? 1 : 0);
            }

            if (_listening == listen)
            {
                return;
            }

            _listening = listen;

            if (listen)
            {
                Application.logMessageReceived += HandleUnityLog;
            }
            else
            {
                Application.logMessageReceived -= HandleUnityLog;
            }
        }

        private static void CacheLog(string condition, string stackTrace, LogType type)
        {
            PendingLogs.Add(new ConsoleLogEntry(condition, stackTrace, type, DateTime.Now.ToString("HH:mm:ss.fff")));
            if (PendingLogs.Count > MaxPendingLogs)
            {
                PendingLogs.RemoveAt(0);
            }
        }

        private static void HandleUnityLog(string condition, string stackTrace, LogType type)
        {
            if (Console.HasInstance)
            {
                Console.Instance.WriteLogEntry(condition, stackTrace, type);
            }
        }
    }
}