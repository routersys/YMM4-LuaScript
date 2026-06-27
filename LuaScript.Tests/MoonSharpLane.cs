using LuaScript;
using MoonSharp.Interpreter;

namespace LuaScript.Tests
{
    internal sealed class MoonSharpLane
    {
        private readonly Script _script;

        public MoonSharpLane()
        {
            _script = new Script(
                CoreModules.Basic |
                CoreModules.Math |
                CoreModules.String |
                CoreModules.Table |
                CoreModules.Bit32 |
                CoreModules.TableIterators |
                CoreModules.Metatables |
                CoreModules.ErrorHandling);

            var anim = new Table(_script);
            AnimTableRegistrar.RegisterFunctions(anim);
            _script.Globals["anim"] = anim;
        }

        public DynValue Run(string code) => _script.DoString(code);

        public double Number(string expression) => Run("return " + expression).Number;

        public bool Boolean(string expression) => Run("return " + expression).Boolean;

        public string? Text(string expression) => Run("return " + expression).CastToString();

        public DataType Type(string expression) => Run("return " + expression).Type;

        public double[] Tuple(string expression, int count)
        {
            var table = Run("return {" + expression + "}").Table;
            var result = new double[count];
            for (int i = 0; i < count; i++)
                result[i] = table.Get(i + 1).Number;
            return result;
        }
    }
}
