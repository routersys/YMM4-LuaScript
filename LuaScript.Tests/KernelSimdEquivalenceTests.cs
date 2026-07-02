using System;
using System.Collections.Generic;
using LuaScript.Engine.Kernel;

namespace LuaScript.Tests
{
    public sealed class KernelSimdEquivalenceTests
    {
        private const int Width = 24;
        private const int Height = 17;

        [Theory]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,r,g,b,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,255-r,255-g,255-b,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,b,g,r,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) local l=0.298912*r+0.586611*g+0.114478*b obj.setpixel(x,y,l,l,l,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,r*r/255,g*g/255,b*b/255,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,x/obj.w*255,y/obj.h*255,b,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) local v=(r+g+b)/3 obj.setpixel(x,y,v>128 and 255 or 0,v>128 and 255 or 0,v>128 and 255 or 0,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,r%64,math.abs(g-128),math.sqrt(b*255),a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,math.floor(r/16)*16,math.ceil(g/16)*16,-b+255,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,r*obj.track0/100,g+x/obj.w*obj.track1,b,a) end end")]
        [InlineData("local pd=obj.getpixeldata() for y=0,pd.height-1 do for x=0,pd.width-1 do local i=(y*pd.width+x)*4 local r=pd:get(i+1) local g=pd:get(i+2) local b=pd:get(i+3) pd:set(i+1,255-r) pd:set(i+2,g*g/255) pd:set(i+3,b) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,((r>128 and g<64) or (not (b>200))) and 255 or 0,g,b,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,(r>=200) and 255 or 0,(g<=50) and 255 or 0,(b==0) and 255 or ((b~=0) and 100 or 0),a) end end")]
        public void SimdBlockMatchesScalarRow(string script)
        {
            var program = KernelExtractor.TryExtract(script);
            Assert.NotNull(program);

            var block = SimdKernelCompiler.TryCompile(program!);
            Assert.NotNull(block);

            var scalarRow = CpuKernelCompiler.CompileRow(program!);
            double[] uniforms = BuildUniforms();

            byte[] source = CreateBuffer();
            byte[] scalar = (byte[])source.Clone();
            byte[] simd = (byte[])source.Clone();

            int stride = Width * 4;
            for (int y = 0; y < Height; y++)
            {
                scalarRow(scalar, y * stride, 0, Width, y, uniforms);
                block!(simd, y * stride, 0, Width, y, uniforms);
            }

            Assert.Equal(scalar, simd);
        }

        [Theory]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,math.min(255,r+50),math.max(0,g-50),b,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,255*math.pow(r/255,0.45),g,b,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,r^2/255,g,b,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,255*math.sin(r/255),g,b,a) end end")]
        [InlineData("for y=0,obj.h-1 do for x=0,obj.w-1 do local r,g,b,a=obj.getpixel(x,y) obj.setpixel(x,y,math.atan2(r,g),b,a,a) end end")]
        public void SimdRejectsNonVectorizableKernels(string script)
        {
            var program = KernelExtractor.TryExtract(script);
            Assert.NotNull(program);
            Assert.Null(SimdKernelCompiler.TryCompile(program!));
        }

        private static double[] BuildUniforms()
        {
            double[] uniforms = new double[KernelUniforms.Count];
            var fields = new Dictionary<string, double> { ["w"] = Width, ["h"] = Height, ["track0"] = 73d, ["track1"] = 40d };
            foreach (var (name, value) in fields)
                if (KernelUniforms.TryResolveMember("obj", name, out var uniform))
                    uniforms[(int)uniform] = value;
            return uniforms;
        }

        private static byte[] CreateBuffer()
        {
            var random = new Random(20260702);
            var buffer = new byte[Width * Height * 4];
            random.NextBytes(buffer);

            void SetPixel(int i, byte b, byte g, byte r, byte a)
            {
                buffer[i * 4 + 0] = b; buffer[i * 4 + 1] = g; buffer[i * 4 + 2] = r; buffer[i * 4 + 3] = a;
            }

            SetPixel(0, 0, 0, 0, 0);
            SetPixel(1, 255, 255, 255, 255);
            SetPixel(2, 1, 0, 0, 1);
            SetPixel(3, 200, 128, 64, 254);
            SetPixel(4, 0, 0, 0, 128);
            SetPixel(5, 255, 255, 255, 2);
            return buffer;
        }
    }
}
