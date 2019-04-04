Off-Heap Allocation of Value Type
====
[UnmanagedMemory](../../api/DotNext.Runtime.InteropServices.UnmanagedMemory-1.yml) is a value type that represents typed pointer to the value type allocated in unmanaged memory. This type controls memory access which is limited to the size of type `T`. 

The simpliest way to understand what is `UnmanagedMemory` is to provide the following example in C:
```c
#include <stdlib.h>

typedef struct {
    double image;
    double real;
} complex;

complex *c = malloc(sizeof(complex));
c->image = 20;
c->real = 30;
free(c);
```

The equivalent code in C# using _UnmanagedMemory_ is
```csharp
using DotNext.Runtime.InteropServices;

struct Complex
{
    public double Image, Real;
}

using(var c = new UnmanagedMemory<Complex>(false))
{
    c.Ref.Image = 20;
    c.Ref.Real = 30;
}
```

Additionally, it is possible to box value type into unmanaged memory:
```csharp
using DotNext.Runtime.InteropServices;

struct Complex
{
    public double Image, Real;
}

using(var c = new UnmanagedMemory<Complex>(new Complex { Image = 20, Real = 30 }))
{
}
```

It is possible to obtain typed pointer to the value in the unmanaged memory:
```csharp
using DotNext.Runtime.InteropServices;

struct Complex
{
    public double Image, Real;
}

using(var c = new UnmanagedMemory<Complex>(new Complex { Image = 20, Real = 30 }))
{
    Pointer<Complex> ptr = c;
    Pointer<double> pImage = c.As<double>();
    Pointer<double> pReal = pImage + 1;
    pImage.Value = 1;
    pReal.Value = 2;
}
```