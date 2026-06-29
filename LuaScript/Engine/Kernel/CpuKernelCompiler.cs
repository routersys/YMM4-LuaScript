using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace LuaScript.Engine.Kernel
{
    internal static class CpuKernelCompiler
    {
        private static readonly MethodInfo Clamp = typeof(Math).GetMethod(nameof(Math.Clamp), [typeof(double), typeof(double), typeof(double)])!;
        private static readonly MethodInfo Floor = Unary(nameof(Math.Floor));
        private static readonly MethodInfo Pow = Binary(nameof(Math.Pow));

        private static readonly Dictionary<KFunc, MethodInfo> Functions = new()
        {
            [KFunc.Abs] = Unary(nameof(Math.Abs)),
            [KFunc.Floor] = Floor,
            [KFunc.Ceil] = Unary(nameof(Math.Ceiling)),
            [KFunc.Sqrt] = Unary(nameof(Math.Sqrt)),
            [KFunc.Sin] = Unary(nameof(Math.Sin)),
            [KFunc.Cos] = Unary(nameof(Math.Cos)),
            [KFunc.Tan] = Unary(nameof(Math.Tan)),
            [KFunc.Asin] = Unary(nameof(Math.Asin)),
            [KFunc.Acos] = Unary(nameof(Math.Acos)),
            [KFunc.Atan] = Unary(nameof(Math.Atan)),
            [KFunc.Atan2] = Binary(nameof(Math.Atan2)),
            [KFunc.Exp] = Unary(nameof(Math.Exp)),
            [KFunc.Log] = Unary(nameof(Math.Log)),
            [KFunc.Pow] = Pow,
            [KFunc.Min] = Binary(nameof(Math.Min)),
            [KFunc.Max] = Binary(nameof(Math.Max)),
        };

        public static CpuKernel Compile(KernelProgram program)
        {
            var buffer = Expression.Parameter(typeof(byte[]), "buffer");
            var width = Expression.Parameter(typeof(int), "width");
            var rowBase = Expression.Parameter(typeof(int), "rowBase");
            var y = Expression.Parameter(typeof(double), "y");
            var uniforms = Expression.Parameter(typeof(double[]), "uniforms");

            var x = Expression.Variable(typeof(int), "x");
            var index = Expression.Variable(typeof(int), "index");
            var alphaRaw = Expression.Variable(typeof(double), "alphaRaw");
            var transparent = Expression.Variable(typeof(bool), "transparent");
            var scale = Expression.Variable(typeof(double), "scale");
            var inputs = new[]
            {
                Expression.Variable(typeof(double), "inR"),
                Expression.Variable(typeof(double), "inG"),
                Expression.Variable(typeof(double), "inB"),
                Expression.Variable(typeof(double), "inA"),
            };
            var slots = new ParameterExpression[program.Bindings.Count];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Expression.Variable(typeof(double), "slot" + i);

            var context = new Context(x, y, uniforms, inputs, slots);

            var statements = new List<Expression>
            {
                Expression.Assign(index, Expression.Add(rowBase, Expression.Multiply(x, Constant(4)))),
                Expression.Assign(alphaRaw, ToDouble(ReadByte(buffer, index, 3))),
                Expression.Assign(transparent, Expression.LessThanOrEqual(alphaRaw, Constant(0d))),
                Expression.Assign(scale, Select(transparent, Constant(0d), Expression.Divide(Constant(255d), alphaRaw))),
                Expression.Assign(inputs[3], Select(transparent, Constant(0d), alphaRaw)),
                Expression.Assign(inputs[0], Select(transparent, Constant(0d), ClampByte(Expression.Multiply(ToDouble(ReadByte(buffer, index, 2)), scale)))),
                Expression.Assign(inputs[1], Select(transparent, Constant(0d), ClampByte(Expression.Multiply(ToDouble(ReadByte(buffer, index, 1)), scale)))),
                Expression.Assign(inputs[2], Select(transparent, Constant(0d), ClampByte(Expression.Multiply(ToDouble(ReadByte(buffer, index, 0)), scale)))),
            };

            for (int i = 0; i < slots.Length; i++)
                statements.Add(Expression.Assign(slots[i], Build(program.Bindings[i], context)));

            var outA = Expression.Variable(typeof(double), "outA");
            var alphaScale = Expression.Variable(typeof(double), "alphaScale");
            statements.Add(Expression.Assign(outA, Build(program.OutputA, context)));
            statements.Add(Expression.Assign(alphaScale, Expression.Divide(ClampByte(outA), Constant(255d))));
            statements.Add(WriteByte(buffer, index, 0, ClampByte(Expression.Multiply(Build(program.OutputB, context), alphaScale))));
            statements.Add(WriteByte(buffer, index, 1, ClampByte(Expression.Multiply(Build(program.OutputG, context), alphaScale))));
            statements.Add(WriteByte(buffer, index, 2, ClampByte(Expression.Multiply(Build(program.OutputR, context), alphaScale))));
            statements.Add(WriteByte(buffer, index, 3, ClampByte(outA)));
            statements.Add(Expression.PostIncrementAssign(x));

            var breakLabel = Expression.Label("break");
            var loop = Expression.Loop(
                Expression.IfThenElse(
                    Expression.LessThan(x, width),
                    Expression.Block(statements),
                    Expression.Break(breakLabel)),
                breakLabel);

            var locals = new List<ParameterExpression> { x, index, alphaRaw, transparent, scale, outA, alphaScale };
            locals.AddRange(inputs);
            locals.AddRange(slots);

            var body = Expression.Block(locals, Expression.Assign(x, Constant(0)), loop);

            var lambda = Expression.Lambda<CpuKernel.RowDelegate>(body, buffer, width, rowBase, y, uniforms);
            return new CpuKernel(lambda.Compile());
        }

        private sealed record Context(
            ParameterExpression X,
            ParameterExpression Y,
            ParameterExpression Uniforms,
            ParameterExpression[] Inputs,
            ParameterExpression[] Slots);

        private static Expression Build(KExpr expression, Context context)
        {
            switch (expression)
            {
                case KConst value:
                    return Constant(value.Value);
                case KInput input:
                    return context.Inputs[(int)input.Channel];
                case KCoord coord:
                    return coord.Axis == KAxis.X ? ToDouble(context.X) : context.Y;
                case KUniformRef uniform:
                    return Expression.ArrayIndex(context.Uniforms, Constant((int)uniform.Uniform));
                case KLocalRef local:
                    return context.Slots[local.Slot];
                case KNegate negate:
                    return Expression.Negate(Build(negate.Operand, context));
                case KArith arithmetic:
                    return BuildArithmetic(arithmetic, context);
                case KCall call:
                    return BuildCall(call, context);
                case KSelect select:
                    return Select(BuildBool(select.Condition, context), Build(select.WhenTrue, context), Build(select.WhenFalse, context));
            }
            throw new KernelUnsupportedException("Unsupported CPU expression.");
        }

        private static Expression BuildArithmetic(KArith arithmetic, Context context)
        {
            var left = Build(arithmetic.Left, context);
            var right = Build(arithmetic.Right, context);
            return arithmetic.Op switch
            {
                KArithOp.Add => Expression.Add(left, right),
                KArithOp.Subtract => Expression.Subtract(left, right),
                KArithOp.Multiply => Expression.Multiply(left, right),
                KArithOp.Divide => Expression.Divide(left, right),
                KArithOp.Modulo => FlooredModulo(left, right),
                KArithOp.Power => Expression.Call(Pow, left, right),
                _ => throw new KernelUnsupportedException("Unsupported arithmetic operator."),
            };
        }

        private static Expression BuildCall(KCall call, Context context)
        {
            var method = Functions[call.Func];
            if (call.Arguments.Count == 1)
                return Expression.Call(method, Build(call.Arguments[0], context));
            return Expression.Call(method, Build(call.Arguments[0], context), Build(call.Arguments[1], context));
        }

        private static Expression BuildBool(KBool condition, Context context)
        {
            switch (condition)
            {
                case KBoolConst value:
                    return Expression.Constant(value.Value);
                case KNot not:
                    return Expression.Not(BuildBool(not.Operand, context));
                case KLogical logical:
                    return logical.IsAnd
                        ? Expression.AndAlso(BuildBool(logical.Left, context), BuildBool(logical.Right, context))
                        : Expression.OrElse(BuildBool(logical.Left, context), BuildBool(logical.Right, context));
                case KCompare compare:
                    var left = Build(compare.Left, context);
                    var right = Build(compare.Right, context);
                    return compare.Op switch
                    {
                        KCompareOp.Less => Expression.LessThan(left, right),
                        KCompareOp.Greater => Expression.GreaterThan(left, right),
                        KCompareOp.LessEqual => Expression.LessThanOrEqual(left, right),
                        KCompareOp.GreaterEqual => Expression.GreaterThanOrEqual(left, right),
                        KCompareOp.Equal => Expression.Equal(left, right),
                        KCompareOp.NotEqual => Expression.NotEqual(left, right),
                        _ => throw new KernelUnsupportedException("Unsupported comparison."),
                    };
            }
            throw new KernelUnsupportedException("Unsupported CPU condition.");
        }

        private static Expression FlooredModulo(Expression left, Expression right) =>
            Expression.Subtract(left, Expression.Multiply(Expression.Call(Floor, Expression.Divide(left, right)), right));

        private static Expression Select(Expression condition, Expression whenTrue, Expression whenFalse) =>
            Expression.Condition(condition, whenTrue, whenFalse);

        private static Expression ClampByte(Expression value) =>
            Expression.Call(Clamp, value, Constant(0d), Constant(255d));

        private static Expression ReadByte(ParameterExpression buffer, ParameterExpression index, int offset) =>
            Expression.ArrayIndex(buffer, offset == 0 ? index : Expression.Add(index, Constant(offset)));

        private static Expression WriteByte(ParameterExpression buffer, ParameterExpression index, int offset, Expression value) =>
            Expression.Assign(
                Expression.ArrayAccess(buffer, offset == 0 ? index : Expression.Add(index, Constant(offset))),
                Expression.Convert(value, typeof(byte)));

        private static Expression ToDouble(Expression value) => Expression.Convert(value, typeof(double));

        private static ConstantExpression Constant(double value) => Expression.Constant(value, typeof(double));

        private static ConstantExpression Constant(int value) => Expression.Constant(value, typeof(int));

        private static MethodInfo Unary(string name) => typeof(Math).GetMethod(name, [typeof(double)])!;

        private static MethodInfo Binary(string name) => typeof(Math).GetMethod(name, [typeof(double), typeof(double)])!;
    }
}
