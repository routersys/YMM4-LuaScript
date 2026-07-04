namespace LuaScript
{
    internal readonly record struct PixelShaderInput(byte[]? Data, int Width, int Height)
    {
        public const int RandomSize = 256;

        public static PixelShaderInput Random { get; } = new(null, RandomSize, RandomSize);

        public bool IsRandom => Data is null;
    }
}
