using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace LuaScript.Anchor
{
    internal enum AnchorConnection
    {
        None,
        Line,
        Loop,
        Star,
        Arm,
    }

    internal readonly record struct AnchorOptions(AnchorConnection Connection, bool Is3D);

    internal readonly record struct AnchorRequestData(string Group, int Count, AnchorConnection Connection, bool Is3D);

    internal static class AnchorSupport
    {
        public const int MaxAnchors = 32;
        public const string TrackGroup = "track";

        public static int ClampCount(int count) => Math.Clamp(count, 0, MaxAnchors);

        public static void ApplyOption(string option, ref AnchorConnection connection, ref bool is3D)
        {
            switch (option)
            {
                case "line": connection = AnchorConnection.Line; break;
                case "loop": connection = AnchorConnection.Loop; break;
                case "star": connection = AnchorConnection.Star; break;
                case "arm": connection = AnchorConnection.Arm; break;
                case "xyz": is3D = true; break;
            }
        }

        public static (double X, double Y) DefaultPosition(int index)
        {
            int col = index % 8;
            int row = index / 8;
            return ((col - 3.5) * 60d, (row - 1.5) * 60d);
        }

        public static void ResolvePosition(IReadOnlyList<LuaAnchorPoint>? source, string group, int index,
            out double x, out double y, out double z)
        {
            if (source is not null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    var a = source[i];
                    if (a.Index == index && string.Equals(a.Group, group, StringComparison.Ordinal))
                    {
                        x = a.X;
                        y = a.Y;
                        z = a.Z;
                        return;
                    }
                }
            }

            var (dx, dy) = DefaultPosition(index);
            x = dx;
            y = dy;
            z = 0d;
        }

        public static ImmutableList<LuaAnchorPoint> ApplyDrag(
            ImmutableList<LuaAnchorPoint> source, string group, int index, double dx, double dy, double dz)
        {
            for (int i = 0; i < source.Count; i++)
            {
                var a = source[i];
                if (a.Index == index && string.Equals(a.Group, group, StringComparison.Ordinal))
                {
                    return source.SetItem(i, new LuaAnchorPoint
                    {
                        Group = group,
                        Index = index,
                        X = a.X + dx,
                        Y = a.Y + dy,
                        Z = a.Z + dz,
                    });
                }
            }

            var (bx, by) = DefaultPosition(index);
            return source.Add(new LuaAnchorPoint
            {
                Group = group,
                Index = index,
                X = bx + dx,
                Y = by + dy,
                Z = dz,
            });
        }
    }
}
