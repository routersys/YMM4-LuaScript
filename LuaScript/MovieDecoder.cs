using System;
using System.IO;
using SharpGen.Runtime.Win32;
using Vortice.MediaFoundation;

namespace LuaScript
{
    internal sealed class MovieDecoder : IDisposable
    {
        private const int MaxDimension = 8192;
        private const int FirstVideoStream = -4;
        private const int AllStreams = -2;
        private const int EndOfStreamFlag = 0x2;

        private bool _mfStarted;
        private string? _path;
        private IMFSourceReader? _reader;
        private int _width;
        private int _height;
        private int _stride;

        public unsafe byte[] Decode(string path, double timeSeconds, out int width, out int height)
        {
            width = 1;
            height = 1;
            if (string.IsNullOrEmpty(path) || !IsSupportedVideo(path))
                return new byte[4];

            try
            {
                EnsureReader(path);
                if (_reader is null || _width <= 0 || _height <= 0)
                    return new byte[4];

                SetPosition(_reader, (long)(Math.Max(0d, timeSeconds) * 10_000_000d));

                using var sample = ReadVideoSample(_reader);
                if (sample is null)
                    return new byte[4];

                using var buffer = sample.ConvertToContiguousBuffer();
                buffer.Lock(out nint data, out _, out _);
                try
                {
                    int w = _width;
                    int h = _height;
                    int absStride = Math.Abs(_stride);
                    bool bottomUp = _stride < 0;
                    var result = new byte[w * h * 4];
                    byte* src = (byte*)data;
                    fixed (byte* dst = result)
                    {
                        for (int y = 0; y < h; y++)
                        {
                            int sourceRow = bottomUp ? h - 1 - y : y;
                            byte* s = src + sourceRow * absStride;
                            byte* d = dst + y * w * 4;
                            for (int x = 0; x < w; x++)
                            {
                                d[0] = s[0];
                                d[1] = s[1];
                                d[2] = s[2];
                                d[3] = 255;
                                s += 4;
                                d += 4;
                            }
                        }
                    }
                    width = w;
                    height = h;
                    return result;
                }
                finally
                {
                    buffer.Unlock();
                }
            }
            catch
            {
                width = 1;
                height = 1;
                return new byte[4];
            }
        }

        private void EnsureReader(string path)
        {
            if (_reader is not null && string.Equals(_path, path, StringComparison.Ordinal))
                return;

            DisposeReader();

            if (!_mfStarted)
            {
                MediaFactory.MFStartup(false);
                _mfStarted = true;
            }

            using var attributes = MediaFactory.MFCreateAttributes(1);
            attributes.Set(SourceReaderAttributeKeys.EnableAdvancedVideoProcessing, 1);
            var reader = MediaFactory.MFCreateSourceReaderFromURL(path, attributes);

            reader.SetStreamSelection(AllStreams, false);
            reader.SetStreamSelection(FirstVideoStream, true);

            using (var mediaType = MediaFactory.MFCreateMediaType())
            {
                mediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                mediaType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32);
                reader.SetCurrentMediaType(FirstVideoStream, mediaType);
            }

            using var current = reader.GetCurrentMediaType(FirstVideoStream);
            var mediaAttributes = (IMFAttributes)current;
            ulong frameSize = Convert.ToUInt64(mediaAttributes.Get(MediaTypeAttributeKeys.FrameSize));
            int w = (int)(frameSize >> 32);
            int h = (int)(frameSize & 0xFFFFFFFF);

            int stride;
            try { stride = Convert.ToInt32(mediaAttributes.Get(MediaTypeAttributeKeys.DefaultStride)); }
            catch { stride = w * 4; }
            if (stride == 0)
                stride = w * 4;

            if (w <= 0 || h <= 0 || w > MaxDimension || h > MaxDimension)
            {
                reader.Dispose();
                return;
            }

            _reader = reader;
            _path = path;
            _width = w;
            _height = h;
            _stride = stride;
        }

        private static void SetPosition(IMFSourceReader reader, long ticks)
        {
            var variant = new Variant
            {
                Value = ticks,
                ElementType = (VariantElementType)20,
            };
            reader.SetCurrentPosition(Guid.Empty, variant);
        }

        private static IMFSample? ReadVideoSample(IMFSourceReader reader)
        {
            for (int guard = 0; guard < 256; guard++)
            {
                reader.ReadSample(FirstVideoStream, 0, out _, out int flags, out _, out IMFSample? sample);
                if (sample is not null)
                    return sample;
                if ((flags & EndOfStreamFlag) != 0)
                    return null;
            }
            return null;
        }

        private static bool IsSupportedVideo(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;
                Span<byte> head = stackalloc byte[16];
                using var stream = File.OpenRead(path);
                int read = stream.Read(head);
                if (read < 12)
                    return false;

                if (head[4] == 0x66 && head[5] == 0x74 && head[6] == 0x79 && head[7] == 0x70)
                    return true;
                if (head[0] == 0x1A && head[1] == 0x45 && head[2] == 0xDF && head[3] == 0xA3)
                    return true;
                if (head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46 &&
                    head[8] == 0x41 && head[9] == 0x56 && head[10] == 0x49 && head[11] == 0x20)
                    return true;
                if (head[0] == 0x46 && head[1] == 0x4C && head[2] == 0x56)
                    return true;
                if (head[0] == 0x30 && head[1] == 0x26 && head[2] == 0xB2 && head[3] == 0x75)
                    return true;
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void DisposeReader()
        {
            _reader?.Dispose();
            _reader = null;
            _path = null;
            _width = 0;
            _height = 0;
            _stride = 0;
        }

        public void Dispose()
        {
            DisposeReader();
            if (_mfStarted)
            {
                try { MediaFactory.MFShutdown(); }
                catch { }
                _mfStarted = false;
            }
        }
    }
}
