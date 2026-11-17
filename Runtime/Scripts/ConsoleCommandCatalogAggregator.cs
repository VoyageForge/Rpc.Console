using System;
using System.Collections.Generic;
using System.Reflection;

namespace VoyageForge.Terminal
{
    /// <summary>
    /// 命令来源聚合器：反射扫描所有已加载程序集里的 ICommandSource 实现，
    /// 让不同程序集（Runtime 内置命令、示例、业务程序集）的生成器目录自动汇总。
    /// 只在控制台初始化时执行一次，不做每帧扫描。
    /// </summary>
    public sealed class ConsoleCommandCatalogAggregator
    {
        /// <summary>收集所有无参构造的 ICommandSource 命令。</summary>
        public IEnumerable<ConsoleCommand> Collect()
        {
            List<ConsoleCommand> commands = new List<ConsoleCommand>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Assembly assembly = assemblies[assemblyIndex];
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    // 某些程序集类型加载失败时只跳过失败部分，不影响其他程序集。
                    types = exception.Types;
                }
                catch (Exception)
                {
                    continue;
                }

                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (type == null || type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(ICommandSource).IsAssignableFrom(type)) continue;
                    if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                    ICommandSource source;
                    try
                    {
                        source = Activator.CreateInstance(type) as ICommandSource;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (source == null) continue;
                    foreach (ConsoleCommand command in source.GetCommands())
                    {
                        if (command == null) continue;
                        if (!seen.Add(command.Name)) continue;
                        commands.Add(command);
                    }
                }
            }

            return commands;
        }
    }
}