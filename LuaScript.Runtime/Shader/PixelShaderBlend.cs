namespace LuaScript
{
    internal readonly record struct PixelShaderBlend(PixelShaderBlendMode Mode, double CompositeBlend)
    {
        public static PixelShaderBlend Copy { get; } = new(PixelShaderBlendMode.Copy, 0d);

        public static PixelShaderBlend Mask { get; } = new(PixelShaderBlendMode.Mask, 0d);

        public static PixelShaderBlend Draw { get; } = new(PixelShaderBlendMode.Draw, 0d);

        public static PixelShaderBlend Add { get; } = new(PixelShaderBlendMode.Add, 0d);

        public static PixelShaderBlend Composite(double blend) => new(PixelShaderBlendMode.Composite, blend);
    }
}
