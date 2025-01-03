using System.Collections.Immutable;
using System.Linq.Expressions;
using static System.Globalization.CultureInfo;

namespace DotNext.Linq.Expressions;

public sealed class ExpressionBuilderTests : Test
{
    private static Predicate<T> MakeNullCheck<T>()
    {
        var param = Expression.Parameter(typeof(T), "input");
        return Expression.Lambda<Predicate<T>>(param.IsNull(), param).Compile();
    }

    private static Predicate<T> MakeNotNullCheck<T>()
    {
        var param = Expression.Parameter(typeof(T), "input");
        return Expression.Lambda<Predicate<T>>(param.IsNotNull(), param).Compile();
    }

    private static Func<T, string> MakeToString<T>()
    {
        var param = Expression.Parameter(typeof(T), "input");
        return Expression.Lambda<Func<T, string>>(NullSafetyExpression.Create(param, p => p.Call(nameof(ToString))), param).Compile();
    }

    private static Func<T, int?> MakeGetHashCode<T>()
    {
        var param = Expression.Parameter(typeof(T), "input");
        return Expression.Lambda<Func<T, int?>>(NullSafetyExpression.Create(param, p => p.Call(nameof(GetHashCode))), param).Compile();
    }

    private static Func<T, int> MakeGetHashCodeNotNull<T>()
        where T : struct
    {
        var param = Expression.Parameter(typeof(T), "input");
        return Expression.Lambda<Func<T, int>>(NullSafetyExpression.Create(param, p => p.Call(nameof(GetHashCode))), param).Compile();
    }

    [Fact]
    public static void NullCheck()
    {
        var stringPred = MakeNullCheck<string>();
        True(stringPred(null));
        False(stringPred(""));

        var intPred = MakeNullCheck<int>();
        False(intPred(default));

        var nullablePred = MakeNullCheck<int?>();
        True(nullablePred(default));
        False(nullablePred(0));

        var optionalPred = MakeNullCheck<Optional<string>>();
        True(optionalPred(Optional<string>.None));
        False(optionalPred(""));
    }

    [Fact]
    public static void NotNullCheck()
    {
        var stringPred = MakeNotNullCheck<string>();
        False(stringPred(null));
        True(stringPred(""));

        var intPred = MakeNotNullCheck<int>();
        True(intPred(default));

        var nullablePred = MakeNotNullCheck<int?>();
        False(nullablePred(default));
        True(nullablePred(0));

        var optionalPred = MakeNotNullCheck<Optional<string>>();
        False(optionalPred(Optional<string>.None));
        True(optionalPred(""));
    }

    [Fact]
    public static void NullSafetyToString()
    {
        var intToString = MakeToString<int>();
        Equal("42", intToString(42));

        var nullableToString = MakeToString<int?>();
        Equal("42", nullableToString(42));
        Null(nullableToString(default));

        var optionalToString = MakeToString<Optional<int>>();
        Equal("42", optionalToString(42));
        Null(optionalToString(Optional<int>.None));
    }

    [Fact]
    public static void NullSafetyGetHashCode()
    {
        var intHash = MakeGetHashCodeNotNull<int>();
        NotNull(intHash);

        var nullableHash = MakeGetHashCode<int?>();
        NotNull(nullableHash(42));
        Null(nullableHash(default));

        var optionalHash = MakeGetHashCode<Optional<string>>();
        NotNull(optionalHash(""));
        Null(optionalHash(Optional<string>.None));
    }

    private delegate ref int RefIntDelegate(TypedReference typedref);

    [Fact]
    public static void RefAnyValExpression()
    {
        var param = Expression.Parameter(typeof(TypedReference));
        var lambda = Expression.Lambda<RefIntDelegate>(param.RefAnyVal<int>(), param).Compile();
        var i = 10;
        Equal(10, lambda(__makeref(i)));
        lambda(__makeref(i)) = 20;
        Equal(20, i);
    }

    [Fact]
    public static void AndBuilder()
    {
        var expr = true.Const().And(false.Const());
        Equal(ExpressionType.And, expr.NodeType);
        Equal(ExpressionType.Constant, expr.Left.NodeType);
        Equal(ExpressionType.Constant, expr.Right.NodeType);
    }

