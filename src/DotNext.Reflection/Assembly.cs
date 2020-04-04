using DotNext.Runtime.CompilerServices;
using System;
using System.Runtime.CompilerServices;

[assembly: CLSCompliant(true)]
[assembly: DisablePrivateReflection]
[assembly: RuntimeFeatures(DynamicCodeCompilation = true, RuntimeGenericInstantiation = true, PrivateReflection = true)]