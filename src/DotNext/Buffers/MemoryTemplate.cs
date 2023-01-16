using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Buffers;

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

    [StructLayout(LayoutKind.Auto)]
    private readonly struct BufferConsumer<TWriter> : IReadOnlySpanConsumer<T>, IConsumer<int>
        where TWriter : class, IBufferWriter<T>
    {
        private readonly TWriter buffer;
        private readonly Action<int, TWriter> rewriter;

        internal BufferConsumer(TWriter buffer, Action<int, TWriter> rewriter)
        {
            this.buffer = buffer;
            this.rewriter = rewriter;
        }

        void IReadOnlySpanConsumer<T>.Invoke(ReadOnlySpan<T> input) => buffer.Write(input);

        void IConsumer<int>.Invoke(int index) => rewriter(index, buffer);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct DelegatingConsumer<TArg> : IReadOnlySpanConsumer<T>, IConsumer<int>
    {
        private readonly ReadOnlySpanAction<T, TArg> output;
        private readonly Action<int, TArg> rewriter;
        private readonly TArg state;

        internal DelegatingConsumer(ReadOnlySpanAction<T, TArg> output, Action<int, TArg> rewriter, TArg state)
        {
            this.output = output;
            this.rewriter = rewriter;
            this.state = state;
        }

        void IReadOnlySpanConsumer<T>.Invoke(ReadOnlySpan<T> input) => output(input, state);

        void IConsumer<int>.Invoke(int index) => rewriter(index, state);
    }

    private readonly ReadOnlyMemory<T> template;
    private readonly Placeholder? firstOccurence;
    private readonly int placeholderLength;

    /// <summary>
    /// Initializes a new pre-compiled template.
    /// </summary>
    /// <param name="template">The template containing placeholders for replacement.</param>
    /// <param name="placeholder">The span of elements representing template placeholder.</param>
    public MemoryTemplate(ReadOnlyMemory<T> template, scoped ReadOnlySpan<T> placeholder)
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

    private static Placeholder? BuildPlaceholdersChain(scoped ReadOnlySpan<T> source, scoped ReadOnlySpan<T> placeholder)
    {
        Placeholder? head = null, tail = null;

        for (var offset = 0; offset < source.Length - placeholder.Length + 1;)
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
    /// <param name="consumer">The consumer of the rendered content.</param>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    public void Render<TConsumer>(TConsumer consumer)
        where TConsumer : notnull, IReadOnlySpanConsumer<T>, IConsumer<int>
    {
        ReadOnlySpan<T> source = template.Span;
        var placeholder = firstOccurence;
        for (int cursor = 0, offset = 0, index = 0; MoveNext(ref cursor, ref placeholder, out var isPlaceholder); offset = cursor)
        {
            if (isPlaceholder)
            {
                consumer.Invoke(index++);
            }
            else
            {
                consumer.Invoke(source.Slice(offset, cursor - offset));
            }
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
    public void Render<TWriter>(TWriter output, Action<int, TWriter> rewriter)
        where TWriter : class, IBufferWriter<T>
        => Render(new BufferConsumer<TWriter>(output ?? throw new ArgumentNullException(nameof(output)), rewriter ?? throw new ArgumentNullException(nameof(rewriter))));

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
    public void Render<TArg>(TArg arg, Action<int, TArg> rewriter, ReadOnlySpanAction<T, TArg> output)
        => Render(new DelegatingConsumer<TArg>(output, rewriter, arg));

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
    [StructLayout(LayoutKind.Auto)]
    private readonly struct StringBuilderRenderer : IReadOnlySpanConsumer<char>, IConsumer<int>
    {
        private readonly IReadOnlyList<string> replacement;
        private readonly StringBuilder output;

        internal StringBuilderRenderer(StringBuilder output, IReadOnlyList<string> replacement)
        {
            this.output = output;
            this.replacement = replacement;
        }

        void IReadOnlySpanConsumer<char>.Invoke(ReadOnlySpan<char> input) => output.Append(input);

        void IConsumer<int>.Invoke(int index) => output.Append(replacement[index]);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct CharBufferRenderer : IReadOnlySpanConsumer<char>, IConsumer<int>
    {
        private readonly IReadOnlyList<string> replacement;
        private readonly IBufferWriter<char> output;

        internal CharBufferRenderer(IBufferWriter<char> output, IReadOnlyList<string> replacement)
        {
            this.output = output;
            this.replacement = replacement;
        }

        void IReadOnlySpanConsumer<char>.Invoke(ReadOnlySpan<char> input) => output.Write(input);

        void IConsumer<int>.Invoke(int index) => output.Write(replacement[index]);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct TextRenderer : IReadOnlySpanConsumer<char>, IConsumer<int>
    {
        private readonly IReadOnlyList<string> replacement;
        private readonly TextWriter output;

        internal TextRenderer(TextWriter output, IReadOnlyList<string> replacement)
        {
            this.output = output;
            this.replacement = replacement;
        }

        void IReadOnlySpanConsumer<char>.Invoke(ReadOnlySpan<char> input) => output.Write(input);

        void IConsumer<int>.Invoke(int index) => output.Write(replacement[index]);
    }

    /// <summary>
    /// Replaces all occurences of placeholders in the template with
    /// actual values from the given array.
    /// </summary>
    /// <param name="template">The string template.</param>
    /// <param name="output">The string builder used to write rendered template.</param>
    /// <param name="replacement">An array of actual values used to replace placeholders.</param>
    public static void Render(this in MemoryTemplate<char> template, StringBuilder output, params string[] replacement)
        => template.Render(new StringBuilderRenderer(output, replacement));

    /// <summary>
    /// Replaces all occurences of placeholders in the template with
    /// actual values from the given array.
    /// </summary>
    /// <param name="template">The string template.</param>
    /// <param name="replacement">An array of actual values used to replace placeholders.</param>
    /// <returns>The rendered template.</returns>
    public static string Render(this in MemoryTemplate<char> template, params string[] replacement)
    {
        using var writer = new PooledArrayBufferWriter<char> { Capacity = template.Value.Length };
        template.Render(new CharBufferRenderer(writer, replacement));
        return new string(writer.WrittenArray);
    }

    /// <summary>
    /// Replaces all occurences of placeholders in the template with
    /// actual values from the given array.
    /// </summary>
    /// <param name="template">The string template.</param>
    /// <param name="output">The text writer used to write rendered template.</param>
    /// <param name="replacement">An array of actual values used to replace placeholders.</param>
    public static void Render(this in MemoryTemplate<char> template, TextWriter output, params string[] replacement)
        => template.Render(new TextRenderer(output, replacement));
}