using System;
using System.Collections.Generic;

namespace LuaScript.Engine.Kernel
{
    internal enum KernelUniform
    {
        Width,
        Height,
        HalfWidth,
        HalfHeight,
        CenterX,
        CenterY,
        CenterZ,
        Diagonal,
        X,
        Y,
        Z,
        Ox,
        Oy,
        Oz,
        Sx,
        Sy,
        Sz,
        Zoom,
        Aspect,
        Alpha,
        Rx,
        Ry,
        Rz,
        Rxr,
        Ryr,
        Rzr,
        Track0,
        Track1,
        Track2,
        Track3,
        Slider0,
        Slider1,
        Slider2,
        Slider3,
        Check0,
        Check1,
        Check2,
        Check3,
        Color,
        Time,
        Frame,
        TotalFrame,
        TotalTime,
        T,
        Framerate,
        Layer,
        Index,
        Num,
        SceneWidth,
        SceneHeight,
        SceneCenterX,
        SceneCenterY,
        TimelineFrame,
        TimelineTime,
    }

    internal static class KernelUniforms
    {
        public static int Count { get; } = Enum.GetValues<KernelUniform>().Length;

        private static readonly Dictionary<string, KernelUniform> Globals = new(StringComparer.Ordinal)
        {
            ["time"] = KernelUniform.Time,
            ["frame"] = KernelUniform.Frame,
            ["totalframe"] = KernelUniform.TotalFrame,
            ["framerate"] = KernelUniform.Framerate,
            ["timelineframe"] = KernelUniform.TimelineFrame,
            ["timelinetime"] = KernelUniform.TimelineTime,
            ["layer"] = KernelUniform.Layer,
            ["color"] = KernelUniform.Color,
        };

        private static readonly Dictionary<string, KernelUniform> ObjectFields = new(StringComparer.Ordinal)
        {
            ["w"] = KernelUniform.Width,
            ["h"] = KernelUniform.Height,
            ["hw"] = KernelUniform.HalfWidth,
            ["hh"] = KernelUniform.HalfHeight,
            ["cx"] = KernelUniform.CenterX,
            ["cy"] = KernelUniform.CenterY,
            ["cz"] = KernelUniform.CenterZ,
            ["diagonal"] = KernelUniform.Diagonal,
            ["x"] = KernelUniform.X,
            ["y"] = KernelUniform.Y,
            ["z"] = KernelUniform.Z,
            ["ox"] = KernelUniform.Ox,
            ["oy"] = KernelUniform.Oy,
            ["oz"] = KernelUniform.Oz,
            ["sx"] = KernelUniform.Sx,
            ["sy"] = KernelUniform.Sy,
            ["sz"] = KernelUniform.Sz,
            ["zoom"] = KernelUniform.Zoom,
            ["aspect"] = KernelUniform.Aspect,
            ["alpha"] = KernelUniform.Alpha,
            ["rx"] = KernelUniform.Rx,
            ["ry"] = KernelUniform.Ry,
            ["rz"] = KernelUniform.Rz,
            ["rxr"] = KernelUniform.Rxr,
            ["ryr"] = KernelUniform.Ryr,
            ["rzr"] = KernelUniform.Rzr,
            ["track0"] = KernelUniform.Track0,
            ["track1"] = KernelUniform.Track1,
            ["track2"] = KernelUniform.Track2,
            ["track3"] = KernelUniform.Track3,
            ["slider0"] = KernelUniform.Slider0,
            ["slider1"] = KernelUniform.Slider1,
            ["slider2"] = KernelUniform.Slider2,
            ["slider3"] = KernelUniform.Slider3,
            ["check0"] = KernelUniform.Check0,
            ["check1"] = KernelUniform.Check1,
            ["check2"] = KernelUniform.Check2,
            ["check3"] = KernelUniform.Check3,
            ["time"] = KernelUniform.Time,
            ["frame"] = KernelUniform.Frame,
            ["totalframe"] = KernelUniform.TotalFrame,
            ["totaltime"] = KernelUniform.TotalTime,
            ["t"] = KernelUniform.T,
            ["framerate"] = KernelUniform.Framerate,
            ["layer"] = KernelUniform.Layer,
            ["index"] = KernelUniform.Index,
            ["num"] = KernelUniform.Num,
        };

        private static readonly Dictionary<string, KernelUniform> SceneFields = new(StringComparer.Ordinal)
        {
            ["width"] = KernelUniform.SceneWidth,
            ["height"] = KernelUniform.SceneHeight,
            ["cx"] = KernelUniform.SceneCenterX,
            ["cy"] = KernelUniform.SceneCenterY,
        };

        public static bool TryResolveGlobal(string name, out KernelUniform uniform) =>
            Globals.TryGetValue(name, out uniform);

        public static bool TryResolveMember(string target, string field, out KernelUniform uniform)
        {
            uniform = default;
            return target switch
            {
                "obj" => ObjectFields.TryGetValue(field, out uniform),
                "scene" => SceneFields.TryGetValue(field, out uniform),
                _ => false,
            };
        }

        public static string Field(KernelUniform uniform) => "u_" + uniform;
    }
}
