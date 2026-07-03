using LuaScript.Api;
using MoonSharp.Interpreter;

namespace LuaScript
{
    [LuaTable("scene")]
    internal sealed partial class SceneTableRegistrar(Func<string, SceneValue> getValue, Action<string, SceneValue> setValue)
    {
        internal static void RegisterFunctions(Table scene, Func<string, SceneValue> getValue, Action<string, SceneValue> setValue) =>
            new SceneTableRegistrar(getValue, setValue).RegisterLuaMembers(scene);

        [LuaVariable("width")]
        private int Width(AviUtlScriptContext ctx) => ctx.SceneWidth;

        [LuaVariable("height")]
        private int Height(AviUtlScriptContext ctx) => ctx.SceneHeight;

        [LuaVariable("cx")]
        private double Cx(AviUtlScriptContext ctx) => ctx.SceneWidth / 2d;

        [LuaVariable("cy")]
        private double Cy(AviUtlScriptContext ctx) => ctx.SceneHeight / 2d;

        [LuaFunction("set", "name", "value")]
        private DynValue Set(CallbackArguments args)
        {
            if (args.Count == 0 || args[0].Type != DataType.String)
                return DynValue.Void;

            var value = args.Count > 1 ? args[1] : DynValue.Nil;
            SceneValue stored;
            switch (value.Type)
            {
                case DataType.Number:
                    stored = SceneValue.FromNumber(value.Number);
                    break;
                case DataType.String:
                    stored = SceneValue.FromString(value.String);
                    break;
                case DataType.Boolean:
                    stored = SceneValue.FromBoolean(value.Boolean);
                    break;
                case DataType.Nil:
                case DataType.Void:
                    stored = SceneValue.Nil;
                    break;
                default:
                    return DynValue.Void;
            }
            setValue(args[0].String, stored);
            return DynValue.Void;
        }

        [LuaFunction("get", "name")]
        private DynValue Get(CallbackArguments args)
        {
            if (args.Count == 0 || args[0].Type != DataType.String)
                return DynValue.Nil;

            var value = getValue(args[0].String);
            return value.Kind switch
            {
                SceneValueKind.Number => DynValue.NewNumber(value.Number),
                SceneValueKind.String => DynValue.NewString(value.Text ?? string.Empty),
                SceneValueKind.Boolean => value.Number != 0d ? DynValue.True : DynValue.False,
                _ => DynValue.Nil,
            };
        }
    }
}
