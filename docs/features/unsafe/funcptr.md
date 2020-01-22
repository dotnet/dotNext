Calling Function by Pointer
====
Unmanaged library can be loaded dynamically and its functions can be called by pointer. This approach is widely used by C programming language. .NET platform allows this using delegates marked with [UnmanagedFunctionPointerAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedfunctionpointerattribute) attribute. In this case, the pointer to the unmanaged function is wrapped into delegate instance, plus overhead caused by marshalling of managed world into unmanaged. Another way is [NativeLibrary](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.nativelibrary) class which exposes pointers to the unmanaged functions. How to be closer to bare metal and call functions by pointer like in C? The answer is [UnmanagedFunction](../../api/DotNext.Runtime.InteropServices.UnmanagedFunction.yml) for calling **void** functions and [UnmanagedFunction&lt;R&gt;](../../api/DotNext.Runtime.InteropServices.UnmanagedFunction-1.yml) for calling functions with return types.

> [!IMPORTANT]
> API described in this article is supported on Mono platform. JIT compiler for .NET Core 2.x or 3.x doesn't support generic parameters  for unmanaged calling conventions. See [this](https://github.com/dotnet/coreclr/issues/14524) issue for more information. It is expected that .NET 5 will support this feature.

Static methods inside of these classes accept raw pointer to the unmanaged function in the form of [IntPtr](https://docs.microsoft.com/en-us/dotnet/api/system.intptr) data and arguments of blittable types. There are two supported unmanaged calling conventions:
* _Cdecl_ is widely used by Linux OS
* _Stdcall_ is widely adopted by Windows OS

The name of the static methods reflects the calling convention to be applied to the target function.