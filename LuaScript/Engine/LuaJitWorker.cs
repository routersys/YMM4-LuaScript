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
            out bool pixelsDirty,
            out string? error)
        {
            pixelsDirty = false;
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

                ResolveCallback(view, resolveObject);
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

            pixelsDirty = view.ReadInt32(NativeProtocol.OffPixelsDirty) != 0;
            if (pixelsDirty)
                view.ReadArray(NativeProtocol.PixelOffset, pixels, 0, pixels.Length);

            return true;
        }

        private void ResolveCallback(MemoryMappedViewAccessor view, Func<string, int, SceneObjectInfo?> resolveObject)
        {
            int frame = view.ReadInt32(NativeProtocol.OffCallbackFrame);
            int tagLen = Math.Clamp(view.ReadInt32(NativeProtocol.OffCallbackTagLen), 0, NativeProtocol.CallbackTagMax);
            var tagBytes = new byte[tagLen];
            view.ReadArray(NativeProtocol.CallbackTagOffset, tagBytes, 0, tagLen);
            string tag = Encoding.UTF8.GetString(tagBytes);

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
