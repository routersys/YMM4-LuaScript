using System;
using System.Collections.Generic;

namespace LuaScript.Engine.Kernel
{
    internal sealed class KernelExtractor
    {
        private readonly record struct FuncSpec(KFunc Func, int MinArgs, int MaxArgs);

        private static readonly Dictionary<string, FuncSpec> MathFunctions = new(StringComparer.Ordinal)
        {
            ["abs"] = new(KFunc.Abs, 1, 1),
            ["floor"] = new(KFunc.Floor, 1, 1),
            ["ceil"] = new(KFunc.Ceil, 1, 1),
            ["sqrt"] = new(KFunc.Sqrt, 1, 1),
            ["sin"] = new(KFunc.Sin, 1, 1),
            ["cos"] = new(KFunc.Cos, 1, 1),
            ["tan"] = new(KFunc.Tan, 1, 1),
            ["asin"] = new(KFunc.Asin, 1, 1),
            ["acos"] = new(KFunc.Acos, 1, 1),
            ["atan"] = new(KFunc.Atan, 1, 1),
            ["atan2"] = new(KFunc.Atan2, 2, 2),
            ["exp"] = new(KFunc.Exp, 1, 1),
            ["log"] = new(KFunc.Log, 1, 1),
            ["pow"] = new(KFunc.Pow, 2, 2),
            ["fmod"] = new(KFunc.Fmod, 2, 2),
            ["min"] = new(KFunc.Min, 2, int.MaxValue),
            ["max"] = new(KFunc.Max, 2, int.MaxValue),
        };

        private readonly List<KExpr> _bindings = [];
        private readonly Dictionary<string, int> _scope = new(StringComparer.Ordinal);
        private readonly SortedSet<KernelUniform> _uniforms = [];

        private string _widthVariable = string.Empty;
        private string _heightVariable = string.Empty;
        private bool _coordsEnabled;
        private string? _pixelDataVariable;
        private string _baseIndexVariable = string.Empty;
        private readonly bool[] _writtenChannels = new bool[4];

        public static KernelProgram? TryExtract(string runnableScript)
        {
            try
            {
                return new KernelExtractor().Extract(runnableScript);
            }
            catch (KernelUnsupportedException)
            {
                return null;
            }
        }

        private KernelProgram? Extract(string runnableScript)
        {
            var block = LuaParser.Parse(runnableScript);

            int index = 0;
            while (index < block.Count && block[index] is LocalStmt prelude)
            {
                if (TryBindPixelData(prelude))
                {
                    index++;
                    continue;
                }
                BindLocal(prelude);
                index++;
            }

            if (index >= block.Count || block[index] is not NumericForStmt outer)
                return null;
            if (index + 1 != block.Count)
                return null;

            if (!TryAxis(outer, out string outerVar, out KAxis outerAxis))
                return null;
            if (outer.Body.Count != 1 || outer.Body[0] is not NumericForStmt inner)
                return null;
            if (!TryAxis(inner, out string innerVar, out KAxis innerAxis))
                return null;
            if (outerAxis == innerAxis)
                return null;

            _widthVariable = outerAxis == KAxis.X ? outerVar : innerVar;
            _heightVariable = outerAxis == KAxis.Y ? outerVar : innerVar;

            return _pixelDataVariable is null ? BuildKernel(inner.Body) : BuildPixelDataKernel(inner.Body);
        }

        private bool TryBindPixelData(LocalStmt local)
        {
            if (local.Names.Count != 1 || local.Values.Count != 1 ||
                local.Values[0] is not CallExpr { Target: MemberExpr { Target: NameExpr { Name: "obj" }, Name: "getpixeldata" }, Arguments.Count: 0 })
                return false;
            if (_pixelDataVariable is not null)
                throw new KernelUnsupportedException("Multiple pixel-data handles are not supported.");
            _pixelDataVariable = local.Names[0];
            return true;
        }

        private KernelProgram? BuildPixelDataKernel(IReadOnlyList<LuaStmt> body)
        {
            if (body.Count < 5)
                return null;
            if (body[0] is not LocalStmt { Names.Count: 1, Values.Count: 1 } baseStmt || !IsBaseIndex(baseStmt.Values[0]))
                return null;

            _baseIndexVariable = baseStmt.Names[0];
            EnsureNotCoordinateName(_baseIndexVariable);
            _coordsEnabled = true;

            KExpr? outR = null, outG = null, outB = null;
            for (int i = 1; i < body.Count; i++)
            {
                switch (body[i])
                {
                    case LocalStmt local:
                        BindLocal(local);
                        break;
                    case AssignStmt assign:
                        BindAssign(assign);
                        break;
                    case CallStmt { Call: MethodCallExpr set } when IsPixelDataSet(set, out int writeChannel):
                        var value = Lower(set.Arguments[1]);
                        if (writeChannel == 0) outR = value;
                        else if (writeChannel == 1) outG = value;
                        else outB = value;
                        _writtenChannels[writeChannel] = true;
                        break;
                    default:
                        return null;
                }
            }

            if (outR is null || outG is null || outB is null)
                return null;

            return new KernelProgram(_bindings, outR, outG, outB, new KInput(KChannel.A), [.. _uniforms]);
        }

