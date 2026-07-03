using LuaScript.Api;

namespace LuaScript
{
    [LuaTable("ymm4")]
    internal static partial class Ymm4TableRegistrar
    {
        [LuaVariable("group_index")]
        private static int GroupIndex(AviUtlScriptContext ctx) => ctx.GroupIndex;

        [LuaVariable("group_count")]
        private static int GroupCount(AviUtlScriptContext ctx) => ctx.GroupCount;

        [LuaVariable("group_ratio")]
        private static double GroupRatio(AviUtlScriptContext ctx) =>
            ctx.GroupCount > 0 ? ctx.GroupIndex / (double)ctx.GroupCount : 0d;

        [LuaVariable("timeline_totalframe")]
        private static int TimelineTotalFrame(AviUtlScriptContext ctx) => ctx.TimelineTotalFrame;

        [LuaVariable("timeline_totaltime")]
        private static double TimelineTotalTime(AviUtlScriptContext ctx) => ctx.TimelineTotalTime;

        [LuaVariable("is_saving")]
        private static bool IsSaving(AviUtlScriptContext ctx) => ctx.IsSaving;

        [LuaVariable("time_ratio")]
        private static double TimeRatio(AviUtlScriptContext ctx) => ctx.TimeRatio;

        [LuaVariable("is_playing")]
        private static bool IsPlaying(AviUtlScriptContext ctx) => ctx.IsPlaying;

        [LuaVariable("is_paused")]
        private static bool IsPaused(AviUtlScriptContext ctx) => ctx.IsPaused;

        [LuaVariable("scene_id")]
        private static string SceneId(AviUtlScriptContext ctx) => ctx.SceneId;
    }
}
