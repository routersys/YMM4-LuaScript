using System.Globalization;
using System.Text;

namespace LuaScript.Engine.Kernel
{
    internal static class HlslKernelEmitter
    {
        public const int ConstantVectorCount = 16;

        public const int ConstantFloatCount = ConstantVectorCount * 4;

        private static readonly string[] Intrinsics =
        [
            "abs", "floor", "ceil", "sqrt", "sin", "cos", "tan", "asin", "acos",
            "atan", "atan2", "exp", "log", "pow", "min", "max", "fmod",
        ];

        public static string Emit(KernelProgram program)
        {
            var builder = new StringBuilder(1024);

            builder.Append("Texture2D InputTexture : register(t0);\n");
            builder.Append("SamplerState InputSampler : register(s0);\n");
            builder.Append("cbuffer constants : register(b0)\n{\n");
            builder.Append("    float4 uniforms[").Append(ConstantVectorCount).Append("];\n");
            builder.Append("};\n\n");
            builder.Append("float4 main(float4 pos : SV_POSITION, float4 posScene : SCENE_POSITION, float4 uv : TEXCOORD0) : SV_Target\n{\n");
            builder.Append("    float4 src = InputTexture.Sample(InputSampler, uv.xy);\n");
            builder.Append("    float srcA = src.a;\n");
            builder.Append("    float invA = srcA <= 0.0 ? 0.0 : 1.0 / srcA;\n");
            builder.Append("    float inR = clamp(src.r * invA * 255.0, 0.0, 255.0);\n");
            builder.Append("    float inG = clamp(src.g * invA * 255.0, 0.0, 255.0);\n");
            builder.Append("    float inB = clamp(src.b * invA * 255.0, 0.0, 255.0);\n");
            builder.Append("    float inA = srcA * 255.0;\n");
            builder.Append("    float px = uv.x * ").Append(Uniform(KernelUniform.Width)).Append(" - 0.5;\n");
            builder.Append("    float py = uv.y * ").Append(Uniform(KernelUniform.Height)).Append(" - 0.5;\n");

            for (int i = 0; i < program.Bindings.Count; i++)
            {
                builder.Append("    float slot").Append(i).Append(" = ");
                EmitExpression(builder, program.Bindings[i]);
                builder.Append(";\n");
            }

            builder.Append("    float outA = clamp(");
            EmitExpression(builder, program.OutputA);
            builder.Append(", 0.0, 255.0);\n");
            builder.Append("    float alphaK = outA / 255.0;\n");

            builder.Append("    float oR = floor(clamp((");
            EmitExpression(builder, program.OutputR);
            builder.Append(") * alphaK, 0.0, 255.0)) / 255.0;\n");
            builder.Append("    float oG = floor(clamp((");
            EmitExpression(builder, program.OutputG);
            builder.Append(") * alphaK, 0.0, 255.0)) / 255.0;\n");
            builder.Append("    float oB = floor(clamp((");
            EmitExpression(builder, program.OutputB);
            builder.Append(") * alphaK, 0.0, 255.0)) / 255.0;\n");
            builder.Append("    float oA = floor(outA) / 255.0;\n");
            builder.Append("    return float4(oR, oG, oB, oA);\n}\n");

            return builder.ToString();
        }

        private static void EmitExpression(StringBuilder builder, KExpr expression)
        {
            switch (expression)
            {
                case KConst value:
                    builder.Append(Number(value.Value));
                    return;
                case KInput input:
                    builder.Append(input.Channel switch
                    {
                        KChannel.R => "inR",
                        KChannel.G => "inG",
                        KChannel.B => "inB",
                        _ => "inA",
                    });
                    return;
                case KCoord coord:
                    builder.Append(coord.Axis == KAxis.X ? "px" : "py");
                    return;
                case KUniformRef uniform:
                    builder.Append(Uniform(uniform.Uniform));
                    return;
                case KLocalRef local:
                    builder.Append("slot").Append(local.Slot);
                    return;
                case KNegate negate:
                    builder.Append("(-(");
                    EmitExpression(builder, negate.Operand);
                    builder.Append("))");
                    return;
                case KArith arithmetic:
                    EmitArithmetic(builder, arithmetic);
                    return;
                case KCall call:
                    EmitCall(builder, call);
                    return;
                case KSelect select:
                    builder.Append('(');
                    EmitCondition(builder, select.Condition);
                    builder.Append(" ? (");
                    EmitExpression(builder, select.WhenTrue);
                    builder.Append(") : (");
                    EmitExpression(builder, select.WhenFalse);
                    builder.Append("))");
                    return;
            }
            throw new KernelUnsupportedException("Unsupported HLSL expression.");
        }