        private bool IsBaseIndex(LuaExpr expression)
        {
            if (expression is not BinaryExpr { Operator: "*", Right: NumberExpr { Value: 4d } } scaled ||
                scaled.Left is not BinaryExpr { Operator: "+" } sum)
                return false;
            if (!IsCoordinate(sum.Right, KAxis.X))
                return false;
            if (sum.Left is not BinaryExpr { Operator: "*" } stride)
                return false;
            return IsCoordinate(stride.Left, KAxis.Y) && IsWidth(stride.Right);
        }

        private bool IsWidth(LuaExpr expression)
        {
            if (expression is MemberExpr { Target: NameExpr { Name: "obj" }, Name: var field } &&
                KernelUniforms.TryResolveMember("obj", field, out var uniform))
                return uniform == KernelUniform.Width;
            return _pixelDataVariable is not null &&
                expression is MemberExpr { Target: NameExpr { } target, Name: "width" } &&
                string.Equals(target.Name, _pixelDataVariable, StringComparison.Ordinal);
        }

        private bool TryPixelDataChannel(LuaExpr expression, out int channel)
        {
            channel = 0;
            if (expression is not MethodCallExpr { Method: "get", Arguments.Count: 1 } call ||
                call.Target is not NameExpr { } handle ||
                !string.Equals(handle.Name, _pixelDataVariable, StringComparison.Ordinal))
                return false;
            if (!TryChannelOffset(call.Arguments[0], 1, 4, out channel))
                return false;
            channel--;
            return !_writtenChannels[channel];
        }

        private bool IsPixelDataSet(MethodCallExpr call, out int channel)
        {
            channel = 0;
            if (call.Method != "set" || call.Arguments.Count != 2 ||
                call.Target is not NameExpr { } handle ||
                !string.Equals(handle.Name, _pixelDataVariable, StringComparison.Ordinal))
                return false;
            if (!TryChannelOffset(call.Arguments[0], 1, 3, out channel))
                return false;
            channel--;
            return true;
        }

        private bool TryChannelOffset(LuaExpr expression, int min, int max, out int offset)
        {
            offset = 0;
            if (expression is not BinaryExpr { Operator: "+", Left: NameExpr baseName, Right: NumberExpr number } ||
                !string.Equals(baseName.Name, _baseIndexVariable, StringComparison.Ordinal))
                return false;
            offset = (int)number.Value;
            return offset == number.Value && offset >= min && offset <= max;
        }

        private KernelProgram? BuildKernel(IReadOnlyList<LuaStmt> body)
        {
            if (body.Count < 2)
                return null;

            if (body[0] is not LocalStmt sample || sample.Values.Count != 1 ||
                !IsGetPixel(sample.Values[0]))
                return null;
            if (sample.Names.Count is < 1 or > 4)
                return null;

            _coordsEnabled = true;
            for (int i = 0; i < sample.Names.Count; i++)
            {
                EnsureNotCoordinateName(sample.Names[i]);
                _scope[sample.Names[i]] = AddBinding(new KInput((KChannel)i));
            }

            for (int i = 1; i < body.Count - 1; i++)
            {
                switch (body[i])
                {
                    case LocalStmt local:
                        BindLocal(local);
                        break;
                    case AssignStmt assign:
                        BindAssign(assign);
                        break;
                    default:
                        return null;
                }
            }

            if (body[^1] is not CallStmt { Call: CallExpr call } || !IsObjectMethod(call, "setpixel"))
                return null;
            if (call.Arguments.Count is < 5 or > 6)
                return null;
            if (!IsCoordinate(call.Arguments[0], KAxis.X) || !IsCoordinate(call.Arguments[1], KAxis.Y))
                return null;

            var outR = Lower(call.Arguments[2]);
            var outG = Lower(call.Arguments[3]);
            var outB = Lower(call.Arguments[4]);
            var outA = call.Arguments.Count == 6 ? Lower(call.Arguments[5]) : new KConst(255d);

            return new KernelProgram(_bindings, outR, outG, outB, outA, [.. _uniforms]);
        }

        private void BindLocal(LocalStmt local)
        {
            if (local.Values.Count == 1 && IsGetPixel(local.Values[0]))
                throw new KernelUnsupportedException("Unexpected pixel sample.");
            if (local.Names.Count != local.Values.Count)
                throw new KernelUnsupportedException("Multiple-value assignment is not supported.");

            var lowered = new KExpr[local.Values.Count];
            for (int i = 0; i < local.Values.Count; i++)
                lowered[i] = Lower(local.Values[i]);
            for (int i = 0; i < local.Names.Count; i++)
            {
                EnsureNotCoordinateName(local.Names[i]);
                _scope[local.Names[i]] = AddBinding(lowered[i]);
            }
        }

