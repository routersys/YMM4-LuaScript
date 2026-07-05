using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LuaScript.Generator
{
    [Generator]
    public sealed class GpuShaderSourceGenerator : IIncrementalGenerator
    {
        private const string AttributeName = "LuaScript.Engine.Processing.GpuPixelOperationShaderAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classes = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                    static (ctx, _) => CreateClass(ctx))
                .Where(static model => model is not null)
                .Select(static (model, _) => model!);

            var files = context.AdditionalTextsProvider
                .Where(static file => file.Path.EndsWith(".hlsl", StringComparison.OrdinalIgnoreCase))
                .Select(static (file, token) => new GpuShaderFile(Normalize(file.Path), file.GetText(token)?.ToString() ?? string.Empty))
                .Collect();

            context.RegisterSourceOutput(classes.Combine(files), static (context, source) =>
            {
                var (model, files) = source;
                var resolved = Resolve(model, files);
                if (resolved.Errors.Length > 0)
                {
                    context.AddSource(ErrorHintName(model), SourceText.From(EmitErrors(resolved.Errors), Encoding.UTF8));
                    return;
                }

                context.AddSource(HintName(model), SourceText.From(Emit(model, resolved.Methods), Encoding.UTF8));
            });
        }

        private static GpuShaderClass? CreateClass(GeneratorSyntaxContext context)
        {
            if (context.Node is not ClassDeclarationSyntax declaration)
                return null;
            if (context.SemanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol type)
                return null;

            var attributes = type.GetAttributes()
                .Where(static attribute => attribute.AttributeClass?.ToDisplayString() == AttributeName)
                .ToArray();
            if (attributes.Length == 0)
                return null;

            var chain = new List<GpuShaderTypePart>();
            for (var current = type; current is not null; current = current.ContainingType)
                chain.Insert(0, new GpuShaderTypePart(current.Name, current.IsStatic));

            var methods = ImmutableArray.CreateBuilder<GpuShaderMethod>();
            var errors = ImmutableArray.CreateBuilder<string>();

            foreach (var attribute in attributes)
            {
                if (attribute.ConstructorArguments.Length < 3 ||
                    attribute.ConstructorArguments[0].Value is not string methodName ||
                    attribute.ConstructorArguments[1].Value is not string fileName ||
                    attribute.ConstructorArguments[2].Value is not string entryPoint)
                {
                    errors.Add("GPU shader attribute arguments are invalid");
                    continue;
                }

                var candidates = type.GetMembers(methodName).OfType<IMethodSymbol>().ToArray();
                if (candidates.Length == 0)
                {
                    errors.Add($"GPU shader method '{methodName}' was not found");
                    continue;
                }
                if (candidates.Length > 1)
                {
                    errors.Add($"GPU shader method '{methodName}' is ambiguous");
                    continue;
                }

                var method = candidates[0];
                string? error = Validate(method, fileName, entryPoint, out var invocation);
                if (error is not null)
                {
                    errors.Add($"GPU shader method '{methodName}': {error}");
                    continue;
                }

                var parameters = ImmutableArray.CreateBuilder<GpuShaderParameter>();
                foreach (var parameter in method.Parameters)
                    parameters.Add(new GpuShaderParameter(ParameterPrefix(parameter), parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), parameter.Name));

                methods.Add(new GpuShaderMethod(
                    AccessibilityName(method.DeclaredAccessibility),
                    method.IsStatic,
                    method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    method.Name,
                    [.. parameters],
                    Normalize(fileName),
                    entryPoint,
                    invocation));
            }

            return new GpuShaderClass(
                type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString(),
                [.. chain],
                [.. methods],
                [.. errors]);
        }

        private static string? Validate(IMethodSymbol method, string fileName, string entryPoint, out GpuShaderInvocation invocation)
        {
            invocation = new GpuShaderInvocation(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            if (string.IsNullOrWhiteSpace(fileName))
                return "file name is empty";
            if (string.IsNullOrWhiteSpace(entryPoint))
                return "entry point is empty";
            if (method.ReturnType.SpecialType != SpecialType.System_Boolean)
                return "return type must be bool";

            var parameters = method.Parameters;
            if (parameters.Length == 4 &&
                IsReadOnlySpanSingle(parameters[0].Type) &&
                IsByteArray(parameters[1].Type) &&
                IsInt32(parameters[2].Type) &&
                IsInt32(parameters[3].Type))
            {
                invocation = new GpuShaderInvocation("[]", parameters[0].Name, parameters[1].Name, parameters[2].Name, parameters[3].Name);
                return null;
            }

            if (parameters.Length == 5 &&
                IsReadOnlySpanPixelShaderInput(parameters[0].Type) &&
                IsReadOnlySpanSingle(parameters[1].Type) &&
                IsByteArray(parameters[2].Type) &&
                IsInt32(parameters[3].Type) &&
                IsInt32(parameters[4].Type))
            {
                invocation = new GpuShaderInvocation(parameters[0].Name, parameters[1].Name, parameters[2].Name, parameters[3].Name, parameters[4].Name);
                return null;
            }

            return "signature is invalid";
        }

        private static GpuShaderResolution Resolve(GpuShaderClass model, ImmutableArray<GpuShaderFile> files)
        {
            var methods = ImmutableArray.CreateBuilder<GpuShaderMethodSource>();
            var errors = ImmutableArray.CreateBuilder<string>();
            errors.AddRange(model.Errors);

            foreach (var method in model.Methods)
            {
                var matched = files.Where(file => Matches(file.Path, method.FileName)).ToArray();
                if (matched.Length == 0)
                {
                    errors.Add($"GPU shader file '{method.FileName}' for method '{method.MethodName}' was not found");
                    continue;
                }
                if (matched.Length > 1)
                {
                    errors.Add($"GPU shader file '{method.FileName}' for method '{method.MethodName}' matched multiple additional files");
                    continue;
                }

                string? failure = ShaderCompiler.Compile(matched[0].Source, method.EntryPoint, out string bytecode);
                if (failure is not null)
                {
                    errors.Add($"GPU shader method '{method.MethodName}' failed to compile: {failure}");
                    continue;
                }

                methods.Add(new GpuShaderMethodSource(method, bytecode));
            }

            return new GpuShaderResolution([.. methods], [.. errors]);
        }

        private static bool Matches(string filePath, string registrationPath)
        {
            if (string.Equals(filePath, registrationPath, StringComparison.OrdinalIgnoreCase))
                return true;
            return filePath.EndsWith("/" + registrationPath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReadOnlySpanSingle(ITypeSymbol type)
        {
            return type is INamedTypeSymbol named &&
                named.OriginalDefinition.ToDisplayString() == "System.ReadOnlySpan<T>" &&
                named.TypeArguments.Length == 1 &&
                named.TypeArguments[0].SpecialType == SpecialType.System_Single;
        }

        private static bool IsReadOnlySpanPixelShaderInput(ITypeSymbol type)
        {
            string display = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return display.Contains("ReadOnlySpan", StringComparison.Ordinal) &&
                display.Contains("PixelShaderInput", StringComparison.Ordinal);
        }

        private static bool IsByteArray(ITypeSymbol type)
        {
            return type is IArrayTypeSymbol array &&
                array.Rank == 1 &&
                array.ElementType.SpecialType == SpecialType.System_Byte;
        }

        private static bool IsInt32(ITypeSymbol type) => type.SpecialType == SpecialType.System_Int32;

        private static string Normalize(string path) => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

        private static string AccessibilityName(Accessibility accessibility)
        {
            return accessibility switch
            {
                Accessibility.Public => "public",
                Accessibility.Internal => "internal",
                Accessibility.Protected => "protected",
                Accessibility.ProtectedAndInternal => "private protected",
                Accessibility.ProtectedOrInternal => "protected internal",
                _ => "private",
            };
        }

        private static string ParameterPrefix(IParameterSymbol parameter)
        {
            return parameter.RefKind switch
            {
                RefKind.In => "in ",
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                _ => string.Empty,
            };
        }

        private static string Emit(GpuShaderClass model, ImmutableArray<GpuShaderMethodSource> sources)
        {
            var builder = new StringBuilder();
            builder.AppendLine("#nullable enable");
            if (model.Namespace.Length > 0)
            {
                builder.AppendLine("namespace " + model.Namespace);
                builder.AppendLine("{");
            }

            int indent = model.Namespace.Length > 0 ? 1 : 0;
            foreach (var type in model.TypeChain)
            {
                AppendIndent(builder, indent);
                builder.Append(type.IsStatic ? "static partial class " : "partial class ");
                builder.AppendLine(type.Name);
                AppendIndent(builder, indent);
                builder.AppendLine("{");
                indent++;
            }

            for (int i = 0; i < sources.Length; i++)
            {
                if (i > 0)
                    builder.AppendLine();
                EmitMethod(builder, indent, sources[i]);
            }

            for (int i = model.TypeChain.Length - 1; i >= 0; i--)
            {
                indent--;
                AppendIndent(builder, indent);
                builder.AppendLine("}");
            }

            if (model.Namespace.Length > 0)
                builder.AppendLine("}");
            return builder.ToString();
        }

        private static void EmitMethod(StringBuilder builder, int indent, GpuShaderMethodSource source)
        {
            var method = source.Method;
            string field = "s_shader_" + method.MethodName;

            AppendIndent(builder, indent);
            builder.Append("private static readonly byte[] ");
            builder.Append(field);
            builder.Append(" = global::System.Convert.FromBase64String(");
            builder.Append(Literal(source.Bytecode));
            builder.AppendLine(");");
            builder.AppendLine();

            AppendIndent(builder, indent);
            builder.Append(method.Accessibility);
            builder.Append(' ');
            if (method.IsStatic)
                builder.Append("static ");
            builder.Append("partial ");
            builder.Append(method.ReturnType);
            builder.Append(' ');
            builder.Append(method.MethodName);
            builder.Append('(');
            for (int i = 0; i < method.Parameters.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                var parameter = method.Parameters[i];
                builder.Append(parameter.Prefix);
                builder.Append(parameter.Type);
                builder.Append(' ');
                builder.Append(parameter.Name);
            }
            builder.AppendLine(")");
            AppendIndent(builder, indent);
            builder.AppendLine("{");
            AppendIndent(builder, indent + 1);
            builder.Append("return Run(");
            builder.Append(field);
            builder.Append(", ");
            builder.Append(method.Invocation.Resources);
            builder.Append(", ");
            builder.Append(method.Invocation.Constants);
            builder.Append(", ");
            builder.Append(method.Invocation.Target);
            builder.Append(", ");
            builder.Append(method.Invocation.Width);
            builder.Append(", ");
            builder.Append(method.Invocation.Height);
            builder.AppendLine(");");
            AppendIndent(builder, indent);
            builder.AppendLine("}");
        }

        private static void AppendIndent(StringBuilder builder, int indent)
        {
            for (int i = 0; i < indent; i++)
                builder.Append("    ");
        }

        private static string EmitErrors(ImmutableArray<string> errors)
        {
            var builder = new StringBuilder();
            foreach (var error in errors)
                builder.AppendLine("#error " + error);
            return builder.ToString();
        }

        private static string HintName(GpuShaderClass model)
        {
            var builder = new StringBuilder("GpuPixelOperationShader");
            if (model.Namespace.Length > 0)
            {
                builder.Append('.');
                builder.Append(HintPart(model.Namespace));
            }
            foreach (var type in model.TypeChain)
            {
                builder.Append('.');
                builder.Append(HintPart(type.Name));
            }
            builder.Append(".g.cs");
            return builder.ToString();
        }

        private static string ErrorHintName(GpuShaderClass model)
        {
            return Path.ChangeExtension(HintName(model), ".Errors.g.cs");
        }

        private static string HintPart(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
                builder.Append(char.IsLetterOrDigit(c) ? c : '_');
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

        private sealed record GpuShaderClass(
            string Namespace,
            ImmutableArray<GpuShaderTypePart> TypeChain,
            ImmutableArray<GpuShaderMethod> Methods,
            ImmutableArray<string> Errors);

        private sealed record GpuShaderTypePart(string Name, bool IsStatic);

        private sealed record GpuShaderMethod(
            string Accessibility,
            bool IsStatic,
            string ReturnType,
            string MethodName,
            ImmutableArray<GpuShaderParameter> Parameters,
            string FileName,
            string EntryPoint,
            GpuShaderInvocation Invocation);

        private sealed record GpuShaderParameter(string Prefix, string Type, string Name);

        private sealed record GpuShaderInvocation(string Resources, string Constants, string Target, string Width, string Height);

        private sealed record GpuShaderMethodSource(GpuShaderMethod Method, string Bytecode);

        private sealed record GpuShaderFile(string Path, string Source);

        private sealed record GpuShaderResolution(ImmutableArray<GpuShaderMethodSource> Methods, ImmutableArray<string> Errors);

        private static class ShaderCompiler
        {
            private const string Profile = "ps_5_0";

            public static string? Compile(string source, string entryPoint, out string bytecode)
            {
                bytecode = string.Empty;
                byte[] data = Encoding.UTF8.GetBytes(source);
                IntPtr code = IntPtr.Zero;
                IntPtr errors = IntPtr.Zero;
                try
                {
                    int hr = D3DCompile(data, (IntPtr)data.Length, null, IntPtr.Zero, IntPtr.Zero, entryPoint, Profile, 0u, 0u, out code, out errors);
                    if (hr < 0 || code == IntPtr.Zero)
                        return ReadError(errors, hr);
                    bytecode = Convert.ToBase64String(ReadBlob(code));
                    return null;
                }
                catch (DllNotFoundException)
                {
                    return "the Direct3D shader compiler is unavailable on this build host";
                }
                catch (EntryPointNotFoundException)
                {
                    return "the Direct3D shader compiler is unavailable on this build host";
                }
                finally
                {
                    Release(errors);
                    Release(code);
                }
            }

            private static byte[] ReadBlob(IntPtr blob)
            {
                IntPtr pointer = Invoke<GetBufferPointerDelegate>(blob, 3)(blob);
                int size = checked((int)(long)Invoke<GetBufferSizeDelegate>(blob, 4)(blob));
                var buffer = new byte[size];
                Marshal.Copy(pointer, buffer, 0, size);
                return buffer;
            }

            private static string ReadError(IntPtr blob, int hr)
            {
                string fallback = "HRESULT 0x" + hr.ToString("X8", CultureInfo.InvariantCulture);
                if (blob == IntPtr.Zero)
                    return fallback;
                IntPtr pointer = Invoke<GetBufferPointerDelegate>(blob, 3)(blob);
                int size = checked((int)(long)Invoke<GetBufferSizeDelegate>(blob, 4)(blob));
                string message = (Marshal.PtrToStringAnsi(pointer, size) ?? string.Empty).Trim();
                return message.Length > 0 ? message : fallback;
            }

            private static void Release(IntPtr unknown)
            {
                if (unknown != IntPtr.Zero)
                    Invoke<ReleaseDelegate>(unknown, 2)(unknown);
            }

            private static T Invoke<T>(IntPtr instance, int slot) where T : Delegate
            {
                IntPtr table = Marshal.ReadIntPtr(instance);
                IntPtr function = Marshal.ReadIntPtr(table, slot * IntPtr.Size);
                return (T)Marshal.GetDelegateForFunctionPointer(function, typeof(T));
            }

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate IntPtr GetBufferPointerDelegate(IntPtr self);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate IntPtr GetBufferSizeDelegate(IntPtr self);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate uint ReleaseDelegate(IntPtr self);

            [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall)]
            private static extern int D3DCompile(
                byte[] srcData,
                IntPtr srcDataSize,
                [MarshalAs(UnmanagedType.LPStr)] string? sourceName,
                IntPtr defines,
                IntPtr include,
                [MarshalAs(UnmanagedType.LPStr)] string entryPoint,
                [MarshalAs(UnmanagedType.LPStr)] string target,
                uint flags1,
                uint flags2,
                out IntPtr code,
                out IntPtr errorMsgs);
        }
    }
}
