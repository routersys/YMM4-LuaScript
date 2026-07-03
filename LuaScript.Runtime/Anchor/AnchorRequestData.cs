namespace LuaScript.Anchor
{
    internal readonly record struct AnchorRequestData(string Group, int Count, AnchorConnection Connection, bool Is3D);
}
