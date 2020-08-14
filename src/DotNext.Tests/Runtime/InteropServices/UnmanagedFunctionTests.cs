using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices
{
    public sealed class UnmanagedFunctionTests : Test
    {
        [OSDependentFact(PlatformID.Win32NT, Skip = "Not yet supported by .NET Core")]
        public static void GetCurrentProcessId()
        {
            var kernel32 = NativeLibrary.Load("kernel32.dll");
            NotEqual(IntPtr.Zero, kernel32);
            try
            {
                var getCurrentProcessId = NativeLibrary.GetExport(kernel32, "GetCurrentProcessId");
                NotEqual(IntPtr.Zero, getCurrentProcessId);
                using var currentProcess = Process.GetCurrentProcess();
                Equal(currentProcess.Id, UnmanagedFunction<int>.StdCall(getCurrentProcessId));
            }
            finally
            {
                NativeLibrary.Free(kernel32);
            }
        }
    }
}
