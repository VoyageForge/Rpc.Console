using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;
using VoyageForge.Depot.Runtime.Utilities;

namespace VoyageForge.Terminal
{
    /// <summary>
    /// 基于 UI Toolkit（UXML + USS）实现的运行时控制台。
    ///
    /// 职责：
    /// 1. 捕获 <see cref="Application.logMessageReceived"/> 产生的日志；
    /// 2. 管理面板显隐、唤醒与拖拽；
    /// 3. 协调日志列表（<see cref="ConsoleLogList"/>）与命令输入（<see cref="ConsoleCommandLine"/>）两个子模块。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class Console : MonoSingleton<Console>, IConsoleRenderer
    {
        // ---------------------------------------------------------------
        // 可配置字段（Inspector 中可见）
        // ---------------------------------------------------------------

        [Header("Log")]
        [SerializeField, Min(1)] private int _maxEntries = 300;   // 日志缓冲上限

        [Header("Panel")]
        [SerializeField] private bool _visibleOnInitialize;        // 初始化后是否立即显示
        [SerializeField] private bool _draggable = true;           // 是否允许拖动标题栏

        [Header("Wake")]
        [SerializeField] private KeyCode _wakeKey = KeyCode.Tab;   // 唤醒按键
        [SerializeField, Min(2)] private int _wakeTapCount = 3;    // 需要连按的次数
        [SerializeField, Min(0.1f)] private float _wakeTapWindow = 0.5f; // 两次按键最大间隔（秒）

        [Header("Input")]
        [SerializeField] private bool _enableCommandInput = true;  // 是否启用命令输入框

        // ---------------------------------------------------------------
        // UI 元素引用
        // ---------------------------------------------------------------

        private UIDocument _document;
        private VisualElement _panel;
        private VisualElement _consoleRoot;
        private VisualElement _header;
        private ListView _list;
        private TextField _commandInput;
        private VisualElement _commandBar;
        private VisualElement _loadingOverlay;
        private VisualElement _loadingSpinner;
        private float _loadingAngle;
        private VisualElement _suggestionsContainer;

        // ---------------------------------------------------------------
        // 子模块
        // ---------------------------------------------------------------

        private ConsoleLogList _logList;         // 日志列表视图
        private ConsoleCommandLine _commandLine; // 命令输入行


        // ---------------------------------------------------------------
        // 命令树
        // ---------------------------------------------------------------

        /// <summary>根命令节点；内置命令 + 外部注册命令都挂在这里。</summary>
        private ConsoleCommand _commandRoot;

        // ---------------------------------------------------------------
        // 自定义字体（文本设置）
        // ---------------------------------------------------------------

        private static PanelTextSettings _customTextSettings;  // 用户自定义的文本设置（如中文字体）

        // ---------------------------------------------------------------
        // 拖拽状态
        // ---------------------------------------------------------------

        private bool _dragging;
        private Vector2 _dragStartPointer;
        private Vector2 _dragStartTranslate;
        private Vector2 _dragTranslate;

        // ---------------------------------------------------------------
        // 唤醒状态
        // ---------------------------------------------------------------

        private int _wakeTaps;
        private float _lastWakeTapTime = float.NegativeInfinity;

        /// <summary>控制台当前是否可见（静态，无实例时返回 false）。</summary>
        public static bool IsVisible => HasInstance && Instance.IsShown;

        /// <summary>当前实例是否可见。</summary>
        public bool IsShown => _consoleRoot != null && _consoleRoot.style.display == DisplayStyle.Flex;

        /// <summary>新日志写入缓冲后触发，供外部监听日志流。</summary>
        public static event Action<ConsoleLogEntry> EntryLogged;

        // ---------------------------------------------------------------
        // 初始化
        // ---------------------------------------------------------------

        /// <summary>创建/获取控制台单例，首次调用会完成面板构建（默认隐藏）。</summary>
        public static Console Initialize()
        {
            return Instance;
        }

        /// <summary>
        /// 设置控制台使用的自定义文本设置（PanelTextSettings），用于加载自定义字体（如中文字体）。
        /// 需在 <see cref="Initialize"/> 之前调用，面板构建时会应用。
        /// 传入 null 则恢复使用默认文本设置。
        /// </summary>
        /// <param name="textSettings">PanelTextSettings 资源；null 恢复默认。</param>
        public static void SetTextSettings(PanelTextSettings textSettings)
        {
            _customTextSettings = textSettings;
        }

        /// <summary>
        /// 直接向控制台写入一行（绕过 logMessageReceived，不捕获调用方栈）。
        /// 供内置命令与命令执行反馈输出使用（help / log / listen 状态、未知命令提示等）。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <param name="type">日志类型（默认 Log）。</param>
        public static void WriteDirect(string message, LogType type = LogType.Log)
        {
            if (HasInstance)
            {
                Instance.WriteLogEntry(message, string.Empty, type);
            }
        }

        // ---------------------------------------------------------------
        // IConsoleRenderer（命令树输出）
        // ---------------------------------------------------------------

        /// <inheritdoc />
        public void WriteLine(string text)
        {
            WriteDirect(text);
        }

        /// <inheritdoc />
        public void WriteError(string text)
        {
            WriteDirect(text, LogType.Warning);
        }

        /// <inheritdoc />
        public void WriteTable(
            System.Collections.Generic.IReadOnlyList<string> headers,
            System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<string>> rows)
        {
            if (headers != null && headers.Count > 0)
            {
                WriteDirect(string.Join(" | ", headers));
            }

            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    WriteDirect(string.Join(" | ", rows[i]));
                }
            }
        }

        // ---------------------------------------------------------------
        // 直接日志输出（Console.Log / Warning / Error）
        // ---------------------------------------------------------------

        /// <summary>
        /// 输出普通日志。
        /// 编辑器非播放模式：仅打印到 Unity 控制台；
        /// 编辑器播放模式与构建（运行时）：仅写入 Console 缓冲。
        /// </summary>
        /// <param name="message">日志内容（任意对象，等价于 Debug.Log(object)）。</param>
        public static void Log(object message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            WriteLog(LogType.Log, message, filePath, lineNumber);
        }

        /// <summary>输出警告日志，行为同 <see cref="Log"/>。</summary>
        /// <param name="message">日志内容。</param>
        public static void Warning(object message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            WriteLog(LogType.Warning, message, filePath, lineNumber);
        }

        /// <summary>输出错误日志，行为同 <see cref="Log"/>。</summary>
        /// <param name="message">日志内容。</param>
        public static void Error(object message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            WriteLog(LogType.Error, message, filePath, lineNumber);
        }

        /// <summary>写入日志的公共实现：根据运行环境分流，并捕获调用方栈。</summary>
        /// <param name="type">日志类型。</param>
        /// <param name="message">日志内容。</param>
        /// <param name="filePath">调用方源文件路径（由 CallerFilePath 注入）。</param>
        /// <param name="lineNumber">调用方行号（由 CallerLineNumber 注入）。</param>
        private static void WriteLog(LogType type, object message, string filePath, int lineNumber)
        {
            string text = message?.ToString() ?? "null";

            // 编辑器非播放模式：Console 不存在，仅打印到 Unity 控制台
            if (Application.isEditor && !Application.isPlaying)
            {
                UnityLog(type, WithCallerLocation(text, filePath, lineNumber));
                return;
            }

            // 捕获调用方栈：跳过 WriteLog 与 Log/Warning/Error 两层包装，从真正的调用方开始记录
            string stack = new System.Diagnostics.StackTrace(2, true).ToString();

            // 播放模式：直接写入 Console 缓冲（不经过 logMessageReceived）
            if (HasInstance)
            {
                Instance.WriteLogEntry(text, stack, type);
            }
        }

        /// <summary>按日志类型调用对应的 Unity Debug 输出。</summary>
        /// <param name="type">日志类型。</param>
        /// <param name="message">日志内容。</param>
        private static void UnityLog(LogType type, string message)
        {
            switch (type)
            {
                case LogType.Warning:
                    Debug.LogWarning(message);
                    break;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }

        /// <summary>给 Unity 控制台消息附加调用方“文件:行号”前缀，便于定位来源。</summary>
        /// <param name="text">原始日志内容。</param>
        /// <param name="filePath">调用方源文件路径。</param>
        /// <param name="lineNumber">调用方行号。</param>
        private static string WithCallerLocation(string text, string filePath, int lineNumber)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return text;
            }

            string fileName = System.IO.Path.GetFileName(filePath);
            return $"[{fileName}:{lineNumber}] {text}";
        }

        // ---------------------------------------------------------------
        // 生命周期（继承自 MonoSingleton）
        // ---------------------------------------------------------------

        /// <summary>MonoSingleton 初始化回调：构建面板、刷新启动窗口日志。</summary>
        protected override void OnInitialize()
        {
            BuildPanel();
            VoyageForge.Terminal.Commands.ListenLogBridge.FlushPending();

            OnConsoleInitialized();
        }

        /// <summary>面板构建完成、启动日志已刷新后调用。派生类可重写。</summary>
        protected virtual void OnConsoleInitialized() { }

        /// <summary>控制台显隐状态发生变化时调用。派生类可重写。</summary>
        protected virtual void OnVisibilityChanged(bool visible) { }

        /// <summary>每收到一条日志时调用（在写入缓冲之后）。派生类可重写。</summary>
        protected virtual void OnLogReceived(ConsoleLogEntry entry) { }

        /// <summary>销毁时清理内置命令的日志订阅（保留用户注册的 Rpc 命令）。</summary>
        protected override void OnDestroying()
        {
            VoyageForge.Terminal.Commands.ListenLogBridge.Shutdown();
        }

        private void Update()
        {
            HandleWakeInput();
            UpdateLoadingSpinner();
        }

        // ---------------------------------------------------------------
        // 唤醒
        // ---------------------------------------------------------------

        /// <summary>检测“连按 N 次唤醒键”的输入，累计达标后切换面板显隐。</summary>
        private void HandleWakeInput()
        {
            // 命令输入框聚焦时，Tab 优先用于输入框交互（补全/全选），不作为唤醒键
            if (_commandLine != null && _commandLine.IsInputFocused())
            {
                return;
            }

            if (!Input.GetKeyDown(_wakeKey))
            {
                return;
            }

            float now = Time.unscaledTime;

            if (now - _lastWakeTapTime > _wakeTapWindow)
            {
                _wakeTaps = 0;
            }

            _lastWakeTapTime = now;
            _wakeTaps++;

            if (_wakeTaps >= _wakeTapCount)
            {
                _wakeTaps = 0;
                _lastWakeTapTime = float.NegativeInfinity;
                Toggle();
            }
        }

        // ---------------------------------------------------------------
        // 面板构建
        // ---------------------------------------------------------------

        /// <summary>根命令节点。外部可向其中追加命令，或直接读取用于补全/帮助。</summary>
        public ConsoleCommand RootCommand
        {
            get { return _commandRoot; }
        }

        /// <summary>
        /// 注册一个外部命令（命令节点及其子命令）。
        /// 同名命令会被忽略，避免运行时重复注册导致命令树膨胀。
        /// </summary>
        /// <returns>注册成功返回 true；命令树未初始化或同名已存在返回 false。</returns>
        public bool RegisterCommand(ConsoleCommand command)
        {
            if (command == null || _commandRoot == null)
            {
                return false;
            }

            if (_commandRoot.FindSubcommand(command.Name) != null)
            {
                return false;
            }

            _commandRoot.Subcommands.Add(command);
            return true;
        }
        /// <summary>构建整个面板：加载资源、查询元素、创建子模块并绑定事件。</summary>
        private void BuildPanel()
        {
            _document = GetComponent<UIDocument>();

            if (_document.panelSettings == null)
            {
                Debug.LogError("[Console] UIDocument 未配置 PanelSettings；请把 Sample 中的 Console.asset 拖到 UIDocument 的 Panel Settings。");
                return;
            }

            if (_document.visualTreeAsset == null)
            {
                Debug.LogError("[Console] UIDocument 未配置 VisualTreeAsset；请把 Sample 中的 Console.uxml 拖到 UIDocument 的 Source Asset。");
                return;
            }

            _document.sortingOrder = 32767;

            // 应用用户自定义的文本设置（中文字体等）
            if (_customTextSettings != null)
            {
                _document.panelSettings.textSettings = _customTextSettings;
            }

            // UXML 由 UIDocument 实例化（样式在 UXML 内引用 Console.uss）
            _document.rootVisualElement.Clear();
            _panel = _document.visualTreeAsset.CloneTree();

            _panel.style.flexGrow = 1;
            _panel.style.width = new Length(100, LengthUnit.Percent);
            _panel.style.height = new Length(100, LengthUnit.Percent);
            _panel.style.alignItems = Align.Center;
            _panel.style.justifyContent = Justify.Center;

            _document.rootVisualElement.Add(_panel);

            // 查询 UXML 中的关键元素
            _consoleRoot = _panel.Q<VisualElement>("console-root");
            _header = _panel.Q<VisualElement>("console-header");
            _list = _panel.Q<ListView>("console-list");
            _commandInput = _panel.Q<TextField>("console-command-input");
            _commandBar = _panel.Q<VisualElement>("console-command-bar");

            // 创建 loading 覆盖层与补全建议容器
            BuildLoadingOverlay();
            BuildSuggestions();

            // 创建子模块：日志列表 + 命令输入行
            _logList = new ConsoleLogList(_list, _maxEntries);


            // 根节点 + 所有程序集的 [ConsoleCommand] 生成器命令（内置命令也由生成器提供）。
            _commandRoot = new ConsoleCommand("console");
            ConsoleCommandCatalogAggregator aggregator = new ConsoleCommandCatalogAggregator();
            foreach (ConsoleCommand command in aggregator.Collect())
            {
                _commandRoot.Subcommands.Add(command);
            }
            _commandLine = new ConsoleCommandLine(
                _commandInput,
                _suggestionsContainer,
                new ConsoleExecutor(this),
                _commandRoot);
            _commandLine.ForceScrollToBottom = force => _logList.ForceScrollToBottom = force;

            // 绑定过滤标签
            _logList.BindFilterButton(_panel, "filter-all", ConsoleFilter.All);
            _logList.BindFilterButton(_panel, "filter-log", ConsoleFilter.Log);
            _logList.BindFilterButton(_panel, "filter-warning", ConsoleFilter.Warning);
            _logList.BindFilterButton(_panel, "filter-error", ConsoleFilter.Error);

            // 绑定关闭按钮（走 exit 命令）
            _panel.Q<Button>("console-close-button")?.RegisterCallback<ClickEvent>(_ => ExecuteExitCommand());

            // 绑定命令输入框
            if (_commandInput != null)
            {
                if (_enableCommandInput)
                {
                    _commandInput.selectAllOnFocus = false;
                    _commandInput.selectAllOnMouseUp = false;
                    _commandInput.RegisterCallback<KeyDownEvent>(evt => _commandLine.OnKeyDown(evt), TrickleDown.TrickleDown);
                    _commandInput.RegisterValueChangedCallback(evt => _commandLine.OnValueChanged(evt.newValue));

            // Send 按钮：与回车共用提交逻辑
            Button sendButton = _panel.Q<Button>("console-send-button");
            if (sendButton != null)
            {
                sendButton.clicked += () => _commandLine.Submit();
            }
                }
                else
                {
                    _commandInput.style.display = DisplayStyle.None;
                }
            }

            // 绑定拖拽事件
            if (_draggable && _header != null)
            {
                _header.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
                _header.RegisterCallback<PointerMoveEvent>(OnHeaderPointerMove);
                _header.RegisterCallback<PointerUpEvent>(OnHeaderPointerUp);
                _header.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);
            }

            // 初始化界面状态
            _logList.ApplyFilterVisual();
            _logList.UpdateFilterCounts();

            // 设置显隐：SetVisible(true) 内部会重建列表；隐藏时不再手动 RebuildList，
            // 避免在后台实例化日志 item。
            SetVisible(_visibleOnInitialize);
        }

        /// <summary>运行时创建默认 PanelSettings（回退方案）。</summary>
        // ---------------------------------------------------------------
        // 日志处理
        // ---------------------------------------------------------------

        /// <summary>把一条日志写入缓冲并渲染（供 Log/Warning/Error、WriteDirect 与 ListenCommand 桥接共用）。</summary>
        public void WriteLogEntry(string condition, string stackTrace, LogType type)
        {
            WriteLogEntry(new ConsoleLogEntry(
                condition,
                stackTrace,
                type,
                DateTime.Now.ToString("HH:mm:ss.fff")));
        }

        /// <summary>把一条已构建的日志条目写入缓冲并渲染（保留条目自带的时间戳）。</summary>
        /// <param name="entry">日志条目。</param>
        public void WriteLogEntry(ConsoleLogEntry entry)
        {
            AddLogEntry(entry);
            RefreshIfShown();
        }

        /// <summary>把日志条目写入缓冲并通知外部与派生类（不渲染）。</summary>
        /// <param name="entry">日志条目。</param>
        private void AddLogEntry(ConsoleLogEntry entry)
        {
            // 面板构建失败（未配置 UIDocument 资源）时 _logList 为空：
            // 仍然派发事件，但不能让日志写入抛 NRE。
            if (_logList == null)
            {
                EntryLogged?.Invoke(entry);
                OnLogReceived(entry);
                return;
            }

            _logList.AddLog(entry);
            EntryLogged?.Invoke(entry);
            OnLogReceived(entry);
        }

        /// <summary>面板可见时刷新列表；仅“命令执行期间”或“本来就在底部”才滚动到底部。</summary>
        private void RefreshIfShown()
        {
            if (IsShown && _logList != null)
            {
                _logList.RefreshView(_logList.ForceScrollToBottom || _logList.IsAtBottom());
            }
        }

        // ---------------------------------------------------------------
        // 可见性与折叠
        // ---------------------------------------------------------------

        /// <summary>显示控制台。</summary>
        public void Show()
        {
            SetVisible(true);
        }

        /// <summary>隐藏控制台。</summary>
        public void Hide()
        {
            SetVisible(false);
        }

        /// <summary>切换控制台显隐。</summary>
        public void Toggle()
        {
            SetVisible(!IsShown);
        }

        /// <summary>设置控制台显隐状态。</summary>
        public void SetVisible(bool visible)
        {
            if (_consoleRoot == null)
            {
                return;
            }

            bool wasShown = IsShown;

            // 同时隐藏占满屏幕的面板容器，避免透明容器拦截后方 UI 输入
            DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _panel.style.display = display;
            _consoleRoot.style.display = display;

            if (visible)
            {
                _logList.RebuildList();
                _commandLine.FocusInput();
            }

            if (wasShown != visible)
            {
                OnVisibilityChanged(visible);
            }
        }

        /// <summary>关闭按钮：直接隐藏控制台（exit 命令同样调用 Hide）。</summary>
        private void ExecuteExitCommand()
        {
            Hide();
        }

        /// <summary>静态：显示当前单例。</summary>
        public static void ShowInstance()
        {
            Instance?.Show();
        }

        /// <summary>静态：隐藏当前单例。</summary>
        public static void HideInstance()
        {
            Instance?.Hide();
        }

        /// <summary>静态：切换当前单例显隐。</summary>
        public static void ToggleInstance()
        {
            Instance?.Toggle();
        }

        /// <summary>清空日志缓冲与计数，并刷新界面。</summary>
        public void Clear()
        {
            if (_logList == null)
            {
                return;
            }

            _logList.Clear();
            _logList.RebuildList();
        }

        // ---------------------------------------------------------------
        // 拖拽
        // ---------------------------------------------------------------

        private void OnHeaderPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _consoleRoot == null)
            {
                return;
            }

            if (IsInsideButton(evt.target as VisualElement))
            {
                return;
            }

            _dragging = true;
            _dragStartPointer = evt.position;
            _dragStartTranslate = _dragTranslate;
            _header.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private static bool IsInsideButton(VisualElement element)
        {
            while (element != null)
            {
                if (element is Button)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        private void OnHeaderPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging || _consoleRoot == null || !_header.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            Vector2 delta = (Vector2)evt.position - _dragStartPointer;
            _dragTranslate = _dragStartTranslate + delta;
            _consoleRoot.style.translate = new Translate(_dragTranslate.x, _dragTranslate.y);
        }

        private void OnHeaderPointerUp(PointerUpEvent evt)
        {
            if (_dragging && _header != null && _header.HasPointerCapture(evt.pointerId))
            {
                _header.ReleasePointer(evt.pointerId);
            }

            _dragging = false;
        }

        /// <summary>每帧驱动 loading 转圈动画（仅扫描期间可见）。</summary>
        private void UpdateLoadingSpinner()
        {
            if (_loadingSpinner == null ||
                _loadingOverlay == null ||
                _loadingOverlay.style.display == DisplayStyle.None)
            {
                return;
            }

            _loadingAngle = (_loadingAngle + 180f * Time.unscaledDeltaTime) % 360f;
            _loadingSpinner.style.rotate = new Rotate(new Angle(_loadingAngle, AngleUnit.Degree));
        }

        /// <summary>创建命令扫描期间的 loading 覆盖层（初始隐藏）。</summary>
        private void BuildLoadingOverlay()
        {
            if (_consoleRoot == null)
            {
                return;
            }

            _loadingOverlay = new VisualElement();
            _loadingOverlay.AddToClassList("console-loading-overlay");

            _loadingSpinner = new VisualElement();
            _loadingSpinner.AddToClassList("console-loading-spinner");
            _loadingOverlay.Add(_loadingSpinner);

            Label text = new Label("正在扫描命令…");
            text.AddToClassList("console-loading-text");
            _loadingOverlay.Add(text);

            _loadingOverlay.style.display = DisplayStyle.None;
            _consoleRoot.Add(_loadingOverlay);
        }

        /// <summary>创建命令补全建议容器（初始隐藏，插在命令输入栏上方）。</summary>
        private void BuildSuggestions()
        {
            if (_commandBar == null || _commandBar.parent == null)
            {
                return;
            }

            _suggestionsContainer = new VisualElement();
            _suggestionsContainer.AddToClassList("console-suggestions");
            _suggestionsContainer.style.display = DisplayStyle.None;

            _commandBar.parent.Insert(_commandBar.parent.IndexOf(_commandBar), _suggestionsContainer);
        }
    }
}
