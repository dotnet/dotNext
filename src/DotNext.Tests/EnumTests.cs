using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class EnumTests : Assert
    {
        [Fact]
        public static void ValuesTest()
        {
            Equal(3, Enum<EnvironmentVariableTarget>.Members.Count);
            True(Enum<EnvironmentVariableTarget>.IsDefined(nameof(EnvironmentVariableTarget.Machine)));
            Equal(EnvironmentVariableTarget.Process, Enum<EnvironmentVariableTarget>.GetMember(nameof(EnvironmentVariableTarget.Process)));
            Equal(nameof(EnvironmentVariableTarget.User), Enum<EnvironmentVariableTarget>.GetMember(EnvironmentVariableTarget.User).Name);
            Equal(nameof(EnvironmentVariableTarget.Process), default(Enum<EnvironmentVariableTarget>).Name);
            Equal(typeof(int), Enum<EnvironmentVariableTarget>.UnderlyingType);
            Equal(TypeCode.Int32, Enum<EnvironmentVariableTarget>.UnderlyingTypeCode);
        }

        [Fact]
        public static void ConversionToPrimitive()
        {
            var member = Enum<EnvironmentVariableTarget>.GetMember(EnvironmentVariableTarget.User);
            Equal(1L, member.Value.ToInt64());
            Equal(1, member.Value.ToInt32());
            Equal(1, member.Value.ToInt16());
            Equal(1, member.Value.ToByte());
        }

        [Fact]
        public static void ConversionFromPrimitive()
        {
            Equal(EnvironmentVariableTarget.User, 1.ToEnum<EnvironmentVariableTarget>());
            Equal(EnvironmentVariableTarget.User, 1L.ToEnum<EnvironmentVariableTarget>());
            Equal(EnvironmentVariableTarget.Machine, ((byte)2).ToEnum<EnvironmentVariableTarget>());
        }

        [Fact]
        public static void MinMaxTest()
        {
            Equal(EnvironmentVariableTarget.Machine, Enum<EnvironmentVariableTarget>.MaxValue);
            Equal(EnvironmentVariableTarget.Process, Enum<EnvironmentVariableTarget>.MinValue);
        }

        [Fact]
        public static void EqualityOperators()
        {
            var e1 = Enum<EnvironmentVariableTarget>.GetMember(EnvironmentVariableTarget.Machine);
            var e2 = Enum<EnvironmentVariableTarget>.GetMember(EnvironmentVariableTarget.Machine);
            True(e1 == e2);
            False(e1 != e2);
            e2 = Enum<EnvironmentVariableTarget>.GetMember(EnvironmentVariableTarget.Process);
            False(e1 == e2);
            True(e1 != e2);
        }

        [Fact]
        public static void MemberExistence()
        {
            True(Enum<EnvironmentVariableTarget>.IsDefined(EnvironmentVariableTarget.Process));
            foreach (var member in Enum<EnvironmentVariableTarget>.Members)
                True(Enum<EnvironmentVariableTarget>.IsDefined(member.Value));
            False(Enum<EnvironmentVariableTarget>.IsDefined((EnvironmentVariableTarget)int.MaxValue));
        }
    }
}
