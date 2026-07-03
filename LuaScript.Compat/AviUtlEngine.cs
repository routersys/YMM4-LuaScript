namespace LuaScript.Compat
{
    internal enum AviUtlEngine
    {
        AviUtl,
        AviUtl2,
        Both,
    }

    internal static class AviUtlEngineExtensions
    {
        public static bool Includes(this AviUtlEngine scope, AviUtlEngine target) =>
            scope == AviUtlEngine.Both || scope == target;

        public static bool TryParse(string? value, out AviUtlEngine engine)
        {
            switch (value)
            {
                case "aviutl":
                    engine = AviUtlEngine.AviUtl;
                    return true;
                case "aviutl2":
                    engine = AviUtlEngine.AviUtl2;
                    return true;
                case "both":
                    engine = AviUtlEngine.Both;
                    return true;
                default:
                    engine = AviUtlEngine.AviUtl;
                    return false;
            }
        }
    }
}