        private static void EmitArithmetic(StringBuilder builder, KArith arithmetic)
        {
            if (arithmetic.Op == KArithOp.Power)
            {
                builder.Append("pow(");
                EmitExpression(builder, arithmetic.Left);
                builder.Append(", ");
                EmitExpression(builder, arithmetic.Right);
                builder.Append(')');
                return;
            }

            if (arithmetic.Op == KArithOp.Modulo)
            {
                builder.Append('(');
                EmitExpression(builder, arithmetic.Left);
                builder.Append(" - floor((");
                EmitExpression(builder, arithmetic.Left);
                builder.Append(") / (");
                EmitExpression(builder, arithmetic.Right);
                builder.Append(")) * (");
                EmitExpression(builder, arithmetic.Right);
                builder.Append("))");
                return;
            }

            builder.Append('(');
            EmitExpression(builder, arithmetic.Left);
            builder.Append(arithmetic.Op switch
            {
                KArithOp.Add => " + ",
                KArithOp.Subtract => " - ",
                KArithOp.Multiply => " * ",
                KArithOp.Divide => " / ",
                _ => throw new KernelUnsupportedException("Unsupported arithmetic operator."),
            });
            EmitExpression(builder, arithmetic.Right);
            builder.Append(')');
        }

        private static void EmitCall(StringBuilder builder, KCall call)
        {
            builder.Append(Intrinsics[(int)call.Func]).Append('(');
            for (int i = 0; i < call.Arguments.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                EmitExpression(builder, call.Arguments[i]);
            }
            builder.Append(')');
        }

        private static void EmitCondition(StringBuilder builder, KBool condition)
        {
            switch (condition)
            {
                case KBoolConst value:
                    builder.Append(value.Value ? "true" : "false");
                    return;
                case KNot not:
                    builder.Append("(!(");
                    EmitCondition(builder, not.Operand);
                    builder.Append("))");
                    return;
                case KLogical logical:
                    builder.Append('(');
                    EmitCondition(builder, logical.Left);
                    builder.Append(logical.IsAnd ? " && " : " || ");
                    EmitCondition(builder, logical.Right);
                    builder.Append(')');
                    return;
                case KCompare compare:
                    builder.Append('(');
                    EmitExpression(builder, compare.Left);
                    builder.Append(compare.Op switch
                    {
                        KCompareOp.Less => " < ",
                        KCompareOp.Greater => " > ",
                        KCompareOp.LessEqual => " <= ",
                        KCompareOp.GreaterEqual => " >= ",
                        KCompareOp.Equal => " == ",
                        _ => " != ",
                    });
                    EmitExpression(builder, compare.Right);
                    builder.Append(')');
                    return;
            }
            throw new KernelUnsupportedException("Unsupported HLSL condition.");
        }

        private static string Uniform(KernelUniform uniform)
        {
            int index = (int)uniform;
            return $"uniforms[{index / 4}].{"xyzw"[index % 4]}";
        }

        private static string Number(double value)
        {
            if (double.IsPositiveInfinity(value))
                return "3.4028235e38";
            if (double.IsNegativeInfinity(value))
                return "-3.4028235e38";
            if (double.IsNaN(value))
                return "(0.0 / 0.0)";

            string text = value.ToString("R", CultureInfo.InvariantCulture);
            if (!text.Contains('.') && !text.Contains('e') && !text.Contains('E'))
                text += ".0";
            return text;
        }
    }
}
