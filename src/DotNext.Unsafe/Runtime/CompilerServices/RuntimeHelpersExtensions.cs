using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotNext.Runtime.CompilerServices;

/// <summary>
/// Extends <see cref="RuntimeHelpers"/> class.
/// </summary>
public static class RuntimeHelpersExtensions
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
        /// <seealso cref="IsStackDetectionSupported"/>
        [UnsupportedOSPlatform("browser")]
        [UnsupportedOSPlatform("wasi")]
        public static bool IsOnExecutionStack<T>(ref readonly T location)
            where T : allows ref struct
            => StackInfoCachePerThread.IsOnStack((nuint)Unsafe.AddressOf(in location));

        /// <summary>
        /// Gets a value indicating that <see cref="RuntimeHelpersExtensions.IsOnExecutionStack{T}"/> method is supported on the target platform.
        /// </summary>
        [UnsupportedOSPlatformGuard("browser")]
        [UnsupportedOSPlatformGuard("wasi")]
        public static bool IsStackDetectionSupported => StackInfo.IsSupported;

        /// <summary>
        /// Gets a value of the execution stack belongs to the current thread.
        /// </summary>
        [UnsupportedOSPlatform("browser")]
        [UnsupportedOSPlatform("wasi")]
        public static nuint ExecutionStackSize => StackInfoCachePerThread.Size;

        /// <summary>
        /// Gets the remaining execution stack space.
        /// </summary>
        [UnsupportedOSPlatform("browser")]
        [UnsupportedOSPlatform("wasi")]
        public static nuint RemainingExecutionStack
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            [SkipLocalsInit]
            get
            {
                Unsafe.SkipInit(out byte marker);
                return StackInfoCachePerThread.GetRemainingSpace((nuint)Unsafe.AddressOf(in marker));
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
        /// <seealso cref="RuntimeHelpersExtensions.get_IsStackDetectionSupported"/>
        [UnsupportedOSPlatform("browser")]
        [UnsupportedOSPlatform("wasi")]
        public bool IsStackAllocated => IsOnExecutionStack(in MemoryMarshal.GetReference(span));
    }
}

file static class StackInfoCachePerThread
{
    [ThreadStatic]
    private static StackInfo Info;

    private static ref StackInfo EnsureInitializedInfo()
    {
        ref var stackInfo = ref Info;
        if (!stackInfo.IsInitialized)
            stackInfo = new();

        return ref stackInfo;
    }

    public static bool IsOnStack(nuint address)
        => EnsureInitializedInfo().IsInRange(address);

    public static nuint Size => EnsureInitializedInfo().Size;

    public static nuint GetRemainingSpace(nuint stackPointer)
        => EnsureInitializedInfo().GetRemainingSpace(stackPointer);
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
            (start, stackSize) = GetStackInfoOnLinux();
            end = start + stackSize;
        }
        else if (OperatingSystem.IsWindows())
        {
            (start, end) = GetStackInfoOnWindows();
        }
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS())
        {
            (start, stackSize) = GetStackInfoOnDarwin();
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
    private static unsafe (nuint LowLimit, nuint Size) GetStackInfoOnLinux()
    {
        nuint stackAddress = 0U, stackSize = 0U;

        var programHandle = NativeLibrary.GetMainProgramHandle();
        if (!NativeLibrary.TryGetExport(programHandle, "pthread_self", out var nativeFunc))
            goto exit;

        var pthread_self = (delegate*unmanaged<nint>)nativeFunc;

        if (!NativeLibrary.TryGetExport(programHandle, "pthread_getattr_np", out nativeFunc))
            goto exit;

        var pthread_getattr_np = (delegate*unmanaged<nint, void*, int>)nativeFunc;

        if (!NativeLibrary.TryGetExport(programHandle, "pthread_attr_getstack", out nativeFunc))
            goto exit;

        var pthread_attr_getstack = (delegate*unmanaged<void*, nuint*, nuint*, int>)nativeFunc;

        if (!NativeLibrary.TryGetExport(programHandle, "pthread_attr_destroy", out nativeFunc))
            goto exit;

        var pthread_attr_destroy = (delegate*unmanaged<void*, int>)nativeFunc;

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

        exit:
        return (stackAddress, stackSize);
    }

    [SupportedOSPlatform("windows")]
    private static unsafe (nuint LowLimit, nuint HighLimit) GetStackInfoOnWindows()
    {
        nuint lowLimit = 0U, highLimit = 0U;

        if (NativeLibrary.TryLoad("kernel32.dll", out var kernel32))
        {
            try
            {
                if (NativeLibrary.TryGetExport(kernel32, "GetCurrentThreadStackLimits", out var nativeFunc))
                {
                    var getCurrentThreadStackLimits = (delegate*unmanaged<nuint*, nuint*, void>)nativeFunc;
                    getCurrentThreadStackLimits(&lowLimit, &highLimit);
                }
            }
            finally
            {
                NativeLibrary.Free(kernel32);
            }
        }

        return (lowLimit, highLimit);
    }

    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("ios")]
    [SupportedOSPlatform("tvos")]
    private static unsafe (nuint LowLimit, nuint Size) GetStackInfoOnDarwin()
    {
        nuint stackAddress = 0U, stackSize = 0U;

        var programHandle = NativeLibrary.GetMainProgramHandle();
        if (!NativeLibrary.TryGetExport(programHandle, "pthread_self", out var nativeFunc))
            goto exit;

        var pthread_self = (delegate*unmanaged<nint>)nativeFunc;

        if (!NativeLibrary.TryGetExport(programHandle, "pthread_get_stackaddr_np", out nativeFunc))
            goto exit;

        var pthread_get_stackaddr_np = (delegate*unmanaged<nint, nuint>)nativeFunc;

        if (!NativeLibrary.TryGetExport(programHandle, "pthread_get_stacksize_np", out nativeFunc))
            goto exit;

        var pthread_get_stacksize_np = (delegate*unmanaged<nint, nuint>)nativeFunc;

        // pthread_get_stackaddr_np returns the top of the stack (the stack grows down)
        var currentThread = pthread_self();
        stackSize = pthread_get_stacksize_np(currentThread);
        stackAddress = pthread_get_stackaddr_np(currentThread) - stackSize;

        exit:
        return (stackAddress, stackSize);
    }
}