using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using CallSiteDescr = InlineIL.StandAloneMethodSig;
using M = InlineIL.MethodRef;

namespace DotNext
{
    using static Reflection.TypeExtensions;
    using static Reflection.MethodExtensions;
    using static Reflection.DelegateType;

    /// <summary>
    /// Represents common interface for static delegates.
    /// </summary>
    /// <typeparam name="D">The type of the delegate that is compatible with the pointer.</typeparam>
    public interface IMethodPointer<out D>
        where D : Delegate
    {
        /// <summary>
        /// Converts function pointer into delegate.
        /// </summary>
        /// <returns>The delegate instance created from this pointer.</returns>
        D ToDelegate();

        /// <summary>
        /// Gets address of the function.
        /// </summary>
        IntPtr Address { get; }
    }

    internal sealed class MethodPointerException : NullReferenceException
    {
        internal MethodPointerException()
            : base(ExceptionMessages.NullMethodPointer)
        {
        }
    }

    /*
        This cache is here because in C# there are no way to obtain method pointer
        directly in code. Thus, we have to use MethodInfo as parameter for creating
        typed method pointers. Then we should check that pointer can be created for
        particular method. This check causes memory allocations because of MethodInfo.GetParameters() which
        allocates fresh array every time. This is not good if consumer wants to have zero-alloc instantiation
        of typed method pointers. Every pointer type has its own cache. Allocation is happened only
        once for the same MethodInfo. Next instantiation of pointer for the same MethodInfo hits the cache
        and do not cause allocation because validation result can be returned immediately without computation.
        There is no free lunch and this cache consumes memory. However, it doesn't create GC pressure or
        even managed heap because all data inside of it are value types without references to the managed objects.
     */
    [StructLayout(LayoutKind.Sequential)]
    internal struct MethodPointerCache<D>
        where D : Delegate
    {
        [StructLayout(LayoutKind.Sequential, Pack = sizeof(int))]
        private struct Entry
        {
            private const int Unchecked = 0;
            private const int Checking = 1;
            private const int True = 2;
            private const int False = 3;

            private volatile IntPtr methodHandle;
            private volatile int status;

            private static bool CheckCore(MethodInfo candidate)
            {
                var invokeMethod = GetInvokeMethod<D>();
                return candidate.ReturnType == invokeMethod.ReturnType && candidate.SignatureEquals(invokeMethod.GetParameterTypes());
            }

            internal bool Check(MethodInfo candidate)
            {
                var spinner = new SpinWait();
            again:
                switch(Interlocked.CompareExchange(ref status, Checking, Unchecked))
                {
                    case Unchecked:
                        var result = CheckCore(candidate);
                        methodHandle = candidate.MethodHandle.Value;
                        status = result ? True : False;
                        return result;
                    case Checking:
                        //we have the same check procedure executing in parallel thread.
                        //This procedure relatively fast (no I/O) so we just spin and wait for completion
                        spinner.SpinOnce();
                        goto again;
                    case True:
                        //another call of CheckCore here because if hash algorithm produces the same
                        //entry index but for different method handle then it is cache miss
                        //and just execute check result again
                        //this is worst case which leads to extra memory allocation
                        return candidate.MethodHandle.Value == methodHandle || CheckCore(candidate);
                    case False:
                        return candidate.MethodHandle.Value != methodHandle && CheckCore(candidate);
                    default:
                        throw new InvalidOperationException();  //this should never happen
                }
            }
        }

        private const byte CacheSize = 10;   //should be aligned with number of fields entry0..entry09
        
        private Entry entry0;
        [SuppressMessage("Performance", "CA1823", Justification = "Field is referenced indirectly through pointer arithmetic in Check method")]
        private Entry entry1;
        [SuppressMessage("Performance", "CA1823", Justification = "Field is referenced indirectly through pointer arithmetic in Check method")]
        private Entry entry2;
        [SuppressMessage("Performance", "CA1823", Justification = "Field is referenced indirectly through pointer arithmetic in Check method")]
        private Entry entry3;
        [SuppressMessage("Performance", "CA1823", Justification = "Field is referenced indirectly through pointer arithmetic in Check method")]
        private Entry entry4;
        [SuppressMessage("Performance", "CA1823", Justification = "Field is referenced indirectly through pointer arithmetic in Check method")]
        private Entry entry5;
        [SuppressMessage("Performance", "CA1823", Justification = "Field is referenced indirectly through pointer arithmetic in Check method")]
        private Entry entry6;
        [SuppressMessage("Performance", "CA1823", Justification = "Field is referenced indirectly through pointer arithmetic in Check method")]
        private Entry entry7;
        [SuppressMessage("Performance", "CA1823", Justification = "Field is referenced indirectly through pointer arithmetic in Check method")]
        private Entry entry8;
        [SuppressMessage("Performance", "CA1823", Justification = "Field is referenced indirectly through pointer arithmetic in Check method")]
        private Entry entry9;

        internal bool Check(MethodInfo candidate)
        {
            var offset = (candidate.MethodHandle.GetHashCode() & int.MaxValue) % CacheSize;
            ref var entry = ref Unsafe.Add(ref entry0, offset);
            return entry.Check(candidate);
        }
    }

