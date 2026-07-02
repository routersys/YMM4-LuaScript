using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;

namespace LuaScript.Engine
{
    internal sealed class LuaJitWorker : IDisposable
    {
        private readonly string _luajitPath;
        private readonly string _workerScript;
        private readonly string _shimScript;
        private readonly string _nativeDir;

        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _view;
        private EventWaitHandle? _workEvent;
        private EventWaitHandle? _doneEvent;
        private Process? _process;
        private int _stringParamsCapacity = NativeProtocol.MinStringParamsCapacity;
        private long _pixelOffset = NativeProtocol.PixelOffset(NativeProtocol.MinStringParamsCapacity);
        private long _allocatedSize;
        private bool _alive;

        private readonly byte[] _callbackTag = new byte[NativeProtocol.CallbackTagMax];
        private readonly byte[] _lastCallbackTag = new byte[NativeProtocol.CallbackTagMax];
        private readonly double[] _anchorBuffer = new double[Anchor.AnchorSupport.MaxAnchors * 3];
        private int _lastCallbackTagLength = -1;
        private string _lastCallbackTagText = string.Empty;
        private int _scriptVersion;
        private string? _lastScript;
        private byte[] _lastScriptBytes = Array.Empty<byte>();
        private bool _scriptWritten;
        private readonly Dictionary<string, byte[]> _stringNameBytes = new(StringComparer.Ordinal);
        private byte[] _stringValueBytes = new byte[256];

        public LuaJitWorker(string nativeDir)
        {
            _nativeDir = nativeDir;
            _luajitPath = Path.Combine(nativeDir, "luajit.exe");
            _workerScript = Path.Combine(nativeDir, "worker.lua");
            _shimScript = Path.Combine(nativeDir, "shim.lua");
        }

        public static bool IsAvailable(string nativeDir) =>
            File.Exists(Path.Combine(nativeDir, "luajit.exe"));

