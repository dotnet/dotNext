using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions;

using List = Collections.Generic.List;

/// <summary>
/// Represents synchronized block of code.
/// </summary>
/// <see href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock Statement.</see>
public sealed class LockExpression : CustomExpression
{
    /// <summary>
    /// Represents constructor of synchronized block of code.
    /// </summary>
    /// <param name="syncRoot">The variable representing monitor object.</param>
    /// <returns>The body of synchronized block of code.</returns>
    public delegate Expression Statement(ParameterExpression syncRoot);

    private readonly BinaryExpression? assignment;
    private Expression? body;

    internal LockExpression(Expression syncRoot)
    {
        if (syncRoot is ParameterExpression syncVar)
        {
            SyncRoot = syncVar;
        }
        else
        {
            SyncRoot = Variable(typeof(object), "syncRoot");
            assignment = Assign(SyncRoot, syncRoot);
        }
    }

    /// <summary>
    /// Creates a new synchronized block of code.
    /// </summary>
    /// <param name="syncRoot">The monitor object.</param>
    /// <param name="body">The delegate used to construct synchronized block of code.</param>
    /// <returns>The synchronized block of code.</returns>
    public static LockExpression Create(Expression syncRoot, Statement body)
    {
        var result = new LockExpression(syncRoot);
        result.Body = body(result.SyncRoot);
        return result;
    }

    /// <summary>
    /// Creates a new synchronized block of code.
    /// </summary>
    /// <param name="syncRoot">The monitor object.</param>
    /// <param name="body">The body of the code block.</param>
    /// <returns>The synchronized block of code.</returns>
    public static LockExpression Create(Expression syncRoot, Expression body)
        => new(syncRoot) { Body = body };

    /// <summary>
    /// Represents monitor object.
    /// </summary>
    public ParameterExpression SyncRoot { get; }

    /// <summary>
    /// Gets body of the synchronized block of code.
    /// </summary>
    public Expression Body
    {
        get => body ?? Empty();
        internal set => body = value;
    }

    /// <summary>
    /// Gets type of this expression.
    /// </summary>
    public override Type Type => Body.Type;

    /// <summary>
    /// Reconstructs synchronized block of code with a new body.
    /// </summary>
    /// <param name="body">The new body of the synchronized block of code.</param>
    /// <returns>Updated expression.</returns>
    public LockExpression Update(Expression body) => new(assignment is null ? SyncRoot : assignment.Right)
    {
        Body = body,
    };

    /// <summary>
    /// Translates this expression into predefined set of expressions
    /// using Lowering technique.
    /// </summary>
    /// <returns>Translated expression.</returns>
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Monitor))]
    public override Expression Reduce()
    {
        var monitorEnter = typeof(Monitor).GetMethod(nameof(Monitor.Enter), [typeof(object)]);
        Debug.Assert(monitorEnter is not null);
        var monitorExit = typeof(Monitor).GetMethod(nameof(Monitor.Exit), [typeof(object)]);
        Debug.Assert(monitorExit is not null);
        var body = TryFinally(Body, Call(monitorExit, SyncRoot));
        return assignment is null ?
                Block(Call(monitorEnter, SyncRoot), body) :
                Block(List.Singleton(SyncRoot), assignment, Call(monitorEnter, SyncRoot), body);
    }
}