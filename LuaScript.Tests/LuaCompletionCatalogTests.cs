using LuaScript.Api;

namespace LuaScript.Tests
{
    public class LuaCompletionCatalogTests
    {
        private static readonly string[] Keywords =
        [
            "and", "break", "do", "else", "elseif", "end", "false", "for",
            "function", "goto", "if", "in", "local", "nil", "not", "or",
            "repeat", "return", "then", "true", "until", "while"
        ];

        private static readonly string[] Globals =
        [
            "time", "frame", "totalframe", "framerate",
            "timelineframe", "timelinetime", "layer", "color",
            "obj", "scene", "anim", "ymm4",
            "math", "string", "table", "bit32",
            "type", "tostring", "tonumber", "select", "error", "assert", "print",
            "debug_print", "RGB", "HSV", "OR", "AND", "XOR", "SHIFT",
            "ipairs", "pairs", "next",
            "unpack",
            "setmetatable", "getmetatable", "rawget", "rawset", "rawequal", "rawlen",
            "pcall", "xpcall",
        ];

        private static readonly (string Prefix, string[] Members)[] Namespaces =
        [
            ("obj", [
                "obj.w", "obj.h", "obj.hw", "obj.hh", "obj.diagonal",
                "obj.cx", "obj.cy", "obj.cz",
                "obj.x", "obj.y", "obj.z",
                "obj.ox", "obj.oy", "obj.oz",
                "obj.sx", "obj.sy",
                "obj.zoom", "obj.alpha", "obj.aspect",
                "obj.rx", "obj.ry", "obj.rz",
                "obj.rxr", "obj.ryr", "obj.rzr",
                "obj.track0", "obj.track1", "obj.track2", "obj.track3",
                "obj.slider0", "obj.slider1", "obj.slider2", "obj.slider3",
                "obj.check0", "obj.check1", "obj.check2", "obj.check3",
                "obj.text", "obj.font", "obj.dir",
                "obj.file_video", "obj.file_audio", "obj.file_image", "obj.file_project",
                "obj.file_mp4", "obj.file_exo", "obj.file_subtitle", "obj.file_shader",
                "obj.time", "obj.totaltime", "obj.t", "obj.frame", "obj.totalframe",
                "obj.framerate", "obj.layer", "obj.index", "obj.num",
                "obj.getobject", "obj.getpixel", "obj.setpixel",
                "obj.getpixeldata", "obj.putpixeldata", "obj.rand", "obj.load",
                "obj.setfont", "obj.draw", "obj.drawpoly", "obj.copybuffer",
                "obj.getvalue", "obj.setoption", "obj.getoption", "obj.pixeloption",
                "obj.setanchor", "obj.effect", "obj.getinfo"
            ]),
            ("math", [
                "math.abs", "math.ceil", "math.cos", "math.exp", "math.floor",
                "math.fmod", "math.huge", "math.log", "math.max", "math.min",
                "math.modf", "math.pi", "math.random", "math.randomseed",
                "math.sin", "math.sqrt", "math.tan", "math.atan", "math.atan2",
                "math.deg", "math.rad", "math.ldexp", "math.frexp",
                "math.sinh", "math.cosh", "math.tanh", "math.type",
                "math.tointeger", "math.maxinteger", "math.mininteger"
            ]),
            ("string", [
                "string.format", "string.len", "string.sub", "string.upper",
                "string.lower", "string.rep", "string.reverse", "string.find",
                "string.match", "string.gmatch", "string.gsub", "string.byte",
                "string.char", "string.dump", "string.pack", "string.unpack",
                "string.packsize"
            ]),
            ("table", [
                "table.insert", "table.remove", "table.sort",
                "table.concat", "table.unpack", "table.move"
            ]),
            ("bit32", [
                "bit32.band", "bit32.bor", "bit32.bxor", "bit32.bnot",
                "bit32.lshift", "bit32.rshift", "bit32.arshift",
                "bit32.extract", "bit32.replace",
                "bit32.btest", "bit32.countlz", "bit32.countrz"
            ]),
            ("scene", [
                "scene.width", "scene.height", "scene.cx", "scene.cy",
                "scene.set", "scene.get"
            ]),
            ("anim", [
                "anim.tau", "anim.e", "anim.phi", "anim.sqrt2",
                "anim.lerp", "anim.smoothstep", "anim.smootherstep", "anim.clamp",
                "anim.map", "anim.norm", "anim.wrap", "anim.pingpong",
                "anim.sign", "anim.oscillate", "anim.triangle", "anim.square",
                "anim.duration", "anim.delay",
                "anim.ease_in", "anim.ease_out", "anim.ease_in_out", "anim.elastic", "anim.back",
                "anim.step", "anim.fract", "anim.bounce",
                "anim.hsv_to_rgb", "anim.rgb_to_hsv",
                "anim.len", "anim.dist", "anim.dot", "anim.normalize",
                "anim.noise", "anim.rand", "anim.polar", "anim.rotate", "anim.bezier"
            ]),
            ("ymm4", [
                "ymm4.group_index", "ymm4.group_count", "ymm4.group_ratio",
                "ymm4.timeline_totalframe", "ymm4.timeline_totaltime",
                "ymm4.is_saving", "ymm4.time_ratio",
                "ymm4.is_playing", "ymm4.is_paused", "ymm4.scene_id"
            ]),
        ];

        private static string[] ExpectedStaticCandidates() =>
            Keywords
            .Concat(Globals)
            .Concat(Namespaces.SelectMany(n => n.Members))
            .Distinct()
            .OrderBy(x => x.Length)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        [Fact]
        public void StaticCandidates_MatchLegacySet()
        {
            Assert.Equal(ExpectedStaticCandidates(), LuaCompletionSource.StaticCandidates);
        }

        [Fact]
        public void Keywords_MatchLegacy()
        {
            Assert.Equal(Keywords, LuaCompletionSource.Keywords);
        }

        [Theory]
        [InlineData("obj")]
        [InlineData("math")]
        [InlineData("string")]
        [InlineData("table")]
        [InlineData("bit32")]
        [InlineData("scene")]
        [InlineData("anim")]
        [InlineData("ymm4")]
        public void TableMembers_MatchLegacyOrder(string prefix)
        {
            var expected = Namespaces.First(n => n.Prefix == prefix).Members;
            Assert.True(LuaCompletionSource.TryGetTableMembers(prefix, out var actual));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void UnknownTable_YieldsNoMembers()
        {
            Assert.False(LuaCompletionSource.TryGetTableMembers("nonexistent", out var members));
            Assert.Empty(members);
        }
    }
}