        public bool Execute(
            string script,
            double[] fields,
            IReadOnlyDictionary<string, string> stringParameters,
            PixelRegionAccess loadPixels,
            int width,
            int height,
            int timeoutMs,
            Func<string, int, SceneObjectInfo?> resolveObject,
            Func<string, int, double, double, double, (byte[] buffer, int w, int h)> loadFigure,
            Func<string, string, double, bool, bool, int, (byte[] buffer, int w, int h)> loadText,
            Func<string, (byte[] buffer, int w, int h)> loadImage,
            Func<string, double, (byte[] buffer, int w, int h)> loadMovie,
            Action<string, IReadOnlyList<KeyValuePair<string, object>>> addEffect,
            Action<DrawCommand> addDraw,
            Action<string, int, bool, int, double[]> setAnchor,
            out bool pixelsDirty,
            out bool bufferReplaced,
            out int resultWidth,
            out int resultHeight,
            out string? error)
        {
            pixelsDirty = false;
            bufferReplaced = false;
            resultWidth = width;
            resultHeight = height;
            error = null;

            bool scriptChanged = !ReferenceEquals(script, _lastScript);
            if (scriptChanged)
            {
                byte[] scriptBytes = Encoding.UTF8.GetBytes(script);
                if (scriptBytes.Length > NativeProtocol.ScriptMax)
                {
                    error = "script too large for native lane";
                    return false;
                }
                _lastScript = script;
                _lastScriptBytes = scriptBytes;
                _scriptVersion++;
            }

            int stringCapacity = ResolveStringCapacity(_stringParamsCapacity, MeasureStringParameters(stringParameters));
            EnsureWorker(width, height, stringCapacity);
            var view = _view!;

            view.WriteArray(NativeProtocol.FieldsOffset, fields, 0, NativeProtocol.FieldCount);
            WriteStringParameters(view, stringParameters);
            view.Write(NativeProtocol.OffWidth, width);
            view.Write(NativeProtocol.OffHeight, height);
            if (scriptChanged || !_scriptWritten)
            {
                view.Write(NativeProtocol.OffScriptLen, _lastScriptBytes.Length);
                view.WriteArray(NativeProtocol.ScriptOffset, _lastScriptBytes, 0, _lastScriptBytes.Length);
                _scriptWritten = true;
            }
            view.Write(NativeProtocol.OffScriptVersion, _scriptVersion);
            view.Write(NativeProtocol.OffPixelsDirty, 0);
            view.Write(NativeProtocol.OffErrorLen, 0);
            view.Write(NativeProtocol.OffStatus, NativeProtocol.StatusIdle);
            view.Write(NativeProtocol.OffCommand, NativeProtocol.CmdRun);

            _workEvent!.Set();

            var stopwatch = Stopwatch.StartNew();
            int status;
            while (true)
            {
                long remaining = timeoutMs - stopwatch.ElapsedMilliseconds;
                if (remaining <= 0 || !_doneEvent!.WaitOne((int)remaining))
                {
                    KillWorker();
                    error = "native script execution timed out";
                    return false;
                }

                status = view.ReadInt32(NativeProtocol.OffStatus);
                if (status != NativeProtocol.StatusCallback)
                    break;

                int callbackKind = view.ReadInt32(NativeProtocol.OffCallbackKind);
                if (callbackKind == NativeProtocol.CbKindRequestPixels)
                {
                    try
                    {
                        AccessPixelRegion(loadPixels);
                    }
                    catch
                    {
                        KillWorker();
                        throw;
                    }
                }
                else if (callbackKind == NativeProtocol.CbKindFlushDraws)
                {
                    DrainDrawRing(view, addDraw);
                }
                else
                {
                    DispatchCallback(view, resolveObject, loadFigure, loadText, loadImage, loadMovie, addEffect, setAnchor);
                }
                _workEvent.Set();
            }

            if (status == NativeProtocol.StatusError)
            {
                int len = view.ReadInt32(NativeProtocol.OffErrorLen);
                var buffer = new byte[Math.Clamp(len, 0, NativeProtocol.ErrorMax)];
                view.ReadArray(NativeProtocol.ErrorOffset, buffer, 0, buffer.Length);
                error = Encoding.UTF8.GetString(buffer);
                return false;
            }

            DrainDrawRing(view, addDraw);

            for (int i = NativeProtocol.FirstWritableField; i <= NativeProtocol.LastWritableField; i++)
                fields[i] = view.ReadDouble(NativeProtocol.FieldsOffset + i * 8);

            fields[NativeProtocol.DrawState] = view.ReadDouble(NativeProtocol.FieldsOffset + NativeProtocol.DrawState * 8);

            resultWidth = view.ReadInt32(NativeProtocol.OffWidth);
            resultHeight = view.ReadInt32(NativeProtocol.OffHeight);
            bufferReplaced = resultWidth != width || resultHeight != height;

            pixelsDirty = view.ReadInt32(NativeProtocol.OffPixelsDirty) != 0;
            if (pixelsDirty && (long)resultWidth * resultHeight * 4 > _allocatedSize - _pixelOffset)
            {
                error = "native worker reported a pixel buffer beyond capacity";
                return false;
            }

            return true;
        }

        public void ReadPixels(PixelRegionAccess reader) => AccessPixelRegion(reader);

        private unsafe void AccessPixelRegion(PixelRegionAccess access)
        {
            var handle = _view!.SafeMemoryMappedViewHandle;
            byte* ptr = null;
            handle.AcquirePointer(ref ptr);
            try
            {
                long offset = _view.PointerOffset + _pixelOffset;
                access((nint)(ptr + offset), (long)handle.ByteLength - offset);
            }
            finally
            {
                handle.ReleasePointer();
            }
        }

