using System;
using System.Globalization;

namespace LuaScript.Compat
{
    internal sealed class AviUtlParameterLayout
    {
        public const int MaxTracks = 4;
        public const int MaxChecks = 4;

        public readonly record struct TrackParam(int Index, string Name, double Min, double Max, double Default, double Step);
        public readonly record struct CheckParam(int Index, string Name, bool Default);
        public readonly record struct ColorParam(int Default);

        private readonly TrackParam?[] _tracks = new TrackParam?[MaxTracks];
        private readonly CheckParam?[] _checks = new CheckParam?[MaxChecks];

        public static readonly AviUtlParameterLayout Empty = new();

        public TrackParam? GetTrack(int index) => (uint)index < MaxTracks ? _tracks[index] : null;
        public CheckParam? GetCheck(int index) => (uint)index < MaxChecks ? _checks[index] : null;
        public ColorParam? Color { get; private set; }

        public bool HasTrack(int index) => GetTrack(index).HasValue;
        public bool HasCheck(int index) => GetCheck(index).HasValue;
        public bool HasColor => Color.HasValue;

        public bool HasAny
        {
            get
            {
                for (int i = 0; i < MaxTracks; i++)
                    if (_tracks[i].HasValue) return true;
                for (int i = 0; i < MaxChecks; i++)
                    if (_checks[i].HasValue) return true;
                return HasColor;
            }
        }

        public static AviUtlParameterLayout Parse(string? source)
        {
            var layout = new AviUtlParameterLayout();
            if (string.IsNullOrEmpty(source))
                return layout;

            var lines = source.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            int sectionStart = 0;
            int sectionEnd = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!ScriptParserHelper.IsSectionHeader(lines[i]))
                    continue;
                sectionStart = i + 1;
                sectionEnd = lines.Length;
                for (int j = sectionStart; j < lines.Length; j++)
                {
                    if (ScriptParserHelper.IsSectionHeader(lines[j]))
                    {
                        sectionEnd = j;
                        break;
                    }
                }
                break;
            }

            for (int i = sectionStart; i < sectionEnd; i++)
                layout.ParseLine(lines[i]);

            return layout;
        }

        private void ParseLine(string line)
        {
            int i = ScriptParserHelper.SkipSpaces(line, 0);
            if (i + 1 >= line.Length || line[i] != '-' || line[i + 1] != '-')
                return;
            i += 2;
            if (i < line.Length && line[i] == '!')
                return;

            int nameStart = i;
            while (i < line.Length && ScriptParserHelper.IsNameChar(line[i]))
                i++;
            if (i >= line.Length || line[i] != ':')
                return;

            string name = line.Substring(nameStart, i - nameStart);
            string content = line.Substring(i + 1);

            if (name.StartsWith("track", StringComparison.OrdinalIgnoreCase) &&
                TryGetIndex(name, 5, MaxTracks, out int trackIndex))
            {
                ParseTrack(trackIndex, content);
            }
            else if (name.StartsWith("check", StringComparison.OrdinalIgnoreCase) &&
                TryGetIndex(name, 5, MaxChecks, out int checkIndex))
            {
                ParseCheck(checkIndex, content);
            }
            else if (name.Equals("color", StringComparison.OrdinalIgnoreCase))
            {
                ParseColor(content);
            }
        }

        private void ParseTrack(int index, string content)
        {
            var parts = content.Split(',');
            string trackName = parts.Length > 0 ? parts[0].Trim() : string.Empty;
            double min = ParseNumber(parts, 1, 0d);
            double max = ParseNumber(parts, 2, 100d);
            double def = ParseNumber(parts, 3, min);
            double step = ParseNumber(parts, 4, 0d);
            if (max < min)
                (min, max) = (max, min);
            def = Math.Clamp(def, min, max);
            _tracks[index] = new TrackParam(index, trackName, min, max, def, step);
        }

        private void ParseCheck(int index, string content)
        {
            var parts = content.Split(',');
            string checkName = parts.Length > 0 ? parts[0].Trim() : string.Empty;
            double def = ParseNumber(parts, 1, 0d);
            _checks[index] = new CheckParam(index, checkName, def != 0d);
        }

        private void ParseColor(string content)
        {
            string token = FirstToken(content);
            if (TryParseColorLiteral(token, out int rgb))
                Color = new ColorParam(rgb);
        }

        private static bool TryGetIndex(string name, int start, int max, out int index)
        {
            index = -1;
            if (name.Length != start + 1)
                return false;
            char c = name[start];
            if (c < '0' || c > '9')
                return false;
            int value = c - '0';
            if (value >= max)
                return false;
            index = value;
            return true;
        }

        private static double ParseNumber(string[] parts, int idx, double fallback)
        {
            if (idx >= parts.Length)
                return fallback;
            string token = parts[idx].Trim();
            return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : fallback;
        }

        private static bool TryParseColorLiteral(string token, out int rgb)
        {
            rgb = 0;
            if (token.Length == 0)
                return false;
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || token.StartsWith("0X", StringComparison.Ordinal))
            {
                if (token.Length <= 2)
                    return false;
                return int.TryParse(token.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb);
            }
            return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out rgb);
        }

        private static string FirstToken(string content)
        {
            int start = ScriptParserHelper.SkipSpaces(content, 0);
            int i = start;
            while (i < content.Length && content[i] != ',' && content[i] != ' ' && content[i] != '\t')
                i++;
            return content.Substring(start, i - start);
        }
    }
}
