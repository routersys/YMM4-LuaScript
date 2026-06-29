using System;
using System.Collections.Generic;
using LuaScript.Engine;
using LuaScript.Engine.Kernel;
using MoonSharp.Interpreter;

namespace LuaScript.Tests
{
    public sealed class KernelCpuEquivalenceTests
    {
        private const int Width = 23;
        private const int Height = 17;

        [Theory]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,r,g,b,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) local l=0.298912*r+0.586611*g+0.114478*b obj.setpixel(x,y,l,l,l,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,b,g,r,a) end end")]
        [InlineData("for x=0,obj.w-1 do for y=0,obj.h-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,255-r,255-g,255-b,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,r*r/255,g*g/255,b*b/255,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,255*math.pow(r/255,0.45),255*math.pow(g/255,0.45),255*math.pow(b/255,0.45),a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) local v=(r+g+b)/3 obj.setpixel(x,y,v>128 and 255 or 0,v>128 and 255 or 0,v>128 and 255 or 0,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,math.min(255,r+50),math.max(0,g-50),math.floor(b/16)*16,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,r%64,math.abs(g-128),math.sqrt(b*255),a) end end")]
        public void CpuKernelMatchesMoonSharp(string script)
        {
            var fields = new Dictionary<string, double> { ["w"] = Width, ["h"] = Height };
            AssertEquivalent(script, fields, new Dictionary<string, double>());
        }

        [Fact]
        public void CpuKernelMatchesMoonSharpWithUniforms()
        {
            const string script =
                "for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) " +
                "obj.setpixel(x,y,r*obj.track0/100,g*math.sin(time)*0.5+g*0.5,b+x/obj.w*obj.track1,a) end end";
            var fields = new Dictionary<string, double> { ["w"] = Width, ["h"] = Height, ["track0"] = 73d, ["track1"] = 40d };
            var globals = new Dictionary<string, double> { ["time"] = 1.2345d };
            AssertEquivalent(script, fields, globals);
        }

        [Theory]
        [InlineData("--!cpu\nfor y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,255-r,255-g,255-b,a) end end")]
        [InlineData("--!gpu\nfor y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) local l=0.298912*r+0.586611*g+0.114478*b obj.setpixel(x,y,l,l,l,a) end end")]
        [InlineData("--!cpu\r\nfor y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,x/obj.w*255,y/obj.h*255,b,a) end end")]
        public void DirectivePrefixedKernelsMatchMoonSharp(string script)
        {
            var fields = new Dictionary<string, double> { ["w"] = Width, ["h"] = Height };
            AssertEquivalent(script, fields, new Dictionary<string, double>());
        }

        [Theory]
        [InlineData("--!cpu", "Cpu")]
        [InlineData("--!gpu", "Gpu")]
        public void DirectivePrefixedKernelsRouteToAcceleratedLane(string directive, string expected)
        {
            string script = directive + "\nfor y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,r,g,b,a) end end";

            Assert.True(ScriptDirective.TryResolveExplicit(script, out var kind));
            Assert.Equal(expected, kind.ToString());
            Assert.NotNull(KernelExtractor.TryExtract(script));
        }

        [Theory]
        [InlineData("obj.setpixel(0,0,0,0,0,0)")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) total=total+r obj.setpixel(x,y,r,g,b,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x+1,y) obj.setpixel(x,y,r,g,b,a) end end")]
        [InlineData("for i=0,10 do end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) if r>128 then r=255 end obj.setpixel(x,y,r,g,b,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,obj.rand(0,255),g,b,a) end end")]
        public void NonKernelScriptsAreRejected(string script)
        {
            Assert.Null(KernelExtractor.TryExtract(script));
        }

        private static void AssertEquivalent(string script, Dictionary<string, double> fields, Dictionary<string, double> globals)
        {
            var program = KernelExtractor.TryExtract(script);
            Assert.NotNull(program);

            byte[] source = CreateBuffer(20260629);
            byte[] oracle = (byte[])source.Clone();
            byte[] candidate = (byte[])source.Clone();

            RunMoonSharp(script, oracle, fields, globals);

            double[] uniforms = new double[KernelUniforms.Count];
            foreach (var (name, value) in fields)
                if (KernelUniforms.TryResolveMember("obj", name, out var uniform))
                    uniforms[(int)uniform] = value;
            foreach (var (name, value) in globals)
                if (KernelUniforms.TryResolveGlobal(name, out var uniform))
                    uniforms[(int)uniform] = value;

            CpuKernelCompiler.Compile(program!).Execute(candidate, Width, Height, uniforms);

            Assert.Equal(oracle, candidate);
        }

        private static byte[] CreateBuffer(int seed)
        {
            var random = new Random(seed);
            var buffer = new byte[Width * Height * 4];
            random.NextBytes(buffer);
            return buffer;
        }

        private static void RunMoonSharp(string script, byte[] buffer, Dictionary<string, double> fields, Dictionary<string, double> globals)
        {
            var engine = new Script(CoreModules.Basic | CoreModules.Math | CoreModules.Bit32);
            var obj = new Table(engine);
            foreach (var (name, value) in fields)
                obj[name] = value;
            foreach (var (name, value) in globals)
                engine.Globals[name] = value;

            obj["getpixel"] = DynValue.NewCallback((_, args) =>
            {
                int x = (int)(args[0].CastToNumber() ?? 0d);
                int y = (int)(args[1].CastToNumber() ?? 0d);
                var (r, g, b, a) = GetPixel(buffer, x, y);
                return DynValue.NewTuple(
                    DynValue.NewNumber(r), DynValue.NewNumber(g), DynValue.NewNumber(b), DynValue.NewNumber(a));
            });

            obj["setpixel"] = DynValue.NewCallback((_, args) =>
            {
                int x = (int)(args[0].CastToNumber() ?? 0d);
                int y = (int)(args[1].CastToNumber() ?? 0d);
                double r = args[2].CastToNumber() ?? 0d;
                double g = args[3].CastToNumber() ?? 0d;
                double b = args[4].CastToNumber() ?? 0d;
                double a = args.Count > 5 ? args[5].CastToNumber() ?? 255d : 255d;
                SetPixel(buffer, x, y, r, g, b, a);
                return DynValue.Void;
            });

            engine.Globals["obj"] = obj;
            engine.DoString(script);
        }

        private static (double r, double g, double b, double a) GetPixel(byte[] buffer, int x, int y)
        {
            if ((uint)x >= Width || (uint)y >= Height)
                return (0d, 0d, 0d, 0d);

            int p = (y * Width + x) * 4;
            double alpha = buffer[p + 3];
            if (alpha <= 0d)
                return (0d, 0d, 0d, 0d);

            double scale = 255d / alpha;
            return (
                Math.Clamp(buffer[p + 2] * scale, 0d, 255d),
                Math.Clamp(buffer[p + 1] * scale, 0d, 255d),
                Math.Clamp(buffer[p] * scale, 0d, 255d),
                alpha);
        }

        private static void SetPixel(byte[] buffer, int x, int y, double r, double g, double b, double a)
        {
            if ((uint)x >= Width || (uint)y >= Height)
                return;

            int p = (y * Width + x) * 4;
            double k = Math.Clamp(a, 0d, 255d) / 255d;
            buffer[p] = (byte)Math.Clamp(b * k, 0d, 255d);
            buffer[p + 1] = (byte)Math.Clamp(g * k, 0d, 255d);
            buffer[p + 2] = (byte)Math.Clamp(r * k, 0d, 255d);
            buffer[p + 3] = (byte)Math.Clamp(a, 0d, 255d);
        }
    }
}
