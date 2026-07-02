using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Intrinsics;

namespace LuaScript.Engine.Kernel
{
    internal static class SimdKernelCompiler
    {
        private static readonly Type Vec = typeof(Vector256<double>);

        private static readonly MethodInfo Broadcast = typeof(Vector256).GetMethod(nameof(Vector256.Create), [typeof(double)])!;
        private static readonly MethodInfo Create4 = typeof(Vector256).GetMethod(nameof(Vector256.Create), [typeof(double), typeof(double), typeof(double), typeof(double)])!;
        private static readonly MethodInfo GetElement = VecOp(nameof(Vector256.GetElement), 1, 1);
        private static readonly MethodInfo Add = VecOp(nameof(Vector256.Add), 2);
        private static readonly MethodInfo Subtract = VecOp(nameof(Vector256.Subtract), 2);
        private static readonly MethodInfo Multiply = VecOp(nameof(Vector256.Multiply), 2);
        private static readonly MethodInfo Divide = VecOp(nameof(Vector256.Divide), 2);
        private static readonly MethodInfo Negate = VecOp(nameof(Vector256.Negate), 1);
        private static readonly MethodInfo Abs = VecOp(nameof(Vector256.Abs), 1);
        private static readonly MethodInfo Sqrt = VecOp(nameof(Vector256.Sqrt), 1);
        private static readonly MethodInfo FloorFn = typeof(Vector256).GetMethod(nameof(Vector256.Floor), [Vec])!;
        private static readonly MethodInfo CeilingFn = typeof(Vector256).GetMethod(nameof(Vector256.Ceiling), [Vec])!;
        private static readonly MethodInfo ConditionalSelect = VecOp(nameof(Vector256.ConditionalSelect), 3);
        private static readonly MethodInfo LessThan = VecOp(nameof(Vector256.LessThan), 2);
        private static readonly MethodInfo GreaterThan = VecOp(nameof(Vector256.GreaterThan), 2);
        private static readonly MethodInfo LessThanOrEqual = VecOp(nameof(Vector256.LessThanOrEqual), 2);
        private static readonly MethodInfo GreaterThanOrEqual = VecOp(nameof(Vector256.GreaterThanOrEqual), 2);
        private static readonly MethodInfo EqualsFn = VecOp(nameof(Vector256.Equals), 2);
        private static readonly MethodInfo BitwiseAnd = VecOp(nameof(Vector256.BitwiseAnd), 2);
        private static readonly MethodInfo BitwiseOr = VecOp(nameof(Vector256.BitwiseOr), 2);
        private static readonly MethodInfo OnesComplement = VecOp(nameof(Vector256.OnesComplement), 1);

        private static readonly Expression Zero = Expression.Constant(Vector256<double>.Zero, Vec);
        private static readonly Expression AllBits = Expression.Constant(Vector256<double>.AllBitsSet, Vec);
        private static readonly Expression V255 = Expression.Constant(Vector256.Create(255d), Vec);

        public static CpuKernel.RowDelegate? TryCompile(KernelProgram program)
        {
            try
            {
                return Compile(program);
            }
            catch (KernelUnsupportedException)
            {
                return null;
            }
        }

