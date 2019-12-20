using System;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using MR = InlineIL.MethodRef;

namespace DotNext.Security.Cryptography
{
    using Intrinsics = Runtime.Intrinsics;

    /// <summary>
    /// Represents convenient facade for <see cref="HashAlgorithm"/>
    /// to avoid memory allocations during hash computing.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct HashBuilder : ICryptoTransform, IDisposable
    {
        private delegate void HashMethod(HashAlgorithm algorithm, ReadOnlySpan<byte> bytes);
        private delegate bool TryHashFinalMethod(HashAlgorithm algorithm, Span<byte> hashCode, out int bytesWritten);

        private static readonly HashMethod HashCore;
        private static readonly TryHashFinalMethod TryHashFinal;
        
        static HashBuilder()
        {
            Ldtoken(new MR(typeof(HashAlgorithm), nameof(HashCore), typeof(ReadOnlySpan<byte>)));
            Pop(out RuntimeMethodHandle handle);
            HashCore = Unsafe.As<MethodInfo>(MethodBase.GetMethodFromHandle(handle)).CreateDelegate<HashMethod>();
            
            Ldtoken(new MR(typeof(HashAlgorithm), nameof(TryHashFinal), typeof(Span<byte>), typeof(int).MakeByRefType()));
            Pop(out handle);
            TryHashFinal = Unsafe.As<MethodInfo>(MethodBase.GetMethodFromHandle(handle)).CreateDelegate<TryHashFinalMethod>();
        }

        private readonly HashAlgorithm algorithm;
        private readonly bool disposeAlg;

        /// <summary>
        /// Wraps the specified hash algorithm.
        /// </summary>
        /// <param name="algorithm">Hash algorithm implementation.</param>
        /// <param name="leaveOpen"><see langword="true"/> to keep <paramref name="algorithm"/> alive after the builder is disposed; otherwise, <see langword="false"/>.</param>
        public HashBuilder(HashAlgorithm algorithm, bool leaveOpen = true)
        {
            this.algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
            this.disposeAlg = !leaveOpen;
        }

        /// <summary>
        /// Initializes a new hash builder using the specified
        /// hash algorithm.
        /// </summary>
        /// <param name="hashName">The name of the algorithm.</param>
        /// <exception cref="ArgumentException">Hash algorithm with name <paramref name="hashName"/> doesn't exist.</exception>
        public HashBuilder(string hashName)
        {
            this.algorithm = HashAlgorithm.Create(hashName) ?? throw new ArgumentException(ExceptionMessages.UnknownHashAlgorithm, nameof(hashName));
            this.disposeAlg = true;
        }

        /// <summary>
        /// Gets a value indicating that this object
        /// is not initialized.
        /// </summary>
        public bool IsEmpty => algorithm is null;

        /// <summary>
        /// Gets the size, in bits, of the computed hash code.
        /// </summary>
        public int HashSize => algorithm?.HashSize ?? 0;

        bool ICryptoTransform.CanReuseTransform => (algorithm?.CanReuseTransform).GetValueOrDefault();

        bool ICryptoTransform.CanTransformMultipleBlocks => (algorithm?.CanTransformMultipleBlocks).GetValueOrDefault();

        int ICryptoTransform.InputBlockSize => algorithm?.InputBlockSize ?? 0;

        int ICryptoTransform.OutputBlockSize => algorithm?.OutputBlockSize ?? 0;

        int ICryptoTransform.TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[]? outputBuffer, int outputOffset)
            => algorithm?.TransformBlock(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset) ?? 0;
        
        byte[] ICryptoTransform.TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
            => algorithm?.TransformFinalBlock(inputBuffer, inputOffset, inputCount) ?? Array.Empty<byte>();

        /// <summary>
        /// Adds a series of bytes to the hash code.
        /// </summary>
        /// <param name="bytes">The bytes to add to the hash code.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(ReadOnlySpan<byte> bytes) => HashCore(algorithm, bytes);

        /// <summary>
        /// Adds a series of bytes to the hash code.
        /// </summary>
        /// <param name="bytes">The bytes to add to the hash code.</param>
        public void Add(ReadOnlySequence<byte> bytes)
        {
            foreach(var segment in bytes)
                Add(segment.Span);
        }

        /// <summary>
        /// Adds a single value to the hash code.
        /// </summary>
        /// <param name="value">The value to add to the hash code.</param>
        /// <typeparam name="T">The type of the value to add to the hash code.</typeparam>        
        public void Add<T>(in T value)
            where T : unmanaged
            => Add(Intrinsics.AsReadOnlySpan(in value));

        /// <summary>
        /// Resets internal state of hash algorithm.
        /// </summary>
        public void Reset() => algorithm?.Initialize();

        /// <summary>
        /// Calculates the final hash.
        /// </summary>
        /// <param name="hash">The buffer used to write the final hash.</param>
        /// <returns>The total number of bytes written into <paramref name="hash"/>.</returns>
        /// <exception cref="InvalidOperationException">Length of <paramref name="hash"/> is not enough to place the final hash.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Build(Span<byte> hash)
            => TryHashFinal(algorithm, hash, out int bytesWritten) ? bytesWritten : throw new InvalidOperationException(ExceptionMessages.NotEnoughMemory);

        /// <summary>
        /// Releases all resources associated with underlying hash algorithm.
        /// </summary>
        public void Dispose()
        {
            if(disposeAlg)
                algorithm.Dispose();
        }
    }
}