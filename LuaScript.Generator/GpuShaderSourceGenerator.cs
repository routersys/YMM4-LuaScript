using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace LuaScript.Generator
{
    [Generator]
    public sealed class GpuShaderSourceGenerator : IIncrementalGenerator
    {
        private const string AttributeName = "LuaScript.Engine.Processing.GpuPixelOperationShaderAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var registrations = context.CompilationProvider.Select(static (compilation, _) => CollectRegistrations(compilation));
            var files = context.AdditionalTextsProvider
                .Where(static file => file.Path.EndsWith(".hlsl", StringComparison.OrdinalIgnoreCase))
                .Select(static (file, token) => new GpuShaderFile(Normalize(file.Path), file.GetText(token)?.ToString() ?? string.Empty))
                .Collect();

            context.RegisterSourceOutput(registrations.Combine(files), static (context, source) =>
            {
                var (registrations, files) = source;
                if (registrations.Length == 0)
                    return;

                var result = Resolve(registrations, files);
                if (result.Errors.Length > 0)
                {
                    context.AddSource("GpuPixelOperationShaderRegistry.Errors.g.cs", SourceText.From(EmitErrors(result.Errors), Encoding.UTF8));
                    return;
                }
                if (result.Sources.IsDefaultOrEmpty)
                    return;

                context.AddSource("GpuPixelOperationShaderRegistry.g.cs", SourceText.From(Emit(result.Sources), Encoding.UTF8));
            });
        }

        private static ImmutableArray<GpuShaderRegistration> CollectRegistrations(Compilation compilation)
        {
            var registrations = ImmutableArray.CreateBuilder<GpuShaderRegistration>();
            foreach (var attribute in compilation.Assembly.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != AttributeName || attribute.ConstructorArguments.Length < 2)
                    continue;
                if (attribute.ConstructorArguments[0].Value is not string apiName || attribute.ConstructorArguments[1].Value is not string fileName)
                    continue;
                registrations.Add(new GpuShaderRegistration(apiName, Normalize(fileName), attribute.ApplicationSyntaxReference?.Span.Start ?? 0));
            }
            return registrations.ToImmutable();
        }

        private static GpuShaderResolution Resolve(ImmutableArray<GpuShaderRegistration> registrations, ImmutableArray<GpuShaderFile> files)
        {
            var apiNames = new HashSet<string>(StringComparer.Ordinal);
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            var sources = ImmutableArray.CreateBuilder<GpuShaderSource>();
            var errors = ImmutableArray.CreateBuilder<string>();

            foreach (var registration in registrations.OrderBy(static r => r.Position).ThenBy(static r => r.ApiName, StringComparer.Ordinal))
            {
                if (!apiNames.Add(registration.ApiName))
                {
                    errors.Add($"GPU shader API '{registration.ApiName}' is registered multiple times");
                    continue;
                }

                var matched = files.Where(file => Matches(file.Path, registration.FileName)).ToArray();
                if (matched.Length == 0)
                {
                    errors.Add($"GPU shader file '{registration.FileName}' for API '{registration.ApiName}' was not found");
                    continue;
                }
                if (matched.Length > 1)
                {
                    errors.Add($"GPU shader file '{registration.FileName}' for API '{registration.ApiName}' matched multiple additional files");
                    continue;
                }

                string identifier = ToIdentifier(registration.ApiName);
                if (!identifiers.Add(identifier))
                {
                    errors.Add($"GPU shader API '{registration.ApiName}' resolves to a duplicate identifier");
                    continue;
                }

                sources.Add(new GpuShaderSource(registration.ApiName, identifier, matched[0].Source));
            }

            return new GpuShaderResolution(sources.ToImmutable(), errors.ToImmutable());
        }

        private static bool Matches(string filePath, string registrationPath)
        {
            if (string.Equals(filePath, registrationPath, StringComparison.OrdinalIgnoreCase))
                return true;
            return filePath.EndsWith("/" + registrationPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string path) => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

        private static string ToIdentifier(string apiName)
        {
            var builder = new StringBuilder();
            bool upper = true;
            foreach (char c in apiName)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    upper = true;
                    continue;
                }
                builder.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            if (builder.Length == 0 || char.IsDigit(builder[0]))
                builder.Insert(0, '_');
            return builder.ToString();
        }

        private static string Emit(ImmutableArray<GpuShaderSource> sources)
        {
            var builder = new StringBuilder();
            builder.AppendLine("#nullable enable");
            builder.AppendLine("namespace LuaScript.Engine.Processing");
            builder.AppendLine("{");
            builder.AppendLine("    internal static class GpuPixelOperationShaderRegistry");
            builder.AppendLine("    {");
            builder.AppendLine("        internal static bool TryGet(string apiName, out string source)");
            builder.AppendLine("        {");
            builder.AppendLine("            switch (apiName)");
            builder.AppendLine("            {");
            foreach (var source in sources)
                builder.AppendLine($"                case {Literal(source.ApiName)}: source = {source.Identifier}; return true;");
            builder.AppendLine("                default: source = string.Empty; return false;");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine();
            foreach (var source in sources)
                builder.AppendLine($"        internal const string {source.Identifier} = {Literal(source.Source)};");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string EmitErrors(ImmutableArray<string> errors)
        {
            var builder = new StringBuilder();
            foreach (var error in errors)
                builder.AppendLine("#error " + error);
            return builder.ToString();
        }

        private static string Literal(string value)
        {
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\':
                        builder.Append(@"\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\0':
                        builder.Append(@"\0");
                        break;
                    case '\a':
                        builder.Append(@"\a");
                        break;
                    case '\b':
                        builder.Append(@"\b");
                        break;
                    case '\f':
                        builder.Append(@"\f");
                        break;
                    case '\n':
                        builder.Append(@"\n");
                        break;
                    case '\r':
                        builder.Append(@"\r");
                        break;
                    case '\t':
                        builder.Append(@"\t");
                        break;
                    case '\v':
                        builder.Append(@"\v");
                        break;
                    default:
                        if (char.IsControl(c))
                        {
                            builder.Append(@"\u");
                            builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
            builder.Append('"');
            return builder.ToString();
        }

        private sealed record GpuShaderRegistration(string ApiName, string FileName, int Position);

        private sealed record GpuShaderFile(string Path, string Source);

        private sealed record GpuShaderSource(string ApiName, string Identifier, string Source);

        private sealed record GpuShaderResolution(ImmutableArray<GpuShaderSource> Sources, ImmutableArray<string> Errors);
    }
}
