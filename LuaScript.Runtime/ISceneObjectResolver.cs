namespace LuaScript
{
    internal interface ISceneObjectResolver
    {
        bool TryResolve(string tag, int timelineFrame, out SceneObjectInfo info);
    }
}
