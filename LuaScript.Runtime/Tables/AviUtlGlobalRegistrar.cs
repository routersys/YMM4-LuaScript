using LuaScript.Api;
using LuaScript.Compat;
using LuaScript.Diagnostics;
using MoonSharp.Interpreter;

namespace LuaScript
{
    [LuaTable("")]
    internal sealed partial class AviUtlGlobalRegistrar(Func<double> timeRatio)
    {
        internal static void RegisterFunctions(Table globals, Func<double> timeRatio) =>
            new AviUtlGlobalRegistrar(timeRatio).RegisterLuaMembers(globals);

        [LuaVariable("time")]
        private double Time(AviUtlScriptContext ctx) => ctx.Time;

        [LuaVariable("frame")]
        private int Frame(AviUtlScriptContext ctx) => ctx.Frame;

        [LuaVariable("totalframe")]
        private int TotalFrame(AviUtlScriptContext ctx) => ctx.TotalFrame;

        [LuaVariable("framerate")]
        private int Framerate(AviUtlScriptContext ctx) => ctx.Framerate;

        [LuaVariable("timelineframe")]
        private int TimelineFrame(AviUtlScriptContext ctx) => ctx.TimelineFrame;

        [LuaVariable("timelinetime")]
        private double TimelineTime(AviUtlScriptContext ctx) => ctx.TimelineTime;

        [LuaVariable("layer")]
        private int Layer(AviUtlScriptContext ctx) => ctx.Layer;

        [LuaVariable("color")]
        private double? Color(AviUtlScriptContext ctx) => ctx.HasColor ? ctx.ColorValue : (double?)null;

        [LuaFunction("debug_print", "value")]
        private DynValue DebugPrint(CallbackArguments args)
        {
            if (args.Count > 0)
            {
                var value = args[0];
                DebugOutput.Write(value.Type == DataType.String ? value.String : value.ToPrintString());
            }
            return DynValue.Void;
        }

        [LuaFunction("RGB")]
        private DynValue Rgb(CallbackArguments args)
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
        }

        [LuaFunction("HSV")]
        private DynValue Hsv(CallbackArguments args)
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
        }

        [LuaFunction("OR", "a", "b")]
        private DynValue Or(CallbackArguments args) =>
            DynValue.NewNumber(AviUtlGlobalFunctions.BitOr(Number(args, 0), Number(args, 1)));

        [LuaFunction("AND", "a", "b")]
        private DynValue And(CallbackArguments args) =>
            DynValue.NewNumber(AviUtlGlobalFunctions.BitAnd(Number(args, 0), Number(args, 1)));

        [LuaFunction("XOR", "a", "b")]
        private DynValue Xor(CallbackArguments args) =>
            DynValue.NewNumber(AviUtlGlobalFunctions.BitXor(Number(args, 0), Number(args, 1)));

        [LuaFunction("SHIFT", "a", "b")]
        private DynValue Shift(CallbackArguments args) =>
            DynValue.NewNumber(AviUtlGlobalFunctions.BitShift(Number(args, 0), Number(args, 1)));

        private static double Number(CallbackArguments args, int index) =>
            args.Count > index ? args[index].CastToNumber() ?? 0d : 0d;
    }
}
