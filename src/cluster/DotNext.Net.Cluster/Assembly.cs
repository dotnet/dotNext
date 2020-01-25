using System;
using System.Runtime.CompilerServices;

[assembly: CLSCompliant(true)]
#if DEBUG
[assembly: InternalsVisibleTo("DotNext.Tests")]
#endif