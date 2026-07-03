namespace LuaScript
{
    internal readonly record struct SceneObjectQuery(
        string Tag,
        int Frame,
        SceneObjectInfo? Result);
}
