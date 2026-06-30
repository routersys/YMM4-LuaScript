using System;

namespace LuaScript.Engine
{
    internal static class NativeFieldMap
    {
        private const double Epsilon = 1e-10;

        public static void ToFields(AviUtlScriptContext ctx, double[] f)
        {
            f[NativeProtocol.W] = ctx.ImageWidth;
            f[NativeProtocol.H] = ctx.ImageHeight;
            f[NativeProtocol.Hw] = ctx.ImageWidth / 2d;
            f[NativeProtocol.Hh] = ctx.ImageHeight / 2d;
            f[NativeProtocol.Cx] = ctx.ImageWidth / 2d;
            f[NativeProtocol.Cy] = ctx.ImageHeight / 2d;
            f[NativeProtocol.Cz] = 0d;
            f[NativeProtocol.Diagonal] = Math.Sqrt((double)ctx.ImageWidth * ctx.ImageWidth + (double)ctx.ImageHeight * ctx.ImageHeight);
            f[NativeProtocol.X] = ctx.X;
            f[NativeProtocol.Y] = ctx.Y;
            f[NativeProtocol.Z] = ctx.Z;
            f[NativeProtocol.Ox] = ctx.Ox;
            f[NativeProtocol.Oy] = ctx.Oy;
            f[NativeProtocol.Oz] = ctx.Oz;
            f[NativeProtocol.Sx] = ctx.Sx;
            f[NativeProtocol.Sy] = ctx.Sy;
            f[NativeProtocol.Sz] = 1d;
            f[NativeProtocol.Zoom] = ctx.Zoom;
            f[NativeProtocol.Aspect] = ctx.Aspect;
            f[NativeProtocol.Alpha] = ctx.Alpha;
            f[NativeProtocol.Rx] = ctx.Rx;
            f[NativeProtocol.Ry] = ctx.Ry;
            f[NativeProtocol.Rz] = ctx.Rz;
            f[NativeProtocol.Rxr] = ctx.RxRad;
            f[NativeProtocol.Ryr] = ctx.RyRad;
            f[NativeProtocol.Rzr] = ctx.RzRad;
            f[NativeProtocol.Track0] = ctx.Track0;
            f[NativeProtocol.Track1] = ctx.Track1;
            f[NativeProtocol.Track2] = ctx.Track2;
            f[NativeProtocol.Track3] = ctx.Track3;
            f[NativeProtocol.Slider0] = ctx.Slider0;
            f[NativeProtocol.Slider1] = ctx.Slider1;
            f[NativeProtocol.Slider2] = ctx.Slider2;
            f[NativeProtocol.Slider3] = ctx.Slider3;
            f[NativeProtocol.Check0] = ctx.Check0 ? 1d : 0d;
            f[NativeProtocol.Check1] = ctx.Check1 ? 1d : 0d;
            f[NativeProtocol.Check2] = ctx.Check2 ? 1d : 0d;
            f[NativeProtocol.Check3] = ctx.Check3 ? 1d : 0d;
            f[NativeProtocol.Color] = ctx.HasColor ? ctx.ColorValue : -1d;
            f[NativeProtocol.Time] = ctx.Time;
            f[NativeProtocol.Frame] = ctx.Frame;
            f[NativeProtocol.TotalFrame] = ctx.TotalFrame;
            f[NativeProtocol.TotalTime] = ctx.TotalTime;
            f[NativeProtocol.T] = ctx.TotalFrame > 0 ? ctx.Frame / (double)ctx.TotalFrame : 0d;
            f[NativeProtocol.Framerate] = ctx.Framerate;
            f[NativeProtocol.Layer] = ctx.Layer;
            f[NativeProtocol.Index] = ctx.Index;
            f[NativeProtocol.Num] = ctx.Num;
            f[NativeProtocol.SceneWidth] = ctx.SceneWidth;
            f[NativeProtocol.SceneHeight] = ctx.SceneHeight;
            f[NativeProtocol.SceneCx] = ctx.SceneWidth / 2d;
            f[NativeProtocol.SceneCy] = ctx.SceneHeight / 2d;
            f[NativeProtocol.GroupIndex] = ctx.GroupIndex;
            f[NativeProtocol.GroupCount] = ctx.GroupCount;
            f[NativeProtocol.TimelineTotalFrame] = ctx.TimelineTotalFrame;
            f[NativeProtocol.TimelineTotalTime] = ctx.TimelineTotalTime;
            f[NativeProtocol.TimeRatio] = ctx.TimeRatio;
            f[NativeProtocol.IsSaving] = ctx.IsSaving ? 1d : 0d;
            f[NativeProtocol.IsPlaying] = ctx.IsPlaying ? 1d : 0d;
            f[NativeProtocol.IsPaused] = ctx.IsPaused ? 1d : 0d;
            f[NativeProtocol.TimelineFrame] = ctx.TimelineFrame;
            f[NativeProtocol.TimelineTime] = ctx.TimelineTime;
        }

        public static void FromFields(double[] f, AviUtlScriptContext ctx)
        {
            ctx.X = f[NativeProtocol.X];
            ctx.Y = f[NativeProtocol.Y];
            ctx.Z = f[NativeProtocol.Z];
            ctx.Ox = f[NativeProtocol.Ox];
            ctx.Oy = f[NativeProtocol.Oy];
            ctx.Oz = f[NativeProtocol.Oz];
            ctx.Alpha = f[NativeProtocol.Alpha];

            ctx.ApplyWriteBack(
                f[NativeProtocol.Sx],
                f[NativeProtocol.Sy],
                f[NativeProtocol.Zoom],
                f[NativeProtocol.Aspect],
                f[NativeProtocol.Rx],
                f[NativeProtocol.Ry],
                f[NativeProtocol.Rz],
                f[NativeProtocol.Rxr],
                f[NativeProtocol.Ryr],
                f[NativeProtocol.Rzr]);
        }
    }
}
