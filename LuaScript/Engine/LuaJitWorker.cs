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
        private int _width;
        private int _height;
        private bool _alive;

        private readonly byte[] _callbackTag = new byte[NativeProtocol.CallbackTagMax];
        private readonly byte[] _lastCallbackTag = new byte[NativeProtocol.CallbackTagMax];
        private int _lastCallbackTagLength = -1;
        private string _lastCallbackTagText = string.Empty;
        private byte[]? _resultBuffer;

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
            byte[] pixels,
            int width,
            int height,
            int timeoutMs,
            Func<string, int, SceneObjectInfo?> resolveObject,
            Func<string, int, double, double, double, (byte[] buffer, int w, int h)> loadFigure,
            Action<string, IReadOnlyList<KeyValuePair<string, object>>> addEffect,
            Action<DrawCommand> addDraw,
            out bool pixelsDirty,
            out bool bufferReplaced,
            out byte[]? newPixels,
            out int resultWidth,
            out int resultHeight,
            out string? error)
        {
            pixelsDirty = false;
            bufferReplaced = false;
            newPixels = null;
            resultWidth = width;
            resultHeight = height;
            error = null;

            byte[] scriptBytes = Encoding.UTF8.GetBytes(script);
            if (scriptBytes.Length > NativeProtocol.ScriptMax)
            {
                error = "script too large for native lane";
                return false;
            }

            EnsureWorker(width, height);
            var view = _view!;

            view.WriteArray(NativeProtocol.FieldsOffset, fields, 0, NativeProtocol.FieldCount);
            view.Write(NativeProtocol.OffWidth, width);
            view.Write(NativeProtocol.OffHeight, height);
            view.Write(NativeProtocol.OffScriptLen, scriptBytes.Length);
            view.WriteArray(NativeProtocol.ScriptOffset, scriptBytes, 0, scriptBytes.Length);
            view.WriteArray(NativeProtocol.PixelOffset, pixels, 0, pixels.Length);
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

                DispatchCallback(view, resolveObject, loadFigure, addEffect, addDraw);
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

            for (int i = NativeProtocol.FirstWritableField; i <= NativeProtocol.LastWritableField; i++)
                fields[i] = view.ReadDouble(NativeProtocol.FieldsOffset + i * 8);

            resultWidth = view.ReadInt32(NativeProtocol.OffWidth);
            resultHeight = view.ReadInt32(NativeProtocol.OffHeight);
            bufferReplaced = resultWidth != width || resultHeight != height;

            pixelsDirty = view.ReadInt32(NativeProtocol.OffPixelsDirty) != 0;
            if (pixelsDirty)
            {
                if (bufferReplaced)
                {
                    int pixelSize = resultWidth * resultHeight * 4;
                    if (_resultBuffer == null || _resultBuffer.Length < pixelSize)
                        _resultBuffer = new byte[pixelSize];
                    view.ReadArray(NativeProtocol.PixelOffset, _resultBuffer, 0, pixelSize);
                    newPixels = _resultBuffer;
                }
                else
                {
                    view.ReadArray(NativeProtocol.PixelOffset, pixels, 0, pixels.Length);
                }
            }

            return true;
        }

        private void DispatchCallback(
            MemoryMappedViewAccessor view,
            Func<string, int, SceneObjectInfo?> resolveObject,
            Func<string, int, double, double, double, (byte[] buffer, int w, int h)> loadFigure,
            Action<string, IReadOnlyList<KeyValuePair<string, object>>> addEffect,
            Action<DrawCommand> addDraw)
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
                case NativeProtocol.CbKindEffect:
                    ResolveEffectCallback(view, addEffect);
                    break;
                case NativeProtocol.CbKindDraw:
                    ResolveDrawCallback(view, addDraw);
                    break;
                case NativeProtocol.CbKindDrawPoly:
                    ResolveDrawPolyCallback(view, addDraw);
                    break;
            }
        }

        private static void ResolveDrawCallback(MemoryMappedViewAccessor view, Action<DrawCommand> addDraw)
        {
            long rOff = NativeProtocol.CallbackResultOffset;
            double ox = view.ReadDouble(rOff + 0 * 8);
            double oy = view.ReadDouble(rOff + 1 * 8);
            double oz = view.ReadDouble(rOff + 2 * 8);
            double zoom = view.ReadDouble(rOff + 3 * 8);
            double alpha = view.ReadDouble(rOff + 4 * 8);
            double aspect = view.ReadDouble(rOff + 5 * 8);
            double antialias = view.ReadDouble(rOff + 6 * 8);

            try { addDraw(new DrawCommand(ox, oy, oz, zoom, alpha, aspect, null, antialias)); }
            catch { }
            view.Write(NativeProtocol.OffCallbackFound, 1);
        }

        private static void ResolveDrawPolyCallback(MemoryMappedViewAccessor view, Action<DrawCommand> addDraw)
        {
            var poly = new double[DrawPolyMath.Length];
            for (int i = 0; i < DrawPolyMath.Length; i++)
                poly[i] = view.ReadDouble(NativeProtocol.CallbackTagOffset + i * 8);
            double antialias = view.ReadDouble(NativeProtocol.CallbackTagOffset + DrawPolyMath.Length * 8);

            try { addDraw(new DrawCommand(0d, 0d, 0d, 1d, poly[20], 0d, poly, antialias)); }
            catch { }
            view.Write(NativeProtocol.OffCallbackFound, 1);
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
            long capacity = view.Capacity - NativeProtocol.PixelOffset;
            if (pixelSize > capacity)
            {
                result = (new byte[4], 1, 1);
                pixelSize = 4;
            }

            view.Write(NativeProtocol.OffLoadResultWidth, result.w);
            view.Write(NativeProtocol.OffLoadResultHeight, result.h);
            view.WriteArray(NativeProtocol.PixelOffset, result.buffer, 0, pixelSize);
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

        private void EnsureWorker(int width, int height)
        {
            if (_alive && _process is { HasExited: false } && _width == width && _height == height)
                return;

            KillWorker();
            StartWorker(width, height);
        }

        private void StartWorker(int width, int height)
        {
            _width = width;
            _height = height;
            long size = NativeProtocol.BufferSize(width, height);

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
