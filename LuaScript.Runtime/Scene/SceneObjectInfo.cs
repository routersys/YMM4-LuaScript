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
        int Layer,
        int Length,
        double Volume,
        string Character,
        string Text,
        string Kind);
}
