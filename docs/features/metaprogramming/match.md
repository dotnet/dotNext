Pattern Matching
====
Metaprogramming model support pattern matching statement with the same capabilities as in [C#](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/switch#-pattern-matching-with-the-switch-statement) including [Recursive Pattern Matching](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-8.0/patterns). This statement can be constructed using `Match` static method from [CodeGenerator](../../api/DotNext.Metaprogramming.CodeGenerator.yml) class.

Supported types of patterns:
1. Conditional matching
1. Typed-based matching where you can specify the expected type
1. Typed-based conditional matching where you can specify the expected type and condition to be applied to the value of this type
1. Structural matching where you can specify expected values of the properties or fields

Tuple-based pattern matching:
```csharp
using DotNext.Linq.Expressions;
using static DotNext.Metaprogramming.CodeGenerator;

var newState = Lambda<Func<(DoorState, Action, bool), DoorState>>(fun => 
{
    Match(fun[0])
        .Case((DoorState.Closed, Action.Open), value => DoorState.Opened.Const())
        .Case((DoorState.Opened, Action.Close), value => DoorState.Closed.Const())
        .Case((DoorState.Closed, Action.Lock, true), value => DoorState.Locked.Const())
        .Case((DoorState.Locked, Action.Unlock, true), value => DoorState.Closed.Const())
        .Default(fun[0])
        .OfType<DoorState>()
    .End();
});

//equivalent generated code is
Func<(DoorState, Action, bool), DoorState> newState = state =>
    switch {
        (DoorState.Closed, Action.Open, _) => DoorState.Opened,
        (DoorState.Opened, Action.Close, _) => DoorState.Closed,
        (DoorState.Closed, Action.Lock, true) => DoorState.Locked,
        (DoorState.Locked, Action.Unlock, true) => DoorState.Closed,
        (var state, _, _) => state
    };
```