        private unsafe void WritePixelRegion(byte[] source, int length)
        {
            var handle = _view!.SafeMemoryMappedViewHandle;
            byte* ptr = null;
            handle.AcquirePointer(ref ptr);
            try
            {
                fixed (byte* src = source)
                    Buffer.MemoryCopy(src, ptr + _view.PointerOffset + _pixelOffset, handle.ByteLength - (ulong)(_view.PointerOffset + _pixelOffset), (ulong)length);
            }
            finally
            {
                handle.ReleasePointer();
            }
        }

        private static int MeasureStringParameters(IReadOnlyDictionary<string, string> stringParameters)
        {
            int total = 4;
            foreach (var pair in stringParameters)
                total += 8 + Encoding.UTF8.GetByteCount(pair.Key) + Encoding.UTF8.GetByteCount(pair.Value ?? string.Empty);
            return total;
        }

        private static int ResolveStringCapacity(int current, int required)
        {
            int capacity = current;
            while (capacity < required)
            {
                if (capacity > int.MaxValue / 2)
                {
                    capacity = required;
                    break;
                }
                capacity *= 2;
            }
            return capacity;
        }

        private void WriteStringParameters(MemoryMappedViewAccessor view, IReadOnlyDictionary<string, string> stringParameters)
        {
            long basePos = NativeProtocol.StringParamsOffset;
            long limit = basePos + _stringParamsCapacity;
            long pos = basePos + 4;
            int count = 0;
            foreach (var pair in stringParameters)
            {
                if (!_stringNameBytes.TryGetValue(pair.Key, out var name))
                {
                    name = Encoding.UTF8.GetBytes(pair.Key);
                    _stringNameBytes[pair.Key] = name;
                }
                string value = pair.Value ?? string.Empty;
                int maxValueBytes = Encoding.UTF8.GetMaxByteCount(value.Length);
                if (_stringValueBytes.Length < maxValueBytes)
                    _stringValueBytes = new byte[Math.Max(maxValueBytes, _stringValueBytes.Length * 2)];
                int valueLen = Encoding.UTF8.GetBytes(value, 0, value.Length, _stringValueBytes, 0);
                if (pos + 8 + name.Length + valueLen > limit)
                    break;
                view.Write(pos, name.Length); pos += 4;
                view.WriteArray(pos, name, 0, name.Length); pos += name.Length;
                view.Write(pos, valueLen); pos += 4;
                view.WriteArray(pos, _stringValueBytes, 0, valueLen); pos += valueLen;
                count++;
            }
            view.Write(basePos, count);
            view.Write(NativeProtocol.OffStringParamsLen, (int)(pos - basePos));
        }

        private void DispatchCallback(
            MemoryMappedViewAccessor view,
            Func<string, int, SceneObjectInfo?> resolveObject,
            Func<string, int, double, double, double, (byte[] buffer, int w, int h)> loadFigure,
            Func<string, string, double, bool, bool, int, (byte[] buffer, int w, int h)> loadText,
            Func<string, (byte[] buffer, int w, int h)> loadImage,
            Func<string, double, (byte[] buffer, int w, int h)> loadMovie,
            Action<string, IReadOnlyList<KeyValuePair<string, object>>> addEffect,
            Action<string, int, bool, int, double[]> setAnchor)
        {
            int kind = view.ReadInt32(NativeProtocol.OffCallbackKind);
            switch (kind)
            {
                case NativeProtocol.CbKindGetObject:
                    ResolveGetObjectCallback(view, resolveObject);
                    break;
                case NativeProtocol.CbKindLoadFigure:
                    ResolveLoadFigureCallback(view, loadFigure);
                    break;
                case NativeProtocol.CbKindLoadText:
                    ResolveLoadTextCallback(view, loadText);
                    break;
                case NativeProtocol.CbKindLoadImage:
                    ResolveLoadImageCallback(view, loadImage);
                    break;
                case NativeProtocol.CbKindLoadMovie:
                    ResolveLoadMovieCallback(view, loadMovie);
                    break;
                case NativeProtocol.CbKindEffect:
                    ResolveEffectCallback(view, addEffect);
                    break;
                case NativeProtocol.CbKindSetAnchor:
                    ResolveSetAnchorCallback(view, setAnchor);
                    break;
            }
        }

