using System.Collections.Generic;

namespace LuaScript.Engine.Kernel
{
    internal enum KChannel
    {
        R,
        G,
        B,
        A,
    }

    internal enum KAxis
    {
        X,
        Y,
    }

    internal enum KArithOp
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulo,
        Power,
    }

    internal enum KCompareOp
    {
        Less,
        Greater,
        LessEqual,
        GreaterEqual,
        Equal,
        NotEqual,
    }

    internal enum KFunc
    {
        Abs,
        Floor,
        Ceil,
        Sqrt,
        Sin,
        Cos,
        Tan,
        Asin,
        Acos,
        Atan,
        Atan2,
        Exp,
        Log,
        Pow,
        Min,
        Max,
        Fmod,
    }

    internal abstract record KExpr;

    internal sealed record KConst(double Value) : KExpr;

    internal sealed record KInput(KChannel Channel) : KExpr;

    internal sealed record KCoord(KAxis Axis) : KExpr;

    internal sealed record KUniformRef(KernelUniform Uniform) : KExpr;

    internal sealed record KLocalRef(int Slot) : KExpr;

    internal sealed record KArith(KArithOp Op, KExpr Left, KExpr Right) : KExpr;

    internal sealed record KNegate(KExpr Operand) : KExpr;

    internal sealed record KCall(KFunc Func, IReadOnlyList<KExpr> Arguments) : KExpr;

    internal sealed record KSelect(KBool Condition, KExpr WhenTrue, KExpr WhenFalse) : KExpr;

    internal abstract record KBool;

    internal sealed record KCompare(KCompareOp Op, KExpr Left, KExpr Right) : KBool;

    internal sealed record KLogical(bool IsAnd, KBool Left, KBool Right) : KBool;

    internal sealed record KNot(KBool Operand) : KBool;

    internal sealed record KBoolConst(bool Value) : KBool;

    internal sealed record KernelProgram(
        IReadOnlyList<KExpr> Bindings,
        KExpr OutputR,
        KExpr OutputG,
        KExpr OutputB,
        KExpr OutputA,
        IReadOnlyList<KernelUniform> Uniforms);
}