    [Fact]
    public static void AssignToIndexer()
    {
        var indexer = Array.Empty<int>().Const().Property(typeof(IList<int>), "Item", 0.Const());
        var expr = indexer.Assign(42.Const());
        Equal(ExpressionType.Assign, expr.NodeType);
        Equal(ExpressionType.Constant, expr.Right.NodeType);
        Equal(ExpressionType.Index, expr.Left.NodeType);
        expr = indexer.AssignDefault();
        Equal(ExpressionType.Assign, expr.NodeType);
        Equal(ExpressionType.Default, expr.Right.NodeType);
        Equal(ExpressionType.Index, expr.Left.NodeType);
    }

    [Fact]
    public static void IndexerAccess()
    {
        var indexer = new List<int>().Const().Property("Item", 0.Const());
        NotEmpty(indexer.Arguments);
        Equal(ExpressionType.Constant, indexer.Arguments[0].NodeType);
    }

    [Fact]
    public static void AssignToVariable()
    {
        var expr = Expression.Parameter(typeof(long)).AssignDefault();
        Equal(ExpressionType.Assign, expr.NodeType);
        Equal(ExpressionType.Default, expr.Right.NodeType);
    }

    [Fact]
    public static void GotoLabel()
    {
        var label = Expression.Label();
        var expr = label.Break();
        Equal(ExpressionType.Goto, expr.NodeType);
        Equal(GotoExpressionKind.Break, expr.Kind);

        expr = label.Continue();
        Equal(ExpressionType.Goto, expr.NodeType);
        Equal(GotoExpressionKind.Continue, expr.Kind);

        expr = label.Goto();
        Equal(ExpressionType.Goto, expr.NodeType);
        Equal(GotoExpressionKind.Goto, expr.Kind);

        expr = label.Return();
        Equal(ExpressionType.Goto, expr.NodeType);
        Equal(GotoExpressionKind.Return, expr.Kind);

        label = Expression.Label(typeof(int));
        expr = label.Break(typeof(int).Default());
        Equal(ExpressionType.Goto, expr.NodeType);
        Equal(GotoExpressionKind.Break, expr.Kind);

        expr = label.Goto(42.Const());
        Equal(ExpressionType.Goto, expr.NodeType);
        Equal(GotoExpressionKind.Goto, expr.Kind);

        expr = label.Return(42.Const());
        Equal(ExpressionType.Goto, expr.NodeType);
        Equal(GotoExpressionKind.Return, expr.Kind);

        var site = label.LandingSite(42.Const());
        Equal(ExpressionType.Label, site.NodeType);
        Equal(ExpressionType.Constant, site.DefaultValue.NodeType);
    }

    [Fact]
    public static void ArrayElement()
    {
        var indexer = new int[0].Const().ElementAt(1.Const());
        Equal(ExpressionType.Index, indexer.NodeType);
        Equal(ExpressionType.Constant, indexer.Object.NodeType);
    }

    [Fact]
    public static void InvokeDelegate()
    {
        var expr = new Action(ArrayElement).Const().Invoke();
        Equal(ExpressionType.Invoke, expr.NodeType);
        Equal(ExpressionType.Constant, expr.Expression.NodeType);
    }

    [Fact]
    public static void BinaryOperations()
    {
        var expr = 42.Const().GreaterThanOrEqual(43.Const());
        Equal(ExpressionType.GreaterThanOrEqual, expr.NodeType);

        expr = 42.Const().LeftShift(2.Const());
        Equal(ExpressionType.LeftShift, expr.NodeType);

        expr = (BinaryExpression)42.Const().RightShift(2.Const());
        Equal(ExpressionType.RightShift, expr.NodeType);

        expr = 42.Const().LessThanOrEqual(43.Const());
        Equal(ExpressionType.LessThanOrEqual, expr.NodeType);

        expr = 42.Const().Modulo(43.Const());
        Equal(ExpressionType.Modulo, expr.NodeType);

        expr = 42.Const().NotEqual(43.Const());
        Equal(ExpressionType.NotEqual, expr.NodeType);

        expr = 42.Const().Xor(43.Const());
        Equal(ExpressionType.ExclusiveOr, expr.NodeType);

        expr = 42.Const().Or(43.Const());
        Equal(ExpressionType.Or, expr.NodeType);

        expr = 42D.Const().Power(2D.Const());
        Equal(ExpressionType.Power, expr.NodeType);
    }

