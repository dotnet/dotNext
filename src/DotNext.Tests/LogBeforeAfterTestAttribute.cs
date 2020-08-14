using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xunit.Sdk;

namespace DotNext
{
    internal enum TraceOutput
    {
        Empty = 0,
        Stdout,
        Stderr,
        Debug,
        Trace
    }

    //I need this attribute to track stuck tests
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    [ExcludeFromCodeCoverage]
    public sealed class LogBeforeAfterTestAttribute : BeforeAfterTestAttribute
    {
        private readonly TraceOutput output;

        internal LogBeforeAfterTestAttribute(TraceOutput output = TraceOutput.Stdout) => this.output = output;

        private void WriteLine(string message)
        {
            switch (output)
            {
                case TraceOutput.Stdout:
                    Console.Out.WriteLine(message);
                    break;
                case TraceOutput.Stderr:
                    Console.Error.WriteLine(message);
                    break;
                case TraceOutput.Debug:
                    Debug.WriteLine(message);
                    break;
                case TraceOutput.Trace:
                    Trace.WriteLine(message);
                    break;
            }
        }

        public override void Before(MethodInfo methodUnderTest)
            => WriteLine($"Starting test {methodUnderTest.DeclaringType.Name}/{methodUnderTest.Name}");

        public override void After(MethodInfo methodUnderTest)
            => WriteLine($"Test {methodUnderTest.DeclaringType.Name}/{methodUnderTest.Name} is finished");
    }
}