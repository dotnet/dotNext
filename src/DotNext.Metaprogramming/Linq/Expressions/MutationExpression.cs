using System.Linq.Expressions;
using System.Reflection;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Linq.Expressions;

/// <summary>
/// Represents record mutation expression.
/// </summary>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/with-expression">with expression</seealso>
public sealed class MutationExpression : CustomExpression
{
    private const string CloneMethodName = "<Clone>$";

    private readonly MethodBase? cloneMethodOrCtor;

    internal MutationExpression(Expression target, IReadOnlyList<MemberAssignment> bindings)
    {
        Debug.Assert(target is not null);
        Debug.Assert(bindings is not null);

        switch (target.Type)
        {
            case { IsValueType: true } recordStruct:
                // struct record doesn't have Clone method. Possible cases:
                // .ctor(T original)
                // .ctor()
                // no constructor
                const BindingFlags ctorFlags = BindingFlags.Instance | BindingFlags.DeclaredOnly;
                cloneMethodOrCtor = recordStruct.GetConstructor(ctorFlags, new[] { recordStruct }) ?? recordStruct.GetConstructor(ctorFlags, Type.EmptyTypes);
                break;
            case { IsClass: true } recordClass:
                const BindingFlags methodFlags = BindingFlags.Public | ctorFlags;
                cloneMethodOrCtor = recordClass.GetMethod(CloneMethodName, methodFlags, Type.EmptyTypes);
                if (cloneMethodOrCtor is null)
                    goto default;
                break;
            default:
                throw new ArgumentException(ExceptionMessages.RecordTypeExpected(target.Type), nameof(target));
        }

        Bindings = bindings;
        Expression = target;
    }

    /// <summary>
    /// Creates an expression equivalent to <see langword="with"/> operator in C#.
    /// </summary>
    /// <param name="target">The left operand.</param>
    /// <param name="bindings">The initialization list.</param>
    /// <returns>A new object representing mutation expression.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public static MutationExpression Create(Expression target, IReadOnlyList<MemberAssignment> bindings)
        => new(target ?? throw new ArgumentNullException(nameof(target)), bindings ?? throw new ArgumentNullException(nameof(bindings)));

    /// <summary>
    /// Creates an expression equivalent to <see langword="with"/> operator in C#.
    /// </summary>
    /// <param name="target">The left operand.</param>
    /// <param name="bindings">The initialization list.</param>
    /// <returns>A new object representing mutation expression.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public static MutationExpression Create(Expression target, MemberBindings bindings)
        => new(target ?? throw new ArgumentNullException(nameof(target)), bindings.Bind(target.Type));

    /// <summary>
    /// Gets the object to copy.
    /// </summary>
    public Expression Expression { get; }

    /// <summary>
    /// Gets mutation expressions.
    /// </summary>
    public IReadOnlyList<MemberAssignment> Bindings { get; }

    /// <summary>
    /// Gets type of this expression.
    /// </summary>
    public override Type Type => Expression.Type;

    /// <summary>
    /// Translates this expression into predefined set of expressions
    /// using Lowering technique.
    /// </summary>
    /// <returns>Translated expression.</returns>
    public override Expression Reduce()
    {
        var result = cloneMethodOrCtor switch
        {
            MethodInfo cloneMethod => Call(Expression, cloneMethod),
            ConstructorInfo ctor => New(ctor, ctor.GetParameters().Length is 0 ? [] : [Expression]),
            _ => Expression,
        };

        if (Bindings.Count > 0)
        {
            var tempVar = Parameter(Type, "copy");
            ICollection<Expression> statements = new List<Expression>(Bindings.Count + 2) { Assign(tempVar, result) };

            foreach (var binding in Bindings)
                statements.Add(Assign(MakeMemberAccess(tempVar, binding.Member), binding.Expression));

            statements.Add(tempVar);
            result = Block(tempVar.Type, new[] { tempVar }, statements);
        }

        return result;
    }

    /// <summary>
    /// Visit children expressions.
    /// </summary>
    /// <param name="visitor">Expression visitor.</param>
    /// <returns>Potentially modified expression if one of children expressions is modified during visit.</returns>
    protected override MutationExpression VisitChildren(ExpressionVisitor visitor)
    {
        var target = visitor.Visit(Expression);
        var bindings = new List<MemberAssignment>(Bindings.Count);

        foreach (var binding in Bindings)
            bindings.Add(binding.Update(visitor.Visit(binding.Expression)));

        bindings.TrimExcess();
        return new(target, bindings);
    }
}