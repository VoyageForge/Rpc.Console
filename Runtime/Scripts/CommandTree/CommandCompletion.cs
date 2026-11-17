using System;
using System.Collections.Generic;
using System.Linq;

namespace VoyageForge.Terminal
{
    /// <summary>从同一棵命令树中获取 Tab 候选；不创建节点、不修改输入，也不执行命令。</summary>
    public static class CommandCompletion
    {
        /// <summary>
        /// 获取当前正在编辑 token 的候选。
        /// tokens 最后一项永远是当前前缀；如果输入行末尾有空格，宿主必须追加空字符串 token。
        /// </summary>
        public static IReadOnlyList<string> Get(ConsoleCommand root, IReadOnlyList<string> tokens)
        {
            if (root == null || tokens == null || tokens.Count == 0) return Array.Empty<string>();

            ConsoleCommand node = root;
            int index = string.Equals(tokens[0], root.Name, StringComparison.Ordinal) ? 1 : 0;
            int lastIndex = tokens.Count - 1;
            List<string> priorArguments = new List<string>();

            // 扫描最后一个 token 之前的完整输入片段。已进入位置参数阶段后，不再把文本误当成子命令。
            while (index < lastIndex)
            {
                ConsoleCommand child = priorArguments.Count == 0 ? node.FindSubcommand(tokens[index]) : null;
                if (child != null)
                {
                    // 进入下级节点后，当前位置参数计数重置，因为新节点有自己的一组参数定义。
                    node = child;
                    priorArguments.Clear();
                }
                else
                {
                    // 第一个不匹配子命令的 token 被当成已完成的位置参数；后续即使与命令同名也仍是参数。
                    priorArguments.Add(tokens[index]);
                }
                index++;
            }

            string prefix = tokens[lastIndex] ?? string.Empty;
            List<string> candidates = new List<string>();

            // 尚未开始输入位置参数时，子命令名是最自然的候选。
            if (priorArguments.Count == 0)
            {
                candidates.AddRange(node.Subcommands
                    .Where(command => command.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(command => command.Name));
            }

            // 当前位置对应第几个参数，就调用 Arguments 中相同索引的 provider；provider 为空则没有候选。
            if (priorArguments.Count < node.Arguments.Count)
            {
                CompletionProvider provider = node.Arguments[priorArguments.Count].CompletionProvider;
                if (provider != null)
                {
                    IEnumerable<string> supplied = provider(new CompletionContext
                    {
                        PriorArguments = priorArguments.ToArray(),
                        CurrentPrefix = prefix,
                        AllTokens = tokens.ToArray()
                    });
                    if (supplied != null) candidates.AddRange(supplied);
                }
            }

            // 合并子命令与动态 provider 候选，过滤空值、Ordinal 去重，再进行确定性排序。
            return candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(candidate => candidate, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
