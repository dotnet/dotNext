using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Linq.Expressions;

/// <summary>
/// Represents a set of members with their values.
/// </summary>
public sealed class MemberBindings : Dictionary<string, Expression>
{
    /// <summary>
    /// Initializes a new empty set of members.
    /// </summary>
    public MemberBindings()
        : base(StringComparer.Ordinal)
    {
    }

    /// <summary>
    /// Constructs a list of bindings.
    /// </summary>
    /// <param name="target">The target type with the declared members.</param>
    /// <returns>A list of bindings.</returns>
    public IReadOnlyList<MemberAssignment> Bind(Type target)
    {
        const MemberTypes memberTypes = MemberTypes.Field | MemberTypes.Property;
        const BindingFlags memberFlags = BindingFlags.Public | BindingFlags.Instance;
        var candidates = target.FindMembers(memberTypes, memberFlags, null, null);
        var result = new List<MemberAssignment>(candidates.Length);

        foreach (var candidate in candidates)
        {
            if (TryGetValue(candidate.Name, out var initializationExpression))
                result.Add(Expression.Bind(candidate, initializationExpression));
        }

        result.TrimExcess();
        return result;
    }
}