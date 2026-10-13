using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotNext.Runtime.CompilerServices;

/// <summary>
/// Extends <see cref="RuntimeHelpers"/> class with execution stack inspection routines.
/// </summary>
public static class ExecutionStack
{
    /// <summary>
    /// Extends <see cref="RuntimeHelpers"/> type.
    /// </summary>
    extension(RuntimeHelpers)
    {
        /// <summary>
        /// Determines whether the specified memory location belongs to the stack memory.
        /// </summary>
        /// <param name="location">The memory location.</param>
        /// <typeparam name="T">The type of the data at the memory location.</typeparam>
        /// <returns><see langword="true"/> if <paramref name="location"/> is on the stack; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="PlatformNotSupportedException">The target platform doesn't have a reliable way to inspect an execution stack.</exception>
        /// <seealso cref="get_IsStackDetectionSupported"/>
        [UnsupportedOSPlatform("browser")]
        [UnsupportedOSPlatform("wasi")]
        public static bool IsOnExecutionStack<T>(ref readonly T location)
            where T : allows ref struct
            => StackInfoCachePerThread.Value.IsInRange((nuint)Unsafe.AddressOf(in location));

        /// <summary>
        /// Gets a value indicating that <see cref="ExecutionStack.IsOnExecutionStack{T}"/> method is supported on the target platform.
        /// </summary>
        [UnsupportedOSPlatformGuard("browser")]
        [UnsupportedOSPlatformGuard("wasi")]
        public static bool IsStackDetectionSupported => StackInfo.IsSupported;

        /// <summary>
        /// Gets a value of the execution stack belongs to the current thread.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">The target platform doesn't have a reliable way to inspect an execution stack.</exception>
        /// <seealso cref="get_IsStackDetectionSupported"/>
        [UnsupportedOSPlatform("browser")]
        [UnsupportedOSPlatform("wasi")]
        [CLSCompliant(false)]
        public static nuint ExecutionStackSize => StackInfoCachePerThread.Value.Size;

        /// <summary>
        /// Gets the remaining execution stack space.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">The target platform doesn't have a reliable way to inspect an execution stack.</exception>
        /// <seealso cref="get_IsStackDetectionSupported"/>
        [UnsupportedOSPlatform("browser")]
        [UnsupportedOSPlatform("wasi")]
        [CLSCompliant(false)]
        public static nuint RemainingExecutionStack
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            [SkipLocalsInit]
            get
            {
                Unsafe.SkipInit(out byte marker);
                return StackInfoCachePerThread.Value.GetRemainingSpace((nuint)Unsafe.AddressOf(in marker));
            }
        }
    }

    /// <summary>
    /// Extends <see cref="ReadOnlySpan{T}"/> type.
    /// </summary>
    /// <param name="span">A span over the elements.</param>
    /// <typeparam name="T">The type of the elements.</typeparam>
    extension<T>(ReadOnlySpan<T> span)
    {
        /// <summary>
        /// Gets a value indicating that the span represents a memory allocated on the stack.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">The target platform doesn't have a reliable way to inspect an execution stack.</exception>
        /// <seealso cref="ExecutionStack.get_IsStackDetectionSupported"/>
        [UnsupportedOSPlatform("browser")]
        [UnsupportedOSPlatform("wasi")]
        public bool IsStackAllocated => IsOnExecutionStack(in MemoryMarshal.GetReference(span));
    }
}

file static class StackInfoCachePerThread
{
    [ThreadStatic]
    private static StackInfo CachedValue;

    public static ref readonly StackInfo Value
    {
        get
        {
            ref var stackInfo = ref CachedValue;
            if (!stackInfo.IsInitialized)
                stackInfo = new();

            return ref stackInfo;
        }
    }
}

