namespace LuaScript
{
    internal readonly record struct SceneObjectInfo(
        string Tag,
        bool Exist,
        double X,
        double Y,
        double Z,
        double Zoom,
        double Rz,
        double Alpha,
        int Layer);

    internal readonly record struct SceneObjectQuery(
        string Tag,
        int Frame,
        SceneObjectInfo? Result);
}
