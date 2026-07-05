using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LuaScript.Generator
{
    [Generator]
    public sealed class LuaSyntaxRuleGenerator : IIncrementalGenerator
    {
        private const string RuleAttributeName = "LuaScript.Compat.Syntax.LuaSyntaxRuleAttribute";
        private const string RuleInterfaceName = "LuaScript.Compat.Syntax.ILuaSyntaxRule";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var rules = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    RuleAttributeName,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, _) => CreateModel(ctx))
                .Where(static model => model is not null)
                .Select(static (model, _) => model!);

            context.RegisterSourceOutput(rules.Collect(), static (ctx, models) =>
            {
                if (models.Length == 0)
                    return;
                ctx.AddSource("LuaSyntaxRuleRegistry.g.cs", SourceText.From(Emit(models), Encoding.UTF8));
            });
        }

        private static LuaSyntaxRuleModel? CreateModel(GeneratorAttributeSyntaxContext context)
        {
            if (context.TargetSymbol is not INamedTypeSymbol symbol)
                return null;

            if (!symbol.AllInterfaces.Any(static contract => contract.ToDisplayString() == RuleInterfaceName))
                return null;

            int order = 0;
            var arguments = context.Attributes[0].ConstructorArguments;
            if (arguments.Length > 0 && arguments[0].Value is int value)
                order = value;

            bool isCatalog = false;
            string keyword = string.Empty;
            foreach (var argument in context.Attributes[0].NamedArguments)
            {
                if (argument.Key == "IsCatalog" && argument.Value.Value is bool catalogValue)
                    isCatalog = catalogValue;
                else if (argument.Key == "Keyword" && argument.Value.Value is string keywordValue)
                    keyword = keywordValue;
            }

            return new LuaSyntaxRuleModel(symbol.ToDisplayString(), order, isCatalog ? keyword : string.Empty);
        }

        private static string Emit(ImmutableArray<LuaSyntaxRuleModel> models)
        {
            var ordered = models
                .OrderBy(static m => m.Order)
                .ThenBy(static m => m.TypeName, StringComparer.Ordinal)
                .ToArray();

            var builder = new StringBuilder();
            builder.AppendLine("#nullable enable");
            builder.AppendLine("namespace LuaScript.Compat.Syntax");
            builder.AppendLine("{");
            builder.AppendLine("    internal static class LuaSyntaxRuleRegistry");
            builder.AppendLine("    {");
            builder.AppendLine("        internal static readonly global::LuaScript.Compat.Syntax.ILuaSyntaxRule[] Rules = new global::LuaScript.Compat.Syntax.ILuaSyntaxRule[]");
            builder.AppendLine("        {");
            foreach (var model in ordered)
                builder.AppendLine($"            new global::{model.TypeName}(),");
            builder.AppendLine("        };");

            var keywords = ordered
                .Where(static m => m.CatalogKeyword.Length > 0)
                .Select(static m => m.CatalogKeyword)
                .Distinct()
                .ToArray();
            if (keywords.Length == 0)
            {
                builder.AppendLine("        internal static readonly string[] CatalogKeywords = global::System.Array.Empty<string>();");
            }
            else
            {
                builder.AppendLine("        internal static readonly string[] CatalogKeywords = new string[]");
                builder.AppendLine("        {");
                foreach (var keyword in keywords)
                    builder.AppendLine($"            \"{keyword}\",");
                builder.AppendLine("        };");
            }
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }
    }
}
