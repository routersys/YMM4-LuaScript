using MoonSharp.Interpreter;

namespace LuaScript
{
    internal sealed class AviUtlFontState
    {
        public string Family { get; private set; } = string.Empty;
        public double Size { get; private set; } = 34d;
        public bool Bold { get; private set; }
        public bool Italic { get; private set; }
        public int Color { get; private set; } = 0xFFFFFF;

        public void Reset()
        {
            Family = string.Empty;
            Size = 34d;
            Bold = false;
            Italic = false;
            Color = 0xFFFFFF;
        }

        public void Apply(DynValue name, DynValue size, DynValue style, DynValue color)
        {
            if (name.Type == DataType.String)
                Family = name.String;
            else if (name.Type == DataType.Number)
                Family = name.CastToString();

            Size = size.CastToNumber() ?? Size;

            if (style.CastToNumber() is double styleNumber)
            {
                double flags = Math.Floor(styleNumber);
                Bold = FloorMod(flags, 2d) == 1d;
                Italic = FloorMod(Math.Floor(flags / 2d), 2d) == 1d;
            }

            Color = (int)(color.CastToNumber() ?? Color);
        }

        private static double FloorMod(double value, double divisor) =>
            value - Math.Floor(value / divisor) * divisor;
    }
}
