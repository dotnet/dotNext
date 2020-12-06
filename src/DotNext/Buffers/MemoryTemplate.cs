using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents generic template for buffer rendering.
    /// </summary>
    /// <remarks>
    /// This type is aimed to fast replacement of the sequence of elements
    /// called placeholder in the original sequence of elements.
    /// In other words, it is an implementation of find-and-replace algorithm.
    /// Pre-compiled template allows to reuse it when rendering with different
    /// arguments is required. The rendering process is much faster
    /// than <see cref="string.Format(string, object[])"/> especially for
    /// large templates. However, the rendering process doesn't offer
    /// formatting procedures.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in the memory.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct MemoryTemplate<T>
        where T : IEquatable<T>
    {
        private sealed class Placeholder
        {
            internal readonly int Offset;
            internal Placeholder? Next;

            internal Placeholder(int offset) => Offset = offset;
        }

        private readonly ReadOnlyMemory<T> template;
        private readonly Placeholder? firstOccurence;
        private readonly int placeholderLength;

        /// <summary>
        /// Initializes a new pre-compiled template.
        /// </summary>
        /// <param name="template">The template containing placeholders for replacement.</param>
        /// <param name="placeholder">The span of elements representing template placeholder.</param>
        public MemoryTemplate(ReadOnlyMemory<T> template, ReadOnlySpan<T> placeholder)
        {
            this.template = template;
            if (placeholder.IsEmpty || placeholder.Length > template.Length)
            {
                placeholderLength = 0;
                firstOccurence = null;
            }
            else
            {
                placeholderLength = placeholder.Length;
                firstOccurence = BuildPlaceholdersChain(template.Span, placeholder);
            }
        }

        /// <summary>
        /// Gets original template passed to this object during construction.
        /// </summary>
        public ReadOnlyMemory<T> Value => template;

        private static Placeholder? BuildPlaceholdersChain(ReadOnlySpan<T> source, ReadOnlySpan<T> placeholder)
        {
            Placeholder? head = null, tail = null;

            for (var offset = 0; offset < source.Length - placeholder.Length + 1; )
            {
                if (source.Slice(offset, placeholder.Length).SequenceEqual(placeholder))
                {
                    CreateNode(ref head, ref tail, offset);
                    offset += placeholder.Length;
                }
                else
                {
                    offset += 1;
                }
            }

            return head;
        }

        private static void CreateNode(ref Placeholder? head, ref Placeholder? tail, int offset)
        {
            if (head is null || tail is null)
            {
                head = tail = new Placeholder(offset);
            }
            else
            {
                var previous = tail;
                tail = previous.Next = new Placeholder(offset);
            }
        }

        /// <summary>
        /// Replaces all placeholders in the template with custom content.
        /// </summary>
        /// <param name="output">The buffer writer used to build rendered content.</param>
        /// <param name="rewriter">
        /// The action responsible for replacing placeholder with custom content.
        /// The first argument of the action indicates placeholder index.
        /// </param>
        /// <typeparam name="TWriter">The type of the buffer writer.</typeparam>
        public unsafe void Render<TWriter>(TWriter output, Action<int, TWriter> rewriter)
            where TWriter : class, IBufferWriter<T>
            => Render(output, rewriter, new (&Span.CopyTo<T>));

        /// <summary>
        /// Replaces all placeholders in the template with custom content.
        /// </summary>
        /// <param name="arg">The argument to be passed to the write actions.</param>
        /// <param name="rewriter">
        /// The action responsible for replacing placeholder with custom content.
        /// The first argument of the action indicates placeholder index.
        /// </param>
        /// <param name="output">The action responsible for writing unmodified segments from the original template.</param>
        /// <typeparam name="TArg">The type of the argument to be passed to <paramref name="rewriter"/> and <paramref name="output"/>.</typeparam>
        public void Render<TArg>(TArg arg, Action<int, TArg> rewriter, in ValueReadOnlySpanAction<T, TArg> output)
        {
            ReadOnlySpan<T> source = template.Span;
            var placeholder = firstOccurence;
            for (int cursor = 0, offset = 0, index = 0; MoveNext(ref cursor, ref placeholder, out var isPlaceholder); offset = cursor)
            {
                if (isPlaceholder)
                {
                    rewriter(index++, arg);
                }
                else
                {
                    output.Invoke(source.Slice(offset, cursor - offset), arg);
                }
            }
        }

        private bool MoveNext(ref int offset, ref Placeholder? placeholder, out bool isPlaceholder)
        {
            if (offset >= template.Length)
            {
                isPlaceholder = false;
                return false;
            }

            if (placeholder is null)
            {
                isPlaceholder = false;
                offset = template.Length;
            }
            else if (placeholder.Offset == offset)
            {
                isPlaceholder = true;
                offset += placeholderLength;
                placeholder = placeholder.Next;
            }
            else
            {
                offset = placeholder.Offset;
                isPlaceholder = false;
            }

            return true;
        }
    }

    /// <summary>
    /// Represents various extensions for <see cref="MemoryTemplate{T}"/> type.
    /// </summary>
    public static class MemoryTemplate
    {
        private static void Rewrite(this string[] replacement, int index, StringBuilder output)
            => output.Append(replacement[index]);

        /// <summary>
        /// Replaces all occurences of placeholders in the template with
        /// actual values from the given array.
        /// </summary>
        /// <param name="template">The string template.</param>
        /// <param name="output">The string builder used to write rendered template.</param>
        /// <param name="replacement">An array of actual values used to replace placeholders.</param>
        public static unsafe void Render(this in MemoryTemplate<char> template, StringBuilder output, params string[] replacement)
            => template.Render(output, replacement.Rewrite, new (&Span.CopyTo));

        private static void Rewrite(this string[] replacement, int index, IBufferWriter<char> output)
            => output.Write(replacement[index]);

        /// <summary>
        /// Replaces all occurences of placeholders in the template with
        /// actual values from the given array.
        /// </summary>
        /// <param name="template">The string template.</param>
        /// <param name="replacement">An array of actual values used to replace placeholders.</param>
        /// <returns>The rendered template.</returns>
        public static string Render(this in MemoryTemplate<char> template, params string[] replacement)
        {
            using var writer = new PooledArrayBufferWriter<char>(template.Value.Length);
            template.Render(writer, replacement.Rewrite);
            return new string(writer.WrittenArray);
        }

        private static void Rewrite(this string[] replacement, int index, TextWriter output)
            => output.Write(replacement[index]);

        /// <summary>
        /// Replaces all occurences of placeholders in the template with
        /// actual values from the given array.
        /// </summary>
        /// <param name="template">The string template.</param>
        /// <param name="output">The text writer used to write rendered template.</param>
        /// <param name="replacement">An array of actual values used to replace placeholders.</param>
        public static unsafe void Render(this in MemoryTemplate<char> template, TextWriter output, params string[] replacement)
            => template.Render(output, replacement.Rewrite, new (&Span.CopyTo));
    }
}