        private static CpuKernel.RowDelegate Compile(KernelProgram program)
        {
            var buffer = Expression.Parameter(typeof(byte[]), "buffer");
            var rowBase = Expression.Parameter(typeof(int), "rowBase");
            var xStart = Expression.Parameter(typeof(int), "xStart");
            var xEnd = Expression.Parameter(typeof(int), "xEnd");
            var yScalar = Expression.Parameter(typeof(double), "y");
            var uniforms = Expression.Parameter(typeof(double[]), "uniforms");

            var x = Expression.Variable(typeof(int), "x");
            var index = Expression.Variable(typeof(int), "index");
            var yVec = Expression.Variable(Vec, "yVec");
            var xVec = Expression.Variable(Vec, "xVec");
            var alphaRaw = Expression.Variable(Vec, "alphaRaw");
            var transparent = Expression.Variable(Vec, "transparent");
            var scale = Expression.Variable(Vec, "scale");
            var inputs = new[]
            {
                Expression.Variable(Vec, "inR"),
                Expression.Variable(Vec, "inG"),
                Expression.Variable(Vec, "inB"),
                Expression.Variable(Vec, "inA"),
            };
            var slots = new ParameterExpression[program.Bindings.Count];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Expression.Variable(Vec, "slot" + i);

            var context = new Context(xVec, yVec, uniforms, inputs, slots);

            var statements = new List<Expression>
            {
                Expression.Assign(index, Expression.Add(rowBase, Expression.Multiply(x, Constant(4)))),
                Expression.Assign(xVec, Expression.Call(Create4,
                    ToDouble(x),
                    ToDouble(Expression.Add(x, Constant(1))),
                    ToDouble(Expression.Add(x, Constant(2))),
                    ToDouble(Expression.Add(x, Constant(3))))),
                Expression.Assign(alphaRaw, LoadChannel(buffer, index, 3)),
                Expression.Assign(transparent, Call(LessThanOrEqual, alphaRaw, Zero)),
                Expression.Assign(scale, Select(transparent, Zero, Call(Divide, V255, alphaRaw))),
                Expression.Assign(inputs[3], Select(transparent, Zero, alphaRaw)),
                Expression.Assign(inputs[0], Select(transparent, Zero, ClampByte(Call(Multiply, LoadChannel(buffer, index, 2), scale)))),
                Expression.Assign(inputs[1], Select(transparent, Zero, ClampByte(Call(Multiply, LoadChannel(buffer, index, 1), scale)))),
                Expression.Assign(inputs[2], Select(transparent, Zero, ClampByte(Call(Multiply, LoadChannel(buffer, index, 0), scale)))),
            };

            for (int i = 0; i < slots.Length; i++)
                statements.Add(Expression.Assign(slots[i], Build(program.Bindings[i], context)));

            var outA = Expression.Variable(Vec, "outA");
            var alphaScale = Expression.Variable(Vec, "alphaScale");
            statements.Add(Expression.Assign(outA, Build(program.OutputA, context)));
            statements.Add(Expression.Assign(alphaScale, Call(Divide, ClampByte(outA), V255)));
            statements.Add(StoreChannel(buffer, index, 0, ClampByte(Call(Multiply, Build(program.OutputB, context), alphaScale))));
            statements.Add(StoreChannel(buffer, index, 1, ClampByte(Call(Multiply, Build(program.OutputG, context), alphaScale))));
            statements.Add(StoreChannel(buffer, index, 2, ClampByte(Call(Multiply, Build(program.OutputR, context), alphaScale))));
            statements.Add(StoreChannel(buffer, index, 3, ClampByte(outA)));
            statements.Add(Expression.AddAssign(x, Constant(4)));

            var breakLabel = Expression.Label("break");
            var loop = Expression.Loop(
                Expression.IfThenElse(
                    Expression.LessThan(x, xEnd),
                    Expression.Block(statements),
                    Expression.Break(breakLabel)),
                breakLabel);

            var locals = new List<ParameterExpression> { x, index, yVec, xVec, alphaRaw, transparent, scale, outA, alphaScale };
            locals.AddRange(inputs);
            locals.AddRange(slots);

            var body = Expression.Block(
                locals,
                Expression.Assign(yVec, Expression.Call(Broadcast, yScalar)),
                Expression.Assign(x, xStart),
                loop);

            var lambda = Expression.Lambda<CpuKernel.RowDelegate>(body, buffer, rowBase, xStart, xEnd, yScalar, uniforms);
            return lambda.Compile();
        }

        private sealed record Context(
            Expression X,
            Expression Y,
            ParameterExpression Uniforms,
            Expression[] Inputs,
            ParameterExpression[] Slots);

        private static Expression Build(KExpr expression, Context context)
        {
            switch (expression)
            {
                case KConst value:
                    return Expression.Constant(Vector256.Create(value.Value), Vec);
                case KInput input:
                    return context.Inputs[(int)input.Channel];
                case KCoord coord:
                    return coord.Axis == KAxis.X ? context.X : context.Y;
                case KUniformRef uniform:
                    return Expression.Call(Broadcast, Expression.ArrayIndex(context.Uniforms, Constant((int)uniform.Uniform)));
                case KLocalRef local:
                    return context.Slots[local.Slot];
                case KNegate negate:
                    return Call(Negate, Build(negate.Operand, context));
                case KArith arithmetic:
                    return BuildArithmetic(arithmetic, context);
                case KCall call:
                    return BuildCall(call, context);
                case KSelect select:
                    return Select(BuildBool(select.Condition, context), Build(select.WhenTrue, context), Build(select.WhenFalse, context));
            }
            throw new KernelUnsupportedException("Unsupported SIMD expression.");
        }