    [Fact]
    public static void UnaryOperators()
    {
        var expr = 20.Const().Negate();
        Equal(ExpressionType.Negate, expr.NodeType);

        expr = (-20).Const().UnaryPlus();
        Equal(ExpressionType.UnaryPlus, expr.NodeType);

        expr = "".Const().TryConvert<IEnumerable<char>>();
        Equal(ExpressionType.TypeAs, expr.NodeType);

        expr = new object().Const().Unbox<int>();
        Equal(ExpressionType.Unbox, expr.NodeType);
    }

    [Fact]
    public static void IncrementDecrement()
    {
        var variable = Expression.Parameter(typeof(long));

        var expr = variable.PreDecrementAssign();
        Equal(ExpressionType.PreDecrementAssign, expr.NodeType);

        expr = variable.PreIncrementAssign();
        Equal(ExpressionType.PreIncrementAssign, expr.NodeType);

        expr = variable.PostIncrementAssign();
        Equal(ExpressionType.PostIncrementAssign, expr.NodeType);

        expr = variable.PreIncrementAssign();
        Equal(ExpressionType.PreIncrementAssign, expr.NodeType);

        var index = new int[0].Const().ElementAt(0.Const());
        expr = index.PreDecrementAssign();
        Equal(ExpressionType.PreDecrementAssign, expr.NodeType);

        expr = index.PreIncrementAssign();
        Equal(ExpressionType.PreIncrementAssign, expr.NodeType);

        expr = index.PostIncrementAssign();
        Equal(ExpressionType.PostIncrementAssign, expr.NodeType);

        expr = index.PreIncrementAssign();
        Equal(ExpressionType.PreIncrementAssign, expr.NodeType);
    }

    [Fact]
    public static void ThrowExceptionExpr()
    {
        var expr = typeof(Exception).New().Throw();
        Equal(typeof(void), expr.Type);
        Equal(ExpressionType.Throw, expr.NodeType);
    }

    [Fact]
    public static void NewString()
    {
        var expr = typeof(string).Const().New('a'.Const().Convert<object>(), 2.Const().Convert<object>());
        var lambda = Expression.Lambda<Func<object>>(expr).Compile();
        Equal("aa", lambda());
    }

    [Fact]
    public static void WithObject()
    {
        var expr = "abc".Const().With(obj =>
        {
            Equal(ExpressionType.Parameter, obj.NodeType);
            return obj.Property(nameof(string.Length));
        });
        True(expr.CanReduce);
        Equal(ExpressionType.Extension, expr.NodeType);
        Equal(typeof(int), expr.Type);
    }

    [Fact]
    public static void ForEachLoop()
    {
        var expr = new int[3].Const().ForEach((current, continueLabel, breakLabel) =>
        {
            Equal(typeof(int), current.Type);
            Equal(ExpressionType.MemberAccess, current.NodeType);
            Equal("Current", current.Member.Name);
            return current;
        });
        True(expr.CanReduce);
        Equal(ExpressionType.Extension, expr.NodeType);
        Equal(typeof(void), expr.Type);
    }

    [Fact]
    public static void ItemIndex()
    {
        const short IndexValue = 10;
        var index = new ItemIndexExpression(IndexValue.Const());
        False(index.IsFromEnd);
        Equal(ExpressionType.New, index.Reduce().NodeType);
        Equal(typeof(Index), index.Type);

        index = 42.Index(false);
        False(index.IsFromEnd);
        Equal(ExpressionType.New, index.Reduce().NodeType);
        Equal(typeof(Index), index.Type);

        var i = ^20;
        index = i.Quote();
        True(index.IsFromEnd);
    }

    [Fact]
    public static void RangeOfIndicies()
    {
        var range = 20.Index(false).To(21);
        Equal(typeof(Range), range.Type);
        Equal(ExpressionType.New, range.Reduce().NodeType);

        range = new RangeExpression();
        Same(ItemIndexExpression.First, range.Start);
        Same(ItemIndexExpression.Last, range.End);

        range = (..^1).Quote();
        Equal(typeof(Range), range.Type);
        Equal(ExpressionType.New, range.Reduce().NodeType);
    }

