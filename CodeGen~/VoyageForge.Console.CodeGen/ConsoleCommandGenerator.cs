using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace VoyageForge.Terminal.CodeGen
{
    /// <summary>
    /// Console 命令树生成器：扫描 [ConsoleCommand] 类/方法，生成实例化的 ConsoleCommand 树。
    /// 约定：
    /// <list type="bullet">
    /// <item>类上的 [ConsoleCommand("path")] 是根命令；类必须是非静态、无参构造；</item>
    /// <item>[RootCommand] 方法处理根节点；[ConsoleCommand("sub path")]/[ConsoleSubcommand("sub path")] 是子命令；</item>
    /// <item>方法参数生成位置参数，返回类型必须是 Task&lt;object&gt;（或 void/Task 的兼容形式）；</item>
    /// <item>生成的 ConsoleCommandCatalog 是实例类（ICommandSource），不生成静态入口。</item>
    /// </list>
    /// </summary>
    [Generator]
    public sealed class ConsoleCommandGenerator : ISourceGenerator
    {
        private const string CommandAttribute = "VoyageForge.Terminal.ConsoleCommandAttribute";
        private const string SubcommandAttribute = "VoyageForge.Terminal.ConsoleSubcommandAttribute";
        private const string RootCommandAttribute = "VoyageForge.Terminal.RootCommandAttribute";
        private const string CompletionSourceAttribute = "VoyageForge.Terminal.CompletionSourceAttribute";
        private const string InvocationType = "VoyageForge.Terminal.CommandInvocation";

        private static readonly DiagnosticDescriptor EmptyPath = new DiagnosticDescriptor(
            "CONSOLE001", "Console 命令路径为空", "命令 {0} 的路径不能为空", "VoyageForge.Console",
            DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor StaticClass = new DiagnosticDescriptor(
            "CONSOLE002", "Console 命令类不能是静态类", "命令类 {0} 必须是非静态实例类，生成器需要 new 实例", "VoyageForge.Console",
            DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor InvalidReturn = new DiagnosticDescriptor(
            "CONSOLE003", "Console 命令方法返回类型不受支持", "命令方法 {0} 必须返回 Task<object>、Task 或 void", "VoyageForge.Console",
            DiagnosticSeverity.Error, true);

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new Receiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is Receiver receiver) || receiver.Classes.Count == 0)
            {
                return;
            }

            var commands = new List<CommandModel>();
            foreach (INamedTypeSymbol type in receiver.Classes.OrderBy(t => t.ToDisplayString()))
            {
                if (context.CancellationToken.IsCancellationRequested) return;

                ConsoleCommandAttributeData root = GetCommandAttribute(type);
                if (root == null) continue;

                if (type.IsStatic)
                {
                    context.ReportDiagnostic(Diagnostic.Create(StaticClass, type.Locations.FirstOrDefault(),
                        type.ToDisplayString()));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(root.Path))
                {
                    context.ReportDiagnostic(Diagnostic.Create(EmptyPath, type.Locations.FirstOrDefault(),
                        type.ToDisplayString()));
                    continue;
                }

                var methods = new List<MethodModel>();
                bool valid = true;
                foreach (IMethodSymbol method in type.GetMembers().OfType<IMethodSymbol>()
                             .Where(m => m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public))
                {
                    ConsoleCommandAttributeData command = GetCommandAttribute(method);
                    ConsoleCommandAttributeData subcommand = GetSubcommandAttribute(method);
                    if (command == null && subcommand == null && !HasAttribute(method, RootCommandAttribute)) continue;

                    string path = command?.Path ?? subcommand?.Path;
                    if (command != null && string.IsNullOrWhiteSpace(path))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(EmptyPath, method.Locations.FirstOrDefault(),
                            type.ToDisplayString() + "." + method.Name));
                        valid = false;
                        continue;
                    }

                    ReturnKind returnKind = GetReturnKind(context.Compilation, method);
                    if (returnKind == ReturnKind.Invalid)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(InvalidReturn, method.Locations.FirstOrDefault(),
                            method.Name));
                        valid = false;
                        continue;
                    }

                    methods.Add(new MethodModel
                    {
                        Method = method,
                        Path = path,
                        Description = command?.Description ?? subcommand?.Description,
                        IsRoot = HasAttribute(method, RootCommandAttribute),
                        ReturnKind = returnKind
                    });
                }

                if (!valid) continue;
                commands.Add(new CommandModel { Type = type, Root = root, Methods = methods });
            }

            if (commands.Count == 0) return;
            string catalogName = GetCatalogName(context.Compilation.AssemblyName);
            EmitCatalog(context, commands, catalogName);
        }

        private static void EmitCatalog(GeneratorExecutionContext context, List<CommandModel> commands, string catalogName)
        {
            StringBuilder source = new StringBuilder(16000);
            source.AppendLine("// <auto-generated />");
            source.AppendLine("// 由 VoyageForge.Console.CodeGen 生成：命令类 → ConsoleCommand 树，请勿手改。");
            source.AppendLine("using System;");
            source.AppendLine("using System.Collections.Generic;");
            source.AppendLine("using System.Threading.Tasks;");
            source.AppendLine("using VoyageForge.Terminal;");
            source.AppendLine("namespace VoyageForge.Terminal.Generated");
            source.AppendLine("{");
            source.AppendLine("    /// <summary>生成器汇总的命令来源；实例类，避免静态入口。</summary>");
            source.Append("    public sealed class ").Append(catalogName).AppendLine(" : global::VoyageForge.Terminal.ICommandSource");
            source.AppendLine("    {");
            source.AppendLine("        /// <summary>返回所有根命令。</summary>");
            source.AppendLine("        public IEnumerable<ConsoleCommand> GetCommands()");
            source.AppendLine("        {");
            for (int index = 0; index < commands.Count; index++)
            {
                source.AppendLine("            yield return BuildCommand" + index + "();");
            }

            source.AppendLine("        }");
            for (int index = 0; index < commands.Count; index++)
            {
                EmitCommand(source, commands[index], index);
            }

            source.AppendLine("    }");
            source.AppendLine("}");

            context.AddSource("ConsoleCommands.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }

        private static void EmitCommand(StringBuilder source, CommandModel model, int index)
        {
            string typeName = model.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            source.AppendLine();
            source.Append("        private static ConsoleCommand BuildCommand").Append(index)
                .AppendLine("()");
            source.AppendLine("        {");
            source.Append("            ").Append(typeName).AppendLine(" instance = new " + typeName + "();");
            source.Append("            ConsoleCommand root = new ConsoleCommand(\"")
                .Append(CSharpEscape(model.Root.Path)).Append("\", \"")
                .Append(CSharpEscape(model.Root.Description ?? string.Empty)).AppendLine("\");");

            foreach (MethodModel method in model.Methods)
            {
                if (method.IsRoot)
                {
                    // 根节点同样生成位置参数（例如 listen <on/off>）。
                    foreach (IParameterSymbol parameter in method.Method.Parameters)
                    {
                        if (IsInvocationParameter(parameter)) continue;
                        source.Append("            root").AppendLine(".Arguments.Add(new ConsoleArgument");
                        source.AppendLine("            {");
                        source.Append("                Name = \"").Append(CSharpEscape(parameter.Name)).AppendLine("\",");
                        source.Append("                Description = \"")
                            .Append(CSharpEscape(parameter.Name)).AppendLine("\",");
                        source.Append("                Type = typeof(")
                            .Append(TypeSyntax(parameter.Type)).AppendLine("),");
                        string completionMethod = GetCompletionSource(parameter);
                        if (completionMethod != null)
                        {
                            source.Append("                CompletionProvider = context => instance.")
                                .Append(completionMethod).AppendLine("(context)");
                        }

                        source.AppendLine("            });");
                    }

                    source.AppendLine("            root.Handler = invocation =>");
                    source.AppendLine("            {");
                    EmitCall(source, method, "root");
                    source.AppendLine("            };");
                }
                else
                {
                    string[] segments = (method.Path ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string variable = "node" + Math.Abs(method.Method.Name.GetHashCode());
                    source.Append("            ConsoleCommand ").Append(variable).Append(" = root");
                    for (int i = 0; i < segments.Length; i++)
                    {
                        source.Append(".GetOrAddSubcommand(\"").Append(CSharpEscape(segments[i])).Append("\"");
                        if (i == segments.Length - 1)
                        {
                            source.Append(", \"").Append(CSharpEscape(method.Description ?? string.Empty)).Append("\"");
                        }

                        source.Append(")");
                    }

                    source.AppendLine(";");

                    // 方法参数 → 位置参数（类型化）。
                    foreach (IParameterSymbol parameter in method.Method.Parameters)
                    {
                        if (IsInvocationParameter(parameter)) continue;
                        source.Append("            ").Append(variable).AppendLine(".Arguments.Add(new ConsoleArgument");
                        source.AppendLine("            {");
                        source.Append("                Name = \"").Append(CSharpEscape(parameter.Name)).AppendLine("\",");
                        source.Append("                Description = \"")
                            .Append(CSharpEscape(parameter.Name)).AppendLine("\",");
                        source.Append("                Type = typeof(")
                            .Append(TypeSyntax(parameter.Type)).AppendLine("),");
                        string completionMethod = GetCompletionSource(parameter);
                        if (completionMethod != null)
                        {
                            source.Append("                CompletionProvider = context => instance.")
                                .Append(completionMethod).AppendLine("(context)");
                        }

                        source.AppendLine("            });");
                    }

                    source.Append("            ").Append(variable).AppendLine(".Handler = invocation =>");
                    source.AppendLine("            {");
                    EmitCall(source, method, variable);
                    source.AppendLine("            };");
                }
            }

            source.AppendLine("            return root;");
            source.AppendLine("        }");
        }

        private static void EmitCall(StringBuilder source, MethodModel method, string nodeVariable)
        {
            List<string> arguments = new List<string>();
            int rawIndex = 0;
            foreach (IParameterSymbol parameter in method.Method.Parameters)
            {
                if (IsInvocationParameter(parameter))
                {
                    arguments.Add("invocation");
                    continue;
                }

                arguments.Add("CommandArgumentConverter.Convert<" + TypeSyntax(parameter.Type) +
                              ">(invocation.Arguments[" + rawIndex + "])");
                rawIndex++;
            }

            string call = "instance." + method.Method.Name + "(" + string.Join(", ", arguments) + ")";
            switch (method.ReturnKind)
            {
                case ReturnKind.TaskObject:
                    source.Append("                return ").Append(call).AppendLine(";");
                    break;
                case ReturnKind.Task:
                    source.Append("                return ").Append(call).AppendLine(";");
                    break;
                case ReturnKind.Void:
                    source.Append("                ").Append(call).AppendLine(";");
                    source.AppendLine("                return Task.FromResult<object>(null);");
                    break;
                case ReturnKind.Object:
                    source.Append("                return Task.FromResult<object>(").Append(call).AppendLine(");");
                    break;
            }
        }

        private static bool IsInvocationParameter(IParameterSymbol parameter)
        {
            return parameter.Type.ToDisplayString() == InvocationType;
        }

        private static ReturnKind GetReturnKind(Compilation compilation, IMethodSymbol method)
        {
            if (method.ReturnsVoid) return ReturnKind.Void;
            string name = method.ReturnType.ToDisplayString();
            if (name == "System.Threading.Tasks.Task<object>") return ReturnKind.TaskObject;
            if (name == "System.Threading.Tasks.Task") return ReturnKind.Task;
            if (method.ReturnType.SpecialType == SpecialType.System_Object) return ReturnKind.Object;
            return ReturnKind.Invalid;
        }

        private static ConsoleCommandAttributeData GetCommandAttribute(ISymbol symbol)
        {
            return ReadAttribute(symbol, CommandAttribute, "NameOrPath");
        }

        private static ConsoleCommandAttributeData GetSubcommandAttribute(ISymbol symbol)
        {
            return ReadAttribute(symbol, SubcommandAttribute, "Path");
        }

        private static ConsoleCommandAttributeData ReadAttribute(ISymbol symbol, string attributeName, string propertyName)
        {
            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != attributeName) continue;
                string path = attribute.ConstructorArguments.Length > 0
                    ? attribute.ConstructorArguments[0].Value as string
                    : null;
                string description = attribute.ConstructorArguments.Length > 1
                    ? attribute.ConstructorArguments[1].Value as string
                    : null;
                return new ConsoleCommandAttributeData { Path = path, Description = description };
            }

            return null;
        }

        private static bool HasAttribute(ISymbol symbol, string attributeName)
        {
            return symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attributeName);
        }

        /// <summary>读取参数上的 [CompletionSource("Method")]，返回补全方法名；没有则 null。</summary>
        private static string GetCompletionSource(IParameterSymbol parameter)
        {
            foreach (AttributeData attribute in parameter.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != CompletionSourceAttribute) continue;
                return attribute.ConstructorArguments.Length > 0
                    ? attribute.ConstructorArguments[0].Value as string
                    : null;
            }

            return null;
        }
        private static string TypeSyntax(ITypeSymbol type)
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static string CSharpEscape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>按程序集名生成唯一 catalog 类名，避免 Runtime 与示例程序集同名冲突。</summary>
        private static string GetCatalogName(string assemblyName)
        {
            string name = string.IsNullOrEmpty(assemblyName) ? "Generated" : assemblyName;
            StringBuilder builder = new StringBuilder(name.Length + 20);
            foreach (char ch in name)
            {
                builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            return builder.Append("CommandCatalog").ToString();
        }
        private sealed class Receiver : ISyntaxContextReceiver
        {
            public List<INamedTypeSymbol> Classes { get; } = new List<INamedTypeSymbol>();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (!(context.Node is ClassDeclarationSyntax declaration)) return;
                if (!(context.SemanticModel.GetDeclaredSymbol(declaration) is INamedTypeSymbol symbol)) return;
                if (symbol.IsGenericType || symbol.IsAbstract) return;
                if (HasAttribute(symbol, CommandAttribute)) Classes.Add(symbol);
            }
        }

        private sealed class CommandModel
        {
            public INamedTypeSymbol Type;
            public ConsoleCommandAttributeData Root;
            public List<MethodModel> Methods;
        }

        private sealed class MethodModel
        {
            public IMethodSymbol Method;
            public string Path;
            public string Description;
            public bool IsRoot;
            public ReturnKind ReturnKind;
        }

        private sealed class ConsoleCommandAttributeData
        {
            public string Path;
            public string Description;
        }

        private enum ReturnKind
        {
            Invalid,
            Void,
            Task,
            TaskObject,
            Object
        }
    }
}
