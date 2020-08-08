using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class EnumTests : Test
    {
        private sealed class TestEnumValueAttribute : Attribute
        {
        }

        private enum EnumWithAttributes
        {
            None = 0,

            [TestEnumValue]
            WithAttribute
        }

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
            Equal(1, member.Value.ToSByte());
            Equal(1U, member.Value.ToUInt16());
            Equal(1U, member.Value.ToUInt32());
            Equal(1UL, member.Value.ToUInt64());
        }

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

        [Fact]
        public static void MemberOrder()
        {
            Equal(EnvironmentVariableTarget.Process, Enum<EnvironmentVariableTarget>.Members[0]);
            Equal(EnvironmentVariableTarget.User, Enum<EnvironmentVariableTarget>.Members[1]);
            Equal(EnvironmentVariableTarget.Machine, Enum<EnvironmentVariableTarget>.Members[2]);
        }

        [Fact]
        public static void HasFlag()
        {
            var flags = BindingFlags.CreateInstance | BindingFlags.Public;
            True(Enum<BindingFlags>.GetMember(BindingFlags.CreateInstance).IsFlag(flags));
            False(Enum<BindingFlags>.GetMember(BindingFlags.NonPublic).IsFlag(flags));
        }

        [Fact]
        public static void Serialization()
        {
            var e = Enum<BindingFlags>.GetMember(BindingFlags.Public);
            Equal(Enum<BindingFlags>.GetMember(BindingFlags.Public), SerializeDeserialize(e));
        }

        [Fact]
        public static void TryGet()
        {
            True(Enum<BindingFlags>.TryGetMember(nameof(BindingFlags.CreateInstance), out var member));
            Equal(BindingFlags.CreateInstance, member.Value);
            Equal(nameof(BindingFlags.CreateInstance), member.Name);
            False(Enum<BindingFlags>.TryGetMember("!!!!", out _));
            True(Enum<BindingFlags>.TryGetMember(BindingFlags.InvokeMethod, out member));
            Equal(BindingFlags.InvokeMethod, member.Value);
            Equal(nameof(BindingFlags.InvokeMethod), member.Name);
            False(Enum<BindingFlags>.TryGetMember(BindingFlags.Public | BindingFlags.NonPublic, out _));
        }

        [Fact]
        public static void Comparison()
        {
            var e = Enum<EnvironmentVariableTarget>.GetMember(EnvironmentVariableTarget.Process);
            Equal(0, e.CompareTo(EnvironmentVariableTarget.Process));
            True(e.CompareTo(EnvironmentVariableTarget.Machine) < 0);
        }

        [Fact]
        public static void Equality()
        {
            var e = Enum<EnvironmentVariableTarget>.GetMember(EnvironmentVariableTarget.Machine);
            object value = EnvironmentVariableTarget.Machine;
            True(e.Equals(value));
            value = EnvironmentVariableTarget.Process;
            False(e.Equals(value));
            value = e;
            True(e.Equals(value));
            value = Enum<EnvironmentVariableTarget>.GetMember(EnvironmentVariableTarget.Process);
            False(e.Equals(value));
        }

        [Fact]
        public static void CustomAttributeProvider()
        {
            ICustomAttributeProvider member = Enum<EnumWithAttributes>.GetMember(nameof(EnumWithAttributes.None));
            False(member.IsDefined(typeof(TestEnumValueAttribute), false));
            Empty(member.GetCustomAttributes(false));
            Empty(member.GetCustomAttributes(typeof(TestEnumValueAttribute), false));

            member = Enum<EnumWithAttributes>.GetMember(nameof(EnumWithAttributes.WithAttribute));
            True(member.IsDefined(typeof(TestEnumValueAttribute), false));
            NotEmpty(member.GetCustomAttributes(false));
            NotEmpty(member.GetCustomAttributes(typeof(TestEnumValueAttribute), false));
        }

        [Fact]
        public static void CustomAttributes()
        {
            var member = Enum<EnumWithAttributes>.GetMember(nameof(EnumWithAttributes.None));
            Null(member.GetCustomAttribute<TestEnumValueAttribute>());
            Empty(member.GetCustomAttributes<TestEnumValueAttribute>());

            member = Enum<EnumWithAttributes>.GetMember(nameof(EnumWithAttributes.WithAttribute));
            NotNull(member.GetCustomAttribute<TestEnumValueAttribute>());
            NotEmpty(member.GetCustomAttributes<TestEnumValueAttribute>());
        }
    }
}