    [Fact]
    public static void CollectionAccess()
    {
        var parameter = Expression.Parameter(typeof(long[]));
        Delegate lambda = Expression.Lambda<Func<long[], long>>(parameter.ElementAt(0.Index(false)), parameter).Compile();
        Equal(42L, lambda.DynamicInvoke(new[] { 42L, 43L }));

        lambda = Expression.Lambda<Func<long[], long>>(new CollectionAccessExpression(parameter, typeof(Index).Default()), parameter).Compile();
        Equal(42L, lambda.DynamicInvoke(new[] { 42L, 43L }));

        lambda = Expression.Lambda<Func<long[], long>>(parameter.ElementAt(1.Index(true)), parameter).Compile();
        Equal(44L, lambda.DynamicInvoke(new[] { 42L, 43L, 44L }));

        parameter = Expression.Parameter(typeof(IList<long>));
        lambda = Expression.Lambda<Func<IList<long>, long>>(parameter.ElementAt(0.Index(false)), parameter).Compile();
        Equal(42L, lambda.DynamicInvoke(new[] { 42L, 43L }));

        lambda = Expression.Lambda<Func<IList<long>, long>>(parameter.ElementAt(1.Index(true)), parameter).Compile();
        Equal(43L, lambda.DynamicInvoke(new[] { 42L, 43L }));

        parameter = Expression.Parameter(typeof(List<long>));
        lambda = Expression.Lambda<Func<List<long>, long>>(parameter.ElementAt(0.Index(false)), parameter).Compile();
        Equal(42L, lambda.DynamicInvoke(new List<long> { 42L, 43L }));

        lambda = Expression.Lambda<Func<List<long>, long>>(parameter.ElementAt(1.Index(true)), parameter).Compile();
        Equal(43L, lambda.DynamicInvoke(new List<long> { 42L, 43L }));

        parameter = Expression.Parameter(typeof(string));
        lambda = Expression.Lambda<Func<string, char>>(parameter.ElementAt(1.Index(false)), parameter).Compile();
        Equal('b', lambda.DynamicInvoke("abc"));
    }

    [Fact]
    public static void ArraySlice()
    {
        var parameter = Expression.Parameter(typeof(long[]));
        var lambda = Expression.Lambda<Func<long[], long[]>>(parameter.Slice(1.Index(false), 0.Index(true)), parameter).Compile();
        Equal(new[] { 1L, 2L, 4L }[1..^0], lambda([1L, 2L, 4L]));
    }

    [Fact]
    public static void StringSlice()
    {
        var parameter = Expression.Parameter(typeof(string));
        var lambda = Expression.Lambda<Func<string, string>>(parameter.Slice(1.Index(false), 1.Index(true)), parameter).Compile();
        Equal("abcd"[1..^1], lambda("abcd"));

        lambda = Expression.Lambda<Func<string, string>>(parameter.Slice(typeof(Range).New(1.Index(false), 1.Index(true))), parameter).Compile();
        Equal("abcd"[1..^1], lambda("abcd"));
    }

    [Fact]
    public static void ListSlice()
    {
        var parameter = Expression.Parameter(typeof(List<long>));
        var lambda = Expression.Lambda<Func<List<long>, List<long>>>(parameter.Slice(1.Index(false), 1.Index(true)), parameter).Compile();
        Equal(new[] { 3L, 5L }, lambda(new List<long> { 1L, 3L, 5L, 7L }));
    }

    [Fact]
    public static void NullCoalescingAssignmentOfValueType()
    {
        var parameter = Expression.Parameter(typeof(int));
        var lambda = Expression.Lambda<Func<int, int>>(parameter.NullCoalescingAssignment(10.Const()), parameter).Compile();
        Equal(0, lambda(0));
        Equal(42, lambda(42));
    }

    [Fact]
    public static void NullCoalescingAssignmentOfNullableValueType()
    {
        var parameter = Expression.Parameter(typeof(int?));
        var lambda = Expression.Lambda<Func<int?, int>>(parameter.NullCoalescingAssignment(new int?(10).Const()).Convert<int>(), parameter).Compile();
        Equal(0, lambda(0));
        Equal(10, lambda(null));
        Equal(42, lambda(42));
    }

    [Fact]
    public static void NullCoalescingAssignmentOfVariable()
    {
        var parameter = Expression.Parameter(typeof(string));
        var lambda = Expression.Lambda<Func<string, string>>(parameter.NullCoalescingAssignment(string.Empty.Const()), parameter).Compile();
        Equal(string.Empty, lambda(null));
        Equal("Hello, world!", lambda("Hello, world!"));
    }

