using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace LuaScript.Tests
{
    public class NativeSharedMemoryTests
    {
        private static string NativeDir => Path.Combine(AppContext.BaseDirectory, "native");

        [Fact]
        public void Worker_ProcessesSharedBgraBuffer_ViaFfi()
        {
            string luajit = Path.Combine(NativeDir, "luajit.exe");
            Assert.True(File.Exists(luajit), "native/luajit.exe must be present");

            const int width = 8;
            const int height = 8;
            const int pixels = width * height;
            long size = pixels * 4L;
            string mapName = "LuaScriptShmPoC_" + Guid.NewGuid().ToString("N");

            using var mmf = MemoryMappedFile.CreateNew(mapName, size);
            var expectedGray = new byte[pixels];

            using (var acc = mmf.CreateViewAccessor())
            {
                for (int i = 0; i < pixels; i++)
                {
                    byte b = (byte)(i * 3 % 256);
                    byte g = (byte)(i * 5 % 256);
                    byte r = (byte)(i * 7 % 256);
                    acc.Write(i * 4 + 0, b);
                    acc.Write(i * 4 + 1, g);
                    acc.Write(i * 4 + 2, r);
                    acc.Write(i * 4 + 3, (byte)255);
                    expectedGray[i] = (byte)Math.Min(255, (int)Math.Floor(r * 0.299 + g * 0.587 + b * 0.114 + 0.5));
                }
            }

            var psi = new ProcessStartInfo
            {
                FileName = luajit,
                WorkingDirectory = NativeDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("worker_grayscale.lua");
            psi.ArgumentList.Add(mapName);
            psi.ArgumentList.Add(size.ToString());

            using var process = Process.Start(psi)!;
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            Assert.True(process.WaitForExit(5000), "worker should finish quickly");
            Assert.Equal(0, process.ExitCode);
            Assert.Equal("done", stdout);
            Assert.Equal("", stderr);

            using (var acc = mmf.CreateViewAccessor())
            {
                for (int i = 0; i < pixels; i++)
                {
                    byte b = acc.ReadByte(i * 4 + 0);
                    byte g = acc.ReadByte(i * 4 + 1);
                    byte r = acc.ReadByte(i * 4 + 2);
                    byte a = acc.ReadByte(i * 4 + 3);
                    Assert.Equal(expectedGray[i], b);
                    Assert.Equal(expectedGray[i], g);
                    Assert.Equal(expectedGray[i], r);
                    Assert.Equal(255, a);
                }
            }
        }
    }
}
