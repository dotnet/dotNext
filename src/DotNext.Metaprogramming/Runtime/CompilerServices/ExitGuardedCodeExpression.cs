using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices;

using static Linq.Expressions.ExpressionBuilder;

internal sealed class ExitGuardedCodeExpression : TransitionExpression
{
    private readonly bool suspendException;

    internal ExitGuardedCodeExpression(uint parentState, bool suspendException)
        : base(parentState)
        => this.suspendException = suspendException;

    internal ExitGuardedCodeExpression(StatePlaceholderExpression placeholder, bool suspendException)
        : base(placeholder)
        => this.suspendException = suspendException;

    public override Type Type => typeof(void);

    public override Expression Reduce() => Empty();

    internal override Expression Reduce(ParameterExpression stateMachine)
        => stateMachine.Call(nameof(AsyncStateMachine<ValueTuple>.ExitGuardedCode), StateId, Constant(suspendException));
}