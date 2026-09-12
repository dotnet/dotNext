using System.Runtime.InteropServices;
#if DEBUG || AOT_TESTS
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DotNext.Tests")]
[assembly: InternalsVisibleTo("DotNext.Aot.Tests")]
#endif

[assembly: CLSCompliant(true)]
[assembly: ComVisible(false)]