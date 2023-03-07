using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct StateTransition : IEquatable<StateTransition>
{
    internal readonly LabelTarget? Successful;
    internal readonly LabelTarget? Failure;

    internal StateTransition(LabelTarget? successful, LabelTarget? failed)
    {
        if (successful is null && failed is null)
            throw new ArgumentNullException(nameof(successful));
        Successful = successful;
        Failure = failed;
    }

    internal void Deconstruct(out LabelTarget? successful, out LabelTarget? failed)
    {
        successful = Successful;
        failed = Failure;
    }

    internal Expression MakeGoto()
    {
        if (Failure is null)
        {
            Debug.Assert(Successful is not null);
            return Expression.Goto(Successful);
        }

        if (Successful is null)
            return Expression.Goto(Failure);

        return Expression.Condition(new HasNoExceptionExpression(), Expression.Goto(Successful), Expression.Goto(Failure));
    }
}