    [Fact]
    public static void NullCoalescingAssignmentOfArrayElement()
    {
        var parameter = Expression.Parameter(typeof(string[]));
        var lambda = Expression.Lambda<Action<string[]>>(parameter.ElementAt(0.Const()).NullCoalescingAssignment(string.Empty.Const()), parameter).Compile();
        string[] array = { null };
        lambda(array);
        Equal(string.Empty, array[0]);

        array[0] = "Hello, world!";
        lambda(array);
        Equal("Hello, world!", array[0]);
    }

    [Fact]
    public static void NullCoalescingAssignmentOfArrayElement2()
    {
        string[] array = { null };
        string[] GetEmptyArray() => array;
        var parameter = Expression.Parameter(typeof(Func<string[]>));
        var lambda = Expression.Lambda<Action<Func<string[]>>>(parameter.Invoke().ElementAt(0.Const()).NullCoalescingAssignment(string.Empty.Const()), parameter).Compile();
        lambda(GetEmptyArray);
        Equal(string.Empty, array[0]);

        array[0] = "Hello, world!";
        lambda(GetEmptyArray);
        Equal("Hello, world!", array[0]);
    }

    [Fact]
    public static void ConvertToNullable()
    {
        var lambda = Expression.Lambda<Func<int?>>(2.Const().AsNullable()).Compile();
        Equal(2, lambda());

        var lambda2 = Expression.Lambda<Func<int?>>(2.Const().AsNullable()).Compile();
        Equal(2, lambda());

        var lambda3 = Expression.Lambda<Func<string>>("Hello, world!".Const().AsNullable()).Compile();
        Equal("Hello, world!", lambda3());
    }

    [Fact]
    public static void ConvertToOptional()
    {
        var lamdba = Expression.Lambda<Func<Optional<int>>>(2.Const().AsOptional()).Compile();
        Equal(2, lamdba());
    }

    [Fact]
    public static void ConvertToResult()
    {
        var parameter = Expression.Parameter(typeof(string));
        var methodCall = Expression.Call(typeof(int), nameof(int.Parse), [], parameter);
        var lambda = Expression.Lambda<Func<string, Result<int>>>(methodCall.AsResult(), parameter).Compile();

        Equal(42, lambda("42"));
        False(lambda("ab").IsSuccessful);
        Throws<FormatException>(() => lambda("ab").Value);
    }

    [Fact]
    public static void CachedBindingRegression()
    {
        BinaryExpression expr1 = 42.Const().AsDynamic() > 2;
        BinaryExpression expr2 = 43.Const().AsDynamic() > 3;
        BinaryExpression expr3 = 43L.Const().AsDynamic() > 3L;
        NotEqual(expr1.Right, expr2.Right);
        NotEqual(expr2, expr3);
    }

    [Fact]
    public static void LateBindConstant()
    {
        object value = 42.Const();
        BinaryExpression expr = 43.Const().AsDynamic() > value;
        IsType<ConstantExpression>(expr.Right);
    }

    [Fact]
    public static void EarlyBindConstant()
    {
        BinaryExpression expr = 43.Const().AsDynamic() > 42.Const();
        IsType<ConstantExpression>(expr.Right);
    }

    [Fact]
    public static void LateBindMetaConstant()
    {
        dynamic right = 42.Const().AsDynamic();
        BinaryExpression expr = 43.Const().AsDynamic() > right;
        IsType<ConstantExpression>(expr.Right);
    }

    [Fact]
    public static void DynamicMethodCallExpr()
    {
        MethodCallExpression expr = 43.Const().AsDynamic().ToString(InvariantCulture);
        IsType<ConstantExpression>(expr.Object);
    }

    [Fact]
    public static void DynamicMemberGetter()
    {
        MemberExpression expr = "Hello, world!".Const().AsDynamic().Length;
        IsType<ConstantExpression>(expr.Expression);
    }

    [Fact]
    public static void DynamicArrayGetElement()
    {
        IndexExpression expr = Array.Empty<int>().Const().AsDynamic()[1];
        IsType<ConstantExpression>(expr.Object);
    }

    [Fact]
    public static void DynamicListGetElement()
    {
        IndexExpression expr = ImmutableList<int>.Empty.Const().AsDynamic()[1];
        IsType<ConstantExpression>(expr.Object);
    }

