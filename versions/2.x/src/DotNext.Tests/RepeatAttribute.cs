using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Xunit.Sdk;

namespace DotNext
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    [ExcludeFromCodeCoverage]
    internal sealed class RepeatAttribute : DataAttribute
    {
        private readonly int count;

        public RepeatAttribute(int count) => this.count = count;

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            => from i in Enumerable.Range(0, count) select new object[] { i };
    }
}