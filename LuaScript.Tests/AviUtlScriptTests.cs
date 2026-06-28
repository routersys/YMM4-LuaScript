using LuaScript.Compat;
using MoonSharp.Interpreter;

namespace LuaScript.Tests
{
    public class AviUtlScriptTests
    {
        private static Script NewScript()
        {
            var script = new Script(
                CoreModules.Basic |
                CoreModules.Math |
                CoreModules.String |
                CoreModules.Table |
                CoreModules.Bit32 |
                CoreModules.TableIterators |
                CoreModules.Metatables |
                CoreModules.ErrorHandling);
            script.Globals["obj"] = new Table(script);
            return script;
        }

        private static double RunNumber(string transformed, string global = "result")
        {
            var script = NewScript();
            script.DoString(transformed);
            return script.Globals.Get(global).Number;
        }

        [Fact]
        public void PlainScript_IsReturnedUnchanged()
        {
            const string source = "obj.x = 1\nobj.y = 2";
            Assert.Equal(source, AviUtlScript.Transform(source));
        }

        [Fact]
        public void TrackOnlyHeader_IsReturnedUnchanged()
        {
            const string source = "--track0:Amp,0,100,10\nobj.ox = obj.track0";
            Assert.Equal(source, AviUtlScript.Transform(source));
        }

        [Fact]
        public void DirectiveComment_IsNotConfusedWithDirective()
        {
            const string source = "-- color reference note\nobj.x = 1";
            Assert.Equal(source, AviUtlScript.Transform(source));
        }

        [Fact]
        public void EngineDirective_IsPreserved()
        {
            const string source = "--!native\n--dialog:V,local v=3;\nresult = v";
            string transformed = AviUtlScript.Transform(source);
            Assert.Contains("--!native", transformed);
            Assert.Equal(3d, RunNumber(transformed));
        }

        [Fact]
        public void Dialog_InjectsDefaultDeclarations()
        {
            const string source = "--dialog:Angle,local angle=45;Dist,local dist=100;\nresult = dist";
            double result = RunNumber(AviUtlScript.Transform(source));
            Assert.Equal(100d, result);
        }

        [Fact]
        public void Color_IsNotInjected_SurfacedAsUiParameter()
        {
            const string source = "--color:0x010203\nresult = color";
            Assert.Equal(source, AviUtlScript.Transform(source));
        }

        [Fact]
        public void Check_IsNotInjected_SurfacedAsUiParameter()
        {
            const string source = "--check0:Enable,1\nresult = obj.check0";
            Assert.Equal(source, AviUtlScript.Transform(source));
        }

        [Fact]
        public void Param_InjectsRawLua()
        {
            const string source = "--param:result = 7\nlocal x = 1";
            double result = RunNumber(AviUtlScript.Transform(source));
            Assert.Equal(7d, result);
        }

        [Fact]
        public void Section_FirstSectionIsSelected()
        {
            const string source = "@First\n--dialog:A,local a=5;\nresult = a\n@Second\nresult = 999";
            double result = RunNumber(AviUtlScript.Transform(source));
            Assert.Equal(5d, result);
        }

        [Fact]
        public void Section_PreambleBeforeFirstHeaderIsDiscarded()
        {
            const string source = "result = 111\n@Only\nresult = 222";
            double result = RunNumber(AviUtlScript.Transform(source));
            Assert.Equal(222d, result);
        }

        [Fact]
        public void ClassicTransformScript_RunsAndMovesObject()
        {
            const string source =
                "--track0:Range,0,2000,10\n" +
                "--dialog:Seed,local seed=3;\n" +
                "obj.ox = obj.ox + obj.track0 * obj.rand(-100,100,seed,obj.time) / 100";

            var script = NewScript();
            var obj = script.Globals.Get("obj").Table;
            obj["ox"] = 0d;
            obj["track0"] = 500d;
            obj["time"] = 2d;
            obj["frame"] = 4d;
            obj["rand"] = DynValue.NewCallback((_, args) =>
            {
                double frameDefault = obj.Get("frame").CastToNumber() ?? 0d;
                double a = args.Count > 0 ? args[0].CastToNumber() ?? 0d : 0d;
                double b = args.Count > 1 ? args[1].CastToNumber() ?? 0d : 0d;
                double seed = args.Count > 2 ? args[2].CastToNumber() ?? 0d : 0d;
                double frame = args.Count > 3 ? args[3].CastToNumber() ?? frameDefault : frameDefault;
                return DynValue.NewNumber(AviUtlRandom.Next(a, b, seed, frame));
            });

            script.DoString(AviUtlScript.Transform(source));
            double moved = obj.Get("ox").Number;

            double expectedRand = AviUtlRandom.Next(-100, 100, 3, 2);
            Assert.Equal(500d * expectedRand / 100d, moved);
        }
    }
}
