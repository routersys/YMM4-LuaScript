using LuaScript.Compat;
using LuaScript.Diagnostics;
using MoonSharp.Interpreter;

namespace LuaScript
{
    internal static class AviUtlGlobalRegistrar
    {
        internal static void RegisterFunctions(Table globals, Func<double> timeRatio)
        {
            globals["debug_print"] = DynValue.NewCallback((_, args) =>
            {
                if (args.Count > 0)
                {
                    var value = args[0];
                    DebugOutput.Write(value.Type == DataType.String ? value.String : value.ToPrintString());
                }
                return DynValue.Void;
            });

            globals["RGB"] = DynValue.NewCallback((_, args) =>
            {
                int count = args.Count;
                if (count >= 6)
                    return DynValue.NewNumber(AviUtlGlobalFunctions.RgbInterpolate(
                        Number(args, 0), Number(args, 1), Number(args, 2),
                        Number(args, 3), Number(args, 4), Number(args, 5),
                        timeRatio()));
                if (count >= 3)
                    return DynValue.NewNumber(AviUtlGlobalFunctions.RgbCompose(
                        Number(args, 0), Number(args, 1), Number(args, 2)));
                if (count >= 1)
                {
                    AviUtlGlobalFunctions.RgbComponents(Number(args, 0), out int r, out int g, out int b);
                    return DynValue.NewTuple(
                        DynValue.NewNumber(r),
                        DynValue.NewNumber(g),
                        DynValue.NewNumber(b));
                }
                return DynValue.Nil;
            });

            globals["HSV"] = DynValue.NewCallback((_, args) =>
            {
                int count = args.Count;
                if (count >= 6)
                    return DynValue.NewNumber(AviUtlGlobalFunctions.HsvInterpolate(
                        Number(args, 0), Number(args, 1), Number(args, 2),
                        Number(args, 3), Number(args, 4), Number(args, 5),
                        timeRatio()));
                if (count >= 3)
                    return DynValue.NewNumber(AviUtlGlobalFunctions.HsvCompose(
                        Number(args, 0), Number(args, 1), Number(args, 2)));
                if (count >= 1)
                {
                    AviUtlGlobalFunctions.HsvComponents(Number(args, 0), out int h, out int s, out int v);
                    return DynValue.NewTuple(
                        DynValue.NewNumber(h),
                        DynValue.NewNumber(s),
                        DynValue.NewNumber(v));
                }
                return DynValue.Nil;
            });

            globals["OR"] = DynValue.NewCallback((_, args) =>
                DynValue.NewNumber(AviUtlGlobalFunctions.BitOr(Number(args, 0), Number(args, 1))));

            globals["AND"] = DynValue.NewCallback((_, args) =>
                DynValue.NewNumber(AviUtlGlobalFunctions.BitAnd(Number(args, 0), Number(args, 1))));

            globals["XOR"] = DynValue.NewCallback((_, args) =>
                DynValue.NewNumber(AviUtlGlobalFunctions.BitXor(Number(args, 0), Number(args, 1))));

            globals["SHIFT"] = DynValue.NewCallback((_, args) =>
                DynValue.NewNumber(AviUtlGlobalFunctions.BitShift(Number(args, 0), Number(args, 1))));
        }

        private static double Number(CallbackArguments args, int index) =>
            args.Count > index ? args[index].CastToNumber() ?? 0d : 0d;
    }
}
