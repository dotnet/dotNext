Compile-Time Augmentations
====
.NEXT Augmented Compilation feature is implemented as a component that sits in the compilation pipeline and tightly integrated with MSBuild. It is called immediately after Roslyn compiler to augment the compiled IL code with .NEXT-specific optimizations and tricks, so they become available at compile-time in contrast to run-time.

> [!NOTE]
> Augmented Compilation is experimental at this moment.

Augmentations are implemented as [Fody](https://github.com/Fody/Fody) add-in which acts as weaver of Intermediate Language code produced by C# compiler. Such approach has its own pros & cons:
1. It is possible to apply arbitrary code transformations at IL level
1. All tricks provided at build-time and do not require runtime dependencies
1. Build time is increased by 50%-100%
1. Hard to diagnose transformation errors

Therefore, Augmented Compilation is an optional feature in .NEXT landscape. This means that you always has a choice between convenience by cost of augmented compilation and redundant code written by yourself. 

To use Augmented Compilation, you need to declare necessary build components as a part of MSBuild processing pipeline inside **csproj** file:
```xml
<ItemGroup>
	<PackageReference Include="Fody" Version="6.*" PrivateAssets="all" />
	<PackageReference Include="DotNext.Augmentation.Fody" Version="2.*" PrivateAssets="all"/>
</ItemGroup>
```

If you use additional Fody weavers then they should be declared after `DotNext.Augmentation.Fody` weaver.

Now MSBuild will call .NEXT weaver after each build. 

# Value Delegates
.NEXT weaver has augmentation that helps Roslyn to recognize instantation of Value Delegate and remove redundant memory allocation of regular delegate. 

All Value Delegates supplied by .NEXT have special constructor that accepts pointer to the managed method. However, this constructor is not available from C# because it has required type modifier. It is used by weaver to replace overloaded constructor that accepts regular delegate allocated on the heap.

```csharp
using DotNext;

private static long Sum(long x, long y) => x + y;

var sum = new ValueFunc<long, long, long>(Sum);
sum.Invoke(2L, 3L);	//returns 5
```

This code is valid but compilation by Roslyn includes instantiation of regular delegate. This happens because the example uses `ValueFunc<long, long, long>(Func<long, long, long> func)` constructor.

Compile-Time augmentation removes this instantiation and changes the callee constructor to `ValueFunc<long, long, long>(native int methodPtr)`. The example will be optimized as follows:
```csharp
using DotNext;

private static long Sum(long x, long y) => x + y;

var sum = new ValueFunc<long, long, long>(&Sum);
sum.Invoke(2L, 3L);	//returns 5
```

The augmentation reduces memory allocation only if the following conditions are met:
1. The method is not a lambda expression
1. The method is static