    /// <summary>
    /// Represents a pointer to parameterless method with <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed static methods only.
    /// </remarks>
    public readonly struct ActionPointer : IMethodPointer<Action>, IEquatable<ActionPointer>
    {
        private static MethodPointerCache<Action> Cache = new MethodPointerCache<Action>();
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type; or it is not static.</exception>
        public ActionPointer(MethodInfo method)
        {
            methodPtr = Cache.Check(method ?? throw new ArgumentNullException(nameof(method))) ? 
                method.MethodHandle.GetFunctionPointer():
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        /// <exception cref="ArgumentException"><see cref="Delegate.Target"/> is not <see langword="null"/>.</exception>
        public ActionPointer(Action @delegate)
        {
            methodPtr = @delegate.Target is null ? 
                @delegate.Method.MethodHandle.GetFunctionPointer() : 
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        private ActionPointer(IntPtr methodPtr) => this.methodPtr = methodPtr;

        IntPtr IMethodPointer<Action>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Action), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Action>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        public void Invoke()
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action(ActionPointer pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ActionPointer first, ActionPointer second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer first, ActionPointer second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to parameterless method with return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed static methods only.
    /// </remarks>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    public readonly struct FunctionPointer<R> : IMethodPointer<Func<R>>, IEquatable<FunctionPointer<R>>
    {
        private static MethodPointerCache<Func<R>> Cache = new MethodPointerCache<Func<R>>();
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <param name="method">The method to convert into pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Signature of <paramref name="method"/> doesn't match to this pointer type; or it is not static.</exception>
        public FunctionPointer(MethodInfo method)
        {
            methodPtr = Cache.Check(method ?? throw new ArgumentNullException(nameof(method))) ? 
                    method.MethodHandle.GetFunctionPointer() :
                    throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public FunctionPointer(Func<R> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate)); 
        }

        IntPtr IMethodPointer<Func<R>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Func{TResult}"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Func<R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Func<R>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Func<R>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        /// <returns>The result of method invocation.</returns>
        public R Invoke()
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R)));
            return Return<R>();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Func{TResult}"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Func<R>(FunctionPointer<R> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FunctionPointer<R> other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(FunctionPointer<R> first, FunctionPointer<R> second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(FunctionPointer<R> first, FunctionPointer<R> second)
            => first.methodPtr != second.methodPtr;
    }

    /// <summary>
    /// Represents a pointer to the method with single parameter and return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed static methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the first method parameter.</typeparam>
    /// <typeparam name="R">The type of the return value of the method that this pointer encapsulates.</typeparam>
    public readonly struct FunctionPointer<T, R> : IMethodPointer<Func<T, R>>
    {
        private static MethodPointerCache<Func<T, R>> Cache = new MethodPointerCache<Func<T, R>>();
        private readonly IntPtr methodPtr;

        public FunctionPointer(MethodInfo method)
        {
            methodPtr = Cache.Check(method ?? throw new ArgumentNullException(nameof(method))) ?
                method.MethodHandle.GetFunctionPointer():
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        public FunctionPointer(Func<T, R> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        IntPtr IMethodPointer<Func<T, R>>.Address => methodPtr;

        public Func<T, R> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Func<T, R>), ".ctor", typeof(object), typeof(IntPtr)));
            return Return<Func<T, R>>();
        }

        public R Invoke(T arg)
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(arg);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(R), typeof(T)));
            return Return<R>();
        }
    }

    /// <summary>
    /// Represents a pointer to parameterless method with <see cref="void"/> return type.
    /// </summary>
    /// <remarks>
    /// This method pointer is intended to call managed methods only.
    /// </remarks>
    /// <typeparam name="T">The type of the first method parameter.</typeparam>
    public readonly struct ActionPointer<T> : IMethodPointer<Action<T>>, IEquatable<ActionPointer<T>>
    {
        private static MethodPointerCache<Action<T>> Cache = new MethodPointerCache<Action<T>>();
        private readonly IntPtr methodPtr;

        /// <summary>
        /// Initializes a new pointer to the method.
        /// </summary>
        /// <param name="method">The method to convert into pointer.</param>
        public ActionPointer(MethodInfo method)
        {
            methodPtr = Cache.Check(method ?? throw new ArgumentNullException(nameof(method))) ? 
                method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(method));
        }

        /// <summary>
        /// Initializes a new pointer based on extracted pointer from the delegate.
        /// </summary>
        /// <param name="delegate">The delegate representing method.</param>
        public ActionPointer(Action<T> @delegate)
        {
            methodPtr = @delegate.Target is null ?
                @delegate.Method.MethodHandle.GetFunctionPointer() :
                throw new ArgumentException(ExceptionMessages.CannotMakeMethodPointer, nameof(@delegate));
        }

        IntPtr IMethodPointer<Action<T>>.Address => methodPtr;

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <returns>The delegate created from this method pointer; or <see langword="null"/> if this pointer is zero.</returns>
        public Action<T> ToDelegate()
        {
            const string makeDelegate = "makeDelegate";
            Push(methodPtr);
            Brtrue(makeDelegate);
            Ldnull();
            Ret();
            MarkLabel(makeDelegate);
            Ldnull();
            Push(methodPtr);
            Newobj(new M(typeof(Action<T>), ConstructorName, typeof(object), typeof(IntPtr)));
            return Return<Action<T>>();
        }

        /// <summary>
        /// Invokes method by pointer.
        /// </summary>
        public void Invoke(T arg)
        {
            const string callIndirect = "indirect";
            Push(methodPtr);
            Brtrue(callIndirect);
            Newobj(new M(typeof(MethodPointerException), ConstructorName));
            Throw();
            MarkLabel(callIndirect);
            Push(arg);
            Push(methodPtr);
            Tail();
            Calli(new CallSiteDescr(CallingConventions.Standard, typeof(void), typeof(T)));
            Ret();
        }

        /// <summary>
        /// Converts this pointer into <see cref="Action"/>.
        /// </summary>
        /// <param name="pointer">The point to convert.</param>
        /// <returns>The delegate created from this method pointer.</returns>
        public static explicit operator Action<T>(ActionPointer<T> pointer) => pointer.ToDelegate();

        /// <summary>
        /// Computes hash code of this pointer.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => methodPtr.GetHashCode();

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ActionPointer<T> other) => methodPtr == other.methodPtr;

        /// <summary>
        /// Determines whether this object points to the same method as other object.
        /// </summary>
        /// <param name="other">The object implementing <see cref="IMethodPointer{D}"/> to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is IMethodPointer<Delegate> ptr && methodPtr == ptr.Address;

        /// <summary>
        /// Obtains pointer value in HEX format.
        /// </summary>
        /// <returns>The address represented by pointer.</returns>
        public override string ToString() => methodPtr.ToString("X");

        /// <summary>
        /// Determines whether the pointers represent the same method.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent the same method; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ActionPointer<T> first, ActionPointer<T> second)
            => first.methodPtr == second.methodPtr;

        /// <summary>
        /// Determines whether the pointers represent different methods.
        /// </summary>
        /// <param name="first">The first pointer to compare.</param>
        /// <param name="second">The second pointer to compare.</param>
        /// <returns><see langword="true"/> if both pointers represent different methods; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ActionPointer<T> first, ActionPointer<T> second)
            => first.methodPtr != second.methodPtr;
    }
}
