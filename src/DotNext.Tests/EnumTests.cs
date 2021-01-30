using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class EnumTests : Test
    {
        [Fact]
        public static void ConversionFromPrimitive()
        {
            Equal(EnvironmentVariableTarget.User, ((sbyte)1).ToEnum<EnvironmentVariableTarget>());
            Equal(EnvironmentVariableTarget.User, ((short)1).ToEnum<EnvironmentVariableTarget>());
            Equal(EnvironmentVariableTarget.User, 1U.ToEnum<EnvironmentVariableTarget>());
            Equal(EnvironmentVariableTarget.User, 1UL.ToEnum<EnvironmentVariableTarget>());
            Equal(EnvironmentVariableTarget.User, ((ushort)1).ToEnum<EnvironmentVariableTarget>());
            Equal(EnvironmentVariableTarget.User, 1.ToEnum<EnvironmentVariableTarget>());
            Equal(EnvironmentVariableTarget.User, 1L.ToEnum<EnvironmentVariableTarget>());
            Equal(EnvironmentVariableTarget.Machine, ((byte)2).ToEnum<EnvironmentVariableTarget>());
        }
    }
}
