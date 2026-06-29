using System;

namespace LuaScript.Engine.Kernel
{
    internal static class KernelUniformBinding
    {
        public static double[] Create() => new double[KernelUniforms.Count];

        public static void Fill(AviUtlScriptContext ctx, double[] values)
        {
            values[(int)KernelUniform.Width] = ctx.ImageWidth;
            values[(int)KernelUniform.Height] = ctx.ImageHeight;
            values[(int)KernelUniform.HalfWidth] = ctx.ImageWidth / 2d;
            values[(int)KernelUniform.HalfHeight] = ctx.ImageHeight / 2d;
            values[(int)KernelUniform.CenterX] = ctx.ImageWidth / 2d;
            values[(int)KernelUniform.CenterY] = ctx.ImageHeight / 2d;
            values[(int)KernelUniform.CenterZ] = 0d;
            values[(int)KernelUniform.Diagonal] = Math.Sqrt((double)ctx.ImageWidth * ctx.ImageWidth + (double)ctx.ImageHeight * ctx.ImageHeight);
            values[(int)KernelUniform.X] = ctx.X;
            values[(int)KernelUniform.Y] = ctx.Y;
            values[(int)KernelUniform.Z] = ctx.Z;
            values[(int)KernelUniform.Ox] = ctx.Ox;
            values[(int)KernelUniform.Oy] = ctx.Oy;
            values[(int)KernelUniform.Oz] = ctx.Oz;
            values[(int)KernelUniform.Sx] = ctx.Sx;
            values[(int)KernelUniform.Sy] = ctx.Sy;
            values[(int)KernelUniform.Sz] = 1d;
            values[(int)KernelUniform.Zoom] = ctx.Zoom;
            values[(int)KernelUniform.Aspect] = ctx.Aspect;
            values[(int)KernelUniform.Alpha] = ctx.Alpha;
            values[(int)KernelUniform.Rx] = ctx.Rx;
            values[(int)KernelUniform.Ry] = ctx.Ry;
            values[(int)KernelUniform.Rz] = ctx.Rz;
            values[(int)KernelUniform.Rxr] = ctx.RxRad;
            values[(int)KernelUniform.Ryr] = ctx.RyRad;
            values[(int)KernelUniform.Rzr] = ctx.RzRad;
            values[(int)KernelUniform.Track0] = ctx.Track0;
            values[(int)KernelUniform.Track1] = ctx.Track1;
            values[(int)KernelUniform.Track2] = ctx.Track2;
            values[(int)KernelUniform.Track3] = ctx.Track3;
            values[(int)KernelUniform.Check0] = ctx.Check0 ? 1d : 0d;
            values[(int)KernelUniform.Check1] = ctx.Check1 ? 1d : 0d;
            values[(int)KernelUniform.Check2] = ctx.Check2 ? 1d : 0d;
            values[(int)KernelUniform.Check3] = ctx.Check3 ? 1d : 0d;
            values[(int)KernelUniform.Color] = ctx.HasColor ? ctx.ColorValue : -1d;
            values[(int)KernelUniform.Time] = ctx.Time;
            values[(int)KernelUniform.Frame] = ctx.Frame;
            values[(int)KernelUniform.TotalFrame] = ctx.TotalFrame;
            values[(int)KernelUniform.TotalTime] = ctx.TotalTime;
            values[(int)KernelUniform.T] = ctx.TotalFrame > 0 ? ctx.Frame / (double)ctx.TotalFrame : 0d;
            values[(int)KernelUniform.Framerate] = ctx.Framerate;
            values[(int)KernelUniform.Layer] = ctx.Layer;
            values[(int)KernelUniform.Index] = ctx.Index;
            values[(int)KernelUniform.Num] = ctx.Num;
            values[(int)KernelUniform.SceneWidth] = ctx.SceneWidth;
            values[(int)KernelUniform.SceneHeight] = ctx.SceneHeight;
            values[(int)KernelUniform.SceneCenterX] = ctx.SceneWidth / 2d;
            values[(int)KernelUniform.SceneCenterY] = ctx.SceneHeight / 2d;
            values[(int)KernelUniform.TimelineFrame] = ctx.TimelineFrame;
            values[(int)KernelUniform.TimelineTime] = ctx.TimelineTime;
        }

        public static void Pack(double[] values, float[] packed)
        {
            int count = Math.Min(values.Length, packed.Length);
            for (int i = 0; i < count; i++)
                packed[i] = (float)values[i];
            for (int i = count; i < packed.Length; i++)
                packed[i] = 0f;
        }
    }
}
