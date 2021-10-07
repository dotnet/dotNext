using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Runtime.CompilerServices;

using Buffers;

/// <summary>
/// Represents a builder of the lambda expression
/// that can be compiled to the renderer of the interpolated string.
/// </summary>
[InterpolatedStringHandler]
[StructLayout(LayoutKind.Auto)]
public struct InterpolatedStringBuilder
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
                    Type.EmptyTypes,
                    Expression.Constant(literalOrFormat, typeof(string)));
            }
            else
            {
                inputVar = Expression.Parameter(argumentType);
                statement = Expression.Call(
                    handler,
                    nameof(BufferWriterSlimInterpolatedStringHandler.AppendFormatted),
                    new[] { argumentType },
                    inputVar,
                    Expression.Constant(alignment, typeof(int)),
                    Expression.Constant(literalOrFormat, typeof(string)));
            }

            statements.Add(statement);
        }
    }

    private readonly int literalLength, formattedCount;
    private List<Segment>? segments;

    /// <summary>
    /// Initializes a new builder.
    /// </summary>
    /// <param name="literalLength">The total number of characters in known at compile-time.</param>
    /// <param name="formattedCount">The number of placeholders.</param>
    public InterpolatedStringBuilder(int literalLength, int formattedCount)
    {
        segments = new(formattedCount);
        this.literalLength = literalLength;
        this.formattedCount = formattedCount;
    }

    private List<Segment> Segments => segments ??= new();

    /// <summary>
    /// Appends literal value.
    /// </summary>
    /// <param name="literal">The string value.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void AppendLiteral(string? literal) => Segments.Add(new(literal));

    private void AddPlaceholder(Type type, string? format, int alignment = 0)
        => Segments.Add(new(type, format, alignment));

    /// <summary>
    /// Appends a placeholder for the value.
    /// </summary>
    /// <typeparam name="T">The type of the placeholder.</typeparam>
    /// <param name="value">The value or expression.</param>
    /// <param name="format">The format of the value.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void AppendFormatted<T>(T value, string? format = null)
    {
        if (value is Expression expression)
            AddPlaceholder(expression.Type, format);
        else
            AddPlaceholder(typeof(T), format);
    }

    /// <summary>
    /// Appends a placeholder for the value.
    /// </summary>
    /// <typeparam name="T">The type of the placeholder.</typeparam>
    /// <param name="value">The value or expression.</param>
    /// <param name="alignment">The alignment of the value.</param>
    /// <param name="format">The format of the value.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void AppendFormatted<T>(T value, int alignment, string? format = null)
    {
        if (value is Expression expression)
            AddPlaceholder(expression.Type, format, alignment);
        else
            AddPlaceholder(typeof(T), format, alignment);
    }

    /// <summary>
    /// Removes all placeholders and literals from this builder.
    /// </summary>
    public void Clear() => Segments.Clear();

    /// <summary>
    /// Builds lambda expression that can be compiled to
    /// the renderer of the interpolated string.
    /// </summary>
    /// <returns>The lambda expression that encapsulates the rendering logic.</returns>
    public LambdaExpression Build()
    {
        var preallocatedBufferLocal = Expression.Variable(typeof(PreallocatedCharBuffer), "buffer");
        var writerLocal = Expression.Variable(typeof(BufferWriterSlim<char>), "writer");
        var handlerLocal = Expression.Variable(typeof(BufferWriterSlimInterpolatedStringHandler), "handler");
        var providerParameter = Expression.Parameter(typeof(IFormatProvider), "provider");
        var allocatorParameter = Expression.Parameter(typeof(MemoryAllocator<char>), "allocator");

        var parameters = new List<ParameterExpression>(Segments.Count + 2);
        parameters.Add(providerParameter);

        var statements = new List<Expression>();

        // instantiate buffer writer
        var ctor = writerLocal.Type.GetConstructor(new[] { typeof(Span<char>), allocatorParameter.Type });
        Debug.Assert(ctor is not null);
        Expression expr = Expression.New(
            ctor,
            Expression.Property(preallocatedBufferLocal, nameof(PreallocatedCharBuffer.Span)),
            allocatorParameter);
        statements.Add(Expression.Assign(writerLocal, expr));

        // instantiate handler
        ctor = handlerLocal.Type.GetConstructor(new[] { typeof(int), typeof(int), writerLocal.Type.MakeByRefType(), providerParameter.Type });
        Debug.Assert(ctor is not null);
        expr = Expression.New(
            ctor,
            Expression.Constant(literalLength),
            Expression.Constant(formattedCount),
            writerLocal,
            providerParameter);
        statements.Add(Expression.Assign(handlerLocal, expr));

        foreach (var segment in Segments)
        {
            segment.WriteStatement(statements, providerParameter, handlerLocal, out var parameter);

            if (parameter is not null)
                parameters.Add(parameter);
        }

        parameters.Add(allocatorParameter);

        // call handler.ToString()
        statements.Add(Expression.Call(handlerLocal, nameof(BufferWriterSlimInterpolatedStringHandler.ToString), Type.EmptyTypes));

        return Expression.Lambda(
            Expression.Block(new[] { preallocatedBufferLocal, writerLocal, handlerLocal }, statements),
            false,
            parameters);
    }
}