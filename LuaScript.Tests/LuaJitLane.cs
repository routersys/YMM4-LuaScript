using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace LuaScript.Tests
{
    internal sealed class LuaJitLane
    {
        private readonly string _nativeDir = Path.Combine(AppContext.BaseDirectory, "native");

        public bool Available => File.Exists(Path.Combine(_nativeDir, "luajit.exe"));

        public string[] Eval(IReadOnlyList<string> expressions)
        {
            string vectors = Path.GetTempFileName();
            File.WriteAllLines(vectors, expressions);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Path.Combine(_nativeDir, "luajit.exe"),
                    WorkingDirectory = _nativeDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                psi.ArgumentList.Add("runner.lua");
                psi.ArgumentList.Add(Path.Combine(_nativeDir, "shim.lua"));
                psi.ArgumentList.Add(vectors);

                using var process = Process.Start(psi)
                    ?? throw new InvalidOperationException("Failed to start luajit.exe");
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("luajit exited " + process.ExitCode + ": " + stderr);

                return stdout.Replace("\r\n", "\n").Split('\n');
            }
            finally
            {
                File.Delete(vectors);
            }
        }
    }
}