[StructLayout(LayoutKind.Auto)]
file readonly struct StackInfo
{
    private readonly nuint start, end;
    public readonly bool IsInitialized;

    public StackInfo()
    {
        nuint stackSize;
        if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
        {
            (start, stackSize) = LinuxApi.Get();
            end = start + stackSize;
        }
        else if (OperatingSystem.IsWindows())
        {
            (start, end) = WindowsApi.Get();
        }
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS())
        {
            (start, stackSize) = DarwinApi.Get();
            end = start + stackSize;
        }
        else
        {
            start = end = 0U;
        }

        IsInitialized = true;
    }

    public static bool IsSupported => OperatingSystem.IsLinux()
                                      || OperatingSystem.IsAndroid()
                                      || OperatingSystem.IsWindows()
                                      || OperatingSystem.IsMacOS()
                                      || OperatingSystem.IsIOS()
                                      || OperatingSystem.IsTvOS();

    private bool IsInvalid => start == end;

    public bool IsInRange(nuint addressToCheck)
    {
        if (IsInvalid)
            ThrowNotSupportedException();

        return addressToCheck >= start && addressToCheck < end;
    }

    public nuint Size
    {
        get
        {
            if (IsInvalid)
                ThrowNotSupportedException();

            return end - start;
        }
    }

    public nuint GetRemainingSpace(nuint stackPointer)
    {
        if (IsInvalid)
            ThrowNotSupportedException();

        return stackPointer - start; // stack grows down on all supported platforms
    }

    [DoesNotReturn]
    [StackTraceHidden]
    private static void ThrowNotSupportedException()
        => throw new PlatformNotSupportedException();

    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("android")]
    private static unsafe class LinuxApi
    {
        private static readonly delegate* unmanaged[SuppressGCTransition]<nint> pthread_self;
        private static readonly delegate* unmanaged<nint, void*, int> pthread_getattr_np;
        private static readonly delegate* unmanaged[SuppressGCTransition]<void*, nuint*, nuint*, int> pthread_attr_getstack;
        private static readonly delegate* unmanaged<void*, int> pthread_attr_destroy;

        static LinuxApi()
        {
            var programHandle = NativeLibrary.GetMainProgramHandle();
            if (NativeLibrary.TryGetExport(programHandle, nameof(pthread_self), out var pthreadSelf)
                && NativeLibrary.TryGetExport(programHandle, nameof(pthread_getattr_np), out var pthreadGetAttr)
                && NativeLibrary.TryGetExport(programHandle, nameof(pthread_attr_getstack), out var pthreadAttrGetStack)
                && NativeLibrary.TryGetExport(programHandle, nameof(pthread_attr_destroy), out var pthreadAttrDestroy))
            {
                pthread_self = (delegate* unmanaged[SuppressGCTransition]<nint>)pthreadSelf;
                pthread_getattr_np = (delegate* unmanaged<nint, void*, int>)pthreadGetAttr;
                pthread_attr_getstack = (delegate* unmanaged[SuppressGCTransition]<void*, nuint*, nuint*, int>)pthreadAttrGetStack;
                pthread_attr_destroy = (delegate* unmanaged<void*, int>)pthreadAttrDestroy;
            }
        }

        public static (nuint LowLimit, nuint Size) Get()
        {
            nuint stackAddress = 0U, stackSize = 0U;

            if (pthread_self is not null)
            {
                var attr = stackalloc byte[64];
                if (pthread_getattr_np(pthread_self(), attr) is 0)
                {
                    try
                    {
                        pthread_attr_getstack(attr, &stackAddress, &stackSize);
                    }
                    finally
                    {
                        pthread_attr_destroy(attr);
                    }
                }
            }

            return (stackAddress, stackSize);
        }
    }

    [SupportedOSPlatform("windows")]
    private static unsafe class WindowsApi
    {
        private static readonly delegate* unmanaged[SuppressGCTransition]<nuint*, nuint*, void> GetCurrentThreadStackLimits;

        static WindowsApi()
        {
            // kernel32.dll is never unloaded, so the handle is not released
            if (NativeLibrary.TryLoad("kernel32.dll", out var kernel32)
                && NativeLibrary.TryGetExport(kernel32, nameof(GetCurrentThreadStackLimits), out var getCurrentThreadStackLimits))
            {
                GetCurrentThreadStackLimits = (delegate* unmanaged[SuppressGCTransition]<nuint*, nuint*, void>)getCurrentThreadStackLimits;
            }
        }

        public static (nuint LowLimit, nuint HighLimit) Get()
        {
            nuint lowLimit = 0U, highLimit = 0U;

            if (GetCurrentThreadStackLimits is not null)
                GetCurrentThreadStackLimits(&lowLimit, &highLimit);

            return (lowLimit, highLimit);
        }
    }

    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("ios")]
    [SupportedOSPlatform("tvos")]
    private static unsafe class DarwinApi
    {
        private static readonly delegate* unmanaged[SuppressGCTransition]<nint> pthread_self;
        private static readonly delegate* unmanaged[SuppressGCTransition]<nint, nuint> pthread_get_stackaddr_np;
        private static readonly delegate* unmanaged[SuppressGCTransition]<nint, nuint> pthread_get_stacksize_np;

        static DarwinApi()
        {
            var programHandle = NativeLibrary.GetMainProgramHandle();
            if (NativeLibrary.TryGetExport(programHandle, nameof(pthread_self), out var pthreadSelf)
                && NativeLibrary.TryGetExport(programHandle, nameof(pthread_get_stackaddr_np), out var pthreadGetStackAddr)
                && NativeLibrary.TryGetExport(programHandle, nameof(pthread_get_stacksize_np), out var pthreadGetStackSize))
            {
                pthread_self = (delegate* unmanaged[SuppressGCTransition]<nint>)pthreadSelf;
                pthread_get_stackaddr_np = (delegate* unmanaged[SuppressGCTransition]<nint, nuint>)pthreadGetStackAddr;
                pthread_get_stacksize_np = (delegate* unmanaged[SuppressGCTransition]<nint, nuint>)pthreadGetStackSize;
            }
        }

        public static (nuint LowLimit, nuint Size) Get()
        {
            nuint stackAddress = 0U, stackSize = 0U;

            if (pthread_self is not null)
            {
                // pthread_get_stackaddr_np returns the top of the stack (the stack grows down)
                var currentThread = pthread_self();
                stackSize = pthread_get_stacksize_np(currentThread);
                stackAddress = pthread_get_stackaddr_np(currentThread) - stackSize;
            }

            return (stackAddress, stackSize);
        }
    }
}
