using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Linq.Expressions;

public sealed class CountTests : Test
{
    [Fact]
    public static void StringLength()
    {
        var length = IsAssignableFrom<MemberExpression>("String".Quoted.CollectionLength);
        True(IsAssignableFrom<PropertyInfo>(length.Member).DeclaringType == typeof(string));
    }

    [Fact]
    public static void ArrayLength()
    {
        IsAssignableFrom<UnaryExpression>(Array.Empty<string>().Quoted.CollectionLength);
    }

    [Fact]
    public static void ListLength()
    {
        var length = IsAssignableFrom<MemberExpression>(new LinkedList<string>().Quoted.CollectionLength);
        True(IsAssignableFrom<PropertyInfo>(length.Member).DeclaringType == typeof(IReadOnlyCollection<string>));
    }
}