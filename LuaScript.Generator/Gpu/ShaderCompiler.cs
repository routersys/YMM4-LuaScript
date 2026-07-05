using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace LuaScript.Generator
{
    internal static class ShaderCompiler
    {
        private const string Profile = "ps_5_0";

        public static string? Compile(string source, string entryPoint, out string bytecode)
        {
            bytecode = string.Empty;
            byte[] data = Encoding.UTF8.GetBytes(source);
            IntPtr code = IntPtr.Zero;
            IntPtr errors = IntPtr.Zero;
            try
            {
                int hr = D3DCompile(data, (IntPtr)data.Length, null, IntPtr.Zero, IntPtr.Zero, entryPoint, Profile, 0u, 0u, out code, out errors);
                if (hr < 0 || code == IntPtr.Zero)
                    return ReadError(errors, hr);
                bytecode = Convert.ToBase64String(ReadBlob(code));
                return null;
            }
            catch (DllNotFoundException)
            {
                return "the Direct3D shader compiler is unavailable on this build host";
            }
            catch (EntryPointNotFoundException)
            {
                return "the Direct3D shader compiler is unavailable on this build host";
            }
            finally
            {
                Release(errors);
                Release(code);
            }
        }

        private static byte[] ReadBlob(IntPtr blob)
        {
            IntPtr pointer = Invoke<GetBufferPointerDelegate>(blob, 3)(blob);
            int size = checked((int)(long)Invoke<GetBufferSizeDelegate>(blob, 4)(blob));
            var buffer = new byte[size];
            Marshal.Copy(pointer, buffer, 0, size);
            return buffer;
        }

        private static string ReadError(IntPtr blob, int hr)
        {
            string fallback = "HRESULT 0x" + hr.ToString("X8", CultureInfo.InvariantCulture);
            if (blob == IntPtr.Zero)
                return fallback;
            IntPtr pointer = Invoke<GetBufferPointerDelegate>(blob, 3)(blob);
            int size = checked((int)(long)Invoke<GetBufferSizeDelegate>(blob, 4)(blob));
            string message = (Marshal.PtrToStringAnsi(pointer, size) ?? string.Empty).Trim();
            return message.Length > 0 ? message : fallback;
        }

        private static void Release(IntPtr unknown)
        {
            if (unknown != IntPtr.Zero)
                Invoke<ReleaseDelegate>(unknown, 2)(unknown);
        }

        private static T Invoke<T>(IntPtr instance, int slot) where T : Delegate
        {
            IntPtr table = Marshal.ReadIntPtr(instance);
            IntPtr function = Marshal.ReadIntPtr(table, slot * IntPtr.Size);
            return (T)Marshal.GetDelegateForFunctionPointer(function, typeof(T));
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetBufferPointerDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetBufferSizeDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseDelegate(IntPtr self);

        [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int D3DCompile(
            byte[] srcData,
            IntPtr srcDataSize,
            [MarshalAs(UnmanagedType.LPStr)] string? sourceName,
            IntPtr defines,
            IntPtr include,
            [MarshalAs(UnmanagedType.LPStr)] string entryPoint,
            [MarshalAs(UnmanagedType.LPStr)] string target,
            uint flags1,
            uint flags2,
            out IntPtr code,
            out IntPtr errorMsgs);
    }
}
