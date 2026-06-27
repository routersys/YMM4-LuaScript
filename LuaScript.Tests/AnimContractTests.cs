namespace LuaScript.Tests
{
    public class AnimContractTests
    {
        private const int Precision = 9;
        private readonly MoonSharpLane _lane = new();

        private void Eq(double expected, string expression) =>
            Assert.Equal(expected, _lane.Number(expression), Precision);

        [Fact]
        public void Constants()
        {
            Eq(Math.PI * 2d, "anim.tau");
            Eq(Math.E, "anim.e");
            Eq((1d + Math.Sqrt(5d)) / 2d, "anim.phi");
            Eq(Math.Sqrt(2d), "anim.sqrt2");
        }

        [Fact]
        public void LerpMapNormClamp()
        {
            Eq(5d, "anim.lerp(0,10,0.5)");
            Eq(12.5d, "anim.lerp(10,20,0.25)");
            Eq(50d, "anim.map(5,0,10,0,100)");
            Eq(0.5d, "anim.norm(5,0,10)");
            Eq(1d, "anim.clamp(5,0,1)");
            Eq(0d, "anim.clamp(-1,0,1)");
            Eq(0.5d, "anim.clamp(0.5,0,1)");
        }

        [Fact]
        public void SmoothSteps()
        {
            Eq(0.5d, "anim.smoothstep(0,1,0.5)");
            Eq(0d, "anim.smoothstep(0,1,0)");
            Eq(1d, "anim.smoothstep(0,1,1)");
            Eq(0.5d, "anim.smootherstep(0,1,0.5)");
        }

        [Fact]
        public void WrapPingpong()
        {
            Eq(2d, "anim.wrap(7,0,5)");
            Eq(4d, "anim.wrap(-1,0,5)");
            Eq(3d, "anim.pingpong(7,5)");
            Eq(3d, "anim.pingpong(3,5)");
        }

        [Fact]
        public void Waves()
        {
            Eq(0.5d, "anim.oscillate(0,0,1,1)");
            Eq(0d, "anim.triangle(0,1)");
            Eq(0.5d, "anim.triangle(0.25,1)");
            Eq(1d, "anim.triangle(0.5,1)");
            Eq(0d, "anim.square(0.25,1)");
            Eq(1d, "anim.square(0.5,1)");
        }

        [Fact]
        public void Easings()
        {
            Eq(0.25d, "anim.ease_in(0.5)");
            Eq(0.75d, "anim.ease_out(0.5)");
            Eq(0.125d, "anim.ease_in_out(0.25)");
            Eq(0.875d, "anim.ease_in_out(0.75)");
            Eq(0d, "anim.back(0)");
            Eq(1d, "anim.back(1)");
            Eq(0d, "anim.elastic(0)");
            Eq(1d, "anim.elastic(1)");
            Eq(0d, "anim.bounce(0)");
        }

        [Fact]
        public void Misc()
        {
            Eq(-1d, "anim.sign(-5)");
            Eq(0d, "anim.sign(0)");
            Eq(1d, "anim.sign(3)");
            Eq(0.5d, "anim.duration(0.5,1)");
            Eq(1d, "anim.duration(2,1)");
            Eq(1d, "anim.delay(2,1)");
            Eq(0d, "anim.delay(0.5,1)");
            Eq(1d, "anim.step(0.5,0.6)");
            Eq(0d, "anim.step(0.5,0.4)");
            Eq(0.25d, "anim.fract(3.25)");
            Eq(0.75d, "anim.fract(-0.25)");
        }

        [Fact]
        public void Vectors()
        {
            Eq(5d, "anim.len(3,4)");
            Eq(5d, "anim.dist(0,0,3,4)");
            Eq(11d, "anim.dot(1,2,3,4)");
            Eq(0d, "anim.bezier(0,0,1,2,3)");
            Eq(3d, "anim.bezier(1,0,1,2,3)");
        }

        [Fact]
        public void TupleFunctions()
        {
            var rgb = _lane.Tuple("anim.hsv_to_rgb(0,1,1)", 3);
            Assert.Equal(255d, rgb[0], Precision);
            Assert.Equal(0d, rgb[1], Precision);
            Assert.Equal(0d, rgb[2], Precision);

            var hsv = _lane.Tuple("anim.rgb_to_hsv(255,0,0)", 3);
            Assert.Equal(0d, hsv[0], Precision);
            Assert.Equal(1d, hsv[1], Precision);
            Assert.Equal(1d, hsv[2], Precision);

            var n = _lane.Tuple("anim.normalize(3,4)", 2);
            Assert.Equal(0.6d, n[0], Precision);
            Assert.Equal(0.8d, n[1], Precision);

            var rot = _lane.Tuple("anim.rotate(1,0,90)", 2);
            Assert.Equal(0d, rot[0], Precision);
            Assert.Equal(1d, rot[1], Precision);
        }

        [Fact]
        public void RandIsDeterministicAndBounded()
        {
            Assert.Equal(_lane.Number("anim.rand(5)"), _lane.Number("anim.rand(5)"));
            Assert.NotEqual(_lane.Number("anim.rand(5)"), _lane.Number("anim.rand(6)"));
            for (int seed = 0; seed < 50; seed++)
            {
                double r = _lane.Number($"anim.rand(10,20,{seed})");
                Assert.InRange(r, 10d, 20d);
            }
        }

        [Fact]
        public void NoiseIsDeterministicAndBounded()
        {
            Assert.Equal(_lane.Number("anim.noise(1.5,2.5,0.5)"), _lane.Number("anim.noise(1.5,2.5,0.5)"));
            for (double x = 0d; x < 5d; x += 0.37d)
            {
                double v = _lane.Number($"anim.noise({x.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
                Assert.InRange(v, 0d, 1d);
            }
        }
    }
}
