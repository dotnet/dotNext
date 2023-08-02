using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices;

public sealed class ValueTupleBuilderTests : Test
{
    [Fact]
    public static void TupleTypeConstructionTest()
    {
        var builder = new ValueTupleBuilder();
        Equal(typeof(ValueTuple), builder.Build());
        builder.Add<DateTime>();
        Equal(typeof(ValueTuple<DateTime>), builder.Build());
        builder.Add<string>();
        Equal(typeof(ValueTuple<DateTime, string>), builder.Build());
        builder.Add<int>();
        Equal(typeof(ValueTuple<DateTime, string, int>), builder.Build());
        for (int i = 0; i < 16; i++)
            builder.Add<bool>();
        Equal(19, builder.Count);
        var tupleType = builder.Build();
        Equal(typeof(ValueTuple<,,,,,,,>), tupleType.GetGenericTypeDefinition());
    }

    [Fact]
    public static void TupleRestTypeConstructionTest()
    {
        var builder = new ValueTupleBuilder();
        Equal(typeof(ValueTuple), builder.Build());
        builder.Add<DateTime>();
        Equal(typeof(ValueTuple<DateTime>), builder.Build());
        builder.Add<string>();
        Equal(typeof(ValueTuple<DateTime, string>), builder.Build());
        builder.Add<int>();
        Equal(typeof(ValueTuple<DateTime, string, int>), builder.Build());
        for (int i = 0; i < 5; i++)
            builder.Add<bool>();
        Equal(8, builder.Count);

        var tupleType = builder.Build();
        const string restFieldName = nameof(ValueTuple<bool, bool, bool, bool, bool, bool, bool, bool>.Rest);
        Equal(typeof(ValueTuple<bool>), tupleType.GetField(restFieldName)?.FieldType);

        var members = builder.Build(Expression.New, out _);
        Equal(typeof(DateTime), members[0].Type);
        Equal(typeof(bool), members[7].Type);
    }
}