using LuaScript.Compat;

namespace LuaScript.Tests
{
    public sealed class AviUtlAudioConverterTests
    {
        private static float[] Interleave(double[] left, double[] right)
        {
            var data = new float[left.Length * 2];
            for (int i = 0; i < left.Length; i++)
            {
                data[i * 2] = (float)left[i];
                data[i * 2 + 1] = (float)right[i];
            }
            return data;
        }

        [Fact]
        public void RequiredFrames_MatchesTypeWindows()
        {
            Assert.Equal(16, AviUtlAudioConverter.RequiredFrames("pcm", 16));
            Assert.Equal(AviUtlAudioConverter.MaxSamples, AviUtlAudioConverter.RequiredFrames("pcm", 0));
            Assert.Equal(AviUtlAudioConverter.MaxSamples, AviUtlAudioConverter.RequiredFrames("pcm", 99999));
            Assert.Equal(AviUtlAudioConverter.SpectrumWindow, AviUtlAudioConverter.RequiredFrames("spectrum", 32));
            Assert.Equal(AviUtlAudioConverter.FourierWindow, AviUtlAudioConverter.RequiredFrames("fourier", 0));
            Assert.Equal(AviUtlAudioConverter.FourierWindow, AviUtlAudioConverter.RequiredFrames("fourier.l", 0));
            Assert.Equal(0, AviUtlAudioConverter.RequiredFrames("unknown", 8));
        }

        [Fact]
        public void Pcm_ScalesMonoMixTo16BitRange()
        {
            var converter = new AviUtlAudioConverter();
            var data = Interleave([0.5d, -1d, 0d], [0.5d, -1d, 1d]);
            var dest = new double[AviUtlAudioConverter.MaxSamples];

            int count = converter.Convert("pcm", data, 3, 3, dest);

            Assert.Equal(3, count);
            Assert.Equal(16384d, dest[0]);
            Assert.Equal(-32768d, dest[1]);
            Assert.Equal(16384d, dest[2]);
        }

        [Fact]
        public void Pcm_SelectsChannelBySuffix()
        {
            var converter = new AviUtlAudioConverter();
            var data = Interleave([0.25d, 0.25d], [-0.5d, -0.5d]);
            var dest = new double[AviUtlAudioConverter.MaxSamples];

            converter.Convert("pcm.l", data, 2, 2, dest);
            Assert.Equal(8192d, dest[0]);

            converter.Convert("pcm.r", data, 2, 2, dest);
            Assert.Equal(-16384d, dest[0]);
        }

        [Fact]
        public void Pcm_ReturnsFewerWhenSourceIsShort()
        {
            var converter = new AviUtlAudioConverter();
            var data = Interleave([0.1d, 0.2d], [0.1d, 0.2d]);
            var dest = new double[AviUtlAudioConverter.MaxSamples];

            int count = converter.Convert("pcm", data, 2, 100, dest);

            Assert.Equal(2, count);
        }

        [Fact]
        public void Spectrum_PeaksInBandContainingSineFrequency()
        {
            var converter = new AviUtlAudioConverter();
            const int frames = AviUtlAudioConverter.SpectrumWindow;
            const int cycle = 64;
            var mono = new double[frames];
            for (int i = 0; i < frames; i++)
                mono[i] = Math.Sin(2d * Math.PI * i * cycle / frames);
            var data = Interleave(mono, mono);
            var dest = new double[AviUtlAudioConverter.MaxSamples];

            int count = converter.Convert("spectrum", data, frames, 32, dest);

            Assert.Equal(32, count);
            int expectedBand = cycle * 32 / AviUtlAudioConverter.SpectrumBins;
            int peakBand = 0;
            for (int i = 1; i < count; i++)
            {
                if (dest[i] > dest[peakBand])
                    peakBand = i;
            }
            Assert.Equal(expectedBand, peakBand);
            Assert.True(dest[peakBand] > 1000d);
        }

        [Fact]
        public void Fourier_ReturnsNormalizedMagnitudes()
        {
            var converter = new AviUtlAudioConverter();
            const int frames = AviUtlAudioConverter.FourierWindow;
            const int cycle = 8;
            var mono = new double[frames];
            for (int i = 0; i < frames; i++)
                mono[i] = Math.Sin(2d * Math.PI * i * cycle / frames);
            var data = Interleave(mono, mono);
            var dest = new double[AviUtlAudioConverter.MaxSamples];

            int count = converter.Convert("fourier", data, frames, 0, dest);

            Assert.Equal(AviUtlAudioConverter.MaxSamples, count);
            int peak = 0;
            for (int i = 1; i < count; i++)
            {
                if (dest[i] > dest[peak])
                    peak = i;
            }
            Assert.Equal(cycle - 1, peak);
            Assert.Equal(1d, dest[peak], 3);
        }

        [Fact]
        public void UnknownTypeOrEmptySource_YieldsZero()
        {
            var converter = new AviUtlAudioConverter();
            var dest = new double[AviUtlAudioConverter.MaxSamples];

            Assert.Equal(0, converter.Convert("waveform", new float[8], 4, 4, dest));
            Assert.Equal(0, converter.Convert("pcm", [], 0, 4, dest));
        }
    }
}
