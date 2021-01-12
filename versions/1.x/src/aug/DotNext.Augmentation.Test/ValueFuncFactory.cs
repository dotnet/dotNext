using System.Diagnostics.CodeAnalysis;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public static class ValueFuncFactory
    {
        private static ulong Sum(ulong x, ulong y) => x + y;

        public static ValueFunc<ulong, ulong, ulong> CreateSumFunction()
            => new ValueFunc<ulong, ulong, ulong>(Sum);
    }
}
