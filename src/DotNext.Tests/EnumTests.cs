using System.Diagnostics.CodeAnalysis;

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

        [Fact]
        public static void NegativeValueConversion()
        {
            var expected = (EnvironmentVariableTarget)(-1);

            Equal(-1, expected.ToSByte());
            Throws<OverflowException>(() => expected.ToByte());

            Equal(-1, expected.ToInt16());
            Throws<OverflowException>(() => expected.ToUInt16());

            Equal(-1, expected.ToInt32());
            Throws<OverflowException>(() => expected.ToUInt32());

            Equal(-1L, expected.ToInt64());
            Throws<OverflowException>(() => expected.ToUInt64());
        }
    }
}