        private void ResolveSetAnchorCallback(MemoryMappedViewAccessor view, Action<string, int, bool, int, double[]> setAnchor)
        {
            int tagLen = Math.Clamp(view.ReadInt32(NativeProtocol.OffCallbackTagLen), 0, NativeProtocol.CallbackTagMax);
            view.ReadArray(NativeProtocol.CallbackTagOffset, _callbackTag, 0, tagLen);
            string group = Encoding.UTF8.GetString(_callbackTag, 0, tagLen);

            long rOff = NativeProtocol.CallbackResultOffset;
            int count = Math.Clamp((int)view.ReadDouble(rOff + 0 * 8), 0, Anchor.AnchorSupport.MaxAnchors);
            bool is3D = view.ReadDouble(rOff + 1 * 8) != 0d;
            int connection = (int)view.ReadDouble(rOff + 2 * 8);

            Array.Clear(_anchorBuffer, 0, _anchorBuffer.Length);
            try { setAnchor(group, count, is3D, connection, _anchorBuffer); }
            catch { }

            int stride = is3D ? 3 : 2;
            for (int i = 0; i < count * stride; i++)
                view.Write(NativeProtocol.CallbackTagOffset + i * 8, _anchorBuffer[i]);
            view.Write(NativeProtocol.OffCallbackFound, 1);
        }

        private void DrainDrawRing(MemoryMappedViewAccessor view, Action<DrawCommand> addDraw)
        {
            long ringBase = NativeProtocol.DrawRingOffset(_stringParamsCapacity);
            int count = Math.Clamp((int)view.ReadDouble(ringBase), 0, NativeProtocol.DrawRingCapacity);
            for (int k = 0; k < count; k++)
            {
                long entry = ringBase + (1 + (long)k * NativeProtocol.DrawEntryDoubles) * 8;
                int kind = (int)view.ReadDouble(entry);
                long payload = entry + 8;
                if (kind == NativeProtocol.CbKindDrawPoly)
                    DispatchRingDrawPoly(view, payload, addDraw);
                else
                    DispatchRingDraw(view, payload, addDraw);
            }
            view.Write(ringBase, 0d);
        }

        private static void DispatchRingDraw(MemoryMappedViewAccessor view, long payload, Action<DrawCommand> addDraw)
        {
            double ox = view.ReadDouble(payload + 0 * 8);
            double oy = view.ReadDouble(payload + 1 * 8);
            double oz = view.ReadDouble(payload + 2 * 8);
            double zoom = view.ReadDouble(payload + 3 * 8);
            double alpha = view.ReadDouble(payload + 4 * 8);
            double aspect = view.ReadDouble(payload + 5 * 8);
            double antialias = view.ReadDouble(payload + 6 * 8);
            double blend = view.ReadDouble(payload + 7 * 8);

            try { addDraw(new DrawCommand(ox, oy, oz, zoom, alpha, aspect, null, antialias, blend)); }
            catch { }
        }

        private static void DispatchRingDrawPoly(MemoryMappedViewAccessor view, long payload, Action<DrawCommand> addDraw)
        {
            var poly = new double[DrawPolyMath.Length];
            for (int i = 0; i < DrawPolyMath.Length; i++)
                poly[i] = view.ReadDouble(payload + i * 8);
            double antialias = view.ReadDouble(payload + DrawPolyMath.Length * 8);
            double blend = view.ReadDouble(payload + (DrawPolyMath.Length + 1) * 8);

            try { addDraw(new DrawCommand(0d, 0d, 0d, 1d, poly[20], 0d, poly, antialias, blend)); }
            catch { }
        }