        private void BindAssign(AssignStmt assign)
        {
            if (assign.Targets.Count != assign.Values.Count)
                throw new KernelUnsupportedException("Unbalanced assignment is not supported.");

            foreach (var target in assign.Targets)
            {
                if (target is not NameExpr name || !_scope.ContainsKey(name.Name))
                    throw new KernelUnsupportedException("Assignment target must be a local variable.");
            }

            var lowered = new KExpr[assign.Values.Count];
            for (int i = 0; i < assign.Values.Count; i++)
                lowered[i] = Lower(assign.Values[i]);
            for (int i = 0; i < assign.Targets.Count; i++)
                _scope[((NameExpr)assign.Targets[i]).Name] = AddBinding(lowered[i]);
        }

        private void EnsureNotCoordinateName(string name)
        {
            if (string.Equals(name, _widthVariable, StringComparison.Ordinal) ||
                string.Equals(name, _heightVariable, StringComparison.Ordinal))
                throw new KernelUnsupportedException("Local variable shadows a loop coordinate.");
        }

        private int AddBinding(KExpr expression)
        {
            _bindings.Add(expression);
            return _bindings.Count - 1;
        }

        private KExpr Lower(LuaExpr expression)
        {
            switch (expression)
            {
                case NumberExpr number:
                    return new KConst(number.Value);
                case NameExpr name:
                    return LowerName(name.Name);
                case MemberExpr member:
                    return LowerMember(member);
                case CallExpr call:
                    return LowerCall(call);
                case MethodCallExpr method when TryPixelDataChannel(method, out int channel):
                    return new KInput((KChannel)channel);
                case UnaryExpr { Operator: "-" } unary:
                    return new KNegate(Lower(unary.Operand));
                case BinaryExpr binary:
                    return LowerBinary(binary);
            }
            throw new KernelUnsupportedException("Unsupported expression in pixel kernel.");
        }

        private KExpr LowerName(string name)
        {
            if (_scope.TryGetValue(name, out int slot))
                return new KLocalRef(slot);
            if (_coordsEnabled && string.Equals(name, _widthVariable, StringComparison.Ordinal))
                return new KCoord(KAxis.X);
            if (_coordsEnabled && string.Equals(name, _heightVariable, StringComparison.Ordinal))
                return new KCoord(KAxis.Y);
            if (KernelUniforms.TryResolveGlobal(name, out var uniform))
                return UseUniform(uniform);
            throw new KernelUnsupportedException($"Unknown identifier '{name}'.");
        }

        private KExpr LowerMember(MemberExpr member)
        {
            if (member.Target is NameExpr { Name: "math" })
            {
                return member.Name switch
                {
                    "pi" => new KConst(Math.PI),
                    "huge" => new KConst(double.PositiveInfinity),
                    _ => throw new KernelUnsupportedException($"Unsupported math field '{member.Name}'."),
                };
            }

            if (member.Target is NameExpr target && KernelUniforms.TryResolveMember(target.Name, member.Name, out var uniform))
                return UseUniform(uniform);

            throw new KernelUnsupportedException("Unsupported member access in pixel kernel.");
        }

        private KExpr LowerCall(CallExpr call)
        {
            if (call.Target is not MemberExpr { Target: NameExpr { Name: "math" }, Name: var function } ||
                !MathFunctions.TryGetValue(function, out var spec))
                throw new KernelUnsupportedException("Unsupported function call in pixel kernel.");

            int count = call.Arguments.Count;
            if (count < spec.MinArgs || count > spec.MaxArgs)
                throw new KernelUnsupportedException($"Wrong argument count for 'math.{function}'.");

            var arguments = new KExpr[count];
            for (int i = 0; i < count; i++)
                arguments[i] = Lower(call.Arguments[i]);

            if (spec.Func is KFunc.Min or KFunc.Max)
            {
                var folded = arguments[0];
                for (int i = 1; i < count; i++)
                    folded = new KCall(spec.Func, [folded, arguments[i]]);
                return folded;
            }

            return new KCall(spec.Func, arguments);
        }

