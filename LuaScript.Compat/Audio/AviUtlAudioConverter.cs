using System;

namespace LuaScript.Compat
{
    internal sealed class AviUtlAudioConverter
    {
        public const int MaxSamples = 1024;
        public const int SpectrumWindow = 1024;
        public const int FourierWindow = 2048;
        public const int SpectrumBins = SpectrumWindow / 2;

        private const double PcmScale = 32768d;

        private readonly double[] _mono = new double[FourierWindow];
        private readonly double[] _real = new double[FourierWindow];
        private readonly double[] _imag = new double[FourierWindow];

        private enum DataKind
        {
            Unknown,
            Pcm,
            Spectrum,
            Fourier,
        }

        public static int RequiredFrames(string type, int size)
        {
            var (kind, _) = Parse(type);
            return kind switch
            {
                DataKind.Pcm => ResolveCount(kind, size),
                DataKind.Spectrum => SpectrumWindow,
                DataKind.Fourier => FourierWindow,
                _ => 0,
            };
        }

        public int Convert(string type, ReadOnlySpan<float> interleaved, int frames, int size, Span<double> destination)
        {
            var (kind, channel) = Parse(type);
            if (kind == DataKind.Unknown || frames <= 0)
                return 0;

            int window = kind switch
            {
                DataKind.Pcm => Math.Min(frames, ResolveCount(kind, size)),
                DataKind.Spectrum => SpectrumWindow,
                _ => FourierWindow,
            };
            FillMono(interleaved, frames, channel, window);

            switch (kind)
            {
                case DataKind.Pcm:
                {
                    int count = Math.Min(ResolveCount(kind, size), frames);
                    for (int i = 0; i < count; i++)
                        destination[i] = Math.Clamp(_mono[i] * PcmScale, -32768d, 32767d);
                    return count;
                }
                case DataKind.Spectrum:
                {
                    int bands = ResolveCount(kind, size);
                    Fft(SpectrumWindow);
                    double scale = 2d / SpectrumWindow * PcmScale;
                    for (int b = 0; b < bands; b++)
                    {
                        int start = b * SpectrumBins / bands;
                        int end = (b + 1) * SpectrumBins / bands;
                        if (end <= start)
                            end = start + 1;
                        double sum = 0d;
                        for (int bin = start; bin < end; bin++)
                            sum += Magnitude(bin);
                        destination[b] = sum / (end - start) * scale;
                    }
                    return bands;
                }
                case DataKind.Fourier:
                {
                    int count = ResolveCount(kind, size);
                    Fft(FourierWindow);
                    double scale = 2d / FourierWindow;
                    for (int i = 0; i < count; i++)
                        destination[i] = Magnitude(i + 1) * scale;
                    return count;
                }
                default:
                    return 0;
            }
        }

        private static int ResolveCount(DataKind kind, int size)
        {
            int fallback = kind == DataKind.Spectrum ? 32 : MaxSamples;
            int max = kind == DataKind.Spectrum ? SpectrumBins : MaxSamples;
            return size <= 0 ? Math.Min(fallback, max) : Math.Clamp(size, 1, max);
        }

        private static (DataKind Kind, int Channel) Parse(string type)
        {
            int channel = 0;
            int length = type.Length;
            if (length >= 2 && type[length - 2] == '.')
            {
                char suffix = type[length - 1];
                if (suffix == 'l')
                    channel = 1;
                else if (suffix == 'r')
                    channel = 2;
                if (channel != 0)
                    length -= 2;
            }

            var name = type.AsSpan(0, length);
            DataKind kind = DataKind.Unknown;
            if (name.SequenceEqual("pcm"))
                kind = DataKind.Pcm;
            else if (name.SequenceEqual("spectrum"))
                kind = DataKind.Spectrum;
            else if (name.SequenceEqual("fourier"))
                kind = DataKind.Fourier;
            return (kind, channel);
        }

        private void FillMono(ReadOnlySpan<float> interleaved, int frames, int channel, int window)
        {
            int available = Math.Min(frames, window);
            for (int i = 0; i < available; i++)
            {
                float left = interleaved[i * 2];
                float right = interleaved[i * 2 + 1];
                _mono[i] = channel switch
                {
                    1 => left,
                    2 => right,
                    _ => (left + right) * 0.5d,
                };
            }
            for (int i = available; i < window; i++)
                _mono[i] = 0d;
        }

        private double Magnitude(int bin) =>
            Math.Sqrt(_real[bin] * _real[bin] + _imag[bin] * _imag[bin]);

        private void Fft(int n)
        {
            for (int i = 0; i < n; i++)
            {
                _real[i] = _mono[i];
                _imag[i] = 0d;
            }

            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                    j ^= bit;
                j |= bit;
                if (i < j)
                {
                    (_real[i], _real[j]) = (_real[j], _real[i]);
                    (_imag[i], _imag[j]) = (_imag[j], _imag[i]);
                }
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = -2d * Math.PI / len;
                double wRe = Math.Cos(angle);
                double wIm = Math.Sin(angle);
                for (int i = 0; i < n; i += len)
                {
                    double curRe = 1d;
                    double curIm = 0d;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k;
                        int b = i + k + len / 2;
                        double tRe = _real[b] * curRe - _imag[b] * curIm;
                        double tIm = _real[b] * curIm + _imag[b] * curRe;
                        _real[b] = _real[a] - tRe;
                        _imag[b] = _imag[a] - tIm;
                        _real[a] += tRe;
                        _imag[a] += tIm;
                        double nextRe = curRe * wRe - curIm * wIm;
                        curIm = curRe * wIm + curIm * wRe;
                        curRe = nextRe;
                    }
                }
            }
        }
    }
}
