namespace LuaScript.Compat
{
    internal readonly record struct AviUtlParameterMapping(string Source, string Property, double Scale, double Offset)
    {
        public double Transform(double value) => value * Scale + Offset;
    }
}
