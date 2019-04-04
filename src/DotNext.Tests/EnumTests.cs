using System;
using Xunit;

namespace DotNext
{
    public sealed class EnumTests: Assert
    {
        [Fact]
        public void ValuesTest()
        {
            True(Enum<EnvironmentVariableTarget>.IsDefined(nameof(EnvironmentVariableTarget.Machine)));
            Equal(EnvironmentVariableTarget.Process, Enum<EnvironmentVariableTarget>.GetMember(nameof(EnvironmentVariableTarget.Process)));
            Equal(nameof(EnvironmentVariableTarget.User), Enum<EnvironmentVariableTarget>.GetMember(EnvironmentVariableTarget.User).Name);
            Equal(nameof(EnvironmentVariableTarget.Process), default(Enum<EnvironmentVariableTarget>).Name);
        }

        [Fact]
        public void ConversionToPrimitive()
        {
            var member = Enum<EnvironmentVariableTarget>.GetMember(EnvironmentVariableTarget.User);
            Equal(1L, (long)member);
            Equal(1, (int)member);
            Equal(1, (short)member);
        }

        [Fact]
        public void ConversionFromPrimitive()
        {
            True(Enum<EnvironmentVariableTarget>.TryGetMember(1, out var member));
            Equal(EnvironmentVariableTarget.User, member.Value);
            True(Enum<EnvironmentVariableTarget>.TryGetMember(1L, out member));
            Equal(EnvironmentVariableTarget.User, member.Value);
            True(Enum<EnvironmentVariableTarget>.TryGetMember((short)1, out member));
            Equal(EnvironmentVariableTarget.User, member.Value);
            True(Enum<EnvironmentVariableTarget>.TryGetMember((sbyte)1, out member));
            Equal(EnvironmentVariableTarget.User, member.Value);
        }

        [Fact]
        public void MinMaxTest()
        {
            Equal(EnvironmentVariableTarget.Machine, Enum<EnvironmentVariableTarget>.MaxValue);
            Equal(EnvironmentVariableTarget.Process, Enum<EnvironmentVariableTarget>.MinValue);
        }
    }
}