        private KExpr LowerBinary(BinaryExpr binary)
        {
            switch (binary.Operator)
            {
                case "+": return new KArith(KArithOp.Add, Lower(binary.Left), Lower(binary.Right));
                case "-": return new KArith(KArithOp.Subtract, Lower(binary.Left), Lower(binary.Right));
                case "*": return new KArith(KArithOp.Multiply, Lower(binary.Left), Lower(binary.Right));
                case "/": return new KArith(KArithOp.Divide, Lower(binary.Left), Lower(binary.Right));
                case "%": return new KArith(KArithOp.Modulo, Lower(binary.Left), Lower(binary.Right));
                case "^": return new KArith(KArithOp.Power, Lower(binary.Left), Lower(binary.Right));
                case "or" when binary.Left is BinaryExpr { Operator: "and" } conditional:
                    return new KSelect(LowerBool(conditional.Left), Lower(conditional.Right), Lower(binary.Right));
            }
            throw new KernelUnsupportedException($"Unsupported operator '{binary.Operator}' in value position.");
        }

        private KBool LowerBool(LuaExpr expression)
        {
            switch (expression)
            {
                case BoolExpr boolean:
                    return new KBoolConst(boolean.Value);
                case UnaryExpr { Operator: "not" } unary:
                    return new KNot(LowerBool(unary.Operand));
                case BinaryExpr { Operator: "and" } conjunction:
                    return new KLogical(true, LowerBool(conjunction.Left), LowerBool(conjunction.Right));
                case BinaryExpr { Operator: "or" } disjunction:
                    return new KLogical(false, LowerBool(disjunction.Left), LowerBool(disjunction.Right));
                case BinaryExpr binary when TryCompareOp(binary.Operator, out var op):
                    return new KCompare(op, Lower(binary.Left), Lower(binary.Right));
                case NameExpr name when IsCheck(LowerName(name.Name)):
                    return new KCompare(KCompareOp.NotEqual, LowerName(name.Name), new KConst(0d));
                case MemberExpr member when IsCheck(LowerMember(member)):
                    return new KCompare(KCompareOp.NotEqual, LowerMember(member), new KConst(0d));
            }
            throw new KernelUnsupportedException("Unsupported condition in pixel kernel.");
        }

        private static bool IsCheck(KExpr expression) =>
            expression is KUniformRef { Uniform: KernelUniform.Check0 or KernelUniform.Check1 or KernelUniform.Check2 or KernelUniform.Check3 };

        private static bool TryCompareOp(string symbol, out KCompareOp op)
        {
            switch (symbol)
            {
                case "<": op = KCompareOp.Less; return true;
                case ">": op = KCompareOp.Greater; return true;
                case "<=": op = KCompareOp.LessEqual; return true;
                case ">=": op = KCompareOp.GreaterEqual; return true;
                case "==": op = KCompareOp.Equal; return true;
                case "~=": op = KCompareOp.NotEqual; return true;
                default: op = default; return false;
            }
        }

        private KExpr UseUniform(KernelUniform uniform)
        {
            _uniforms.Add(uniform);
            return new KUniformRef(uniform);
        }

        private bool TryAxis(NumericForStmt loop, out string variable, out KAxis axis)
        {
            variable = loop.Variable;
            axis = default;

            if (loop.Start is not NumberExpr { Value: 0d })
                return false;
            if (loop.Step is not null && loop.Step is not NumberExpr { Value: 1d })
                return false;
            if (loop.Stop is not BinaryExpr { Operator: "-", Right: NumberExpr { Value: 1d } } bound)
                return false;
            if (bound.Left is MemberExpr { Target: NameExpr { Name: "obj" }, Name: var field } &&
                KernelUniforms.TryResolveMember("obj", field, out var uniform))
            {
                if (uniform == KernelUniform.Width)
                {
                    axis = KAxis.X;
                    return true;
                }
                if (uniform == KernelUniform.Height)
                {
                    axis = KAxis.Y;
                    return true;
                }
                return false;
            }

            if (_pixelDataVariable is not null &&
                bound.Left is MemberExpr { Target: NameExpr { } handle, Name: var pixelField } &&
                string.Equals(handle.Name, _pixelDataVariable, StringComparison.Ordinal))
            {
                if (pixelField == "width")
                {
                    axis = KAxis.X;
                    return true;
                }
                if (pixelField == "height")
                {
                    axis = KAxis.Y;
                    return true;
                }
            }
            return false;
        }

        private bool IsGetPixel(LuaExpr expression) =>
            expression is CallExpr call && IsObjectMethod(call, "getpixel") &&
            call.Arguments.Count == 2 &&
            IsCoordinate(call.Arguments[0], KAxis.X) && IsCoordinate(call.Arguments[1], KAxis.Y);

        private bool IsCoordinate(LuaExpr expression, KAxis axis) =>
            expression is NameExpr name &&
            string.Equals(name.Name, axis == KAxis.X ? _widthVariable : _heightVariable, StringComparison.Ordinal);

        private static bool IsObjectMethod(CallExpr call, string method) =>
            call.Target is MemberExpr { Target: NameExpr { Name: "obj" }, Name: var name } &&
            string.Equals(name, method, StringComparison.Ordinal);
    }
}
