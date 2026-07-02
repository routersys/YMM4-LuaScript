using System;
using System.Numerics;

namespace LuaScript
{
    internal static class DrawTransform
    {
        public static bool TryResolve(in DrawCommand command, int srcW, int srcH, out Matrix3x2 matrix)
        {
            if (command.Poly is { } poly)
                return DrawPolyMath.TrySolveAffine(poly, out matrix);

            double aspect = Math.Clamp(command.Aspect, -1d, 1d);
            double zx = command.Zoom * (1d + aspect);
            double zy = command.Zoom * (1d - aspect);
            if (zx == 0d || zy == 0d)
            {
                matrix = Matrix3x2.Identity;
                return false;
            }

            matrix =
                Matrix3x2.CreateTranslation(-srcW * 0.5f, -srcH * 0.5f) *
                Matrix3x2.CreateScale((float)zx, (float)zy) *
                Matrix3x2.CreateTranslation((float)command.Ox, (float)command.Oy);
            return true;
        }
    }
}
