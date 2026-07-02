using System.Runtime.InteropServices;

namespace LuaScript.Diagnostics
{
    internal static partial class DebugOutput
    {
        [LibraryImport("kernel32.dll", EntryPoint = "OutputDebugStringW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial void OutputDebugStringW(string message);

        internal static void Write(string message) => OutputDebugStringW(message);
    }
}