        private void ResolveGetObjectCallback(MemoryMappedViewAccessor view, Func<string, int, SceneObjectInfo?> resolveObject)
        {
            int frame = view.ReadInt32(NativeProtocol.OffCallbackFrame);
            int tagLen = Math.Clamp(view.ReadInt32(NativeProtocol.OffCallbackTagLen), 0, NativeProtocol.CallbackTagMax);
            view.ReadArray(NativeProtocol.CallbackTagOffset, _callbackTag, 0, tagLen);
            string tag = ResolveTag(tagLen);

            SceneObjectInfo? info;
            try { info = resolveObject(tag, frame); }
            catch { info = null; }

            if (info is { } value)
            {
                long result = NativeProtocol.CallbackResultOffset;
                view.Write(result + NativeProtocol.CbExist * 8, value.Exist ? 1d : 0d);
                view.Write(result + NativeProtocol.CbX * 8, value.X);
                view.Write(result + NativeProtocol.CbY * 8, value.Y);
                view.Write(result + NativeProtocol.CbZ * 8, value.Z);
                view.Write(result + NativeProtocol.CbZoom * 8, value.Zoom);
                view.Write(result + NativeProtocol.CbRz * 8, value.Rz);
                view.Write(result + NativeProtocol.CbAlpha * 8, value.Alpha);
                view.Write(result + NativeProtocol.CbLayer * 8, (double)value.Layer);
                view.Write(NativeProtocol.OffCallbackFound, 1);
            }
            else
            {
                view.Write(NativeProtocol.OffCallbackFound, 0);
            }
        }

        private void ResolveLoadFigureCallback(
            MemoryMappedViewAccessor view,
            Func<string, int, double, double, double, (byte[] buffer, int w, int h)> loadFigure)
        {
            int tagLen = Math.Clamp(view.ReadInt32(NativeProtocol.OffCallbackTagLen), 0, NativeProtocol.CallbackTagMax);
            view.ReadArray(NativeProtocol.CallbackTagOffset, _callbackTag, 0, tagLen);
            string name = Encoding.UTF8.GetString(_callbackTag, 0, tagLen);

            long rOff = NativeProtocol.CallbackResultOffset;
            int color = (int)view.ReadDouble(rOff + 0 * 8);
            double size = view.ReadDouble(rOff + 1 * 8);
            double lineWidth = view.ReadDouble(rOff + 2 * 8);
            double aspect = view.ReadDouble(rOff + 3 * 8);

            (byte[] buffer, int w, int h) result;
            try { result = loadFigure(name, color, size, lineWidth, aspect); }
            catch { result = (new byte[4], 1, 1); }

            int pixelSize = result.w * result.h * 4;
            long capacity = view.Capacity - _pixelOffset;
            if (pixelSize > capacity)
            {
                result = (new byte[4], 1, 1);
                pixelSize = 4;
            }

            view.Write(NativeProtocol.OffLoadResultWidth, result.w);
            view.Write(NativeProtocol.OffLoadResultHeight, result.h);
            WritePixelRegion(result.buffer, pixelSize);
            view.Write(NativeProtocol.OffCallbackFound, 1);
        }

        private void ResolveLoadTextCallback(
            MemoryMappedViewAccessor view,
            Func<string, string, double, bool, bool, int, (byte[] buffer, int w, int h)> loadText)
        {
            int tagLen = Math.Clamp(view.ReadInt32(NativeProtocol.OffCallbackTagLen), 0, NativeProtocol.CallbackTagMax);
            view.ReadArray(NativeProtocol.CallbackTagOffset, _callbackTag, 0, tagLen);
            string payload = Encoding.UTF8.GetString(_callbackTag, 0, tagLen);
            int separator = payload.IndexOf('\0');
            string family = separator >= 0 ? payload[..separator] : string.Empty;
            string text = separator >= 0 ? payload[(separator + 1)..] : payload;

            long rOff = NativeProtocol.CallbackResultOffset;
            double size = view.ReadDouble(rOff + 0 * 8);
            bool bold = view.ReadDouble(rOff + 1 * 8) != 0;
            bool italic = view.ReadDouble(rOff + 2 * 8) != 0;
            int color = (int)view.ReadDouble(rOff + 3 * 8);

            (byte[] buffer, int w, int h) result;
            try { result = loadText(family, text, size, bold, italic, color); }
            catch { result = (new byte[4], 1, 1); }

            int pixelSize = result.w * result.h * 4;
            long capacity = view.Capacity - _pixelOffset;
            if (pixelSize > capacity)
            {
                result = (new byte[4], 1, 1);
                pixelSize = 4;
            }

            view.Write(NativeProtocol.OffLoadResultWidth, result.w);
            view.Write(NativeProtocol.OffLoadResultHeight, result.h);
            WritePixelRegion(result.buffer, pixelSize);
            view.Write(NativeProtocol.OffCallbackFound, 1);
        }

