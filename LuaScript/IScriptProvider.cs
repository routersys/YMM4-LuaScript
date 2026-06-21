namespace LuaScript
{
    internal interface IScriptProvider
    {
        string Script { get; set; }
        string DefaultScript { get; }
    }
}
