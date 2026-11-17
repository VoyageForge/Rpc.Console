using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VoyageForge.Terminal
{
    /// <summary>纯命令解析工具。只处理字符串和命令树，不调用命令业务方法。</summary>
    public static class CommandParser
    {
        /// <summary>
        /// 把整行输入拆成 token。双引号中的空格不会切割参数，支持引号内部的反斜杠转义。
        /// </summary>
        /// <remarks>
        /// 这里用三个状态值实现一个小型状态机：quoted（是否在引号内）、escaping（下一个字符是否转义）、
        /// tokenStarted（当前 token 是否开始过）。tokenStarted 很重要，因为输入的空字符串参数 "" 长度为 0，
        /// 但它仍然是一个真实参数，不能被误认为“没有参数”。
        /// </remarks>
        public static IReadOnlyList<string> SplitArgs(string line)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(line)) return result;

            StringBuilder token = new StringBuilder();
            bool quoted = false;
            bool escaping = false;
            bool tokenStarted = false;

            foreach (char current in line)
            {
                if (escaping)
                {
                    token.Append(current);
                    escaping = false;
                    tokenStarted = true;
                    continue;
                }

                if (quoted && current == '\\')
                {
                    escaping = true;
                    tokenStarted = true;
                    continue;
                }

                if (current == '"')
                {
                    quoted = !quoted;
                    tokenStarted = true;
                    continue;
                }

                if (char.IsWhiteSpace(current) && !quoted)
                {
                    if (tokenStarted)
                    {
                        result.Add(token.ToString());
                        token.Clear();
                        tokenStarted = false;
                    }
                    continue;
                }

                token.Append(current);
                tokenStarted = true;
            }

            if (escaping) token.Append('\\');
            if (quoted) throw new FormatException("命令包含未闭合的双引号。请检查输入。");
            if (tokenStarted) result.Add(token.ToString());
            return result;
        }

        /// <summary>
        /// 沿命令树解析 token。遇到不匹配的子命令 token 后，当前和后续 token 全部作为位置参数。
        /// </summary>
        public static ParseResult Parse(ConsoleCommand root, IReadOnlyList<string> tokens)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (tokens == null || tokens.Count == 0) return new ParseResult { Error = "请输入命令。" };

            int index = 0;
            ConsoleCommand node = root;
            // 宿主可传入完整行（listen on），也可传入已剥离根名的 token；两种输入均支持。
            if (string.Equals(tokens[0], root.Name, StringComparison.Ordinal)) index = 1;

            // 只有当前 token 能匹配直接子命令时才下钻。第一个不匹配的 token 即进入位置参数阶段。
            while (index < tokens.Count)
            {
                ConsoleCommand child = node.FindSubcommand(tokens[index]);
                if (child == null) break;
                node = child;
                index++;
            }

            if (node.Handler == null)
                return new ParseResult { Error = "命令不完整或不可执行：" + node.Name, Command = node };

            string[] rawArguments = tokens.Skip(index).ToArray();
            if (rawArguments.Length < node.Arguments.Count)
            {
                string missingName = node.Arguments[rawArguments.Length].Name;
                return new ParseResult { Error = "缺少参数: " + missingName, Command = node };
            }
            if (rawArguments.Length > node.Arguments.Count)
                return new ParseResult { Error = "参数过多", Command = node };

            return new ParseResult { Success = true, Command = node, Arguments = rawArguments };
        }
    }
}
