using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;

namespace DotNext.Threading
{
    internal static class Atomic<T>//T should not be greater than maximum size of primitive type. For .NET Standard it is sizeof(long)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T Read(ref T value)
        {
            Push(ref value);
            Volatile();
            Ldobj(typeof(T));
            return Return<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Write(ref T storage, T value)
        {
            Push(ref storage);
            Push(value);
            Volatile();
            Stobj(typeof(T));
            Ret();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Equals(T x, T y)
        {
            Push(x);
            Push(y);
            Ceq();
            return Return<bool>();
        }
    }
}