        private void ResolveLoadImageCallback(
            MemoryMappedViewAccessor view,
            Func<string, (byte[] buffer, int w, int h)> loadImage)
        {
            int tagLen = Math.Clamp(view.ReadInt32(NativeProtocol.OffCallbackTagLen), 0, NativeProtocol.CallbackTagMax);
            view.ReadArray(NativeProtocol.CallbackTagOffset, _callbackTag, 0, tagLen);
            string path = Encoding.UTF8.GetString(_callbackTag, 0, tagLen);

            (byte[] buffer, int w, int h) result;
            try { result = loadImage(path); }
            catch { result = (new byte[4], 1, 1); }

            int pixelSize = result.w * result.h * 4;
            long capacity = view.Capacity - _pixelOffset;
            if (pixelSize > capacity)
            {
                result = (new byte[4], 1, 1);
                pixelSize = 4;
            }

            view.Write(NativeProtocol.OffLoadResultWidth, result.w);
            view.Write(NativeProtocol.OffLoadResultHeight, result.h);
            WritePixelRegion(result.buffer, pixelSize);
            view.Write(NativeProtocol.OffCallbackFound, 1);
        }

        private void ResolveLoadMovieCallback(
            MemoryMappedViewAccessor view,
            Func<string, double, (byte[] buffer, int w, int h)> loadMovie)
        {
            int tagLen = Math.Clamp(view.ReadInt32(NativeProtocol.OffCallbackTagLen), 0, NativeProtocol.CallbackTagMax);
            view.ReadArray(NativeProtocol.CallbackTagOffset, _callbackTag, 0, tagLen);
            string path = Encoding.UTF8.GetString(_callbackTag, 0, tagLen);
            double time = view.ReadDouble(NativeProtocol.CallbackResultOffset);

            (byte[] buffer, int w, int h) result;
            try { result = loadMovie(path, time); }
            catch { result = (new byte[4], 1, 1); }

            int pixelSize = result.w * result.h * 4;
            long capacity = view.Capacity - _pixelOffset;
            if (pixelSize > capacity)
            {
                result = (new byte[4], 1, 1);
                pixelSize = 4;
            }

            view.Write(NativeProtocol.OffLoadResultWidth, result.w);
            view.Write(NativeProtocol.OffLoadResultHeight, result.h);
            WritePixelRegion(result.buffer, pixelSize);
            view.Write(NativeProtocol.OffCallbackFound, 1);
        }

