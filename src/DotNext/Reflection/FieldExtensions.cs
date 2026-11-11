using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Reflection;

using Runtime;
using Runtime.CompilerServices;

/// <summary>
/// Extends <see cref="FieldInfo"/> class.
/// </summary>
public static class FieldExtensions
{
    [ThreadStatic] private static FieldInfo[]? threadCache;

    /// <summary>
    /// Extends <see cref="FieldInfo"/> class.
    /// </summary>
    /// <param name="field">The field reference.</param>
    extension(FieldInfo field)
    {
        /// <summary>
        /// Gets value of the field.
        /// </summary>
        /// <param name="owner">The field owner.</param>
        /// <typeparam name="T">The type of the field.</typeparam>
        /// <returns>The field location.</returns>
        public ref T GetValue<T>(object owner)
        {
            EnsureCacheInitialized(field);
            return ref TypedReference.MakeTypedReference(owner, threadCache).AsRef<T>();
        }
    }

    /// <summary>
    /// Extends <see cref="Variant"/> data type.
    /// </summary>
    extension(Variant)
    {
        /// <summary>
        /// Returns a reference to the field.
        /// </summary>
        /// <param name="owner">The object which contains a field.</param>
        /// <param name="field">The field reference.</param>
        /// <returns>Location of the field value.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="owner"/> or <paramref name="field"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException"> <paramref name="field"/> is static.</exception>
        public static Variant FromField(object owner, FieldInfo field)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(field);

            if (field.IsStatic)
                throw new ArgumentException(ExceptionMessages.FieldIsStatic, nameof(field));

            EnsureCacheInitialized(field);
            return new(TypedReference.MakeTypedReference(owner, threadCache), !field.IsInitOnly);
        }
    }

    [MemberNotNull(nameof(threadCache))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureCacheInitialized(FieldInfo field)
    {
        ref var fieldRef = ref MemoryMarshal.GetArrayDataReference(threadCache ??= new FieldInfo[1]);
        fieldRef = field;
    }
}