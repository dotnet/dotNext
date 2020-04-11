using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class EnvarDependentFact : FactAttribute
    {
        public EnvarDependentFact(string variableName, string expectedValue, string defaultValue = null)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if(string.IsNullOrEmpty(value))
                value = defaultValue;
            if(value != expectedValue)
                Skip = $"Environment variable ${variableName} is not equal to ${expectedValue}";
        }
    }
}