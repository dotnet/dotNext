Calling Function by Pointer
====
Unmanaged library can be loaded dynamically and its functions can be called by pointer. This approach is widely used by C programming language. .NET platform allows this using delegates marked with [UnmanagedFunctionPointerAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedfunctionpointerattribute) attribute. In this case, the pointer to the unmanaged function is wrapped into delegate instance, plus overhead caused by marshalling of managed world into unmanaged. Another way is [NativeLibrary](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.nativelibrary) class which exposes pointers to the unmanaged functions. How to be closer to bare metal and call functions by pointer like in C? The answer is [UnmanagedFunction](../../api/DotNext.Runtime.InteropServices.UnmanagedFunction.yml) for calling **void** functions and [UnmanagedFunction&lt;R&gt;](../../api/DotNext.Runtime.InteropServices.UnmanagedFunction-1.yml) for calling functions with return types.

> [!IMPORTANT]
> API described in this article is supported on Mono platform. JIT compiler for .NET Core 2.x or 3.x doesn't support generic parameters  for unmanaged calling conventions. See [this](https://github.com/dotnet/coreclr/issues/14524) issue for more information. It is expected that .NET 5 will support this feature.

Static methods inside of these classes accept raw pointer to the unmanaged function in the form of [IntPtr](https://docs.microsoft.com/en-us/dotnet/api/system.intptr) data and arguments of blittable types. There are two supported unmanaged calling conventions:
* _Cdecl_ is widely used by Linux OS
* _Stdcall_ is widely adopted by Windows OS

The name of the static methods reflects the calling convention to be applied to the target function. 

Additionally, these classes are very helpful when you need to write COM component in C# with callback capabilities.
```csharp
using DotNext.Runtime.InteropServices;
using System.Runtime.InteropServices;
using PCallback = System.IntPtr;

[Guid("EAA4976A-45C3-4BC5-BC0B-E474F4C3C83F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IComComponent
{
	void DoCallback(PCallback pFunc);	//C equivalent is void(__stdcall *pFunc)(int, int)
}

[Guid("0D53A3E8-E51A-49C7-944E-E72A2064F938")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class ComComponent : IComComponent
{
	private int x, y;	

	public DoCallback(PCallback pFunc)
	{
		UnmanagedFunction.StdCall(pFunc, x, y);
	}
}
```
