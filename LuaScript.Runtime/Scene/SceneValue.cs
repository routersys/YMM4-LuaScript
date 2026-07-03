namespace LuaScript
{
    internal readonly record struct SceneValue(SceneValueKind Kind, double Number, string? Text)
    {
        public static readonly SceneValue Nil = new(SceneValueKind.Nil, 0d, null);

        public static SceneValue FromNumber(double value) => new(SceneValueKind.Number, value, null);

        public static SceneValue FromBoolean(bool value) => new(SceneValueKind.Boolean, value ? 1d : 0d, null);

        public static SceneValue FromString(string value) => new(SceneValueKind.String, 0d, value);
    }
}