    [Fact]
    public static void DynamicUnaryOperator()
    {
        UnaryExpression expr = ~42.Const().AsDynamic();
        IsType<ConstantExpression>(expr.Operand);
    }

    [Fact]
    public static void DynamicConvertOperator()
    {
        dynamic result = 42.Const().AsDynamic();
        Throws<NotSupportedException>(() => (long)result);
    }

    [Fact]
    public static void InitExpression()
    {
        MemberInitExpression initialization = typeof(UriBuilder).New().Init(new()
            {
                { nameof(UriBuilder.Host), "localhost".Const() },
                { nameof(UriBuilder.Scheme), Uri.UriSchemeHttps.Const() }
            });

        Contains(initialization.Bindings, static item => nameof(UriBuilder.Host) == item.Member.Name);
        Contains(initialization.Bindings, static item => nameof(UriBuilder.Scheme) == item.Member.Name);
    }

    public record class RecordClass(int A);

    [Fact]
    public static void MutateRecordClass1()
    {
        MutationExpression mut = typeof(RecordClass).New(42.Const()).With(new MemberBindings()
            {
                { nameof(RecordClass.A), 52.Const() }
            });

        Contains(mut.Bindings, static item => nameof(RecordClass.A) == item.Member.Name);
        Equal(typeof(RecordClass), mut.Reduce().Type);
    }

    [Fact]
    public static void MutateRecordClass2()
    {
        var constructExpr = typeof(RecordClass).New(42.Const());
        var bindings = new MemberBindings()
            {
                { nameof(RecordClass.A), 52.Const() }
            };

        MutationExpression mut = MutationExpression.Create(constructExpr, bindings.Bind(constructExpr.Type));

        Contains(mut.Bindings, static item => nameof(RecordClass.A) == item.Member.Name);
        Equal(typeof(RecordClass), mut.Reduce().Type);
    }

    public record struct RecordStruct(int A);

    [Fact]
    public static void MutateRecordStruct()
    {
        MutationExpression mut = typeof(RecordStruct).New(42.Const()).With(new MemberBindings
            {
                { nameof(RecordStruct.A), 52.Const() }
            });

        Contains(mut.Bindings, static item => nameof(RecordStruct.A) == item.Member.Name);
        Equal(typeof(RecordStruct), mut.Reduce().Type);
    }

    [Fact]
    public static void MutateRegularStruct()
    {
        MutationExpression mut = typeof(Net.Cluster.Consensus.Raft.Result<bool>).New().With(new MemberBindings
            {
                {nameof(Net.Cluster.Consensus.Raft.Result<bool>.Value), false.Const()}
            });

        Contains(mut.Bindings, static item => nameof(Net.Cluster.Consensus.Raft.Result<bool>.Value) == item.Member.Name);
        Equal(typeof(Net.Cluster.Consensus.Raft.Result<bool>), mut.Reduce().Type);
    }

    [Theory]
    [InlineData(typeof(byte), typeof(BinaryExpression))]
    [InlineData(typeof(ushort), typeof(BinaryExpression))]
    [InlineData(typeof(uint), typeof(BinaryExpression))]
    [InlineData(typeof(ulong), typeof(BinaryExpression))]
    [InlineData(typeof(sbyte), typeof(UnaryExpression))]
    [InlineData(typeof(short), typeof(UnaryExpression))]
    [InlineData(typeof(int), typeof(UnaryExpression))]
    [InlineData(typeof(long), typeof(UnaryExpression))]
    [InlineData(typeof(nint), typeof(MethodCallExpression))]
    [InlineData(typeof(nuint), typeof(MethodCallExpression))]
    [InlineData(typeof(Int128), typeof(MethodCallExpression))]
    [InlineData(typeof(UInt128), typeof(MethodCallExpression))]
    public static void UnsignedRightShift(Type primitiveType, Type expressionType)
    {
        var expr = new UnsignedRightShiftExpression(Expression.Default(primitiveType), 2.Const());
        IsAssignableFrom<DefaultExpression>(expr.Left);
        IsAssignableFrom<ConstantExpression>(expr.Right);
        True(expr.Method.IsStatic);
        
        var reduced = expr.Reduce();
        IsAssignableFrom(expressionType, reduced);
        Same(primitiveType, reduced.Type);
    }
}