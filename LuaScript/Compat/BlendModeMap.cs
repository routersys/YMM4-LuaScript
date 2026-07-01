using YukkuriMovieMaker.Project;

namespace LuaScript.Compat
{
    internal static class BlendModeMap
    {
        private static readonly Blend[] Order =
        [
            Blend.Normal,
            Blend.Dissolve,
            Blend.Darker,
            Blend.Multiply,
            Blend.ColorBurn,
            Blend.LinearBurn,
            Blend.Lighter,
            Blend.Screen,
            Blend.ColorDodge,
            Blend.LinearDodge,
            Blend.Add,
            Blend.Overlay,
            Blend.SoftLight,
            Blend.HardLight,
            Blend.VividLight,
            Blend.LinearLight,
            Blend.PinLight,
            Blend.HardMix,
            Blend.Difference,
            Blend.Exclusion,
            Blend.Subtract,
            Blend.Division,
            Blend.Hue,
            Blend.Saturation,
            Blend.Color,
            Blend.Luminosity,
            Blend.LighterColor,
            Blend.DestinationOver,
            Blend.DarkerColor,
            Blend.DestinationOut,
            Blend.SourceAtop,
            Blend.XOR,
            Blend.MaskInvert,
        ];

        public static Blend Resolve(double value)
        {
            if (double.IsNaN(value))
                return Blend.Normal;

            int index = (int)value;
            return (uint)index < (uint)Order.Length ? Order[index] : Blend.Normal;
        }
    }
}
