using System.Runtime.InteropServices.Marshalling;

namespace DotNext.Runtime.InteropServices;

[NativeMarshalling(typeof(PointerMarshaller<>))]
partial struct Pointer<T>;