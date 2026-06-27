using System;
using System.Diagnostics;
using System.IO;

namespace LuaScript.Tests
{
    public class NativeWorkerTimeoutTests
    {
        private static string LuaJit => Path.Combine(AppContext.BaseDirectory, "native", "luajit.exe");

        private static Process Launch(string inlineScript)
        {
            var psi = new ProcessStartInfo
            {
                FileName = LuaJit,
                WorkingDirectory = Path.GetDirectoryName(LuaJit)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add(inlineScript);
            return Process.Start(psi) ?? throw new InvalidOperationException("failed to start luajit");
        }

        [Fact]
        public void JitCompiledInfiniteLoop_IsHardKilledWithinTimeout_AndHostRecovers()
        {
            Assert.True(File.Exists(LuaJit), "native/luajit.exe must be present");

            const int timeoutMs = 1500;

            using (var hung = Launch("local x=0 for i=1,1e9 do for j=1,1e9 do x=x+1 end end"))
            {
                bool exitedOnItsOwn = hung.WaitForExit(timeoutMs);
                Assert.False(exitedOnItsOwn, "the hot loop should still be running at the timeout");

                var sw = Stopwatch.StartNew();
                hung.Kill(entireProcessTree: true);
                Assert.True(hung.WaitForExit(2000), "the process must terminate promptly after Kill");
                sw.Stop();
                Assert.True(sw.ElapsedMilliseconds < 2000, "kill should take effect quickly");
            }

            using var ok = Launch("io.write('recovered')");
            string output = ok.StandardOutput.ReadToEnd();
            Assert.True(ok.WaitForExit(5000), "a fresh worker must run after the previous was killed");
            Assert.Equal(0, ok.ExitCode);
            Assert.Equal("recovered", output);
        }
    }
}
