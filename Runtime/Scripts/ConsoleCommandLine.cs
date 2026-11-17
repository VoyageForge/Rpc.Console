using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace VoyageForge.Terminal
{
    /// <summary>
    /// 控制台命令输入行：负责命令输入框的按键处理、补全、命令历史与命令执行。
    /// 命令解析/执行交给命令树（<see cref="ConsoleExecutor"/>），补全交给 <see cref="CommandCompletion"/>。
    /// </summary>
    public sealed class ConsoleCommandLine
    {
        private readonly TextField _input;
        private readonly VisualElement _suggestionsContainer;

        /// <summary>命令树执行器（解析 + 分发 + 输出）。</summary>
        private readonly ConsoleExecutor _executor;

        /// <summary>命令树根节点。</summary>
        private readonly ConsoleCommand _root;

        // 补全建议状态
        private List<Label> _suggestionItems;
        private List<string> _suggestionLabels;
        private int _selectedSuggestionIndex = -1;
        private float _lastTabPressTime = float.NegativeInfinity;
        private string _suggestionFillPrefix = string.Empty;

        // 命令历史状态
        private readonly List<string> _commandHistory = new List<string>();
        private int _historyIndex = -1;
        private string _pendingInput = string.Empty;

        private const float TabDoubleTapWindow = 0.3f; // 双击 Tab 的判定时间窗口（秒）

        /// <summary>
        /// 创建命令输入行。
        /// </summary>
        /// <param name="input">命令输入框（来自 UXML）。</param>
        /// <param name="suggestionsContainer">补全建议容器。</param>
        /// <param name="executor">命令树执行器。</param>
        /// <param name="root">命令树根节点。</param>
        public ConsoleCommandLine(
            TextField input,
            VisualElement suggestionsContainer,
            ConsoleExecutor executor,
            ConsoleCommand root)
        {
            _input = input;
            _suggestionsContainer = suggestionsContainer;
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>命令执行期间回调（用于设置日志列表强制滚动到底部）。</summary>
        public Action<bool> ForceScrollToBottom { get; set; }

        /// <summary>判断命令输入框当前是否持有焦点。</summary>
        public bool IsInputFocused()
        {
            return _input != null &&
                   _input.focusController != null &&
                   _input.focusController.focusedElement == _input;
        }

        /// <summary>延迟一帧让输入框获得焦点，并把光标移到文本末尾（供显示面板与补全后调用）。</summary>
        public void FocusInput()
        {
            if (_input == null)
            {
                return;
            }

            _input.schedule.Execute(() =>
            {
                if (_input == null || !_input.enabledSelf)
                {
                    return;
                }

                // 先失焦再聚焦，强制走一遍完整的焦点切换，重新初始化文本输入状态
                _input.Blur();
                _input.Focus();

                SetCursorToEnd();
            });
        }

        /// <summary>设置输入框可用状态。</summary>
        public void SetInputEnabled(bool enabled)
        {
            if (_input != null)
            {
                _input.SetEnabled(enabled);
            }
        }

        /// <summary>命令输入框按键处理：回车执行、Tab 补全/全选、上下方向键选择建议或历史。</summary>
        public void OnKeyDown(KeyDownEvent evt)
        {
            if (_input == null)
            {
                return;
            }

            // 上下方向键：有补全建议时导航建议，否则浏览命令历史
            if (evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow)
            {
                if (IsSuggestionsVisible())
                {
                    NavigateSuggestion(evt.keyCode == KeyCode.DownArrow ? 1 : -1);
                }
                else
                {
                    NavigateHistory(evt.keyCode == KeyCode.UpArrow ? -1 : 1);
                }

                evt.StopPropagation();
                evt.PreventDefault();
                return;
            }

            // Tab：有建议时填入选中项，无建议时双击全选
            if (evt.keyCode == KeyCode.Tab)
            {
                HandleTabKey(evt);
                return;
            }

            // 回车：执行命令，并阻止 TextField 默认回车行为
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                ExecuteCommand();
                evt.StopPropagation();
                evt.PreventDefault();
            }
        }

        /// <summary>输入内容变化时刷新补全建议。</summary>
        public void OnValueChanged(string text)
        {
            UpdateSuggestions(text);
        }

        // ---------------------------------------------------------------
        // 命令执行
        // ---------------------------------------------------------------

        /// <summary>提交当前输入（回车 / Send 按钮共用）。</summary>
        public void Submit()
        {
            ExecuteCommand();
        }

        /// <summary>取出输入框内容并交给命令树执行。</summary>
        private void ExecuteCommand()
        {
            string raw = _input.value;
            _input.value = string.Empty;

            FocusInput();

            if (!string.IsNullOrWhiteSpace(raw))
            {
                _commandHistory.Add(raw);
                _historyIndex = -1;
                _pendingInput = string.Empty;
            }
            else
            {
                return;
            }

            try
            {
                ForceScrollToBottom?.Invoke(true);
                _executor.ExecuteAsync(_root, raw).GetAwaiter().GetResult();
                ForceScrollToBottom?.Invoke(false);
            }
            catch (Exception ex)
            {
                ForceScrollToBottom?.Invoke(false);
                Console.WriteDirect($"[Console] 命令 '{raw}' 执行失败：{ex.Message}", LogType.Error);
            }
        }

        // ---------------------------------------------------------------
        // 命令历史
        // ---------------------------------------------------------------

        /// <summary>浏览命令历史（delta 为 -1 上翻更早命令、+1 下翻更新命令）。</summary>
        private void NavigateHistory(int delta)
        {
            if (_commandHistory.Count == 0)
            {
                return;
            }

            if (_historyIndex < 0)
            {
                _pendingInput = _input.value;
                _historyIndex = _commandHistory.Count;
            }

            int newIndex = _historyIndex + delta;

            if (newIndex >= _commandHistory.Count)
            {
                _historyIndex = _commandHistory.Count;
                _input.SetValueWithoutNotify(_pendingInput);
                SetCursorToEnd();
                return;
            }

            if (newIndex < 0)
            {
                newIndex = 0;
            }

            _historyIndex = newIndex;
            _input.SetValueWithoutNotify(_commandHistory[_historyIndex]);
            SetCursorToEnd();
        }

        // ---------------------------------------------------------------
        // 命令补全（命令树）
        // ---------------------------------------------------------------

        /// <summary>根据输入内容刷新补全建议（子命令 + 参数候选）。</summary>
        private void UpdateSuggestions(string text)
        {
            if (_suggestionsContainer == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                HideSuggestions();
                return;
            }

            IReadOnlyList<string> candidates = GetCandidates(text);
            if (candidates.Count == 0)
            {
                HideSuggestions();
                return;
            }

            // 输入已经完整唯一时隐藏（避免只剩一个候选还占位）
            if (candidates.Count == 1 && candidates[0].Equals(GetCurrentToken(text), StringComparison.OrdinalIgnoreCase))
            {
                HideSuggestions();
                return;
            }

            _suggestionFillPrefix = text.EndsWith(" ", StringComparison.Ordinal)
                ? text
                : GetArgumentPrefix(text);

            _suggestionsContainer.Clear();
            _suggestionItems = new List<Label>();
            _suggestionLabels = new List<string>();
            _selectedSuggestionIndex = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                Label item = new Label(candidate);
                item.AddToClassList("console-suggestion-item");

                string fillValue = candidate;
                item.RegisterCallback<ClickEvent>(_ => AcceptSuggestionValue(fillValue));

                _suggestionsContainer.Add(item);
                _suggestionItems.Add(item);
                _suggestionLabels.Add(candidate);
            }

            UpdateSuggestionHighlight();
            _suggestionsContainer.style.display = DisplayStyle.Flex;
        }

        /// <summary>用命令树计算当前输入的补全候选。</summary>
        private IReadOnlyList<string> GetCandidates(string text)
        {
            List<string> tokens;
            try
            {
                tokens = new List<string>(CommandParser.SplitArgs(text));
            }
            catch (FormatException)
            {
                return Array.Empty<string>();
            }

            // 末尾空格表示“正在输入下一个 token”
            if (text.EndsWith(" ", StringComparison.Ordinal))
            {
                tokens.Add(string.Empty);
            }

            return CommandCompletion.Get(_root, tokens);
        }

        /// <summary>取输入行最后一个 token（用于判断是否已完整输入）。</summary>
        private static string GetCurrentToken(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (text.EndsWith(" ", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            int space = text.LastIndexOf(' ');
            return space >= 0 ? text.Substring(space + 1) : text;
        }

        /// <summary>计算参数补全时，填入建议项前需要保留的前缀。</summary>
        private static string GetArgumentPrefix(string text)
        {
            int lastSpace = text.LastIndexOf(' ');
            return lastSpace >= 0 ? text.Substring(0, lastSpace + 1) : string.Empty;
        }

        /// <summary>补全建议当前是否可见。</summary>
        private bool IsSuggestionsVisible()
        {
            return _suggestionsContainer != null &&
                   _suggestionsContainer.style.display == DisplayStyle.Flex;
        }

        /// <summary>按方向移动补全建议的选中项（delta 为 +1 下移、-1 上移）。</summary>
        private void NavigateSuggestion(int delta)
        {
            if (_suggestionItems == null || _suggestionItems.Count == 0)
            {
                return;
            }

            _selectedSuggestionIndex =
                (_selectedSuggestionIndex + delta + _suggestionItems.Count) % _suggestionItems.Count;
            UpdateSuggestionHighlight();
        }

        /// <summary>刷新补全建议项的选中高亮。</summary>
        private void UpdateSuggestionHighlight()
        {
            for (int i = 0; i < _suggestionItems.Count; i++)
            {
                _suggestionItems[i].EnableInClassList("console-suggestion-item-selected", i == _selectedSuggestionIndex);
            }
        }

        /// <summary>处理 Tab 键：有补全建议时填入选中项，否则检测双击全选。</summary>
        private void HandleTabKey(KeyDownEvent evt)
        {
            if (IsSuggestionsVisible())
            {
                AcceptSuggestion();
                evt.StopPropagation();
                evt.PreventDefault();
                return;
            }

            float now = Time.unscaledTime;
            if (now - _lastTabPressTime <= TabDoubleTapWindow)
            {
                _input.SelectAll();
                _lastTabPressTime = float.NegativeInfinity;
            }
            else
            {
                _lastTabPressTime = now;
            }

            evt.StopPropagation();
            evt.PreventDefault();
        }

        /// <summary>把当前选中的建议项填入输入框（供 Tab 键使用）。</summary>
        private void AcceptSuggestion()
        {
            if (_suggestionItems == null || _suggestionItems.Count == 0)
            {
                return;
            }

            if (_selectedSuggestionIndex < 0 || _selectedSuggestionIndex >= _suggestionItems.Count)
            {
                _selectedSuggestionIndex = 0;
            }

            AcceptSuggestionValue(_suggestionLabels[_selectedSuggestionIndex]);
        }

        /// <summary>把补全候选填入输入框。</summary>
        private void AcceptSuggestionValue(string completion)
        {
            string fill = _suggestionFillPrefix + completion + " ";

            _input.SetValueWithoutNotify(fill);
            SetCursorToEnd();

            if (!IsInputFocused())
            {
                FocusInput();
            }

            UpdateSuggestions(fill);
        }

        /// <summary>隐藏补全建议并清理选中状态。</summary>
        private void HideSuggestions()
        {
            if (_suggestionsContainer != null)
            {
                _suggestionsContainer.Clear();
                _suggestionsContainer.style.display = DisplayStyle.None;
            }

            _suggestionItems = null;
            _selectedSuggestionIndex = -1;
        }

        /// <summary>把命令输入框光标与选择起点移到文本末尾。</summary>
        private void SetCursorToEnd()
        {
            int length = _input.value.Length;
            _input.cursorIndex = length;
            _input.selectIndex = length;
        }
    }
}
