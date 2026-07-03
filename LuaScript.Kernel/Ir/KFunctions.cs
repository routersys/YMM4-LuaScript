using System;
using System.Collections.Generic;

namespace LuaScript.Engine.Kernel
{
    internal static class KFunctions
    {
        private static readonly KFunctionInfo[] Catalog =
        [
            new(KFunc.Abs, "abs", 1, 1),
            new(KFunc.Floor, "floor", 1, 1),
            new(KFunc.Ceil, "ceil", 1, 1),
            new(KFunc.Sqrt, "sqrt", 1, 1),
            new(KFunc.Sin, "sin", 1, 1),
            new(KFunc.Cos, "cos", 1, 1),
            new(KFunc.Tan, "tan", 1, 1),
            new(KFunc.Asin, "asin", 1, 1),
            new(KFunc.Acos, "acos", 1, 1),
            new(KFunc.Atan, "atan", 1, 1),
            new(KFunc.Atan2, "atan2", 2, 2),
            new(KFunc.Exp, "exp", 1, 1),
            new(KFunc.Log, "log", 1, 1),
            new(KFunc.Pow, "pow", 2, 2),
            new(KFunc.Min, "min", 2, int.MaxValue),
            new(KFunc.Max, "max", 2, int.MaxValue),
            new(KFunc.Fmod, "fmod", 2, 2),
        ];

        private static readonly Dictionary<string, KFunctionInfo> ByName = BuildByName();
        private static readonly string[] Intrinsics = BuildIntrinsics();

        public static bool TryResolve(string name, out KFunctionInfo info) => ByName.TryGetValue(name, out info);

        public static string Intrinsic(KFunc func) => Intrinsics[(int)func];

        private static Dictionary<string, KFunctionInfo> BuildByName()
        {
            var map = new Dictionary<string, KFunctionInfo>(Catalog.Length, StringComparer.Ordinal);
            foreach (var info in Catalog)
                map[info.Name] = info;
            return map;
        }

        private static string[] BuildIntrinsics()
        {
            var names = new string[Enum.GetValues<KFunc>().Length];
            foreach (var info in Catalog)
                names[(int)info.Func] = info.Name;
            return names;
        }
    }
}
