namespace LuaScript
{
    internal readonly record struct DrawCommand(
        double Ox,
        double Oy,
        double Oz,
        double Zoom,
        double Alpha,
        double Aspect);
}