        private void ResolveEffectCallback(MemoryMappedViewAccessor view, Action<string, IReadOnlyList<KeyValuePair<string, object>>> addEffect)
        {
            int tagLen = Math.Clamp(view.ReadInt32(NativeProtocol.OffCallbackTagLen), 0, NativeProtocol.CallbackTagMax);
            view.ReadArray(NativeProtocol.CallbackTagOffset, _callbackTag, 0, tagLen);

            var segments = Encoding.UTF8.GetString(_callbackTag, 0, tagLen).Split('\0');
            if (segments.Length == 0)
                return;

            string effectName = segments[0];
            var arguments = new List<KeyValuePair<string, object>>();
            for (int i = 1; i + 1 < segments.Length; i += 2)
            {
                string key = segments[i];
                string raw = segments[i + 1];
                object value;
                if (raw.Length > 0 && raw[0] == 'n')
                    value = double.TryParse(raw.AsSpan(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0d;
                else if (raw.Length > 0 && raw[0] == 'b')
                    value = raw.Length > 1 && raw[1] == '1';
                else if (raw.Length > 0 && raw[0] == 's')
                    value = raw.Substring(1);
                else
                    value = raw;
                arguments.Add(new KeyValuePair<string, object>(key, value));
            }

            try { addEffect(effectName, arguments); }
            catch { }
            view.Write(NativeProtocol.OffCallbackFound, 1);
        }

        private string ResolveTag(int tagLen)
        {
            if (tagLen == _lastCallbackTagLength &&
                _callbackTag.AsSpan(0, tagLen).SequenceEqual(_lastCallbackTag.AsSpan(0, tagLen)))
                return _lastCallbackTagText;

            _lastCallbackTagText = Encoding.UTF8.GetString(_callbackTag, 0, tagLen);
            _callbackTag.AsSpan(0, tagLen).CopyTo(_lastCallbackTag);
            _lastCallbackTagLength = tagLen;
            return _lastCallbackTagText;
        }

        private void EnsureWorker(int width, int height, int stringCapacity)
        {
            long required = NativeProtocol.PixelOffset(stringCapacity) + (long)width * height * 4;
            if (_alive && _process is { HasExited: false } && _stringParamsCapacity == stringCapacity && required <= _allocatedSize)
                return;

            KillWorker();
            StartWorker(width, height, stringCapacity);
        }

        private void StartWorker(int width, int height, int stringCapacity)
        {
            _stringParamsCapacity = stringCapacity;
            _scriptWritten = false;
            _pixelOffset = NativeProtocol.PixelOffset(stringCapacity);
            long size = Math.Max(NativeProtocol.BufferSize(width, height, stringCapacity), _allocatedSize);
            _allocatedSize = size;

            string suffix = Guid.NewGuid().ToString("N");
            string mapName = "LuaScriptMap_" + suffix;
            string workName = "LuaScriptWork_" + suffix;
            string doneName = "LuaScriptDone_" + suffix;

            _mmf = MemoryMappedFile.CreateNew(mapName, size);
            _view = _mmf.CreateViewAccessor();
            _view.Write(NativeProtocol.OffMagic, NativeProtocol.Magic);
            _workEvent = new EventWaitHandle(false, EventResetMode.AutoReset, workName);
            _doneEvent = new EventWaitHandle(false, EventResetMode.AutoReset, doneName);

            var psi = new ProcessStartInfo
            {
                FileName = _luajitPath,
                WorkingDirectory = _nativeDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add(_workerScript);
            psi.ArgumentList.Add(mapName);
            psi.ArgumentList.Add(size.ToString());
            psi.ArgumentList.Add(workName);
            psi.ArgumentList.Add(doneName);
            psi.ArgumentList.Add(_shimScript);
            psi.ArgumentList.Add(stringCapacity.ToString());

            _process = Process.Start(psi);
            if (_process is not null)
                ProcessJobObject.Assign(_process.Handle);
            _alive = _process is not null;
        }

        private void KillWorker()
        {
            if (_process is { HasExited: false })
            {
                try { _process.Kill(entireProcessTree: true); }
                catch { }
            }
            _process?.Dispose();
            _process = null;
            _view?.Dispose();
            _view = null;
            _mmf?.Dispose();
            _mmf = null;
            _workEvent?.Dispose();
            _workEvent = null;
            _doneEvent?.Dispose();
            _doneEvent = null;
            _alive = false;
        }

        public void Dispose()
        {
            if (_alive && _process is { HasExited: false } && _view is not null && _workEvent is not null)
            {
                try
                {
                    _view.Write(NativeProtocol.OffCommand, NativeProtocol.CmdShutdown);
                    _workEvent.Set();
                    _process.WaitForExit(500);
                }
                catch { }
            }
            KillWorker();
        }
    }
}
