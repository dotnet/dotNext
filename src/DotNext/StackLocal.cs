using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using F = InlineIL.FieldRef;

namespace DotNext
{
    /// <summary>
    /// Provides guarantees that value type is allocated on the stack.
    /// </summary>
    /// <remarks>
    /// <see cref="System.Threading.ThreadLocal{T}"/> provides guarantees that each thread has its own local view of the stored value.
    /// <see cref="System.Threading.AsyncLocal{T}"/> provides guarantees that each asynchronous execution flow has its own local view of the stored value.
    /// <see cref="StackLocal{T}"/> is more restrictive and provides guarantees that the value stored inside of it is allocated on the stack
    /// and can be accessible only by subsequent method calls placed into the current call stack.
    /// </remarks>
    /// <typeparam name="T">The type which value is stack-allocated.</typeparam>
    public ref struct StackLocal<T>
        where T : struct
    {
        /// <summary>
        /// Gets or sets stack-allocated value.
        /// </summary>
        public T Value;

        /// <summary>
        /// Allocates stack memory space and place the specified value into it.
        /// </summary>
        /// <param name="value">The value to be placed to the stack.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StackLocal(T value) => Value = value;

        internal ref T Ref
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Ldarg_0();
                Ldflda(new F(typeof(StackLocal<T>), nameof(Value)));
                return ref ReturnRef<T>();
            }
        }
    }
}
