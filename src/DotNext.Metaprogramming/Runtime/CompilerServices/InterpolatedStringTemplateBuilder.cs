using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Globalization.CultureInfo;

namespace DotNext.Runtime.CompilerServices;

using Buffers;

/// <summary>
/// Represents a builder of the lambda expression
/// that can be compiled to the renderer of the interpolated string.
/// </summary>
/// <param name="literalLength">The total number of characters in known at compile-time.</param>
/// <param name="formattedCount">The number of placeholders.</param>
[InterpolatedStringHandler]
[StructLayout(LayoutKind.Auto)]
public struct InterpolatedStringTemplateBuilder(int literalLength, int formattedCount)
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct Segment
    {
        private readonly string? literalOrFormat;
        private readonly int alignment;
        private readonly Type? argumentType;

        internal Segment(string? literal)
        {
            literalOrFormat = literal;
            alignment = 0;
            argumentType = null;
        }

        internal Segment(Type argumentType, string? format, int alignment)
        {
            literalOrFormat = format;
            this.alignment = alignment;
            this.argumentType = argumentType;
        }

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(BufferWriterSlimInterpolatedStringHandler))]
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DynamicDependencyAttribute is applied")]
        internal void WriteStatement(IList<Expression> statements, ParameterExpression provider, ParameterExpression handler, out ParameterExpression? inputVar)
        {
            Debug.Assert(provider.Type == typeof(IFormatProvider));
            Debug.Assert(handler.Type == typeof(BufferWriterSlimInterpolatedStringHandler));

            Expression statement;
            if (argumentType is null)
            {
                inputVar = null;
                statement = Expression.Call(
                    handler,
                    nameof(BufferWriterSlimInterpolatedStringHandler.AppendLiteral),
                    [],
                    Expression.Constant(literalOrFormat, typeof(string)));
            }
            else
            {
                inputVar = Expression.Parameter(argumentType);
                statement = Expression.Call(
                    handler,
                    nameof(BufferWriterSlimInterpolatedStringHandler.AppendFormatted),
                    [argumentType],
                    inputVar,
                    Expression.Constant(alignment, typeof(int)),
                    Expression.Constant(literalOrFormat, typeof(string)));
            }

            statements.Add(statement);
        }

        internal void WriteTo(scoped ref int position, scoped ref BufferWriterSlim<char> output)
        {
            if (argumentType is null)
            {
                output.Write(literalOrFormat);
                return;
            }

            output.Add('{');
            output.Format(position++, provider: InvariantCulture);

            if (alignment is not 0)
            {
                output.Add(',');
                output.Format(alignment, provider: InvariantCulture);
            }

            if (literalOrFormat is { Length: > 0 })
            {
                output.Add(':');
                output.Write(literalOrFormat);
            }

            output.Add('}');
        }
    }

    private List<Segment>? segments = new(formattedCount);

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private List<Segment> Segments => segments ??= [];

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly ReadOnlySpan<Segment> SegmentsSpan => CollectionsMarshal.AsSpan(segments);

    /// <summary>
    /// Appends literal value.
    /// </summary>
    /// <param name="literal">The string value.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void AppendLiteral(string? literal) => Segments.Add(new(literal));

    /// <summary>
    /// Appends placeholder.
    /// </summary>
    /// <param name="type">The type of the value.</param>
    /// <param name="alignment">The alignment of the value.</param>
    /// <param name="format">The format of the value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void AppendFormatted(Type type, int alignment, string? format = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        Segments.Add(new(type, format, alignment));
    }

    /// <summary>
    /// Appends placeholder.
    /// </summary>
    /// <param name="type">The type of the value.</param>
    /// <param name="format">The format of the value.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void AppendFormatted(Type type, string? format = null)
        => AppendFormatted(type, 0, format);

    /// <summary>
    /// Removes all placeholders and literals from this builder.
    /// </summary>
    public readonly void Clear() => segments?.Clear();

    /// <summary>
    /// Builds lambda expression that can be compiled to
    /// the renderer of the interpolated string.
    /// </summary>
    /// <returns>The lambda expression that encapsulates the rendering logic.</returns>
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PreallocatedCharBuffer))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(BufferWriterSlim<char>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(BufferWriterSlimInterpolatedStringHandler))]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DynamicDependencyAttribute is applied")]
    public readonly LambdaExpression Build()
    {
        var preallocatedBufferLocal = Expression.Variable(typeof(PreallocatedCharBuffer), "buffer");

        var bufferWriterSlimType = typeof(BufferWriterSlim<char>);
        var writerLocal = Expression.Variable(bufferWriterSlimType, "writer");

        var stringHandlerType = typeof(BufferWriterSlimInterpolatedStringHandler);
        var handlerLocal = Expression.Variable(stringHandlerType, "handler");
        var providerParameter = Expression.Parameter(typeof(IFormatProvider), "provider");
        var allocatorParameter = Expression.Parameter(typeof(MemoryAllocator<char>), "allocator");

        var parameters = new List<ParameterExpression>(SegmentsSpan.Length + 2)
        {
            providerParameter,
        };

        var statements = new List<Expression>();

        // instantiate buffer writer
        var ctor = bufferWriterSlimType.GetConstructor([typeof(Span<char>), allocatorParameter.Type]);
        Debug.Assert(ctor is not null);
        Expression expr = Expression.New(
            ctor,
            Expression.Property(preallocatedBufferLocal, nameof(PreallocatedCharBuffer.Span)),
            allocatorParameter);
        statements.Add(Expression.Assign(writerLocal, expr));

        // instantiate handler
        ctor = stringHandlerType.GetConstructor([typeof(int), typeof(int), writerLocal.Type.MakeByRefType(), providerParameter.Type]);
        Debug.Assert(ctor is not null);
        expr = Expression.New(
            ctor,
            Expression.Constant(literalLength),
            Expression.Constant(formattedCount),
            writerLocal,
            providerParameter);
        statements.Add(Expression.Assign(handlerLocal, expr));

        foreach (ref readonly var segment in SegmentsSpan)
        {
            segment.WriteStatement(statements, providerParameter, handlerLocal, out var parameter);

            if (parameter is not null)
                parameters.Add(parameter);
        }

        parameters.Add(allocatorParameter);

        // call handler.ToString()
        statements.Add(Expression.Call(handlerLocal, nameof(BufferWriterSlimInterpolatedStringHandler.ToString), []));

        // try-finally block to dispose the writer
        expr = Expression.Block(statements);
        expr = Expression.TryFinally(expr, Expression.Call(writerLocal, nameof(BufferWriterSlim<char>.Dispose), []));
        expr = Expression.Block([preallocatedBufferLocal, writerLocal, handlerLocal], expr);

        return Expression.Lambda(
            expr,
            false,
            parameters);
    }

    /// <summary>
    /// Gets original template.
    /// </summary>
    /// <returns>The original template.</returns>
    public readonly override string ToString()
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[64]);
        try
        {
            var position = 0;
            foreach (ref readonly var segment in SegmentsSpan)
                segment.WriteTo(ref position, ref writer);

            return writer.ToString();
        }
        finally
        {
            writer.Dispose();
        }
    }
}