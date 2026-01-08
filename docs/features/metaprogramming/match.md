Pattern Matching
====
Metaprogramming model support pattern matching statement with the same capabilities as in [C#](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/switch#-pattern-matching-with-the-switch-statement) including [Recursive Pattern Matching](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-8.0/patterns). This statement can be constructed using `Match` static method from [CodeGenerator](xref:DotNext.Metaprogramming.CodeGenerator) class.

Supported types of patterns:
1. Conditional matching
1. Typed-based matching where you can specify the expected type
1. Typed-based conditional matching where you can specify the expected type and condition to be applied to the value of this type
1. Structural matching where you can specify expected values of the properties or fields

Example of structural pattern matching:
```csharp
using DotNext.Linq.Expressions;
using static DotNext.Metaprogramming.CodeGenerator;

var newState = Lambda<Func<(DoorState, Action, bool), DoorState>>(fun => 
{
    Match(fun[0])
        .Case((DoorState.Closed, Action.Open), DoorState.Opened.Quoted)
        .Case((DoorState.Opened, Action.Close), DoorState.Closed.Quoted)
        .Case((DoorState.Closed, Action.Lock, true), DoorState.Locked.Quoted)
        .Case((DoorState.Locked, Action.Unlock, true), DoorState.Closed.Quoted)
        .Default(state => state)
        .OfType<DoorState>()
    .End();
}).Compile();

//equivalent generated code is
Func<(DoorState, Action, bool), DoorState> newState = state =>
{
    switch(state) 
    {
        case (DoorState.Closed, Action.Open, _): return DoorState.Opened;
        case (DoorState.Opened, Action.Close, _): return DoorState.Closed;
        case (DoorState.Closed, Action.Lock, true): return DoorState.Locked;
        case (DoorState.Locked, Action.Unlock, true): return DoorState.Closed;
        case (var state, _, _): return state;
    }
}
```

Structural pattern can be defined using any type including anonymous type. The following example demonstrates various supported patterns:
```csharp
using DotNext.Linq.Expressions;
using static DotNext.Metaprogramming.CodeGenerator;

struct Point
{
    internal long X, Y;
}

var lambda = Lambda<Func<Point, string>>(fun =>
{
    Match(fun[0])
        .Case("X", 0L.Quoted, value => "X is zero".Quoted)
        .Case(new { X = long.MaxValue, Y = long.MaxValue }, "MaxValue".Quoted)
        .Case("X", long.MinValue.Quoted, "Y", long.MinValue.Quoted, (x, y) => "MinValue".Quoted)
        .Default("Unknown".Quoted)
        .OfType<string>()
    .End();
}).Compile();

//equivalent generated code is

new Func<Point, string>(point => 
{
    switch(point)
    {
        case {X: 0L}: return "X is zero";
        case {X: long.MaxValue, Y: long.MaxValue}: return "MaxValue";
        case {X: long.MinValue, Y: long.MinValue}: return "MinValue";
        default: return "Unknown";
    }
});
```