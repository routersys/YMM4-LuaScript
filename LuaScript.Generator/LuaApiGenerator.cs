using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LuaScript.Generator
{
    internal enum LuaParameterKind
    {
        ExecutionContext,
        Arguments,
        DynValue,
        Double,
        Int,
        Bool,
    }

    internal enum LuaReturnKind
    {
        DynValue,
        Double,
        Void,
    }

    internal sealed record LuaParameterModel(string Name, LuaParameterKind Kind, string DefaultLiteral);

    internal sealed record LuaFunctionModel(
        string LuaName,
        string MethodName,
        LuaReturnKind ReturnKind,
        EquatableArray<LuaParameterModel> Parameters);

    internal sealed record LuaConstantModel(string LuaName, string MemberName);

    internal enum LuaUpdateKind
    {
        Direct,
        OptionalValue,
        OptionalReference,
    }

    internal enum LuaValueKind
    {
        Number,
        Boolean,
        String,
        Other,
    }

    internal sealed record LuaUpdateModel(string LuaName, string MethodName, LuaUpdateKind Kind, LuaValueKind ValueKind);

    internal sealed record LuaCatalogEntry(
        string Table,
        string Name,
        bool IsFunction,
        EquatableArray<string> Parameters);

    internal sealed record LuaTypePart(string Name, bool IsStatic);

    internal sealed record LuaTableModel(
        string Namespace,
        EquatableArray<LuaTypePart> TypeChain,
        string TableName,
        string ContextType,
        EquatableArray<LuaFunctionModel> Functions,
        EquatableArray<LuaConstantModel> Constants,
        EquatableArray<LuaUpdateModel> Updates,
        EquatableArray<LuaCatalogEntry> Entries);

    [Generator]
    public sealed class LuaApiGenerator : IIncrementalGenerator
    {
        private const string TableAttributeName = "LuaScript.Api.LuaTableAttribute";
        private const string FunctionAttributeName = "LuaScript.Api.LuaFunctionAttribute";
        private const string ConstantAttributeName = "LuaScript.Api.LuaConstantAttribute";
        private const string VariableAttributeName = "LuaScript.Api.LuaVariableAttribute";
        private const string BuiltinAttributeName = "LuaScript.Api.LuaBuiltinAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var tables = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    TableAttributeName,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, _) => CreateModel(ctx))
                .Where(static model => model is not null)
                .Select(static (model, _) => model!);

            var assemblyEntries = context.CompilationProvider
                .Select(static (compilation, _) => CollectAssemblyEntries(compilation));

            context.RegisterSourceOutput(tables, static (ctx, model) =>
            {
                if (model.Functions.Count == 0 && model.Constants.Count == 0 && model.Updates.Count == 0)
                    return;
                ctx.AddSource(RegistrationHintName(model), SourceText.From(EmitRegistration(model), Encoding.UTF8));
            });

            var catalog = tables.Collect().Combine(assemblyEntries);
            context.RegisterSourceOutput(catalog, static (ctx, source) =>
            {
                var (models, extra) = source;
                var entries = models
                    .OrderBy(static m => m.Namespace, StringComparer.Ordinal)
                    .ThenBy(static m => string.Join("+", m.TypeChain.Select(static p => p.Name)), StringComparer.Ordinal)
                    .SelectMany(static m => m.Entries)
                    .Concat(extra)
                    .ToArray();
                if (entries.Length == 0)
                    return;
                ctx.AddSource("LuaApiCatalog.g.cs", SourceText.From(EmitCatalog(entries), Encoding.UTF8));
            });
        }

        private static LuaTableModel? CreateModel(GeneratorAttributeSyntaxContext context)
        {
            if (context.TargetSymbol is not INamedTypeSymbol symbol)
                return null;

            var tableArguments = context.Attributes[0].ConstructorArguments;
            string tableName = tableArguments.Length > 0 && tableArguments[0].Value is string name ? name : string.Empty;

            var chain = new List<LuaTypePart>();
            for (var type = symbol; type is not null; type = type.ContainingType)
                chain.Insert(0, new LuaTypePart(type.Name, type.IsStatic));

            var entries = new List<(int Position, LuaCatalogEntry Entry)>();
            foreach (var attribute in symbol.GetAttributes())
            {
                string? attributeName = attribute.AttributeClass?.ToDisplayString();
                if (attributeName is not (VariableAttributeName or BuiltinAttributeName or FunctionAttributeName))
                    continue;
                var entry = CreateAttributeEntry(attribute, tableName);
                if (entry is null)
                    continue;
                int position = attribute.ApplicationSyntaxReference?.Span.Start ?? 0;
                entries.Add((position, entry));
            }
            var classEntries = entries.OrderBy(static e => e.Position).Select(static e => e.Entry).ToList();

            var functions = new List<(int Position, LuaFunctionModel Function, LuaCatalogEntry Entry)>();
            var constants = new List<(int Position, LuaConstantModel Constant, LuaCatalogEntry Entry)>();
            var updates = new List<(int Position, LuaUpdateModel Update, LuaCatalogEntry? Entry)>();
            string contextType = string.Empty;
            foreach (var member in symbol.GetMembers())
            {
                int position = member.DeclaringSyntaxReferences.Length > 0
                    ? member.DeclaringSyntaxReferences[0].Span.Start
                    : 0;

                if (member is IMethodSymbol method)
                {
                    var functionAttribute = FindAttribute(method, FunctionAttributeName);
                    if (functionAttribute is not null)
                    {
                        var function = CreateFunctionModel(method, functionAttribute, out var catalogParameters);
                        if (function is null)
                            continue;
                        functions.Add((position, function, new LuaCatalogEntry(tableName, function.LuaName, true, catalogParameters)));
                        continue;
                    }

                    var variableAttribute = FindAttribute(method, VariableAttributeName);
                    if (variableAttribute is not null)
                    {
                        var update = CreateUpdateModel(method, variableAttribute, tableName, ref contextType, out var entry);
                        if (update is not null)
                            updates.Add((position, update, entry));
                    }
                    continue;
                }

                if (member is IFieldSymbol or IPropertySymbol)
                {
                    var attribute = FindAttribute(member, ConstantAttributeName);
                    if (attribute is null)
                        continue;
                    if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value is not string constantName)
                        continue;
                    constants.Add((
                        position,
                        new LuaConstantModel(constantName, member.Name),
                        new LuaCatalogEntry(tableName, constantName, false, EquatableArray<string>.Empty)));
                }
            }

            var orderedMembers = functions
                .Select(static f => (Position: f.Position, Entry: f.Entry))
                .Concat(constants.Select(static c => (Position: c.Position, Entry: c.Entry)))
                .Concat(updates.Where(static u => u.Entry is not null).Select(static u => (Position: u.Position, Entry: u.Entry!)))
                .OrderBy(static e => e.Position)
                .Select(static e => e.Entry);

            return new LuaTableModel(
                symbol.ContainingNamespace.ToDisplayString(),
                new EquatableArray<LuaTypePart>([.. chain]),
                tableName,
                contextType,
                new EquatableArray<LuaFunctionModel>([.. functions.OrderBy(static f => f.Position).Select(static f => f.Function)]),
                new EquatableArray<LuaConstantModel>([.. constants.OrderBy(static c => c.Position).Select(static c => c.Constant)]),
                new EquatableArray<LuaUpdateModel>([.. updates.OrderBy(static u => u.Position).Select(static u => u.Update)]),
                new EquatableArray<LuaCatalogEntry>([.. classEntries, .. orderedMembers]));
        }

        private static AttributeData? FindAttribute(ISymbol symbol, string attributeName)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == attributeName)
                    return attribute;
            }
            return null;
        }

        private static LuaCatalogEntry? CreateAttributeEntry(AttributeData attribute, string defaultTable)
        {
            if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value is not string name)
                return null;

            string table = defaultTable;
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "Table" && argument.Value.Value is string overridden)
                    table = overridden;
            }

            bool isFunction = attribute.AttributeClass?.ToDisplayString() is (BuiltinAttributeName or FunctionAttributeName);
            var parameters = isFunction && attribute.ConstructorArguments.Length > 1
                ? ExtractStringArray(attribute.ConstructorArguments[1])
                : EquatableArray<string>.Empty;

            return new LuaCatalogEntry(table, name, isFunction, parameters);
        }

        private static EquatableArray<string> ExtractStringArray(TypedConstant constant)
        {
            if (constant.Kind != TypedConstantKind.Array)
                return EquatableArray<string>.Empty;
            return new EquatableArray<string>([.. constant.Values.Select(static v => v.Value as string ?? string.Empty)]);
        }

        private static LuaFunctionModel? CreateFunctionModel(IMethodSymbol method, AttributeData attribute, out EquatableArray<string> catalogParameters)
        {
            catalogParameters = EquatableArray<string>.Empty;
            if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value is not string luaName)
                return null;

            LuaReturnKind returnKind;
            if (method.ReturnsVoid)
                returnKind = LuaReturnKind.Void;
            else if (method.ReturnType.ToDisplayString() == "MoonSharp.Interpreter.DynValue")
                returnKind = LuaReturnKind.DynValue;
            else if (method.ReturnType.SpecialType == SpecialType.System_Double)
                returnKind = LuaReturnKind.Double;
            else
                return null;

            var parameters = new List<LuaParameterModel>();
            foreach (var parameter in method.Parameters)
            {
                LuaParameterKind kind;
                string defaultLiteral = string.Empty;
                switch (parameter.Type.ToDisplayString())
                {
                    case "MoonSharp.Interpreter.ScriptExecutionContext":
                        kind = LuaParameterKind.ExecutionContext;
                        break;
                    case "MoonSharp.Interpreter.CallbackArguments":
                        kind = LuaParameterKind.Arguments;
                        break;
                    case "MoonSharp.Interpreter.DynValue":
                        kind = LuaParameterKind.DynValue;
                        break;
                    case "double":
                        kind = LuaParameterKind.Double;
                        defaultLiteral = FormatDouble(parameter.HasExplicitDefaultValue ? (double)parameter.ExplicitDefaultValue! : 0d);
                        break;
                    case "int":
                        kind = LuaParameterKind.Int;
                        defaultLiteral = FormatDouble(parameter.HasExplicitDefaultValue ? (int)parameter.ExplicitDefaultValue! : 0);
                        break;
                    case "bool":
                        kind = LuaParameterKind.Bool;
                        defaultLiteral = parameter.HasExplicitDefaultValue && (bool)parameter.ExplicitDefaultValue! ? "true" : "false";
                        break;
                    default:
                        return null;
                }
                parameters.Add(new LuaParameterModel(parameter.Name, kind, defaultLiteral));
            }

            var explicitParameters = attribute.ConstructorArguments.Length > 1
                ? ExtractStringArray(attribute.ConstructorArguments[1])
                : EquatableArray<string>.Empty;
            catalogParameters = explicitParameters.Count > 0
                ? explicitParameters
                : new EquatableArray<string>([.. parameters
                    .Where(static p => p.Kind is not (LuaParameterKind.ExecutionContext or LuaParameterKind.Arguments))
                    .Select(static p => p.Name)]);

            return new LuaFunctionModel(luaName, method.Name, returnKind, new EquatableArray<LuaParameterModel>([.. parameters]));
        }

        private static LuaUpdateModel? CreateUpdateModel(IMethodSymbol method, AttributeData attribute, string tableName, ref string contextType, out LuaCatalogEntry? entry)
        {
            entry = null;
            if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value is not string luaName)
                return null;
            if (method.Parameters.Length != 1)
                return null;

            string parameterType = method.Parameters[0].Type.ToDisplayString();
            if (contextType.Length == 0)
                contextType = parameterType;
            else if (contextType != parameterType)
                return null;

            var returnType = method.ReturnType;
            LuaUpdateKind kind;
            ITypeSymbol valueType;
            if (returnType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                kind = LuaUpdateKind.OptionalValue;
                valueType = ((INamedTypeSymbol)returnType).TypeArguments[0];
            }
            else if (returnType.IsReferenceType)
            {
                kind = LuaUpdateKind.OptionalReference;
                valueType = returnType;
            }
            else
            {
                kind = LuaUpdateKind.Direct;
                valueType = returnType;
            }

            var valueKind = valueType.SpecialType switch
            {
                SpecialType.System_Double or SpecialType.System_Int32 => LuaValueKind.Number,
                SpecialType.System_Boolean => LuaValueKind.Boolean,
                SpecialType.System_String => LuaValueKind.String,
                _ => LuaValueKind.Other,
            };

            bool inCatalog = true;
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "InCatalog" && argument.Value.Value is bool value)
                    inCatalog = value;
            }

            if (inCatalog)
                entry = new LuaCatalogEntry(tableName, luaName, false, EquatableArray<string>.Empty);
            return new LuaUpdateModel(luaName, method.Name, kind, valueKind);
        }

        private static string FormatDouble(double value) =>
            value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "d";

        private static string RegistrationHintName(LuaTableModel model)
        {
            var chain = string.Join(".", model.TypeChain.Select(static p => p.Name));
            return $"{model.Namespace}.{chain}.LuaRegistration.g.cs";
        }

        private static string EmitRegistration(LuaTableModel model)
        {
            var builder = new StringBuilder();
            builder.AppendLine("#nullable enable");
            builder.AppendLine($"namespace {model.Namespace}");
            builder.AppendLine("{");

            string indent = "    ";
            foreach (var part in model.TypeChain)
            {
                builder.AppendLine($"{indent}{(part.IsStatic ? "static partial class " : "partial class ")}{part.Name}");
                builder.AppendLine($"{indent}{{");
                indent += "    ";
            }

            bool isStatic = model.TypeChain[model.TypeChain.Count - 1].IsStatic;
            string body = indent + "    ";

            if (model.Constants.Count > 0 || model.Functions.Count > 0)
            {
                builder.AppendLine($"{indent}internal {(isStatic ? "static " : "")}void RegisterLuaMembers(global::MoonSharp.Interpreter.Table table)");
                builder.AppendLine($"{indent}{{");

                foreach (var constant in model.Constants)
                    builder.AppendLine($"{body}table[\"{constant.LuaName}\"] = {constant.MemberName};");

                foreach (var function in model.Functions)
                {
                    string call = $"{function.MethodName}({EmitArguments(function.Parameters)})";
                    string lambda = function.ReturnKind switch
                    {
                        LuaReturnKind.DynValue => $"(execCtx, args) => {call}",
                        LuaReturnKind.Double => $"(execCtx, args) => global::MoonSharp.Interpreter.DynValue.NewNumber({call})",
                        _ => $"(execCtx, args) => {{ {call}; return global::MoonSharp.Interpreter.DynValue.Void; }}",
                    };
                    builder.AppendLine($"{body}table[\"{function.LuaName}\"] = global::MoonSharp.Interpreter.DynValue.NewCallback({lambda});");
                }

                builder.AppendLine($"{indent}}}");
            }

            if (model.Updates.Count > 0)
            {
                builder.AppendLine($"{indent}internal {(isStatic ? "static " : "")}void UpdateLuaMembers(global::MoonSharp.Interpreter.Table table, global::{model.ContextType} context)");
                builder.AppendLine($"{indent}{{");
                foreach (var update in model.Updates)
                {
                    switch (update.Kind)
                    {
                        case LuaUpdateKind.OptionalValue:
                            builder.AppendLine(update.ValueKind == LuaValueKind.Other
                                ? $"{body}{{ var value = {update.MethodName}(context); if (value.HasValue) table[\"{update.LuaName}\"] = value.Value; }}"
                                : $"{body}{{ var value = {update.MethodName}(context); if (value.HasValue) table.Set(\"{update.LuaName}\", {WrapValue(update.ValueKind, "value.Value")}); }}");
                            break;
                        case LuaUpdateKind.OptionalReference:
                            builder.AppendLine(update.ValueKind == LuaValueKind.Other
                                ? $"{body}{{ var value = {update.MethodName}(context); if (value is not null) table[\"{update.LuaName}\"] = value; }}"
                                : $"{body}{{ var value = {update.MethodName}(context); if (value is not null) table.Set(\"{update.LuaName}\", {WrapValue(update.ValueKind, "value")}); }}");
                            break;
                        default:
                            builder.AppendLine(update.ValueKind == LuaValueKind.Other
                                ? $"{body}table[\"{update.LuaName}\"] = {update.MethodName}(context);"
                                : $"{body}table.Set(\"{update.LuaName}\", {WrapValue(update.ValueKind, $"{update.MethodName}(context)")});");
                            break;
                    }
                }
                builder.AppendLine($"{indent}}}");
            }

            for (int i = model.TypeChain.Count - 1; i >= 0; i--)
            {
                indent = new string(' ', 4 * (i + 1));
                builder.AppendLine($"{indent}}}");
            }
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string WrapValue(LuaValueKind kind, string expression) => kind switch
        {
            LuaValueKind.Number => $"global::MoonSharp.Interpreter.DynValue.NewNumber({expression})",
            LuaValueKind.Boolean => $"global::MoonSharp.Interpreter.DynValue.NewBoolean({expression})",
            _ => $"global::MoonSharp.Interpreter.DynValue.NewString({expression})",
        };

        private static string EmitArguments(EquatableArray<LuaParameterModel> parameters)
        {
            var rendered = new List<string>();
            int position = 0;
            foreach (var parameter in parameters)
            {
                if (parameter.Kind == LuaParameterKind.ExecutionContext)
                {
                    rendered.Add("execCtx");
                    continue;
                }
                if (parameter.Kind == LuaParameterKind.Arguments)
                {
                    rendered.Add("args");
                    continue;
                }
                rendered.Add(parameter.Kind switch
                {
                    LuaParameterKind.DynValue => $"args.Count > {position} ? args[{position}] : global::MoonSharp.Interpreter.DynValue.Nil",
                    LuaParameterKind.Double => $"args.Count > {position} ? args[{position}].CastToNumber() ?? {parameter.DefaultLiteral} : {parameter.DefaultLiteral}",
                    LuaParameterKind.Int => $"(int)(args.Count > {position} ? args[{position}].CastToNumber() ?? {parameter.DefaultLiteral} : {parameter.DefaultLiteral})",
                    _ => $"args.Count > {position} ? args[{position}].CastToBool() : {parameter.DefaultLiteral}",
                });
                position++;
            }
            return string.Join(", ", rendered);
        }

        private static string EmitCatalog(LuaCatalogEntry[] entries)
        {
            var builder = new StringBuilder();
            builder.AppendLine("#nullable enable");
            builder.AppendLine("namespace LuaScript.Generated");
            builder.AppendLine("{");
            builder.AppendLine("    internal static class LuaApiModuleInitializer");
            builder.AppendLine("    {");
            builder.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
            builder.AppendLine("        internal static void Initialize()");
            builder.AppendLine("        {");
            builder.AppendLine("            global::LuaScript.Api.LuaApiCatalog.Register(new global::LuaScript.Api.LuaApiMember[]");
            builder.AppendLine("            {");
            foreach (var entry in entries)
            {
                string kind = entry.IsFunction
                    ? "global::LuaScript.Api.LuaApiMemberKind.Function"
                    : "global::LuaScript.Api.LuaApiMemberKind.Variable";
                string parameters = entry.Parameters.Count == 0
                    ? "global::System.Array.Empty<string>()"
                    : $"new string[] {{ {string.Join(", ", entry.Parameters.Select(static p => $"\"{p}\""))} }}";
                builder.AppendLine($"                new(\"{entry.Table}\", \"{entry.Name}\", {kind}, {parameters}),");
            }
            builder.AppendLine("            });");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static IReadOnlyList<LuaCatalogEntry> CollectAssemblyEntries(Compilation compilation)
        {
            var entries = new List<(int Position, LuaCatalogEntry Entry)>();
            foreach (var attribute in compilation.Assembly.GetAttributes())
            {
                string? attributeName = attribute.AttributeClass?.ToDisplayString();
                if (attributeName is not (VariableAttributeName or BuiltinAttributeName or FunctionAttributeName))
                    continue;
                var entry = CreateAttributeEntry(attribute, string.Empty);
                if (entry is null)
                    continue;
                entries.Add((attribute.ApplicationSyntaxReference?.Span.Start ?? 0, entry));
            }
            return [.. entries.OrderBy(static e => e.Position).Select(static e => e.Entry)];
        }
    }
}
