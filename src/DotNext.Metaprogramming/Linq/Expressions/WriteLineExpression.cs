using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Linq.Expressions;

/// <summary>
/// Represents expression that writes some object into particular output.
/// </summary>
/// <seealso cref="Console.Out"/>
/// <seealso cref="Console.Error"/>
/// <seealso cref="TextWriter.WriteLine(object)"/>
/// <seealso cref="System.Diagnostics.Debug.WriteLine(object)"/>
public sealed class WriteLineExpression : CustomExpression
{
    private enum Kind : byte
    {
        Out = 0,

        Error,

        Debug,
    }

    private readonly Kind kind;
    private readonly Expression value;

    private WriteLineExpression(Expression value, Kind kind)
    {
        this.value = value;
        this.kind = kind;
    }

    /// <summary>
    /// Always returns <see cref="void"/>.
    /// </summary>
    public override Type Type => typeof(void);

    /// <summary>
    /// Creates an expression that writes the object into <see cref="Console.Out"/>.
    /// </summary>
    /// <param name="value">The value to be written into the stdout.</param>
    /// <returns>A new instance of <see cref="WriteLineExpression"/>.</returns>
    public static WriteLineExpression Out(Expression value) => new(value, Kind.Out);

    /// <summary>
    /// Creates an expression that writes the object into <see cref="Console.Error"/>.
    /// </summary>
    /// <param name="value">The value to be written into the stderr.</param>
    /// <returns>A new instance of <see cref="WriteLineExpression"/>.</returns>
    public static WriteLineExpression Error(Expression value) => new(value, Kind.Error);

    /// <summary>
    /// Creates an expression that writes the object using <see cref="System.Diagnostics.Debug.WriteLine(object)"/>.
    /// </summary>
    /// <param name="value">The value to be written into the stderr.</param>
    /// <returns>A new instance of <see cref="WriteLineExpression"/>.</returns>
    public static WriteLineExpression Debug(Expression value) => new(value, Kind.Debug);

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(TextWriter))]
    private static MethodCallExpression WriteLineTo(MemberExpression stream, Expression value)
    {
        MethodInfo? writeLineMethod = typeof(TextWriter).GetMethod(nameof(TextWriter.WriteLine), new[] { value.Type });

        // WriteLine method will always be resolved here because Type.DefaultBinder
        // chooses TextWriter.WriteLine(object) if there is no exact match
        System.Diagnostics.Debug.Assert(writeLineMethod is not null);
        var firstParam = writeLineMethod.GetParameters()[0].ParameterType;
        if (firstParam != value.Type && value.Type.IsValueType)
            value = Expression.Convert(value, typeof(object));

        return Call(stream, writeLineMethod, value);
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Console))]
    private MethodCallExpression WriteLineToOut()
    {
        var outProperty = typeof(Console).GetProperty(nameof(Console.Out));
        System.Diagnostics.Debug.Assert(outProperty is not null);
        return WriteLineTo(Property(null, outProperty), value);
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Console))]
    private MethodCallExpression WriteLineToError()
    {
        var outProperty = typeof(Console).GetProperty(nameof(Console.Error));
        System.Diagnostics.Debug.Assert(outProperty is not null);
        return WriteLineTo(Property(null, outProperty), value);
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(System.Diagnostics.Debug))]
    private MethodCallExpression WriteLineToDebug()
    {
        var writeLineMethod = typeof(System.Diagnostics.Debug).GetMethod(nameof(System.Diagnostics.Debug.WriteLine), new[] { typeof(object) });
        System.Diagnostics.Debug.Assert(writeLineMethod is not null);
        return Call(writeLineMethod, value.Type.IsValueType ? Convert(value, typeof(object)) : value);
    }

    /// <summary>
    /// Translates this expression into predefined set of expressions
    /// using Lowering technique.
    /// </summary>
    /// <returns>Translated expression.</returns>
    public override Expression Reduce() => kind switch
    {
        Kind.Out => WriteLineToOut(),
        Kind.Error => WriteLineToError(),
        Kind.Debug => WriteLineToDebug(),
        _ => Empty(),
    };
}