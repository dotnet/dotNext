using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System.Reflection;
using FastMember;

namespace DotNext.Reflection
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class FieldReflectionBenchmark
    {
        public class MyType
        {
            public string Value;
        }

        private static readonly FieldInfo ValueField = typeof(MyType).GetField(nameof(MyType.Value));
        private static readonly MyType Instance = new MyType { Value = "Hello, world!" };
        private static readonly DynamicInvoker DynamicFieldGetter = ValueField.Unreflect(false, BindingFlags.GetField);
        private static readonly DynamicInvoker DynamicFieldGetterVolatile = ValueField.Unreflect(true, BindingFlags.GetField);
        private static readonly ObjectAccessor FieldAccessor = ObjectAccessor.Create(Instance);
        private static readonly MemberGetter<MyType, string> StronglyTypedAccessor = ValueField.Unreflect<MyType, string>();
    
        [Benchmark]
        public object GetFieldUsingDynamicInvoker() => DynamicFieldGetter(Instance);

        [Benchmark]
        public object GetFieldUsingDynamicInvokerVolatile() => DynamicFieldGetterVolatile(Instance);

        [Benchmark]
        public object GetFieldUsingFastMemberLibrary() => FieldAccessor[nameof(MyType.Value)];

        [Benchmark]
        public object GetFieldUsingTypedReflection() => StronglyTypedAccessor(Instance);

        [Benchmark]
        public object GetFieldDirect() => Instance.Value;

        [Benchmark]
        public object GetFieldUsingReflection() => ValueField.GetValue(Instance);
    }
}