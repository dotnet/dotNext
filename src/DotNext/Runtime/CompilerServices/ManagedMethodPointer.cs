using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using CallSiteDescr = InlineIL.StandAloneMethodSig;
using TR = InlineIL.TypeRef;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Acts as modifier type for the parameter representing
    /// pointer to the managed method.
    /// </summary>
    /// <remarks>
    /// This class is not intended to be used directly from your code.
    /// </remarks>
    public readonly struct ManagedMethodPointer : IEquatable<ManagedMethodPointer>
    {
        private readonly IntPtr methodPtr;

        internal ManagedMethodPointer(IntPtr methodPtr) => this.methodPtr = methodPtr;

        internal ManagedMethodPointer(RuntimeMethodHandle method)
            => methodPtr = method.GetFunctionPointer();

        internal O Invoke<I, O>(in I arg0, int arg1)
        {
            Push(nameof(arg0));
            Push(arg1);
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(O), new TR(typeof(I)).MakeByRefType(), typeof(int)));
            return Return<O>();
        }

        /// <summary>
        /// Determines whether this method pointer is equal to the specified method pointer.
        /// </summary>
        /// <param name="other">The method pointer to compare.</param>
        /// <returns><see langword="true"/> if this method pointer is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ManagedMethodPointer other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this method pointer is equal to the specified method pointer.
        /// </summary>
        /// <param name="other">The method pointer to compare.</param>
        /// <returns><see langword="true"/> if this method pointer is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is ManagedMethodPointer ptr && Equals(ptr);

        /// <summary>
        /// Gets hash code of this method pointer.
        /// </summary>
        /// <returns>The hash code of this method pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Gets method pointer in HEX format.
        /// </summary>
        /// <returns>The method pointer in HEX format.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether two method pointers are equal.
        /// </summary>
        /// <param name="x">The first pointer to compare.</param>
        /// <param name="y">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="x"/> is equal to <paramref name="y"/>; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ManagedMethodPointer x, ManagedMethodPointer y)
            => x.methodPtr == y.methodPtr;

        /// <summary>
        /// Determines whether two method pointers are not equal.
        /// </summary>
        /// <param name="x">The first pointer to compare.</param>
        /// <param name="y">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="x"/> is not equal to <paramref name="y"/>; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ManagedMethodPointer x, ManagedMethodPointer y)
            => x.methodPtr != y.methodPtr;
    }
}