        private static Expression BuildArithmetic(KArith arithmetic, Context context)
        {
            var left = Build(arithmetic.Left, context);
            var right = Build(arithmetic.Right, context);
            return arithmetic.Op switch
            {
                KArithOp.Add => Call(Add, left, right),
                KArithOp.Subtract => Call(Subtract, left, right),
                KArithOp.Multiply => Call(Multiply, left, right),
                KArithOp.Divide => Call(Divide, left, right),
                KArithOp.Modulo => Call(Subtract, left, Call(Multiply, Call(FloorFn, Call(Divide, left, right)), right)),
                _ => throw new KernelUnsupportedException("Unsupported SIMD arithmetic operator."),
            };
        }

        private static Expression BuildCall(KCall call, Context context)
        {
            var argument = Build(call.Arguments[0], context);
            return call.Func switch
            {
                KFunc.Abs => Call(Abs, argument),
                KFunc.Floor => Call(FloorFn, argument),
                KFunc.Ceil => Call(CeilingFn, argument),
                KFunc.Sqrt => Call(Sqrt, argument),
                _ => throw new KernelUnsupportedException("Unsupported SIMD function."),
            };
        }

        private static Expression BuildBool(KBool condition, Context context)
        {
            switch (condition)
            {
                case KBoolConst value:
                    return value.Value ? AllBits : Zero;
                case KNot not:
                    return Call(OnesComplement, BuildBool(not.Operand, context));
                case KLogical logical:
                    return logical.IsAnd
                        ? Call(BitwiseAnd, BuildBool(logical.Left, context), BuildBool(logical.Right, context))
                        : Call(BitwiseOr, BuildBool(logical.Left, context), BuildBool(logical.Right, context));
                case KCompare compare:
                    var left = Build(compare.Left, context);
                    var right = Build(compare.Right, context);
                    return compare.Op switch
                    {
                        KCompareOp.Less => Call(LessThan, left, right),
                        KCompareOp.Greater => Call(GreaterThan, left, right),
                        KCompareOp.LessEqual => Call(LessThanOrEqual, left, right),
                        KCompareOp.GreaterEqual => Call(GreaterThanOrEqual, left, right),
                        KCompareOp.Equal => Call(EqualsFn, left, right),
                        KCompareOp.NotEqual => Call(OnesComplement, Call(EqualsFn, left, right)),
                        _ => throw new KernelUnsupportedException("Unsupported SIMD comparison."),
                    };
            }
            throw new KernelUnsupportedException("Unsupported SIMD condition.");
        }

        private static Expression ClampByte(Expression value)
        {
            var low = Select(Call(LessThan, value, Zero), Zero, value);
            return Select(Call(GreaterThan, low, V255), V255, low);
        }

        private static Expression Select(Expression condition, Expression whenTrue, Expression whenFalse) =>
            Expression.Call(ConditionalSelect, condition, whenTrue, whenFalse);

        private static Expression LoadChannel(ParameterExpression buffer, ParameterExpression index, int offset) =>
            Expression.Call(Create4,
                ToDouble(ReadByte(buffer, index, offset)),
                ToDouble(ReadByte(buffer, index, offset + 4)),
                ToDouble(ReadByte(buffer, index, offset + 8)),
                ToDouble(ReadByte(buffer, index, offset + 12)));

        private static Expression StoreChannel(ParameterExpression buffer, ParameterExpression index, int offset, Expression value)
        {
            var writes = new Expression[4];
            for (int lane = 0; lane < 4; lane++)
                writes[lane] = Expression.Assign(
                    Expression.ArrayAccess(buffer, Expression.Add(index, Constant(lane * 4 + offset))),
                    Expression.Convert(Expression.Call(GetElement, value, Constant(lane)), typeof(byte)));
            return Expression.Block(writes);
        }

        private static Expression ReadByte(ParameterExpression buffer, ParameterExpression index, int offset) =>
            Expression.ArrayIndex(buffer, offset == 0 ? index : Expression.Add(index, Constant(offset)));

        private static Expression Call(MethodInfo method, params Expression[] arguments) =>
            Expression.Call(method, arguments);

        private static Expression ToDouble(Expression value) => Expression.Convert(value, typeof(double));

        private static ConstantExpression Constant(int value) => Expression.Constant(value, typeof(int));

        private static MethodInfo VecOp(string name, int vectorArgs, int intArgs = 0) =>
            typeof(Vector256).GetMethods()
                .Single(m => m.Name == name && m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == vectorArgs + intArgs &&
                    m.GetParameters().Take(vectorArgs).All(p => IsVector(p.ParameterType)) &&
                    m.GetParameters().Skip(vectorArgs).All(p => p.ParameterType == typeof(int)))
                .MakeGenericMethod(typeof(double));

        private static bool IsVector(Type type) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Vector256<>);
    }
}
